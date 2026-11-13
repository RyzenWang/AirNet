using System.Runtime.InteropServices;
using System.Text;

namespace AirNet.Core.Probes;

/// <summary>One visible access point returned by the Windows WLAN BSS list.</summary>
public sealed record WifiNeighbor(
    string Ssid,
    string Bssid,
    int SignalQuality,
    int RssiDbm,
    string Phy,
    double RxMbps,
    double TxMbps)
{
    public string SignalText => $"{SignalQuality}% · {RssiDbm} dBm";
    public string RateText => RxMbps > 0 ? $"收 {RxMbps:0} / 发 {TxMbps:0} Mbps" : "速率未上报";
}

public sealed record WifiNeighborScan(
    IReadOnlyList<WifiNeighbor> Neighbors,
    bool IsSupported,
    string? Note = null)
{
    public bool HasNeighbors => Neighbors.Count > 0;
}

/// <summary>
/// Read-only neighbor scan through wlanapi's WlanGetNetworkBssList. It lists SSID/BSSID,
/// signal quality and PHY/rate; Windows does not expose the channel in this structure, so the
/// UI deliberately does not invent one.
/// </summary>
public static class WlanNeighborProbe
{
    private const uint ClientVersion = 2;
    private const uint BssTypeAny = 3;
    private const int InterfaceInfoSize = 532;
    private const int InterfaceListHeader = 8;
    private const int BssEntrySize = 72;

    public static Task<WifiNeighborScan> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    public static WifiNeighborScan Scan(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new WifiNeighborScan([], false, "邻区扫描仅支持 Windows（WLAN API）");
        }

        var found = new Dictionary<string, WifiNeighbor>(StringComparer.OrdinalIgnoreCase);
        string? note = null;

        IntPtr client = IntPtr.Zero;
        try
        {
            if (WlanOpenHandle(ClientVersion, IntPtr.Zero, out _, out client) != 0 || client == IntPtr.Zero)
            {
                return new WifiNeighborScan([], true, "WLAN 服务不可用（无线网卡或服务未启用）");
            }

            if (WlanEnumInterfaces(client, IntPtr.Zero, out var listPtr) != 0 || listPtr == IntPtr.Zero)
            {
                return new WifiNeighborScan([], true, "无法枚举无线网卡");
            }

            try
            {
                var count = Marshal.ReadInt32(listPtr);
                var guids = new List<Guid>();
                for (var i = 0; i < count; i++)
                {
                    var entry = IntPtr.Add(listPtr, InterfaceListHeader + (i * InterfaceInfoSize));
                    var state = Marshal.ReadInt32(entry + 528);
                    if (state != 1)
                    {
                        continue;
                    }

                    var guidBytes = new byte[16];
                    Marshal.Copy(entry, guidBytes, 0, 16);
                    guids.Add(new Guid(guidBytes));
                }

                // If no interface is associated, still ask the driver: some cards can scan while
                // disconnected, and an explicit empty result is more useful than "unsupported".
                if (guids.Count == 0)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var entry = IntPtr.Add(listPtr, InterfaceListHeader + (i * InterfaceInfoSize));
                        var guidBytes = new byte[16];
                        Marshal.Copy(entry, guidBytes, 0, 16);
                        guids.Add(new Guid(guidBytes));
                    }
                }

                foreach (var guid in guids)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (QueryBssList(client, guid, out var list) && list is not null)
                    {
                        foreach (var neighbor in list)
                        {
                            found[neighbor.Bssid] = neighbor;
                        }
                    }
                }
            }
            finally
            {
                WlanFreeMemory(listPtr);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return new WifiNeighborScan([], false, "当前系统没有 WLAN API");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            note = ex.Message;
        }
        finally
        {
            if (client != IntPtr.Zero)
            {
                WlanCloseHandle(client, IntPtr.Zero);
            }
        }

        var neighbors = found.Values
            .OrderByDescending(item => item.SignalQuality)
            .ThenBy(item => item.Ssid, StringComparer.Ordinal)
            .ToList();
        return new WifiNeighborScan(neighbors, true, neighbors.Count == 0 ? note ?? "未发现可见邻区" : note);
    }

    /// <summary>Parses a WLAN_BSS_ENTRY byte block; kept internal so the layout is unit-testable.</summary>
    internal static WifiNeighbor? ParseEntry(ReadOnlySpan<byte> entry)
    {
        if (entry.Length < BssEntrySize)
        {
            return null;
        }

        var ssidLength = Math.Clamp(BitConverter.ToInt32(entry[..4]), 0, 32);
        if (ssidLength == 0)
        {
            return null;   // hidden SSID: not actionable and can be private
        }

        var ssid = Encoding.UTF8.GetString(entry.Slice(4, ssidLength)).TrimEnd('\0');
        var bssid = string.Join(':', entry.Slice(40, 6).ToArray().Select(value => value.ToString("X2")));
        var phyType = BitConverter.ToInt32(entry[50..54]);
        var quality = Math.Clamp(BitConverter.ToInt32(entry[58..62]), 0, 100);
        var rxKbps = Math.Max(0, BitConverter.ToInt32(entry[62..66]));
        var txKbps = Math.Max(0, BitConverter.ToInt32(entry[66..70]));

        return new WifiNeighbor(
            ssid,
            bssid,
            quality,
            (int)Math.Round((quality / 2d) - 100),
            PhyLabel(phyType),
            Math.Round(rxKbps / 1000d, 0),
            Math.Round(txKbps / 1000d, 0));
    }

    private static bool QueryBssList(IntPtr client, Guid interfaceGuid, out IReadOnlyList<WifiNeighbor>? neighbors)
    {
        neighbors = null;
        if (WlanGetNetworkBssList(
                client,
                ref interfaceGuid,
                IntPtr.Zero,
                BssTypeAny,
                false,
                IntPtr.Zero,
                out _,
                out var listPtr) != 0 || listPtr == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var totalSize = Marshal.ReadInt32(listPtr);
            var count = Math.Clamp(Marshal.ReadInt32(listPtr, 4), 0, 256);
            if (totalSize < 8 + (count * BssEntrySize))
            {
                count = Math.Max(0, (totalSize - 8) / BssEntrySize);
            }

            var buffer = new byte[count * BssEntrySize];
            if (buffer.Length > 0)
            {
                Marshal.Copy(IntPtr.Add(listPtr, 8), buffer, 0, buffer.Length);
            }

            neighbors = Enumerable.Range(0, count)
                .Select(index => ParseEntry(buffer.AsSpan(index * BssEntrySize, BssEntrySize)))
                .Where(item => item is not null)
                .Select(item => item!)
                .ToList();
            return true;
        }
        finally
        {
            WlanFreeMemory(listPtr);
        }
    }

    private static string PhyLabel(int phyType) => phyType switch
    {
        7 => "Wi-Fi 4",
        8 => "Wi-Fi 5",
        10 => "Wi-Fi 6",
        11 => "Wi-Fi 7",
        _ => "Wi-Fi",
    };

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanOpenHandle(uint clientVersion, IntPtr reserved, out uint negotiated, out IntPtr handle);

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanCloseHandle(IntPtr handle, IntPtr reserved);

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr list);

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanGetNetworkBssList(
        IntPtr handle,
        ref Guid interfaceGuid,
        IntPtr ssid,
        uint bssType,
        [MarshalAs(UnmanagedType.Bool)] bool securityEnabled,
        IntPtr reserved,
        out uint numberOfItems,
        out IntPtr list);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr memory);
}
