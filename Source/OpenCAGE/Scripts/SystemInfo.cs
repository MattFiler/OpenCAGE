using System.Management;
using System.Runtime.InteropServices;
using System.Text;

public static class SystemInfo
{
    public static string GetOsName()
    {
        return RuntimeInformation.OSDescription;
    }

    public static string GetCpuName()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return obj["Name"]?.ToString() ?? "Unknown";
                    }
                }
            }
        }
        catch { }
        return "Not available";
    }

    public static string GetTotalPhysicalMemory()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var totalMemoryBytes = (ulong)obj["TotalPhysicalMemory"];
                        return $"{totalMemoryBytes / 1024 / 1024 / 1024} GB";
                    }
                }
            }
        }
        catch { }
        return "Not available";
    }
}