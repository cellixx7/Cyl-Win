using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Principal;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace CylWin;

/// <summary>
/// Lê o uso de CPU, GPU, RAM, rede e (quando possível) temperatura.
/// Tudo vem de contadores e APIs do próprio Windows.
/// </summary>
public sealed class SystemStats : IDisposable
{
    private readonly PerformanceCounter? _cpu;
    private readonly Dictionary<string, PerformanceCounter> _gpu = new();
    private long _lastRx, _lastTx;
    private DateTime _lastNet = DateTime.MinValue;
    private bool _tempUnavailable;
    private bool _lhmTried;
    private Computer? _lhm;
    private IHardware? _cpuHw;
    private ISensor? _tempSensor;

    /// <summary>Por que a temperatura não aparece (vazio quando aparece).</summary>
    public string TempHint { get; private set; } = "";

    public string CpuName { get; }
    public string GpuName { get; }
    public double TotalRamGb { get; }

    public SystemStats()
    {
        // "% Processor Utility" é o mesmo número que o Gerenciador de Tarefas mostra.
        _cpu = TryCounter("Processor Information", "% Processor Utility", "_Total")
            ?? TryCounter("Processor", "% Processor Time", "_Total");
        _cpu?.NextValue();

        CpuName = ReadCpuName();
        GpuName = ReadGpuName(CpuName);
        TotalRamGb = ReadMemory().total;
        ReadNet(); // primeira amostra
    }

    public double Cpu() => Math.Clamp(Safe(() => _cpu?.NextValue() ?? 0), 0, 100);

    /// <summary>Soma do uso 3D de todos os processos, como no Gerenciador de Tarefas.</summary>
    public double Gpu()
    {
        try
        {
            var cat = new PerformanceCounterCategory("GPU Engine");
            var names = cat.GetInstanceNames().Where(n => n.EndsWith("engtype_3D")).ToHashSet();

            foreach (var gone in _gpu.Keys.Where(k => !names.Contains(k)).ToList())
            {
                _gpu[gone].Dispose();
                _gpu.Remove(gone);
            }

            double total = 0;
            foreach (var name in names)
            {
                if (!_gpu.TryGetValue(name, out var c))
                {
                    c = new PerformanceCounter("GPU Engine", "Utilization Percentage", name, true);
                    c.NextValue(); // contador de taxa: a primeira leitura é sempre 0
                    _gpu[name] = c;
                    continue;
                }
                total += Safe(() => c.NextValue());
            }
            return Math.Clamp(total, 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    public (double used, double total) Ram()
    {
        var (total, avail) = ReadMemory();
        return (total - avail, total);
    }

    /// <summary>Download e upload em MB/s desde a última chamada.</summary>
    public (double down, double up) Net()
    {
        var now = DateTime.UtcNow;
        var (rx, tx) = ReadNet();
        double secs = (now - _lastNet).TotalSeconds;
        double down = 0, up = 0;
        if (_lastNet != DateTime.MinValue && secs > 0.1)
        {
            down = Math.Max(0, rx - _lastRx) / secs / 1_048_576d;
            up = Math.Max(0, tx - _lastTx) / secs / 1_048_576d;
        }
        _lastRx = rx; _lastTx = tx; _lastNet = now;
        return (down, up);
    }

    /// <summary>
    /// Temperatura da CPU. No Ryzen ela vem do sensor Tctl/Tdie, lido pela biblioteca
    /// LibreHardwareMonitor através do driver PawnIO (precisa estar instalado e o
    /// Cyl Win rodando como administrador). Sem isso, tenta a zona térmica ACPI
    /// do Windows e, se nada funcionar, devolve null e o modal mostra "—".
    /// </summary>
    public double? Temperature()
    {
        if (!_lhmTried) InitLhm();

        if (_cpuHw is not null && _tempSensor is not null)
        {
            try
            {
                _cpuHw.Update();
                if (_tempSensor.Value is float v && v > 0 && v < 120) return v;
            }
            catch { }
        }

        if (_tempUnavailable) return null;
        try
        {
            using var s = new ManagementObjectSearcher(@"root\WMI",
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (ManagementObject o in s.Get())
            {
                double kelvin10 = Convert.ToDouble(o["CurrentTemperature"]);
                double c = kelvin10 / 10.0 - 273.15;
                if (c > 0 && c < 120) return c;
            }
        }
        catch { }
        _tempUnavailable = true;
        return null;
    }

    private void InitLhm()
    {
        _lhmTried = true;

        if (!IsAdmin())
        {
            TempHint = "abra como admin";
            return;
        }
        if (!IsPawnIoInstalled())
        {
            TempHint = "instale o PawnIO";
            return;
        }

        try
        {
            _lhm = new Computer { IsCpuEnabled = true };
            _lhm.Open();
            _cpuHw = _lhm.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            _cpuHw?.Update();
            _tempSensor = _cpuHw?.Sensors
                .Where(s => s.SensorType == SensorType.Temperature)
                .OrderByDescending(s => s.Name.Contains("Tctl") || s.Name.Contains("Tdie"))
                .ThenByDescending(s => s.Name.Contains("Package"))
                .FirstOrDefault();
            TempHint = _tempSensor is null ? "sensor não encontrado" : "";
        }
        catch
        {
            TempHint = "erro ao ler o sensor";
        }
    }

    public static bool IsAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool IsPawnIoInstalled() =>
        Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO") is not null;

    /// <summary>Espaço do disco: (livre, total) em GB, ou null se o disco não existir.</summary>
    public static (double free, double total)? Disk(string letter)
    {
        try
        {
            var d = new DriveInfo(letter);
            if (!d.IsReady) return null;
            return (d.AvailableFreeSpace / 1_073_741_824d, d.TotalSize / 1_073_741_824d);
        }
        catch { return null; }
    }

    public static string DiskLabel(string letter)
    {
        try { var l = new DriveInfo(letter).VolumeLabel; return string.IsNullOrWhiteSpace(l) ? "Disco local" : l; }
        catch { return "Disco"; }
    }

    public static TimeSpan Uptime() => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public static bool IsWifi() =>
        NetworkInterface.GetAllNetworkInterfaces().Any(n =>
            n.OperationalStatus == OperationalStatus.Up &&
            n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

    public static bool IsOnline() => NetworkInterface.GetIsNetworkAvailable();

    // ---------- auxiliares ----------

    private static PerformanceCounter? TryCounter(string cat, string counter, string instance)
    {
        try { return new PerformanceCounter(cat, counter, instance, true); }
        catch { return null; }
    }

    private static double Safe(Func<double> f)
    {
        try { return f(); } catch { return 0; }
    }

    private static (double total, double avail) ReadMemory()
    {
        var m = new Native.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
        if (!Native.GlobalMemoryStatusEx(ref m)) return (0, 0);
        return (m.ullTotalPhys / 1_073_741_824d, m.ullAvailPhys / 1_073_741_824d);
    }

    private static (long rx, long tx) ReadNet()
    {
        long rx = 0, tx = 0;
        foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (n.OperationalStatus != OperationalStatus.Up) continue;
            if (n.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
            try
            {
                var s = n.GetIPStatistics();
                rx += s.BytesReceived;
                tx += s.BytesSent;
            }
            catch { }
        }
        return (rx, tx);
    }

    private static string ReadCpuName()
    {
        var raw = Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
            "ProcessorNameString", "CPU") as string ?? "CPU";
        // "AMD Ryzen 5 5600GT with Radeon Graphics" -> "Ryzen 5 5600GT"
        raw = raw.Replace("with Radeon Graphics", "").Replace("AMD ", "").Replace("(R)", "").Replace("(TM)", "");
        return string.Join(' ', raw.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ReadGpuName(string cpuName)
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (ManagementObject o in s.Get())
            {
                var name = (o["Name"] as string ?? "").Replace("(TM)", "").Replace("AMD ", "").Trim();
                if (name.Length == 0) continue;
                // O 5600G/5600GT aparece só como "Radeon Graphics"; a GPU dele é a Vega 7.
                return name == "Radeon Graphics" && cpuName.Contains("5600G") ? "Radeon Vega 7" : name;
            }
        }
        catch { }
        return "GPU";
    }

    public void Dispose()
    {
        _cpu?.Dispose();
        try { _lhm?.Close(); } catch { }
        foreach (var c in _gpu.Values) c.Dispose();
        _gpu.Clear();
    }
}
