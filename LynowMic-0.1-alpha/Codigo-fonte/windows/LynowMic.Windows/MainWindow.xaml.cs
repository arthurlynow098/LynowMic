using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using QRCoder;

namespace LynowMic;

public partial class MainWindow : Window
{
    private record NetworkChoice(string Label, IPAddress Address);
    private record OutputChoice(string Name, string Id);
    
    private MicServer? server;
    private AudioOutput? audio;
    private string? pairing;
    private bool connected, closing, allowClose, busy;
    private long frames;
    private readonly Stopwatch elapsed = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    
    private readonly List<Border> ledSegments = new();
    private const int TOTAL_LEDS = 28;
    private double wavePhase = 0;
    private double currentSmoothedPeak = 0;
    private double peakHold = 0;
    private int peakHoldDecay = 0;

    public MainWindow()
    {
        InitializeComponent();
        BuildLedMeter();
        LoadHummingbirdLogo();
        Loaded += (_, _) => Refresh();
        Closing += OnClosing;
        timer.Tick += (_, _) => Tick();
        timer.Start();
    }

    private void LoadHummingbirdLogo()
    {
        try
        {
            string[] possiblePaths = new[]
            {
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.jpg"),
                @"c:\Users\PC\Desktop\LynowMic\logo_source.jpg",
                @"c:\Users\PC\Desktop\LynowMic\LynowMic-0.1-alpha\Windows\logo.jpg",
                @"c:\Users\arthur.mendes\Desktop\LynowMic\logo_source.jpg"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    HeaderLogoImage.Source = bmp;
                    TitleBarLogoImage.Source = bmp;
                    HeaderLogoVector.Visibility = Visibility.Collapsed;
                    return;
                }
            }
        }
        catch { }

        // Fallback to stylized vector
        HeaderLogoVector.Visibility = Visibility.Visible;
    }

    private void BuildLedMeter()
    {
        LedMeterStack.Children.Clear();
        ledSegments.Clear();

        // 28 segments stacked vertically from top to bottom
        for (int i = TOTAL_LEDS - 1; i >= 0; i--)
        {
            var segment = new Border
            {
                Height = 4,
                Margin = new Thickness(0, 1.5, 0, 1.5),
                CornerRadius = new CornerRadius(2),
                Background = GetLedColor(i, active: false),
                Opacity = 0.22
            };
            ledSegments.Add(segment);
            LedMeterStack.Children.Add(segment);
        }
    }

    private SolidColorBrush GetLedColor(int index, bool active)
    {
        float ratio = (float)index / TOTAL_LEDS;
        Color c;

        if (ratio < 0.40f)
        {
            // Low: Neon Blue (#3B82F6)
            c = Color.FromRgb(59, 130, 246);
        }
        else if (ratio < 0.70f)
        {
            // Mid: Electric Violet (#8B5CF6)
            c = Color.FromRgb(139, 92, 246);
        }
        else if (ratio < 0.88f)
        {
            // High: Vivid Magenta/Pink (#EC4899)
            c = Color.FromRgb(236, 72, 153);
        }
        else
        {
            // Peak: Red (#EF4444)
            c = Color.FromRgb(239, 68, 68);
        }

        return new SolidColorBrush(c);
    }

    private void Refresh()
    {
        try
        {
            var nets = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && IsPrivate(a.Address))
                    .Select(a => new NetworkChoice($"{n.Name} · {a.Address}", a.Address))).ToList();
            
            Networks.ItemsSource = nets;
            Networks.SelectedIndex = nets.Count > 0 ? 0 : -1;

            using var enumerator = new MMDeviceEnumerator();
            var outputs = new List<OutputChoice>();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                using (device) outputs.Add(new OutputChoice(device.FriendlyName, device.ID));
            }
            Outputs.ItemsSource = outputs;
            Outputs.SelectedIndex = Math.Max(0, outputs.FindIndex(x => x.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase)));

            StartButton.IsEnabled = nets.Count > 0 && outputs.Count > 0;
            if (nets.Count == 0)
                Status.Text = "Status: Nenhuma rede local encontrada. Conecte ao Wi-Fi.";
            else if (outputs.Count == 0)
                Status.Text = "Status: Instale o VB-CABLE ou conecte seus fones.";
            else
                Status.Text = "Status: READY (Aguardando preparação...)";
        }
        catch (Exception ex)
        {
            Status.Text = "Status Erro: " + ex.Message;
        }
    }

    private static bool IsPrivate(IPAddress address)
    {
        var a = address.GetAddressBytes();
        return a[0] == 10 || (a[0] == 172 && a[1] is >= 16 and <= 31) || (a[0] == 192 && a[1] == 168) || (a[0] == 169 && a[1] == 254);
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        busy = true;
        StartButton.IsEnabled = false;

        try
        {
            if (server != null)
            {
                await Stop();
                return;
            }

            if (Networks.SelectedItem is not NetworkChoice network || Outputs.SelectedItem is not OutputChoice output)
                return;

            audio = new AudioOutput { Gain = 1.0f };
            audio.Failed = error => Dispatcher.BeginInvoke(new Action(async () =>
            {
                if (server == null || busy) return;
                busy = true;
                StartButton.IsEnabled = false;
                try
                {
                    await Stop();
                    Status.Text = "Status: Saída de áudio interrompida. " + error;
                }
                finally
                {
                    busy = false;
                    StartButton.IsEnabled = true;
                }
            }));

            audio.Start(output.Id);
            server = new MicServer(network.Address);
            server.Connected = name => Dispatcher.BeginInvoke(new Action(() =>
            {
                connected = true;
                elapsed.Restart();
                PhoneName.Text = name;
                Badge.Text = "Conectado";
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Emerald Green
                Status.Text = "Status: CONNECTED (Ultra Low Latency)";
            }));

            server.Disconnected = () =>
            {
                audio?.Clear();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    connected = false;
                    elapsed.Stop();
                    PhoneName.Text = "Esperando seu celular";
                    Badge.Text = "Aguardando";
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(123, 77, 255)); // Violet
                    Status.Text = "Status: DISCONNECTED (Aguardando pareamento)";
                }));
            };

            server.Notice = text => Dispatcher.BeginInvoke(new Action(() => Status.Text = "Status: " + text));
            server.Audio = bytes =>
            {
                audio?.Add(bytes);
                Interlocked.Increment(ref frames);
            };

            server.Start();
            pairing = server.Pairing(network.Address.ToString());

            // Generate Crisp QR Code
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(pairing, QRCodeGenerator.ECCLevel.M);
            using var qr = new PngByteQRCode(data);
            using var stream = new MemoryStream(qr.GetGraphic(7));
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            QrImage.Source = image;
            QrPlaceholder.Visibility = Visibility.Collapsed;
            Endpoint.Text = $"{network.Address}:{server.Port} · TLS";
            Networks.IsEnabled = Outputs.IsEnabled = RefreshButton.IsEnabled = false;
            CopyButton.IsEnabled = true;

            StartButton.Content = "Encerrar Conexão";
            DeviceToggleLabel.Text = "On";
            DeviceToggleKnob.HorizontalAlignment = HorizontalAlignment.Right;
            DeviceToggleBorder.Background = new SolidColorBrush(Color.FromRgb(91, 45, 238));

            Badge.Text = "Aguardando";
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(123, 77, 255));
            Status.Text = "Status: READY (Escaneie o QR Code no seu celular)";
        }
        catch (Exception ex)
        {
            await Stop();
            Status.Text = "Status Erro: " + ex.Message;
        }
        finally
        {
            busy = false;
            if (!closing) StartButton.IsEnabled = true;
        }
    }

    private async Task Stop()
    {
        var previous = server;
        server = null;
        if (previous != null) await previous.DisposeAsync();
        audio?.Dispose();
        audio = null;

        pairing = null;
        connected = false;
        elapsed.Reset();
        frames = 0;

        QrImage.Source = null;
        QrPlaceholder.Visibility = Visibility.Visible;
        CopyButton.IsEnabled = false;
        Networks.IsEnabled = Outputs.IsEnabled = RefreshButton.IsEnabled = true;

        StartButton.Content = "Preparar Conexão";
        DeviceToggleLabel.Text = "Off";
        DeviceToggleKnob.HorizontalAlignment = HorizontalAlignment.Left;
        DeviceToggleBorder.Background = new SolidColorBrush(Color.FromRgb(40, 35, 70));

        Badge.Text = "Desconectado";
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(106, 101, 136));
        PhoneName.Text = "Esperando seu celular";
        Endpoint.Text = "Na mesma rede local";
        Status.Text = "Status: DISCONNECTED";
    }

    private void Tick()
    {
        double peak = connected ? audio?.Peak ?? 0 : 0;
        
        // Smooth peak response
        currentSmoothedPeak += (peak - currentSmoothedPeak) * 0.45;
        if (currentSmoothedPeak < 0.001) currentSmoothedPeak = 0;

        // Peak Hold logic
        if (currentSmoothedPeak >= peakHold)
        {
            peakHold = currentSmoothedPeak;
            peakHoldDecay = 14;
        }
        else
        {
            if (--peakHoldDecay <= 0)
            {
                peakHold -= 0.04;
                if (peakHold < 0) peakHold = 0;
            }
        }

        // dBFS Calculation
        double dbfs = currentSmoothedPeak < 0.0001 ? -60 : 20 * Math.Log10(currentSmoothedPeak);
        LevelText.Text = currentSmoothedPeak < 0.0001 ? "dBFS" : $"{dbfs:0} dBFS";

        // Update Vertical LED Segments
        UpdateLedBars(currentSmoothedPeak);

        // Update Oscilloscope Waveform Animation
        wavePhase += 0.08 + (currentSmoothedPeak * 0.15);
        if (wavePhase > Math.PI * 2) wavePhase -= Math.PI * 2;
        RenderWaveform(currentSmoothedPeak);

        Stats.Text = server == null 
            ? "PCM 48 kHz / 16-bit · mono · TLS" 
            : $"48 kHz · mono · buffer: {audio?.BufferMs ?? 0} ms · quadros: {Interlocked.Read(ref frames)}";
    }

    private void UpdateLedBars(double peak)
    {
        int activeCount = (int)Math.Round(peak * TOTAL_LEDS);
        activeCount = Math.Clamp(activeCount, 0, TOTAL_LEDS);

        for (int i = 0; i < TOTAL_LEDS; i++)
        {
            // In the stack, index 0 is top (highest dB), index 27 is bottom (lowest dB)
            int ledIndexFromBottom = TOTAL_LEDS - 1 - i;
            bool isActive = ledIndexFromBottom < activeCount;
            
            var border = ledSegments[i];
            border.Opacity = isActive ? 1.0 : 0.20;
            border.Background = GetLedColor(ledIndexFromBottom, isActive);
        }

        // Position Peak Marker Triangle
        if (LedMeterStack.ActualHeight > 0)
        {
            double meterHeight = LedMeterStack.ActualHeight;
            double topPos = (1.0 - Math.Clamp(peakHold, 0, 1)) * (meterHeight - 12);
            Canvas.SetTop(PeakMarker, Math.Max(0, topPos));
        }
    }

    private void RenderWaveform(double peak)
    {
        double width = WaveCanvas.ActualWidth;
        double height = WaveCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double midY = height / 2.0;
        double baseAmp = Math.Max(8.0, peak * (height * 0.42));

        // Create 4 harmonic strand paths
        WaveGlowPath.Data = CreateSineGeometry(width, midY, baseAmp * 1.15, wavePhase, freq: 2.8);
        WavePath1.Data = CreateSineGeometry(width, midY, baseAmp * 0.95, wavePhase + 0.35, freq: 2.8);
        WavePath2.Data = CreateSineGeometry(width, midY, baseAmp * 0.75, wavePhase + 0.70, freq: 3.2);
        WavePath3.Data = CreateSineGeometry(width, midY, baseAmp * 0.50, wavePhase + 1.05, freq: 3.6);
        WavePath4.Data = CreateSineGeometry(width, midY, baseAmp * 0.25, wavePhase + 1.40, freq: 4.0);
    }

    private PathGeometry CreateSineGeometry(double width, double midY, double amplitude, double phase, double freq)
    {
        var figure = new PathFigure { IsClosed = false };
        int points = 48;
        double step = width / points;

        for (int i = 0; i <= points; i++)
        {
            double x = i * step;
            // Envelope damping at edges for smooth studio look
            double envelope = Math.Sin((double)i / points * Math.PI);
            double y = midY + Math.Sin((x / width * Math.PI * freq) + phase) * amplitude * envelope;

            if (i == 0)
                figure.StartPoint = new Point(x, y);
            else
                figure.Segments.Add(new LineSegment(new Point(x, y), true));
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private void WaveCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RenderWaveform(currentSmoothedPeak);

    private void Toggle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (audio == null) return;
        audio.Muted = !audio.Muted;
        MuteButton.Content = audio.Muted ? "Unmute" : "Distort";
        ToggleKnob.HorizontalAlignment = audio.Muted ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        MainToggleSwitch.Background = audio.Muted 
            ? new SolidColorBrush(Color.FromRgb(40, 35, 70)) 
            : new SolidColorBrush(Color.FromRgb(91, 45, 238));
    }

    private void DeviceToggle_MouseDown(object sender, MouseButtonEventArgs e) => Start_Click(sender, e);

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (audio == null) return;
        audio.Muted = !audio.Muted;
        MuteButton.Content = audio.Muted ? "Unmute" : "Distort";
    }

    private void Gain_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (GainText != null) GainText.Text = $"{e.NewValue:0}%";
        if (audio != null) audio.Gain = (float)e.NewValue / 100;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (pairing == null)
        {
            MessageBox.Show(this, "Clique primeiro em 'Preparar Conexão' para gerar o código.", "Código de Conexão", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Clipboard.SetText(pairing);
            Status.Text = "Status: Código copiado para a área de transferência!";
        }
        catch
        {
            Status.Text = "Status: Não foi possível copiar. Use o QR Code.";
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Output_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (RouteHint == null) return;
        RouteHint.Text = Outputs.SelectedItem is OutputChoice o && o.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase)
            ? "No Discord, OBS ou jogo, escolha CABLE Output como microfone."
            : "Saída de reprodução selecionada.";
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (HelpModal != null)
            HelpModal.Visibility = Visibility.Visible;
    }

    private void CloseHelpModal_Click(object sender, RoutedEventArgs e)
    {
        if (HelpModal != null)
            HelpModal.Visibility = Visibility.Collapsed;
    }

    private void Help_Click(object sender, RoutedEventArgs e) => Settings_Click(sender, e);

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true;
        if (closing || busy) return;
        closing = true;
        timer.Stop();
        await Stop();
        allowClose = true;
        Close();
    }
}
