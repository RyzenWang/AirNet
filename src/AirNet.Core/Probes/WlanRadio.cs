using System.Runtime.InteropServices;
using System.Text;

namespace AirNet.Core.Probes;

/// <summary>
/// Reads the current Wi-Fi association through wlanapi. This is the only way to get radio data
/// (signal quality, negotiated rate, PHY generation) without shelling out and without parsing
/// localized console text.
/// </summary>
/// <remarks>
/// Structured buffers are parsed by offset rather than by marshalling a
/// <c>WLAN_CONNECTION_ATTRIBUTES</c> struct: the layout is fixed by the SDK headers, and a byte
/// read cannot be silently broken by a packing attribute.
/// </remarks>
internal static class WlanRadio
{
    private const uint ClientVersion = 2;
    private const uint OpcodeCurrentConnection = 7;
    private const int InterfaceInfoSize = 532;    // GUID(16) + WCHAR[256](512) + state(4)
    private const int InterfaceListHeader = 8;    // dwNumberOfItems + dwIndex
    private const int AssociationOffset = 516;    // after mode(4) + profileName(512)
    private const int SsidOffset = 520;
    private const int PhyTypeOffset = 556;
    private const int SignalQualityOffset = 564;
    private const int RxRateOffset = 568;

    public static WifiState? TryRead()
    {
        if (!OperatingSystem.IsWindows())
        {
            // macOS and Linux go through command output; see UnixRadio for the trade-off.
            return UnixRadio.TryRead();
        }

        IntPtr client = IntPtr.Zero;
        try
        {
            if (WlanOpenHandle(ClientVersion, IntPtr.Zero, out _, out client) != 0 || client == IntPtr.Zero)
            {
                return null;
            }

            if (WlanEnumInterfaces(client, IntPtr.Zero, out var listPtr) != 0 || listPtr == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var count = Marshal.ReadInt32(listPtr);
                for (var i = 0; i < count; i++)
                {
                    var entry = IntPtr.Add(listPtr, InterfaceListHeader + (i * InterfaceInfoSize));
                    var state = Marshal.ReadInt32(entry + 528);
                    if (state != 1)
                    {
                        continue;   // only the associated interface matters
                    }

                    var guidBytes = new byte[16];
                    Marshal.Copy(entry, guidBytes, 0, 16);
                    var result = QueryCurrentConnection(client, new Guid(guidBytes));
                    if (result is not null)
                    {
                        return result;
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
            return null;
        }
        finally
        {
            if (client != IntPtr.Zero)
            {
                WlanCloseHandle(client, IntPtr.Zero);
            }
        }

        return null;
    }

    private static WifiState? QueryCurrentConnection(IntPtr client, Guid interfaceGuid)
    {
        if (WlanQueryInterface(
                client,
                ref interfaceGuid,
                OpcodeCurrentConnection,
                IntPtr.Zero,
                out var size,
                out var data,
                IntPtr.Zero) != 0 || data == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var length = (int)Math.Min(size, 1024u);
            if (length < AssociationOffset + 64)
            {
                return null;
            }

            var buffer = new byte[length];
            Marshal.Copy(data, buffer, 0, length);

            var ssidLength = Math.Clamp(BitConverter.ToInt32(buffer, AssociationOffset), 0, 32);
            var ssid = DecodeSsid(buffer, SsidOffset, ssidLength);
            var quality = Math.Clamp(BitConverter.ToInt32(buffer, SignalQualityOffset), 0, 100);
            var phy = BitConverter.ToInt32(buffer, PhyTypeOffset);
            var rxKbps = Math.Max(0, BitConverter.ToInt32(buffer, RxRateOffset));

            return new WifiState(ssid, quality, rxKbps, PhyLabel(phy));
        }
        finally
        {
            WlanFreeMemory(data);
        }
    }

    private static string DecodeSsid(byte[] buffer, int offset, int length)
    {
        if (length <= 0)
        {
            return string.Empty;
        }

        try
        {
            return Encoding.UTF8.GetString(buffer, offset, length);
        }
        catch (ArgumentException)
        {
            return Encoding.Latin1.GetString(buffer, offset, length);
        }
    }

    private static string PhyLabel(int phyType) => phyType switch
    {
        8 => "Wi-Fi 5",
        10 => "Wi-Fi 6",
        11 => "Wi-Fi 7",
        7 => "Wi-Fi 4",
        _ => "Wi-Fi",
    };

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanOpenHandle(
        uint dwClientVersion,
        IntPtr pReserved,
        out uint pdwNegotiatedVersion,
        out IntPtr phClientHandle);

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanEnumInterfaces(
        IntPtr hClientHandle,
        IntPtr pReserved,
        out IntPtr ppInterfaceList);

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanQueryInterface(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        uint OpCode,
        IntPtr pReserved,
        out uint pdwDataSize,
        out IntPtr ppData,
        IntPtr pWlanOpcodeValueType);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr pMemory);
}

/// <summary>What wlanapi told us about the current association.</summary>
internal sealed record WifiState(string Ssid, int SignalPercent, int RxRateKbps, string PhyLabel);
