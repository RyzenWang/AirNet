using System.Text;
using System.IO.Compression;
using System.Text.Json;

namespace AirNet.Core.Diagnostics;

/// <summary>
/// Resolves where AirNet keeps its local state, so crash logs, history and the diagnostic bundle
/// all agree on one directory.
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// <c>AIRNET_DATA_DIR</c> wins so the screenshot pipeline and tests can use an isolated
    /// directory; otherwise <c>%LOCALAPPDATA%\AirNet</c> (and its platform equivalents).
    /// </summary>
    public static string DataDirectory(string? overridePath = null) =>
        overridePath
        ?? Environment.GetEnvironmentVariable("AIRNET_DATA_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AirNet");

    public static string CrashLogPath(string? dataDirectory = null) =>
        Path.Combine(DataDirectory(dataDirectory), "crash.log");
}

/// <summary>
/// Last-resort crash writer.
/// </summary>
/// <remarks>
/// Without this, an AOT build that dies on a user's machine leaves nothing behind: the window
/// vanishes and the report we get is "打不开". The handler is deliberately dumb — no dialog, no
/// formatting that can throw, just an append to a file next to the history.
/// </remarks>
public static class CrashLog
{
    private const long MaxBytes = 256 * 1024;

    /// <summary>Appends one crash entry; never throws, because it runs while everything is broken.</summary>
    public static string? Write(
        string source,
        Exception? exception,
        string? note = null,
        string? dataDirectory = null)
    {
        try
        {
            var path = AppPaths.CrashLogPath(dataDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // Keep the file small enough that it can still be attached to an issue.
            if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
            {
                File.Move(path, path + ".old", overwrite: true);
            }

            var entry = new StringBuilder()
                .AppendLine("----")
                .AppendLine($"at      : {DateTimeOffset.Now:O}")
                .AppendLine($"source  : {source}")
                .AppendLine($"version : {AppVersion.Current}")
                .AppendLine($"os      : {Environment.OSVersion} ({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture})")
                .AppendLine($"runtime : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");

            if (!string.IsNullOrWhiteSpace(note))
            {
                entry.AppendLine($"note    : {note}");
            }

            if (exception is not null)
            {
                entry.AppendLine($"type    : {exception.GetType().FullName}");
                entry.AppendLine($"message : {exception.Message}");
                entry.AppendLine("stack   :");
                entry.AppendLine(exception.ToString());
            }

            File.AppendAllText(path, entry.ToString());
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Writing the crash log must never be the reason the process dies.
            return null;
        }
    }

    /// <summary>Installs the process-wide hooks once; safe to call from the app entry point.</summary>
    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Write("AppDomain.UnhandledException", args.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Write("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };
    }
}

/// <summary>Version string shared by the crash log, the JSON probe and the diagnostic bundle.</summary>
public static class AppVersion
{
    public static string Current { get; } = Read();

    private static string Read()
    {
        try
        {
            var version = typeof(AppVersion).Assembly.GetName().Version;
            return version is null ? "0.0.0" : version.ToString(3);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return "0.0.0";
        }
    }
}

/// <summary>
/// Collects the local state a maintainer needs to diagnose a report: settings, history, the last
/// crash, DNS restore records and a human-readable header. Written as a zip the user can attach.
/// </summary>
public static class DiagnosticBundle
{
    /// <summary>Files copied verbatim when present; order defines the summary listing.</summary>
    public static readonly string[] CollectedFiles =
    [
        "settings.json",
        "history.jsonl",
        "sites.json",
        "sites.jsonl",
        "dns-backup.json",
        "crash.log",
    ];

    /// <summary>The header text: what the bundle is, so nobody has to guess which build it came from.</summary>
    public static string BuildSummary(
        string version,
        NetworkSnapshot? snapshot,
        string? note = null)
    {
        var text = new StringBuilder()
            .AppendLine("AirNet 诊断包")
            .AppendLine($"生成时间 : {DateTimeOffset.Now:O}")
            .AppendLine($"版本     : {version}")
            .AppendLine($"系统     : {Environment.OSVersion} ({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture})")
            .AppendLine($"运行时   : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}")
            .AppendLine();

        if (snapshot is not null)
        {
            text.AppendLine("最近一次测量")
                .AppendLine($"  评分     : {snapshot.Score} ({snapshot.GradeLabel})")
                .AppendLine($"  延迟     : {snapshot.Latency.AverageMs:0.0} ms / P95 {snapshot.Latency.P95Ms:0.0} ms")
                .AppendLine($"  抖动·丢包: {snapshot.Latency.JitterMs:0.0} ms / {snapshot.Latency.LossPercent:0.0}%")
                .AppendLine($"  网卡     : {snapshot.Interface.Kind} · {snapshot.Interface.Name}")
                .AppendLine($"  双栈     : {snapshot.DualStack.VerdictText}")
                .AppendLine($"  干扰     : {(snapshot.Interference.IsInterfering ? snapshot.Interference.ChipText : "无")}")
                .AppendLine();
        }

        text.AppendLine("包含文件")
            .AppendLine("  summary.txt          本文件")
            .AppendLine("  settings.json        用户偏好（不含任何网络内容）")
            .AppendLine("  history.jsonl        本机测量历史（含延迟/丢包，不含网页内容）")
            .AppendLine("  sites.json / jsonl   自定义站点与站点历史")
            .AppendLine("  dns-backup.json      DNS 变更的还原记录")
            .AppendLine("  crash.log            崩溃记录（若有）")
            .AppendLine();

        if (!string.IsNullOrWhiteSpace(note))
        {
            text.AppendLine("说明").AppendLine("  " + note).AppendLine();
        }

        text.AppendLine("隐私")
            .AppendLine("  包内不含浏览记录、不含网页内容、不含账号信息。")
            .AppendLine("  历史里可能出现局域网地址；分享前请自行确认。");

        return text.ToString();
    }

    /// <summary>Writes the bundle to <paramref name="targetPath"/> and returns how many files it holds.</summary>
    public static int Write(string targetPath, string dataDirectory, string summary)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(targetPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        var count = 0;
        using (var archive = ZipFile.Open(
                   targetPath, ZipArchiveMode.Create))
        {
            var summaryEntry = archive.CreateEntry("summary.txt", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(summaryEntry.Open(), new UTF8Encoding(false)))
            {
                writer.Write(summary);
            }

            foreach (var name in CollectedFiles)
            {
                var path = Path.Combine(dataDirectory, name);
                if (!File.Exists(path))
                {
                    continue;
                }

                archive.CreateEntryFromFile(path, name, CompressionLevel.Optimal);
                count++;
            }
        }

        return count;
    }
}
