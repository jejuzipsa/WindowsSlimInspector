using System.Text;

namespace WindowsSlimInspector.Services;

public static class LogService
{
    public static string LogDirectory
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static void AppendSlimLog(string message)
    {
        var path = Path.Combine(LogDirectory, "SlimManager.log");
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
        File.AppendAllText(path, line, new UTF8Encoding(false));
    }
}
