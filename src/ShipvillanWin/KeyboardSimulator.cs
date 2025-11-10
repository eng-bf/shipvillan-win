using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace ShipvillanWin;

/// <summary>
/// Simulates keyboard input using the Windows SendInput API.
/// Produces input indistinguishable from actual keyboard hardware.
/// </summary>
[SupportedOSPlatform("windows")]
public static class KeyboardSimulator
{
    #region Windows API Declarations

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetForegroundWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public INPUTUNION Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT Mouse;
        [FieldOffset(0)] public KEYBDINPUT Keyboard;
        [FieldOffset(0)] public HARDWAREINPUT Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint Msg;
        public ushort ParamL;
        public ushort ParamH;
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    // Virtual key codes
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_SHIFT = 0x10;

    #endregion

    /// <summary>
    /// Sends a string as keyboard input to the foreground window.
    /// </summary>
    /// <param name="text">The text to send.</param>
    /// <param name="delayMs">Delay in milliseconds between characters.</param>
    /// <param name="appendEnter">Whether to append Enter key at the end.</param>
    public static async Task SendKeysAsync(string text, int delayMs = 5, bool appendEnter = true)
    {
        if (string.IsNullOrEmpty(text))
            return;

        // Check if there's a foreground window
        if (GetForegroundWindow() == IntPtr.Zero)
        {
            throw new InvalidOperationException("No foreground window available to receive input.");
        }

        // Send each character
        foreach (var c in text)
        {
            SendCharacter(c);

            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }
        }

        // Send Enter key if requested
        if (appendEnter)
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }
            SendKey(VK_RETURN);
        }
    }

    /// <summary>
    /// Sends a single character using Unicode input.
    /// </summary>
    private static void SendCharacter(char c)
    {
        // Use Unicode input for reliable character entry
        var inputs = new INPUT[2];

        // Key down
        inputs[0] = new INPUT
        {
            Type = INPUT_KEYBOARD,
            Union = new INPUTUNION
            {
                Keyboard = new KEYBDINPUT
                {
                    Vk = 0,
                    Scan = c,
                    Flags = KEYEVENTF_UNICODE,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };

        // Key up
        inputs[1] = new INPUT
        {
            Type = INPUT_KEYBOARD,
            Union = new INPUTUNION
            {
                Keyboard = new KEYBDINPUT
                {
                    Vk = 0,
                    Scan = c,
                    Flags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    /// <summary>
    /// Sends a virtual key (like Enter).
    /// </summary>
    private static void SendKey(ushort virtualKey)
    {
        var inputs = new INPUT[2];

        // Key down
        inputs[0] = new INPUT
        {
            Type = INPUT_KEYBOARD,
            Union = new INPUTUNION
            {
                Keyboard = new KEYBDINPUT
                {
                    Vk = virtualKey,
                    Scan = 0,
                    Flags = 0,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };

        // Key up
        inputs[1] = new INPUT
        {
            Type = INPUT_KEYBOARD,
            Union = new INPUTUNION
            {
                Keyboard = new KEYBDINPUT
                {
                    Vk = virtualKey,
                    Scan = 0,
                    Flags = KEYEVENTF_KEYUP,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }
}
