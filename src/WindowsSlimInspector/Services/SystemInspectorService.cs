using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Text;

namespace WindowsSlimInspector.Services;

public sealed class SystemInspectorService
{
    public async Task<string> RunAsync()
    {
        return await Task.Run(() =>
        {
            var sb = new StringBuilder();
            sb.AppendLine("WindowsSlimInspector - System Inspection Report");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Machine: {Environment.MachineName}");
            sb.AppendLine($"User: {Environment.UserName}");
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
            sb.AppendLine(new string('=', 72));

            AppendSection(sb, "CPU / Memory / GPU", InspectHardware());
            AppendSection(sb, "Processes", InspectProcesses());
            AppendSection(sb, "Services", InspectServices());
            AppendSection(sb, "Startup Entries", InspectStartup());
            AppendSection(sb, "Scheduled Tasks", RunCommand("schtasks", "/Query /FO LIST /V"));
            AppendSection(sb, "Network Adapters", InspectNetworkAdapters());
            AppendSection(sb, "Active Network Connections", RunCommand("netstat", "-abno"));
            AppendSection(sb, "IP Configuration", RunCommand("ipconfig", "/all"));
            AppendSection(sb, "Hosts File", ReadHostsFile());
            AppendSection(sb, "Windows Defender Status", RunPowerShell("Get-MpComputerStatus | Format-List *"));
            AppendSection(sb, "Windows Security / Firewall", RunPowerShell("Get-NetFirewallProfile | Format-List Name,Enabled,DefaultInboundAction,DefaultOutboundAction"));
            AppendSection(sb, "Recent Startup Approved Entries", InspectStartupApproved());

            var path = Path.Combine(LogService.LogDirectory, $"SystemInspector_{DateTime.Now:yyyy-MM-dd_HHmmss}.txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return path;
        });
    }

    private static void AppendSection(StringBuilder sb, string title, string content)
    {
        sb.AppendLine();
        sb.AppendLine($"## {title}");
        sb.AppendLine(new string('-', 72));
        sb.AppendLine(string.IsNullOrWhiteSpace(content) ? "(no data)" : content.TrimEnd());
    }

    private static string InspectHardware()
    {
        var sb = new StringBuilder();
        try
        {
            using var cpu = new ManagementObjectSearcher("SELECT Name,NumberOfCores,NumberOfLogicalProcessors FROM Win32_Processor");
            foreach (ManagementObject o in cpu.Get())
                sb.AppendLine($"CPU: {o["Name"]} | Cores={o["NumberOfCores"]} | Threads={o["NumberOfLogicalProcessors"]}");

            using var cs = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (ManagementObject o in cs.Get())
                sb.AppendLine($"RAM: {Convert.ToDouble(o["TotalPhysicalMemory"]) / 1024 / 1024 / 1024:F1} GB");

            using var gpu = new ManagementObjectSearcher("SELECT Name,AdapterRAM,DriverVersion FROM Win32_VideoController");
            foreach (ManagementObject o in gpu.Get())
                sb.AppendLine($"GPU: {o["Name"]} | Driver={o["DriverVersion"]}");
        }
        catch (Exception ex) { sb.AppendLine($"ERROR: {ex.Message}"); }
        return sb.ToString();
    }

    private static string InspectProcesses()
    {
        var sb = new StringBuilder();
        foreach (var p in Process.GetProcesses().OrderByDescending(p => SafeWorkingSet(p)))
        {
            try
            {
                string path = "";
                try { path = p.MainModule?.FileName ?? ""; } catch { }
                sb.AppendLine($"PID={p.Id,-7} RAM={p.WorkingSet64 / 1024 / 1024,6} MB  {p.ProcessName}  {path}");
            }
            catch { }
        }
        return sb.ToString();
    }

    private static long SafeWorkingSet(Process p)
    {
        try { return p.WorkingSet64; } catch { return 0; }
    }

    private static string InspectServices()
    {
        return RunPowerShell("Get-CimInstance Win32_Service | Sort-Object State,Name | Select-Object State,StartMode,Name,DisplayName,PathName | Format-Table -AutoSize");
    }

    private static string InspectStartup()
    {
        var sb = new StringBuilder();
        foreach (var tuple in new[]
        {
            (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU Run"),
            (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM Run"),
            (Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM WOW6432 Run")
        })
        {
            sb.AppendLine($"[{tuple.Item3}]");
            try
            {
                using var key = tuple.Item1.OpenSubKey(tuple.Item2);
                if (key is null) { sb.AppendLine("(missing)"); continue; }
                foreach (var name in key.GetValueNames()) sb.AppendLine($"{name} = {key.GetValue(name)}");
            }
            catch (Exception ex) { sb.AppendLine($"ERROR: {ex.Message}"); }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string InspectStartupApproved()
    {
        return RunPowerShell("$paths=@('HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run','HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run','HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run32'); foreach($p in $paths){ Write-Output \"[$p]\"; if(Test-Path $p){ Get-ItemProperty $p | Format-List * } } ");
    }

    private static string InspectNetworkAdapters()
    {
        var sb = new StringBuilder();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            sb.AppendLine($"{nic.Name} | {nic.NetworkInterfaceType} | {nic.OperationalStatus} | {nic.Description}");
        return sb.ToString();
    }

    private static string ReadHostsFile()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
            return File.Exists(path) ? File.ReadAllText(path) : "Hosts file not found.";
        }
        catch (Exception ex) { return $"ERROR: {ex.Message}"; }
    }

    private static string RunPowerShell(string command) => RunCommand("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"");

    private static string RunCommand(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(30000);
            return stdout + (string.IsNullOrWhiteSpace(stderr) ? "" : Environment.NewLine + "STDERR:" + Environment.NewLine + stderr);
        }
        catch (Exception ex) { return $"ERROR: {ex.Message}"; }
    }
}
