using System.Buffers.Binary;
using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace LynowMic;

public sealed class AudioOutput : IDisposable
{
    private readonly object gate = new();
    private readonly BufferedWaveProvider buffer = new(new WaveFormat(48000, 16, 1)) { BufferDuration = TimeSpan.FromMilliseconds(240), DiscardOnBufferOverflow = true, ReadFully = true };
    private WasapiOut? player;
    private MMDevice? device;
    public volatile float Gain = 1;
    public volatile bool Muted;
    public volatile float Peak;
    public Action<string>? Failed;
    public int BufferMs { get { lock (gate) return (int)buffer.BufferedDuration.TotalMilliseconds; } }
    public int Resyncs { get; private set; }
    public void Start(string deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();
        device = enumerator.GetDevice(deviceId);
        player = new WasapiOut(device, AudioClientShareMode.Shared, true, 60);
        player.PlaybackStopped += (_, e) => { if (e.Exception != null) Failed?.Invoke(e.Exception.Message); };
        player.Init(buffer); player.Play();
    }
    public void Add(byte[] bytes)
    {
        float peak = 0, gain = Muted ? 0 : Gain;
        for (int i = 0; i < bytes.Length; i += 2)
        {
            int sample = (int)Math.Clamp(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(i, 2)) * gain, short.MinValue, short.MaxValue);
            peak = Math.Max(peak, Math.Abs(sample / 32768f));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i, 2), (short)sample);
        }
        Peak = peak;
        lock (gate)
        {
            // Drop stale queued sound instead of letting delay grow without bound.
            if (buffer.BufferedDuration.TotalMilliseconds > 160) { buffer.ClearBuffer(); Resyncs++; }
            buffer.AddSamples(bytes, 0, bytes.Length);
        }
    }
    public void Clear() { lock (gate) buffer.ClearBuffer(); Peak = 0; }
    public void Dispose() { player?.Stop(); player?.Dispose(); device?.Dispose(); }
}
