using System.Runtime.InteropServices;

namespace DungeonCoopFix;

internal static class NativeMethods
{
    internal const uint Success = 0x20000000;
    internal const uint BusNotFound = 0xE0000001;

    // Exact XUSB_REPORT / XINPUT_GAMEPAD layout from the upstream and Windows headers.
    [StructLayout(LayoutKind.Sequential)]
    internal struct GamepadReport
    {
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short LeftX, LeftY, RightX, RightY;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct XInputState
    {
        public uint PacketNumber;
        public GamepadReport Gamepad;
    }

    // ViGEmClient exports use the C calling convention, not WinAPI/stdcall.
    [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr vigem_alloc();
    [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void vigem_free(IntPtr client);
    [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint vigem_connect(IntPtr client);
    [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void vigem_disconnect(IntPtr client);
    [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr vigem_target_x360_alloc();
    [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void vigem_target_free(IntPtr target);
    [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint vigem_target_add(IntPtr client, IntPtr target);
    [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint vigem_target_remove(IntPtr client, IntPtr target);
    [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint vigem_target_x360_update(IntPtr client, IntPtr target, GamepadReport report);
    [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint vigem_target_x360_get_user_index(IntPtr client, IntPtr target, out uint index);

    [DllImport("xinput1_4.dll", ExactSpelling = true)]
    internal static extern uint XInputGetState(uint index, out XInputState state);
}
