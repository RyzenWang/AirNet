using System.IO.Compression;
using AirNet.Core.Diagnostics;
using Xunit;

namespace AirNet.Core.Tests;

public sealed class DiagnosticBundleTests
{
    [Fact]
    public void Crash_entries_record_what_is_needed_to_reproduce()
    {
        var directory = TempDirectory();
        try
        {
            var path = CrashLog.Write(
                "unit-test",
                new InvalidOperationException("boom"),
                note: "step 3",
                dataDirectory: directory);

            Assert.NotNull(path);
            var text = File.ReadAllText(path!);
            Assert.Contains("source  : unit-test", text);
            Assert.Contains("InvalidOperationException", text);
            Assert.Contains("boom", text);
            Assert.Contains("step 3", text);
            Assert.Contains("version :", text);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void A_missing_exception_still_records_the_event()
    {
        var directory = TempDirectory();
        try
        {
            var path = CrashLog.Write("TaskScheduler", null, dataDirectory: directory);

            Assert.NotNull(path);
            Assert.Contains("source  : TaskScheduler", File.ReadAllText(path!));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void Crash_log_writes_never_throw_on_a_bad_directory()
    {
        // A colon is illegal in a Windows path segment; the writer must swallow it, not rethrow.
        var path = CrashLog.Write("test", new Exception("x"), dataDirectory: "Z:\\:invalid::dir");

        Assert.True(path is null || File.Exists(path));
    }

    [Fact]
    public void The_summary_says_which_build_and_what_is_inside()
    {
        var summary = DiagnosticBundle.BuildSummary("0.2.9", null, note: "启动即闪退");

        Assert.Contains("AirNet 诊断包", summary);
        Assert.Contains("0.2.9", summary);
        Assert.Contains("crash.log", summary);
        Assert.Contains("启动即闪退", summary);
        Assert.Contains("隐私", summary);
    }

    [Fact]
    public void Existing_files_are_packed_and_missing_ones_are_skipped()
    {
        var directory = TempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{}");
            File.WriteAllText(Path.Combine(directory, "history.jsonl"), "{}\n{}");
            var target = Path.Combine(directory, "bundle.zip");

            var collected = DiagnosticBundle.Write(target, directory, "summary body");

            Assert.Equal(2, collected);
            using var archive = ZipFile.OpenRead(target);
            var names = archive.Entries.Select(e => e.FullName).ToList();
            Assert.Contains("summary.txt", names);
            Assert.Contains("settings.json", names);
            Assert.Contains("history.jsonl", names);
            Assert.DoesNotContain("crash.log", names);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void Rewriting_over_an_existing_bundle_replaces_it()
    {
        var directory = TempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{}");
            var target = Path.Combine(directory, "bundle.zip");

            DiagnosticBundle.Write(target, directory, "first");
            DiagnosticBundle.Write(target, directory, "second");

            using var archive = ZipFile.OpenRead(target);
            using var reader = new StreamReader(archive.GetEntry("summary.txt")!.Open());
            Assert.Equal("second", reader.ReadToEnd());
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "airnet-bundle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp directory must not fail the suite.
        }
    }
}
