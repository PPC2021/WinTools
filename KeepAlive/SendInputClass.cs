using System;
using System.Runtime.InteropServices;

namespace KeepAlive;

public static class SendInputClass
{
	public struct KeyboardInput
	{
		internal ushort wVk;

		internal ushort wScan;

		internal uint dwFlags;

		internal uint time;

		internal IntPtr dwExtraInfo;
	}

	public struct MouseInput
	{
		internal int dx;

		internal int dy;

		internal uint mouseData;

		internal uint dwFlags;

		internal uint time;

		internal IntPtr dwExtraInfo;
	}

	public struct HardwareInput
	{
		internal uint uMsg;

		internal ushort wParamL;

		internal ushort wParamH;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct InputUnion
	{
		[FieldOffset(0)]
		internal MouseInput mi;

		[FieldOffset(0)]
		internal KeyboardInput ki;

		[FieldOffset(0)]
		internal HardwareInput hi;
	}

	public struct INPUT
	{
		internal uint type;

		internal InputUnion u;

		internal static int Size => Marshal.SizeOf(typeof(INPUT));
	}

	[Flags]
	public enum InputType : uint
	{
		Mouse = 0u,
		Keyboard = 1u,
		Hardware = 2u
	}

	[Flags]
	public enum KeyEventF
	{
		KeyDown = 0,
		ExtendedKey = 1,
		KeyUp = 2,
		Unicode = 4,
		Scancode = 8
	}

	[Flags]
	public enum MouseEventF
	{
		Absolute = 0x8000,
		HWheel = 0x1000,
		Move = 1,
		MoveNoCoalesce = 0x2000,
		LeftDown = 2,
		LeftUp = 4,
		RightDown = 8,
		RightUp = 0x10,
		MiddleDown = 0x20,
		MiddleUp = 0x40,
		VirtualDesk = 0x4000,
		Wheel = 0x800,
		XDown = 0x80,
		XUp = 0x100
	}

	public enum KeyCode : ushort
	{
		MEDIA_NEXT_TRACK = 176,
		MEDIA_PLAY_PAUSE = 179,
		MEDIA_PREV_TRACK = 177,
		MEDIA_STOP = 178,
		ADD = 107,
		MULTIPLY = 106,
		DIVIDE = 111,
		SUBTRACT = 109,
		BROWSER_BACK = 166,
		BROWSER_FAVORITES = 171,
		BROWSER_FORWARD = 167,
		BROWSER_HOME = 172,
		BROWSER_REFRESH = 168,
		BROWSER_SEARCH = 170,
		BROWSER_STOP = 169,
		NUMPAD0 = 96,
		NUMPAD1 = 97,
		NUMPAD2 = 98,
		NUMPAD3 = 99,
		NUMPAD4 = 100,
		NUMPAD5 = 101,
		NUMPAD6 = 102,
		NUMPAD7 = 103,
		NUMPAD8 = 104,
		NUMPAD9 = 105,
		F1 = 112,
		F10 = 121,
		F11 = 122,
		F12 = 123,
		F13 = 124,
		F14 = 125,
		F15 = 126,
		F16 = 127,
		F17 = 128,
		F18 = 129,
		F19 = 130,
		F2 = 113,
		F20 = 131,
		F21 = 132,
		F22 = 133,
		F23 = 134,
		F24 = 135,
		F3 = 114,
		F4 = 115,
		F5 = 116,
		F6 = 117,
		F7 = 118,
		F8 = 119,
		F9 = 120,
		OEM_1 = 186,
		OEM_102 = 226,
		OEM_2 = 191,
		OEM_3 = 192,
		OEM_4 = 219,
		OEM_5 = 220,
		OEM_6 = 221,
		OEM_7 = 222,
		OEM_8 = 223,
		OEM_CLEAR = 254,
		OEM_COMMA = 188,
		OEM_MINUS = 189,
		OEM_PERIOD = 190,
		OEM_PLUS = 187,
		KEY_0 = 48,
		KEY_1 = 49,
		KEY_2 = 50,
		KEY_3 = 51,
		KEY_4 = 52,
		KEY_5 = 53,
		KEY_6 = 54,
		KEY_7 = 55,
		KEY_8 = 56,
		KEY_9 = 57,
		KEY_A = 65,
		KEY_B = 66,
		KEY_C = 67,
		KEY_D = 68,
		KEY_E = 69,
		KEY_F = 70,
		KEY_G = 71,
		KEY_H = 72,
		KEY_I = 73,
		KEY_J = 74,
		KEY_K = 75,
		KEY_L = 76,
		KEY_M = 77,
		KEY_N = 78,
		KEY_O = 79,
		KEY_P = 80,
		KEY_Q = 81,
		KEY_R = 82,
		KEY_S = 83,
		KEY_T = 84,
		KEY_U = 85,
		KEY_V = 86,
		KEY_W = 87,
		KEY_X = 88,
		KEY_Y = 89,
		KEY_Z = 90,
		VOLUME_DOWN = 174,
		VOLUME_MUTE = 173,
		VOLUME_UP = 175,
		SNAPSHOT = 44,
		RightClick = 93,
		BACKSPACE = 8,
		CANCEL = 3,
		CAPS_LOCK = 20,
		CONTROL = 17,
		ALT = 18,
		DECIMAL = 110,
		DELETE = 46,
		DOWN = 40,
		END = 35,
		ESC = 27,
		HOME = 36,
		INSERT = 45,
		LAUNCH_APP1 = 182,
		LAUNCH_APP2 = 183,
		LAUNCH_MAIL = 180,
		LAUNCH_MEDIA_SELECT = 181,
		LCONTROL = 162,
		LEFT = 37,
		LSHIFT = 160,
		LWIN = 91,
		PAGEDOWN = 34,
		NUMLOCK = 144,
		PAGE_UP = 33,
		RCONTROL = 163,
		ENTER = 13,
		RIGHT = 39,
		RSHIFT = 161,
		RWIN = 92,
		SHIFT = 16,
		SPACE_BAR = 32,
		TAB = 9,
		UP = 38
	}

	[DllImport("user32.dll", SetLastError = true)]
	public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

	[DllImport("user32.dll")]
	public static extern IntPtr GetMessageExtraInfo();

	[DllImport("user32.dll")]
	private static extern bool SetCursorPos(int X, int Y);
}
