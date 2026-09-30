using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

// File responsibility: Reads battery-pack temperature from the Windows battery class IOCTL and an optional battery-specific WMI fallback.

namespace BatteryDoctor.Services;

/// <summary>
/// Reads battery-pack temperature without substituting CPU/GPU/ACPI thermal-zone values.
/// The primary path uses the documented Windows battery device IOCTL. A battery-specific
/// ROOT\WMI BatteryTemperature query is attempted only when the battery driver rejects the IOCTL.
/// </summary>
internal static class BatteryTemperatureProvider
{
    private static readonly Guid BatteryInterfaceGuid = new("72631E54-78A4-11D0-BCF7-00AA00B7B32A");
    private static readonly object CacheGate = new();
    private static string? _cachedDevicePath;

    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const int ErrorNoMoreItems = 259;
    private const uint IoctlBatteryQueryTag = 0x00294040;
    private const uint IoctlBatteryQueryInformation = 0x00294044;
    private const int BatteryTemperatureInformationLevel = 2;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    /// <summary>
    /// Tries the native battery-class API first, then the battery-specific WMI class.
    /// Returns null when the OEM battery/driver does not expose temperature.
    /// </summary>
    public static BatteryTemperatureReading? TryRead()
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            var native = TryReadNative();
            if (native is not null) return native;
        }
        catch
        {
            // Temperature is optional battery information. A provider failure must never block
            // the rest of Battery Doctor telemetry.
        }

        return TryReadBatteryWmi();
    }

    /// <summary>
    /// Queries a cached battery path first, then enumerates battery interfaces only when the
    /// cache is empty/stale. The current tag is still reacquired on every read.
    /// </summary>
    private static BatteryTemperatureReading? TryReadNative()
    {
        string? cachedPath;
        lock (CacheGate) cachedPath = _cachedDevicePath;

        if (!string.IsNullOrWhiteSpace(cachedPath))
        {
            var cachedReading = TryReadDevice(cachedPath);
            if (cachedReading is not null) return cachedReading;
            lock (CacheGate) _cachedDevicePath = null;
        }

        var guid = BatteryInterfaceGuid;
        var deviceInfo = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (deviceInfo == InvalidHandleValue) return null;

        try
        {
            for (uint index = 0; index < 32; index++)
            {
                var interfaceData = new SpDeviceInterfaceData
                {
                    CbSize = Marshal.SizeOf<SpDeviceInterfaceData>()
                };

                if (!SetupDiEnumDeviceInterfaces(deviceInfo, IntPtr.Zero, ref guid, index, ref interfaceData))
                {
                    if (Marshal.GetLastWin32Error() == ErrorNoMoreItems) break;
                    continue;
                }

                var path = TryGetDevicePath(deviceInfo, ref interfaceData);
                if (string.IsNullOrWhiteSpace(path)) continue;

                var reading = TryReadDevice(path);
                if (reading is null) continue;

                lock (CacheGate) _cachedDevicePath = path;
                return reading;
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfo);
        }

        return null;
    }

    /// <summary>
    /// Opens one battery interface, reacquires its current tag, and returns a validated temperature reading.
    /// </summary>
    private static BatteryTemperatureReading? TryReadDevice(string path)
    {
        using var handle = CreateFile(
            path,
            GenericRead,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);
        if (handle.IsInvalid) return null;

        if (!TryQueryBatteryTag(handle, out var tag) || tag == 0) return null;
        if (!TryQueryTemperature(handle, tag, out var rawTemperature)) return null;

        var celsius = ConvertTenthsKelvinToCelsius(rawTemperature);
        return IsPlausibleBatteryTemperature(celsius)
            ? new BatteryTemperatureReading(celsius, "Battery Class IOCTL", tag)
            : null;
    }

    /// <summary>
    /// Reads the variable-length SP_DEVICE_INTERFACE_DETAIL_DATA buffer and extracts the Unicode device path.
    /// </summary>
    private static string? TryGetDevicePath(IntPtr deviceInfo, ref SpDeviceInterfaceData interfaceData)
    {
        SetupDiGetDeviceInterfaceDetail(deviceInfo, ref interfaceData, IntPtr.Zero, 0, out var requiredSize, IntPtr.Zero);
        if (requiredSize < 6) return null;

        var detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
        try
        {
            // SP_DEVICE_INTERFACE_DETAIL_DATA_W has a cbSize of 8 on x64 and 6 on x86.
            // DevicePath itself begins immediately after the 4-byte cbSize field.
            Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);
            if (!SetupDiGetDeviceInterfaceDetail(
                    deviceInfo,
                    ref interfaceData,
                    detailBuffer,
                    requiredSize,
                    out _,
                    IntPtr.Zero))
                return null;

            return Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, 4));
        }
        finally
        {
            Marshal.FreeHGlobal(detailBuffer);
        }
    }

    /// <summary>
    /// Retrieves the current battery tag. Tags protect subsequent queries from returning data for a battery that changed mid-query.
    /// </summary>
    private static bool TryQueryBatteryTag(SafeFileHandle handle, out uint tag)
    {
        uint waitMilliseconds = 0;
        return DeviceIoControl(
            handle,
            IoctlBatteryQueryTag,
            ref waitMilliseconds,
            sizeof(uint),
            out tag,
            sizeof(uint),
            out _,
            IntPtr.Zero);
    }

    /// <summary>
    /// Requests BatteryTemperature for the current tag. Windows returns tenths of a degree Kelvin.
    /// </summary>
    private static bool TryQueryTemperature(SafeFileHandle handle, uint tag, out uint rawTemperature)
    {
        var query = new BatteryQueryInformation
        {
            BatteryTag = tag,
            InformationLevel = BatteryTemperatureInformationLevel,
            AtRate = 0
        };

        return DeviceIoControl(
            handle,
            IoctlBatteryQueryInformation,
            ref query,
            Marshal.SizeOf<BatteryQueryInformation>(),
            out rawTemperature,
            sizeof(uint),
            out _,
            IntPtr.Zero);
    }

    /// <summary>
    /// Attempts the battery driver's WMI temperature block only; it intentionally does not use generic ACPI thermal zones.
    /// </summary>
    private static BatteryTemperatureReading? TryReadBatteryWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM BatteryTemperature");
            foreach (ManagementObject item in searcher.Get())
            {
                var raw = Convert.ToUInt32(item["Temperature"]);
                var celsius = ConvertTenthsKelvinToCelsius(raw);
                if (!IsPlausibleBatteryTemperature(celsius)) continue;
                uint? tag = null;
                try { tag = item["Tag"] is null ? null : Convert.ToUInt32(item["Tag"]); } catch { }
                return new BatteryTemperatureReading(celsius, "Battery WMI", tag);
            }
        }
        catch
        {
            // Many OEM drivers do not register the optional BatteryTemperature WMI block.
        }

        return null;
    }

    /// <summary>
    /// Converts the battery API unit (tenths Kelvin) to Celsius.
    /// </summary>
    private static double ConvertTenthsKelvinToCelsius(uint tenthsKelvin)
        => tenthsKelvin / 10.0 - 273.15;

    /// <summary>
    /// Rejects sentinel/corrupt values while allowing a deliberately broad real-world battery range.
    /// </summary>
    private static bool IsPlausibleBatteryTemperature(double celsius)
        => !double.IsNaN(celsius) && !double.IsInfinity(celsius) && celsius is >= -50 and <= 120;

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        public int CbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public UIntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BatteryQueryInformation
    {
        public uint BatteryTag;
        public int InformationLevel;
        public int AtRate;
    }

    /// <summary>Native SetupAPI entry point used to open the battery device-interface set.</summary>
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        IntPtr enumerator,
        IntPtr hwndParent,
        uint flags);

    /// <summary>Native SetupAPI entry point used to enumerate battery interfaces in the device set.</summary>
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref SpDeviceInterfaceData deviceInterfaceData);

    /// <summary>Native SetupAPI entry point used to resolve a battery interface to its device path.</summary>
    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInterfaceDetailW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SpDeviceInterfaceData deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        IntPtr deviceInfoData);

    /// <summary>Native SetupAPI entry point that releases the device-information set.</summary>
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    /// <summary>Native Kernel32 entry point that opens a battery device interface for read-only queries.</summary>
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    /// <summary>Native DeviceIoControl overload used for IOCTL_BATTERY_QUERY_TAG.</summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        ref uint inputBuffer,
        int inputBufferSize,
        out uint outputBuffer,
        int outputBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);

    /// <summary>Native DeviceIoControl overload used for IOCTL_BATTERY_QUERY_INFORMATION.</summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        ref BatteryQueryInformation inputBuffer,
        int inputBufferSize,
        out uint outputBuffer,
        int outputBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);
}

/// <summary>
/// Result from a real battery-temperature sensor source exposed by the Windows battery stack.
/// </summary>
internal sealed record BatteryTemperatureReading(double Celsius, string Source, uint? BatteryTag);
