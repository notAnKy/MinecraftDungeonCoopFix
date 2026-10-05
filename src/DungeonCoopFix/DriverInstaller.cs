using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace DungeonCoopFix;

internal static class DriverInstaller
{
    internal const string FileName = "ViGEmBus_1.22.0_x64_x86_arm64.exe";
    internal const string Sha256 = "89220A7865076B342892F98865F3499FB7C4CFD673159E89D352C360FD014C6A";
    internal static string InstallerPath => Path.Combine(AppContext.BaseDirectory, "drivers", FileName);

    internal static async Task<string> InstallAsync()
    {
        string path = InstallerPath;
        Verify(path);
        Process? process;
        try
        {
            process = Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(path)!
                // No quiet flags: the user sees the official setup wizard.
            });
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223)
        {
            return "Driver installation was cancelled. You can install it later.";
        }
        using (process)
        {
            if (process is null) throw new InvalidOperationException("Windows could not open the driver installer.");
            await process.WaitForExitAsync();
            return process.ExitCode switch
            {
                0 => "Setup finished. Click Enable Co-op Fix to try again.",
                3010 or 1641 => "Driver setup requires a Windows restart. Restart before enabling the fix.",
                1602 => "Driver installation was cancelled. You can install it later.",
                _ => $"Driver setup ended with code {process.ExitCode}. Follow any instructions from its setup window, then try again."
            };
        }
    }

    internal static void Verify(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("The driver installer is missing. Extract the complete DungeonCoopFix folder again.");
        using var file = File.OpenRead(path);
        if (Convert.ToHexString(SHA256.HashData(file)) != Sha256)
            throw new InvalidDataException("The driver installer has changed. Installation was blocked. Extract a fresh copy of DungeonCoopFix.");
        VerifySignature(path);
    }

    private static void VerifySignature(string path)
    {
        var info = new TrustFileInfo { Size = (uint)Marshal.SizeOf<TrustFileInfo>(), Path = path };
        IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFileInfo>());
        Marshal.StructureToPtr(info, pointer, false);
        var data = new TrustData
        {
            Size = (uint)Marshal.SizeOf<TrustData>(),
            UiChoice = 2, // WTD_UI_NONE
            UnionChoice = 1, // WTD_CHOICE_FILE
            File = pointer,
            StateAction = 1, // WTD_STATEACTION_VERIFY
            ProviderFlags = 0x1000 // WTD_CACHE_ONLY_URL_RETRIEVAL: verification stays offline
        };
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        try
        {
            int result = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            if (result != 0)
                throw new InvalidDataException($"Windows could not validate the official installer's signature (0x{result:X8}). Installation was blocked.");
        }
        finally
        {
            data.StateAction = 2; // WTD_STATEACTION_CLOSE
            WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            Marshal.DestroyStructure<TrustFileInfo>(pointer);
            Marshal.FreeHGlobal(pointer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustFileInfo
    {
        public uint Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string Path;
        public IntPtr FileHandle, KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public uint Size;
        public IntPtr PolicyCallback, SipClient;
        public uint UiChoice, RevocationChecks, UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData, UrlReference;
        public uint ProviderFlags, UiContext;
        public IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
}
