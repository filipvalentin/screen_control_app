using System;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace ScreenControlApp.Desktop.ScreenSharing {
	class NativeMethods {
		[DllImport("user32.dll")]
		public static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

		[DllImport("user32.dll")]
		public static extern UIntPtr GetMessageExtraInfo();

		[StructLayout(LayoutKind.Sequential)]
		public struct INPUT {
			public uint type;
			public InputUnion u;
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct InputUnion {
			[FieldOffset(0)] public MOUSEINPUT mi;
			[FieldOffset(0)] public KEYBDINPUT ki;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct MOUSEINPUT {
			public int dx;
			public int dy;
			public int mouseData;
			public uint dwFlags;
			public uint time;
			public UIntPtr dwExtraInfo;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct KEYBDINPUT {
			public ushort wVk;
			public ushort wScan;
			public uint dwFlags;
			public uint time;
			public UIntPtr dwExtraInfo;
		}

		public static ushort MapKeyToVirtualKey(Key key) {
			// Convert WPF key to Win32 virtual key
			return (ushort)KeyInterop.VirtualKeyFromKey(key);
		}

		public const int INPUT_MOUSE = 0;
		public const int INPUT_KEYBOARD = 1;

		public const uint MOUSEEVENTF_MOVE = 0x01;
		public const uint MOUSEEVENTF_LEFTDOWN = 0x02;
		public const uint MOUSEEVENTF_LEFTUP = 0x04;
		public const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
		public const uint MOUSEEVENTF_RIGHTUP = 0x10;
		public const uint MOUSEEVENTF_MIDDLEDOWN = 0x20;
		public const uint MOUSEEVENTF_MIDDLEUP = 0x40;
		public const uint MOUSEEVENTF_XDOWN = 0x80;
		public const uint MOUSEEVENTF_XUP = 0x0100;
		public const uint MOUSEEVENTF_WHEEL = 0x0800;
		public const uint MOUSEEVENTF_HWHEEL = 0x1000;
		public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
		public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
		// Constants for extended mouse buttons
		public const int XBUTTON1 = 0x01;
		public const int XBUTTON2 = 0x02;


		public const uint KEYEVENTF_KEYDOWN = 0x00;
		public const uint KEYEVENTF_KEYUP = 0x02;
	}

}
