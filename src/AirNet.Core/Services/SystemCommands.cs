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
            // macOS needs osascript and Linux needs pkexec/polkit; until that is verified on real
            // machines the command is shown for manual use instead of half-working.
            return CommandResult.Declined("当前平台请手动执行上方命令（自动提权暂只支持 Windows）");
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
