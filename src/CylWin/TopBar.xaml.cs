using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CylWin;

/// <summary>
/// A barra de vidro do topo. Fica escondida acima da tela e desce quando o
/// mouse encosta na borda de cima. Passando o mouse em "Cyl" abre o modal do PC.
/// </summary>
public partial class TopBar : Window
{
    private const double ShownTop = 6;      // distância da borda quando visível
    private const double HideDelayMs = 500; // espera antes de esconder
    private const double Slack = 30;        // folga em volta da barra antes de esconder

    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly SystemStats _stats = new();
    private readonly DispatcherTimer _mouseTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    private bool _shown = true;
    private bool _pinned;
    private DateTime _leftAt = DateTime.MaxValue;
    private DateTime _popupLeftAt = DateTime.MaxValue;

    private (TextBlock value, ScaleTransform fill) _cpu, _temp, _gpu, _ram;

    public TopBar()
    {
        InitializeComponent();

        _cpu = BuildRow(RowCpu, "CPU", _stats.CpuName, "Accent");
        _temp = BuildRow(RowTemp, "TEMP", "CPU", "Warm");
        _gpu = BuildRow(RowGpu, "GPU", _stats.GpuName, "Accent");
        _ram = BuildRow(RowRam, "RAM", $"{_stats.TotalRamGb:0} GB", "Accent");

        PcButton.MouseEnter += (_, _) => OpenPcPopup();

        _mouseTimer.Tick += (_, _) => CheckMouse();
        _clockTimer.Tick += (_, _) => Tick();

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = ShownTop;
            Tick();
            _mouseTimer.Start();
            _clockTimer.Start();
            _leftAt = DateTime.Now; // começa visível e some sozinha se o mouse não estiver nela
        };
        Closed += (_, _) => _stats.Dispose();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        Native.MakeToolWindow(hwnd);
        Native.EnableBlur(hwnd);

        var dpi = VisualTreeHelper.GetDpi(this);
        Native.RoundCorners(hwnd,
            (int)Math.Round(Width * dpi.DpiScaleX),
            (int)Math.Round(Height * dpi.DpiScaleY),
            (int)Math.Round(12 * dpi.DpiScaleX));
    }

    // ---------------- mostrar / esconder ----------------

    private void CheckMouse()
    {
        if (!Native.GetCursorPos(out var p)) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        double x = p.X / dpi.DpiScaleX;
        double y = p.Y / dpi.DpiScaleY;

        bool overPopup = PcPopup.IsOpen && PcPanel.IsMouseOver;
        bool nearBar = x >= Left - Slack && x <= Left + Width + Slack && y <= ShownTop + Height + Slack;

        if (!_shown)
        {
            if (y <= 1) SlideTo(true); // encostou na borda de cima
            return;
        }

        // modal do PC: fecha quando o mouse sai do botão e do próprio modal
        if (PcPopup.IsOpen)
        {
            if (PcButton.IsMouseOver || overPopup) _popupLeftAt = DateTime.MaxValue;
            else if (_popupLeftAt == DateTime.MaxValue) _popupLeftAt = DateTime.Now;
            else if ((DateTime.Now - _popupLeftAt).TotalMilliseconds > 300) PcPopup.IsOpen = false;
        }

        if (_pinned || nearBar || overPopup || Bar.ContextMenu.IsOpen)
        {
            _leftAt = DateTime.MaxValue;
            return;
        }

        if (_leftAt == DateTime.MaxValue) _leftAt = DateTime.Now;
        else if ((DateTime.Now - _leftAt).TotalMilliseconds > HideDelayMs) SlideTo(false);
    }

    private void SlideTo(bool show)
    {
        _shown = show;
        _leftAt = DateTime.MaxValue;
        if (!show) PcPopup.IsOpen = false;

        var anim = new DoubleAnimation
        {
            To = show ? ShownTop : -Height - 4,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = show ? EasingMode.EaseOut : EasingMode.EaseIn },
        };
        BeginAnimation(TopProperty, anim);
    }

    // ---------------- relógio e números ----------------

    private void Tick()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm", PtBr);
        DateText.Text = now.ToString("ddd d MMM", PtBr).Replace(".", "");
        NetIcon.Text = SystemStats.IsWifi() ? "" : "";
        NetIcon.Opacity = SystemStats.IsOnline() ? 1 : 0.4;

        // só mede o hardware quando o modal está aberto: o Cyl Win fica leve
        if (PcPopup.IsOpen) RefreshStats();
    }

    private void OpenPcPopup()
    {
        if (PcPopup.IsOpen) return;
        RefreshStats();
        PcPopup.IsOpen = true;
    }

    private void RefreshStats()
    {
        double cpu = _stats.Cpu();
        Set(_cpu, $"{cpu:0}%", cpu / 100);

        double? t = _stats.Temperature();
        Set(_temp, t is null ? "—" : $"{t:0}°C", t is null ? 0 : Math.Clamp((t.Value - 30) / 60, 0, 1));

        double gpu = _stats.Gpu();
        Set(_gpu, $"{gpu:0}%", gpu / 100);

        var (used, total) = _stats.Ram();
        Set(_ram, $"{used.ToString("0.0", PtBr)} de {total:0} GB", total > 0 ? used / total : 0);

        var (down, up) = _stats.Net();
        NetText.Text = $"Rede  ↓ {down.ToString("0.0", PtBr)} MB/s   ↑ {up.ToString("0.0", PtBr)} MB/s";

        var upt = SystemStats.Uptime();
        InfoText.Text = $"Ligado há {(int)upt.TotalHours} h {upt.Minutes} min · Disco C: {SystemStats.DiskFreeGb():0} GB livres";
    }

    private static void Set((TextBlock value, ScaleTransform fill) row, string text, double pct)
    {
        row.value.Text = text;
        row.fill.ScaleX = Math.Clamp(pct, 0, 1);
    }

    /// <summary>Monta uma linha: CHAVE  nome ........ valor, com a barrinha embaixo.</summary>
    private (TextBlock, ScaleTransform) BuildRow(Grid g, string key, string name, string brushKey)
    {
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(9) });

        var k = new TextBlock { Text = key, Style = (Style)FindResource("RowKey") };
        var n = new TextBlock { Text = name, Style = (Style)FindResource("RowName") };
        var v = new TextBlock { Text = "…", Style = (Style)FindResource("RowValue") };
        Grid.SetColumn(n, 1);
        Grid.SetColumn(v, 2);

        var scale = new ScaleTransform(0, 1);
        var track = new Border
        {
            Height = 5, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Bottom,
            Background = (Brush)FindResource("Track"),
            Child = new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = (Brush)FindResource(brushKey),
                RenderTransform = scale,
                RenderTransformOrigin = new Point(0, 0.5),
            },
        };
        Grid.SetRow(track, 1);
        Grid.SetColumn(track, 1);
        Grid.SetColumnSpan(track, 2);

        g.Children.Add(k);
        g.Children.Add(n);
        g.Children.Add(v);
        g.Children.Add(track);
        return (v, scale);
    }

    // ---------------- cliques ----------------

    private static void Run(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch { /* se o Windows não tiver o atalho, ignora */ }
    }

    private void PcButton_Click(object sender, RoutedEventArgs e) => Run("taskmgr.exe");
    private void Net_Click(object sender, RoutedEventArgs e) => Run("ms-settings:network-status");
    private void Volume_Click(object sender, RoutedEventArgs e) => Run("sndvol.exe"); // mixer por app (versão própria vem depois)
    private void Notifications_Click(object sender, RoutedEventArgs e) => Run("ms-actioncenter:");
    private void Clock_Click(object sender, RoutedEventArgs e) => Run("outlookcal:");

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        _pinned = PinItem.IsChecked;
        if (_pinned && !_shown) SlideTo(true);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
