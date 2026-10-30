using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace LynowMic;

// No audio or UI dependencies: exercised by the cross-platform protocol tests.
public sealed class MicServer : IAsyncDisposable
{
    public const int SampleRate = 48000, FrameBytes = 1920;
    private readonly X509Certificate2 certificate;
    private readonly CancellationTokenSource stop = new();
    private readonly TcpListener listener;
    private Task? loop;
    public string Token { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string Pin => Convert.ToHexString(SHA256.HashData(certificate.RawData));
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public Action<string>? Connected;
    public Action? Disconnected;
    public Action<byte[]>? Audio;
    public Action<string>? Notice;

    public MicServer(IPAddress address, int port = 48765)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Lynow Mic local session", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(7));
        certificate = new X509Certificate2(ephemeral.Export(X509ContentType.Pfx));
        listener = new TcpListener(address, port);
    }

    public string Pairing(string host) => JsonSerializer.Serialize(new { app = "lynow-mic", v = 1, host, port = Port, pin = Pin, token = Token });
    public void Start() { listener.Start(4); loop = Task.Run(Run); }
    private async Task Run()
    {
        while (!stop.IsCancellationRequested)
        {
            bool authenticated = false;
            try
            {
                using var client = await listener.AcceptTcpClientAsync(stop.Token);
                client.NoDelay = true;
                using var stream = new SslStream(client.GetStream(), false);
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(6));
                    await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
                        ServerCertificate = certificate, ClientCertificateRequired = false,
                        EnabledSslProtocols = SslProtocols.Tls12
                    }, timeout.Token);
                    using var hello = JsonDocument.Parse(await ReadLine(stream, timeout.Token));
                    var root = hello.RootElement;
                    var token = root.GetProperty("token").GetString() ?? "";
                    if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Token), Encoding.ASCII.GetBytes(token)) ||
                        root.GetProperty("v").GetInt32() != 1 || root.GetProperty("rate").GetInt32() != SampleRate ||
                        root.GetProperty("channels").GetInt32() != 1 || root.GetProperty("bits").GetInt32() != 16)
                        throw new InvalidDataException("Pareamento recusado. Gere e escaneie um novo QR Code.");
                    string name = root.GetProperty("name").GetString() ?? "Android";
                    name = new string(name.Where(c => !char.IsControl(c)).Take(60).ToArray());
                    await stream.WriteAsync(new byte[] { 1 }, timeout.Token);
                    authenticated = true;
                    Connected?.Invoke(name);
                }
                var header = new byte[4];
                while (!stop.IsCancellationRequested)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    await stream.ReadExactlyAsync(header, timeout.Token);
                    if (BinaryPrimitives.ReadInt32LittleEndian(header) != FrameBytes)
                        throw new InvalidDataException("Formato de áudio incompatível.");
                    var frame = new byte[FrameBytes];
                    await stream.ReadExactlyAsync(frame, timeout.Token);
                    Audio?.Invoke(frame);
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or SocketException or AuthenticationException or OperationCanceledException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException)
            {
                if (!stop.IsCancellationRequested) Notice?.Invoke(authenticated ? "Conexão interrompida. Aguardando reconexão…" : "Aguardando um celular autorizado…");
            }
            finally { if (authenticated) Disconnected?.Invoke(); }
        }
    }
    private static async Task<string> ReadLine(Stream stream, CancellationToken ct)
    {
        using var bytes = new MemoryStream();
        var one = new byte[1];
        while (bytes.Length < 2048)
        {
            await stream.ReadExactlyAsync(one, ct);
            if (one[0] == 10) return Encoding.UTF8.GetString(bytes.ToArray());
            bytes.WriteByte(one[0]);
        }
        throw new InvalidDataException("Mensagem de pareamento muito longa.");
    }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop();
        if (loop != null) await loop;
        certificate.Dispose(); stop.Dispose();
    }
}
