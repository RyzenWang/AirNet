using AirNet.Core.Diagnostics;

namespace AirNet.Core.Tests;

public sealed class DiagnosisEngineTests
{
    [Theory]
    [InlineData(90, HealthGrade.Excellent)]
    [InlineData(89, HealthGrade.Good)]
    [InlineData(75, HealthGrade.Good)]
    [InlineData(74, HealthGrade.Fair)]
    [InlineData(60, HealthGrade.Fair)]
    [InlineData(59, HealthGrade.Poor)]
    public void GradeBoundariesAreStable(int score, HealthGrade expected)
    {
        Assert.Equal(expected, DiagnosisEngine.GradeOf(score));
    }

    [Fact]
    public void TwoPercentLossPreventsAnExcellentGrade()
    {
        var snapshot = Evaluate(Measurement(averageMs: 10, jitterMs: 0, lossPercent: 2), down: 0);

        Assert.True(snapshot.Score < 90);
        Assert.NotEqual(HealthGrade.Excellent, snapshot.Grade);
    }

    [Fact]
    public void MissingBandwidthIsNotPresentedAsAMeasuredZero()
    {
        var snapshot = Evaluate(Measurement(averageMs: 20, jitterMs: 1, lossPercent: 0), down: 0);

        Assert.False(snapshot.HasBandwidth);
        var download = Assert.Single(snapshot.Scenarios, s => s.Name == "大文件下载");
        Assert.False(download.IsMeasured);
        Assert.Equal("—", download.ScoreText);
        Assert.Contains("未测速", snapshot.Summary);
    }

    [Fact]
    public void MeasuredBandwidthTurnsTheDownloadScenarioOn()
    {
        var snapshot = Evaluate(Measurement(averageMs: 20, jitterMs: 1, lossPercent: 0), down: 384.2);

        Assert.True(snapshot.HasBandwidth);
        Assert.False(snapshot.HasUpload);
        var download = Assert.Single(snapshot.Scenarios, s => s.Name == "大文件下载");
        Assert.True(download.IsMeasured);
        Assert.NotEqual("—", download.ScoreText);
    }

    [Fact]
    public void MissingUploadDoesNotPenaliseTheMeetingScenario()
    {
        var snapshot = Evaluate(Measurement(averageMs: 10, jitterMs: 0, lossPercent: 0), down: 0);

        var meeting = Assert.Single(snapshot.Scenarios, s => s.Name == "视频会议");
        Assert.Equal(100, meeting.Score);
    }

    [Fact]
    public void Lopsided_bandwidth_is_reported_on_the_upload_side()
    {
        var snapshot = DiagnosisEngine.Evaluate(
            Measurement(averageMs: 20, jitterMs: 1, lossPercent: 0), downloadMbps: 400, uploadMbps: 5);

        var finding = Assert.Single(snapshot.Findings, f => f.Title.Contains("上行"));
        Assert.Equal(Severity.Warn, finding.Severity);
        Assert.Contains("视频会议", finding.Detail);
    }

    [Fact]
    public void Balanced_bandwidth_produces_no_asymmetry_finding()
    {
        var snapshot = DiagnosisEngine.Evaluate(
            Measurement(averageMs: 20, jitterMs: 1, lossPercent: 0), downloadMbps: 300, uploadMbps: 90);

        Assert.DoesNotContain(snapshot.Findings, f => f.Title.Contains("上行"));
    }

    [Fact]
    public void A_vpn_is_called_out_before_the_numbers_are_read()
    {
        var snapshot = DiagnosisEngine.Evaluate(
            Measurement(
                averageMs: 20,
                jitterMs: 1,
                lossPercent: 0,
                interference: InterferenceAnalyzer.Evaluate(false, null, ["WireGuard Tunnel"])),
            downloadMbps: 0,
            uploadMbps: 0);

        var finding = Assert.Single(snapshot.Findings, f => f.Title.Contains("VPN"));
        Assert.Equal(Severity.Warn, finding.Severity);
        Assert.Contains("WireGuard", finding.Detail);
    }

    [Fact]
    public void A_plain_proxy_is_only_an_information_line()
    {
        var snapshot = DiagnosisEngine.Evaluate(
            Measurement(
                averageMs: 20,
                jitterMs: 1,
                lossPercent: 0,
                interference: InterferenceAnalyzer.Evaluate(true, "127.0.0.1:7890", null)),
            downloadMbps: 0,
            uploadMbps: 0);

        var finding = Assert.Single(snapshot.Findings, f => f.Title.Contains("代理"));
        Assert.Equal(Severity.Info, finding.Severity);
    }

    private static NetworkSnapshot Evaluate(MeasurementResult raw, double down) =>
        DiagnosisEngine.Evaluate(raw, down, 0);

    private static MeasurementResult Measurement(
        double averageMs,
        double jitterMs,
        double lossPercent,
        InterferenceInfo? interference = null)
    {
        var nic = new InterfaceInfo(
            "Ethernet", "有线以太网", false, null, null, string.Empty,
            1000, "192.168.1.2", "192.168.1.1", "223.5.5.5");

        var received = (int)Math.Round(10 * (1 - (lossPercent / 100)));
        var stats = new LatencyStats(
            averageMs,
            averageMs,
            averageMs,
            jitterMs,
            lossPercent,
            Sent: 10,
            Received: received);

        var timing = new LayerTiming("www.baidu.com", 8, 5, 20, 30, true);
        return new MeasurementResult(
            nic,
            stats,
            ProbeMode.Icmp,
            new ProbeDiagnostics(true, true, 10, received, 10, received),
            timing,
            DualStackResult.Unavailable,
            interference ?? InterferenceInfo.None,
            DnsComparison.Unavailable,
            [averageMs],
            [new HopReading("家庭网关", "192.168.1.1", 1.5, true)],
            "203.0.113.42",
            DateTimeOffset.Now);
    }
}
