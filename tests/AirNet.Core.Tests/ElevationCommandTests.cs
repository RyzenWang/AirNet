using AirNet.Core.Services;
using Xunit;

namespace AirNet.Core.Tests;

public sealed class ElevationCommandTests
{
    [Fact]
    public void Mac_wraps_the_command_in_an_admin_shell_script()
    {
        var script = ElevationCommands.MacAppleScript("networksetup -setdnsservers \"Wi-Fi\" 223.5.5.5");

        Assert.StartsWith("do shell script \"", script);
        Assert.EndsWith("with administrator privileges", script);
        Assert.Contains("networksetup", script);
    }

    [Fact]
    public void Mac_escapes_quotes_and_backslashes_so_a_command_cannot_break_out()
    {
        var script = ElevationCommands.MacAppleScript("echo \"x\" && echo C:\\temp");
        var inner = script["do shell script \"".Length..^"\" with administrator privileges".Length];

        Assert.Equal("echo \\\"x\\\" && echo C:\\\\temp", inner);
    }

    [Fact]
    public void Linux_runs_through_a_shell_because_commands_use_pipes()
    {
        var arguments = ElevationCommands.LinuxArguments("nmcli con up \"Wi-Fi\" && echo done");

        Assert.Equal(["/bin/sh", "-c", "nmcli con up \"Wi-Fi\" && echo done"], arguments);
    }

    [Theory]
    [InlineData("execution error: User canceled. (-128)", "已取消提权")]
    [InlineData("osascript: Operation not permitted", "osascript: Operation not permitted")]
    [InlineData("", "osascript 执行失败")]
    public void Mac_failures_are_translated_for_the_user(string stderr, string expected)
    {
        Assert.Equal(expected, ElevationCommands.DescribeMacFailure(stderr));
    }

    [Theory]
    [InlineData(126, "已取消提权")]
    [InlineData(127, "没有获得授权（pkexec 被拒绝）")]
    [InlineData(5, "退出码 5")]
    public void Linux_exit_codes_are_translated_for_the_user(int exitCode, string expected)
    {
        Assert.Equal(expected, ElevationCommands.DescribeLinuxFailure(exitCode));
    }
}
