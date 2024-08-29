using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using ScreenControlApp.Desktop.ScreenControlling;
using ScreenControlApp.Desktop.ScreenSharing;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenControlApp.Desktop {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		public MainWindow() {
			InitializeComponent();
			//Application.Current.MainWindow.WindowState = WindowState.Maximized;

			shareHost_HostId.Text = "123-456-789";
			shareHost_Passcode.Text = "1234";

			//Program.Test();
		}

		//test
		class Program {
			[DllImport("user32.dll")]
			static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

			[StructLayout(LayoutKind.Sequential)]
			public struct DEVMODE {
				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
				public string dmDeviceName;
				public short dmSpecVersion;
				public short dmDriverVersion;
				public short dmSize;
				public short dmDriverExtra;
				public int dmFields;
				public int dmPositionX;
				public int dmPositionY;
				public ScreenOrientation dmDisplayOrientation;
				public int dmDisplayFixedOutput;
				public short dmColor;
				public short dmDuplex;
				public short dmYResolution;
				public short dmTTOption;
				public short dmCollate;
				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
				public string dmFormName;
				public short dmLogPixels;
				public int dmBitsPerPel;
				public int dmPelsWidth;
				public int dmPelsHeight;
				public int dmDisplayFlags;
				public int dmDisplayFrequency;
				public int dmICMMethod;
				public int dmICMIntent;
				public int dmMediaType;
				public int dmDitherType;
				public int dmReserved1;
				public int dmReserved2;
				public int dmPanningWidth;
				public int dmPanningHeight;
			}

			[DllImport("shcore.dll")]
			private static extern int GetDpiForMonitor(IntPtr hmonitor, MONITOR_DPI_TYPE dpiType, out uint dpiX, out uint dpiY);

			private enum MONITOR_DPI_TYPE {
				MDT_EFFECTIVE_DPI = 0,
				MDT_ANGULAR_DPI = 1,
				MDT_RAW_DPI = 2,
				MDT_DEFAULT = MDT_EFFECTIVE_DPI
			}

			[DllImport("user32.dll")]
			private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

			[DllImport("user32.dll", SetLastError = true)]
			private static extern bool GetCursorPos(out POINT lpPoint);

			[StructLayout(LayoutKind.Sequential)]
			private struct POINT {
				public int X;
				public int Y;
			}

			public static void Test() {
				foreach (Screen screen in Screen.AllScreens) {
					// Retrieve screen settings
					var dm = new DEVMODE();
					dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
					EnumDisplaySettings(screen.DeviceName, -1, ref dm);

					Debug.WriteLine($"Device: {screen.DeviceName}");
					Debug.WriteLine($"Real Resolution: {dm.dmPelsWidth}x{dm.dmPelsHeight}");

					// Calculate the scaled resolution
					IntPtr hMonitor = MonitorFromWindow(IntPtr.Zero, 2); // Default to the primary monitor
					uint dpiX, dpiY;
					GetDpiForMonitor(hMonitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out dpiX, out dpiY);

					// Calculate scaling factor
					float scaleFactorX = dpiX / 96.0f; // 96 DPI is standard scaling
					float scaleFactorY = dpiY / 96.0f;

					// Apply scaling factor
					int scaledWidth = (int)(dm.dmPelsWidth / scaleFactorX);
					int scaledHeight = (int)(dm.dmPelsHeight / scaleFactorY);

					Debug.WriteLine($"Scaled Resolution: {scaledWidth}x{scaledHeight}");
				}
			}
		}


		private void ControlHost_Button_Click(object sender, RoutedEventArgs e) {
			Window window = new ScreenControllingWindow(controlHost_HostId.Text, controlHost_Passcode.Text);
			Visibility = Visibility.Hidden;
			window.Show();
			window.Closing += (sender, args) => {
				Visibility = Visibility.Visible;
			};
		}

		private void ShareHost_Button_Click(object sender, RoutedEventArgs e) {
			Window window = new ScreenSharingWindow(shareHost_HostId.Text, shareHost_Passcode.Text);
			Visibility = Visibility.Hidden;
			window.Show();
			window.Closing += (sender, args) => {
				Visibility = Visibility.Visible;
			};
		}

		private void StackPanel_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) {
			//SWidth.Content
			SWidth.Content = e.GetPosition(null).X;
			SHeight.Content = e.GetPosition(null).Y;
		}
	}
}