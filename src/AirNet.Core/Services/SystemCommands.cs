using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AirNet.Core.Services;

/// <summary>Outcome of an elevated command, including the "user said no to UAC" case.</summary>
public sealed record CommandResult(bool Started, int? ExitCode, string Message)
{
    public bool Succeeded => Started && ExitCode == 0;

    public static CommandResult Declined(string message) => new(false, null, message);
}

/// <summary>
/// Runs system-changing commands through the platform's elevation dialog. AirNet never elevates
/// silently: the caller shows the exact command first, and a declined prompt is a normal result.
/// </summary>
public static class ElevatedCommandRunner
{
    public static async Task<CommandResult> RunAsync(string command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return CommandResult.Declined("没有可执行的命令");
        }

        if (!OperatingSystem.IsWindows())
        {
            return OperatingSystem.IsMacOS()
                ? await RunMacAsync(command, cancellationToken).ConfigureAwait(false)
                : OperatingSystem.IsLinux()
                    ? await RunLinuxAsync(command, cancellationToken).ConfigureAwait(false)
                    : CommandResult.Declined("当前平台不支持自动提权，请手动执行上方命令");
        }

        var scriptPath = Path.Combine(Path.GetTempPath(), $"airnet-{Guid.NewGuid():N}.cmd");
        try
        {
            await File.WriteAllTextAsync(scriptPath, "@echo off\r\n" + command + "\r\n", cancellationToken)
                .ConfigureAwait(false);

            var startInfo = new ProcessStartInfo("cmd.exe", $"/c \"{scriptPath}\"")
            {
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return CommandResult.Declined("无法启动提权进程");
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return new CommandResult(true, process.ExitCode, process.ExitCode == 0 ? "已完成" : $"退出码 {process.ExitCode}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // 1223 = ERROR_CANCELLED, i.e. the user dismissed the UAC prompt.
            return CommandResult.Declined(ex.NativeErrorCode == 1223 ? "已取消提权" : ex.Message);
        }
        finally
        {
            try
            {
                File.Delete(scriptPath);
            }
            catch (IOException)
            {
                // best effort
            }
        }
    }
    private static async Task<CommandResult> RunMacAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo("osascript")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add(ElevationCommands.MacAppleScript(command));

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return CommandResult.Declined("无法启动 osascript");
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            return process.ExitCode == 0
                ? new CommandResult(true, 0, "已应用")
                : CommandResult.Declined(ElevationCommands.DescribeMacFailure(error));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return CommandResult.Declined(ex.Message);
        }
    }

    private static async Task<CommandResult> RunLinuxAsync(string command, CancellationToken cancellationToken)
    {
        const string pkexec = "/usr/bin/pkexec";
        if (!File.Exists(pkexec))
        {
            return CommandResult.Declined(
                "未找到 pkexec（polkit），请用 sudo 执行上方命令");
        }

        try
        {
            var startInfo = new ProcessStartInfo(pkexec)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (var argument in ElevationCommands.LinuxArguments(command))
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return CommandResult.Declined("无法启动 pkexec");
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0
                ? new CommandResult(true, 0, "已应用")
                : CommandResult.Declined(ElevationCommands.DescribeLinuxFailure(process.ExitCode));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return CommandResult.Declined(ex.Message);
        }
    }
}
/// <summary>
/// The strings each platform's elevation path needs. Kept apart from the process plumbing so
/// escaping and exit-code mapping can be tested without an OS dialog.
/// </summary>
public static class ElevationCommands
{
    /// <summary>
    /// Wraps a shell command in an AppleScript "do shell script … with administrator privileges",
    /// escaping backslashes and quotes so a command containing them cannot break out.
    /// </summary>
    public static string MacAppleScript(string command)
    {
        var escaped = command
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

        return $"do shell script \"{escaped}\" with administrator privileges";
    }

    /// <summary>pkexec runs the command directly; a shell is needed because our commands use pipes.</summary>
    public static string[] LinuxArguments(string command) => ["/bin/sh", "-c", command];

    /// <summary>osascript reports a dismissed dialog as "User canceled." on stderr.</summary>
    public static string DescribeMacFailure(string stderr) =>
        stderr.Contains("cancel", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("(-128)", StringComparison.Ordinal)
            ? "已取消提权"
            : string.IsNullOrWhiteSpace(stderr) ? "osascript 执行失败" : stderr.Trim();

    /// <summary>126 = the auth dialog was dismissed; 127 = not authorized.</summary>
    public static string DescribeLinuxFailure(int exitCode) => exitCode switch
    {
        126 => "已取消提权",
        127 => "没有获得授权（pkexec 被拒绝）",
        _ => $"退出码 {exitCode}",
    };
}

/// <summary>The DNS we replaced, so it can be put back after a restart.</summary>
public sealed class DnsBackup
{
    public string AdapterName { get; set; } = string.Empty;
    public string OriginalDns { get; set; } = string.Empty;
    public string RestoreCommand { get; set; } = string.Empty;
    public DateTimeOffset AppliedAt { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DnsBackup))]
internal sealed partial class DnsBackupJsonContext : JsonSerializerContext;

/// <summary>Persists the single "what did we change" record next to the history file.</summary>
public sealed class DnsBackupStore
{
    public DnsBackupStore(string? directory = null)
    {
        var root = directory
            ?? Environment.GetEnvironmentVariable("AIRNET_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AirNet");
        Directory.CreateDirectory(root);
        FilePath = Path.Combine(root, "dns-backup.json");
    }

    public string FilePath { get; }

    public async Task SaveAsync(DnsBackup backup, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(backup, DnsBackupJsonContext.Default.DnsBackup);
        await File.WriteAllTextAsync(FilePath, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DnsBackup?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize(json, DnsBackupJsonContext.Default.DnsBackup);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Clear()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }
}
