using AirNet.Core.Services;
using Xunit;

namespace AirNet.Core.Tests;

public sealed class PackagedAppTests
{
    [Fact]
    public void The_version_is_read_from_the_package_full_name()
    {
        Assert.Equal("1.0.0.0", PackagedApp.ParseVersion("RyzenWang.AirNet_1.0.0.0_x64__d87d6846"));
        Assert.Equal("1.2.3.0", PackagedApp.ParseVersion("Contoso.App_1.2.3.0_arm64__abcdef"));
    }

    [Fact]
    public void An_unpackaged_process_has_no_full_name()
    {
        Assert.Null(PackagedApp.ParseVersion(null));
        Assert.Null(PackagedApp.ParseVersion(string.Empty));
        Assert.Null(PackagedApp.ParseVersion("   "));
    }

    [Fact]
    public void A_malformed_name_yields_no_version_instead_of_throwing()
    {
        Assert.Null(PackagedApp.ParseVersion("NoUnderscoresHere"));
        Assert.Null(PackagedApp.ParseVersion("Name_notaversion_x64__id"));
        Assert.Null(PackagedApp.ParseVersion("_1.0.0.0_"));
    }

    [Fact]
    public void Running_from_a_test_host_is_not_packaged()
    {
        // dotnet test runs unpackaged; if this ever fails, the detection is wrong, not the test.
        Assert.False(PackagedApp.IsPackaged);
        Assert.Null(PackagedApp.PackageVersion);
    }
}
