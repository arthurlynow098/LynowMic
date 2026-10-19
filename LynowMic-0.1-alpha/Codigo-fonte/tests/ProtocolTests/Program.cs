using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LynowMic;

static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
static async Task<SslStream> Connect(MicServer server, string? pin = null)
{
    var tcp = new TcpClient { NoDelay = true };
    await tcp.ConnectAsync(IPAddress.Loopback, server.Port);
    var ssl = new SslStream(tcp.GetStream(), false, (_, cert, _, _) => cert != null && Convert.ToHexString(SHA256.HashData(cert.GetRawCertData())) == (pin ?? server.Pin));
    try { using var timeout = new CancellationTokenSource(3000); await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "Lynow Mic", EnabledSslProtocols = SslProtocols.Tls12 }, timeout.Token); }
    catch { ssl.Dispose(); tcp.Dispose(); throw; }
    return ssl;
}
static async Task SendHello(SslStream ssl, string token)
{
    var hello = JsonSerializer.Serialize(new { v = 1, token, name = "Protocol test", rate = 48000, channels = 1, bits = 16 });
    await ssl.WriteAsync(Encoding.UTF8.GetBytes(hello + "\n"));
}
static async Task<int> ReadByte(SslStream ssl)
{
    var one = new byte[1]; using var ct = new CancellationTokenSource(3000);
    try { return await ssl.ReadAsync(one, ct.Token) == 0 ? -1 : one[0]; }
    catch (IOException) { return -1; }
}

await using var server = new MicServer(IPAddress.Loopback, 0);
server.Start();
using (var json = JsonDocument.Parse(server.Pairing("192.168.1.50")))
    Check(json.RootElement.GetProperty("pin").GetString()?.Length == 64 && json.RootElement.GetProperty("token").GetString()?.Length == 64, "QR contains certificate pin and random 256-bit token");

bool rejected = false;
try { using var bad = await Connect(server, new string('0', 64)); } catch (AuthenticationException) { rejected = true; }
Check(rejected, "client rejects incorrect certificate pin");
using (var wrong = await Connect(server)) { await SendHello(wrong, new string('0', 64)); Check(await ReadByte(wrong) == -1, "server rejects unauthorized token"); }
using (var malformed = await Connect(server)) {
    await malformed.WriteAsync(Encoding.UTF8.GetBytes("{\"token\":\"" + server.Token + "\",\"v\":1e100}\n"));
    Check(await ReadByte(malformed) == -1, "out-of-range JSON number is rejected without stopping listener");
}

var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
server.Audio = frame => received.TrySetResult(frame);
using (var good = await Connect(server))
{
    await SendHello(good, server.Token); Check(await ReadByte(good) == 1, "valid pinned TLS session is accepted");
    byte[] payload = Enumerable.Range(0, MicServer.FrameBytes).Select(i => (byte)(i % 251)).ToArray();
    byte[] packet = new byte[payload.Length + 4]; BinaryPrimitives.WriteInt32LittleEndian(packet, payload.Length); payload.CopyTo(packet, 4);
    for (int offset = 0; offset < packet.Length; offset += 37) await good.WriteAsync(packet.AsMemory(offset, Math.Min(37, packet.Length - offset)));
    Check(payload.SequenceEqual(await received.Task.WaitAsync(TimeSpan.FromSeconds(3))), "fragmented TCP stream reconstructs exact PCM samples");
    await good.WriteAsync(new byte[] { 255, 255, 255, 127 });
    Check(await ReadByte(good) == -1, "invalid frame length closes session before allocation");
}
using (var again = await Connect(server)) { await SendHello(again, server.Token); Check(await ReadByte(again) == 1, "server accepts reconnect after malformed frame"); }
using (var truncated = await Connect(server)) { await SendHello(truncated, server.Token); await ReadByte(truncated); await truncated.WriteAsync(new byte[] { 128, 7, 0, 0, 1, 2 }); }
using (var again = await Connect(server)) { await SendHello(again, server.Token); Check(await ReadByte(again) == 1, "server recovers from truncated PCM frame"); }
var stopping = new MicServer(IPAddress.Loopback, 0); stopping.Start();
using var silent = new TcpClient(); await silent.ConnectAsync(IPAddress.Loopback, stopping.Port);
await Task.Delay(50);
await stopping.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
Check(true, "stop cancels stalled TLS handshake promptly");
Console.WriteLine("All protocol integration tests passed.");
