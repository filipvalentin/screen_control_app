using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Forms;
using static ScreenControlApp.Desktop.ScreenSharing.NativeMethods;
using MessageBox = System.Windows.MessageBox;

namespace ScreenControlApp.Desktop.ScreenSharing {
	/// <summary>
	/// Interaction logic for ScreenSharingWindow.xaml
	/// </summary>
	public partial class ScreenSharingWindow : Window, IDisposable {
		private HubConnection Connection { get; set; } = null!;
		private string User { get; set; } = null!;
		private string Passcode { get; set; } = null!;
		private string PeerConnectionId { get; set; } = null!;

		private readonly CancellationTokenSource CancellationTokenSource = new();

		private readonly TaskCompletionSource<string> PeerConnectionIdCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource IsInitializedCompletionSource = new();

		int virtualWidth = SystemInformation.VirtualScreen.Width;
		int virtualHeight = SystemInformation.VirtualScreen.Height;
		int virtualLeft = SystemInformation.VirtualScreen.Left;
		int virtualTop = SystemInformation.VirtualScreen.Top;

		private readonly Screen SharedScreen = Screen.AllScreens[0];

		public ScreenSharingWindow(string user, string passcode) {
			InitializeComponent();

			User = user;
			Passcode = passcode;

			this.Closed += (sender, args) => {
				CancellationTokenSource.Cancel();
				Dispose();
			};

			_ = Task.Run(InitializeWindowState);

			// Information for each screen
			//foreach (var screen in screens) {
			//	Debug.WriteLine($"Screen: {screen.DeviceName}, Bounds: {screen.Bounds}, Primary: {screen.Primary}");
			//}
		}

		private async Task InitializeWindowState() {
			await InitializeSignalR();

			if (CancellationTokenSource.IsCancellationRequested) {
				this.Close();
				return;
			}

			await Connection.InvokeAsync("AnnounceShare", User, Passcode);

			PeerConnectionId = await PeerConnectionIdCompletionSource.Task;

			await Connection.InvokeAsync("AnnounceScreenSize", PeerConnectionId, Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height); //TODO: CHANGE TO SETTINGS-BASED SCREEN SELECTION

			IsInitializedCompletionSource.SetResult();

			_ = Task.Factory.StartNew(ShareVideoFeed, TaskCreationOptions.LongRunning);
		}
		private async Task InitializeSignalR() {
			try {
				Connection = new HubConnectionBuilder()
					.WithUrl("http://localhost:5026/screenControlHub")
					.Build();

				Connection.Closed += async (obj) => {
					await Task.Delay(new Random().Next(0, 5) * 1000);
					await Connection.StartAsync();//TODO: RESET CONNECTION IDS
				};

				//Connection.On<string, string>("ReceivePacket", (user, message) => {
				//	this.Dispatcher.Invoke(() => {
				//		var newMessage = $"sharing {user}: {message}";
				//		MessageBox.Show(newMessage);
				//	});
				//});
				Connection.On<string>("FailedConnection", (message) => {
					MessageBox.Show($"Couldn't connect: {message}");
				});
				Connection.On<string>("FailedTransfer", (message) => {
					MessageBox.Show($"Couldn't transfer: {message}");
				});
				Connection.On<string>("ReceiveConnectionToShare", (peerId) => {
					PeerConnectionIdCompletionSource.SetResult(peerId);
					this.Dispatcher.Invoke(() => {
						ConnectionStatus.Content = "Connected";
						//MessageBox.Show($"share received connection {peerId}");
					});
				});
				Connection.On<double, double>("ReceiveMouseMove", MouseMoveReceived);
				Connection.On<int>("ReceiveMouseDown", MouseDownReceived);
				Connection.On<int>("ReceiveMouseUp", MouseUpReceived);
				Connection.On<int>("ReceiveMouseScroll", MouseScrollReceived);
				await Connection.StartAsync();

				//test.Text = Connection.ConnectionId;
			}
			catch (Exception ex) {
				//if (IsClosed)
				//	return;
				MessageBox.Show(ex.Message);
			}
		}

		private void MouseMoveReceived(double normalizedX, double normalizedY) {
			int targetX = SharedScreen.Bounds.Left + (int)(normalizedX * SharedScreen.Bounds.Width);
			int targetY = SharedScreen.Bounds.Top + (int)(normalizedY * SharedScreen.Bounds.Height);

			var inputs = new NativeMethods.INPUT[1];
			inputs[0].type = INPUT_MOUSE;
			inputs[0].u.mi.dx = targetX * 65535 / SystemInformation.VirtualScreen.Width;
			inputs[0].u.mi.dy = targetY * 65535 / SystemInformation.VirtualScreen.Height;
			inputs[0].u.mi.dwFlags = NativeMethods.MOUSEEVENTF_ABSOLUTE | NativeMethods.MOUSEEVENTF_MOVE | MOUSEEVENTF_VIRTUALDESK;
			_ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
		}
		private void MouseDownReceived(int buttonCode) {
			var inputs = new NativeMethods.INPUT[1];
			inputs[0].type = INPUT_MOUSE;
			//MessageBox.Show(buttonCode.ToString());
			switch ((System.Windows.Input.MouseButton)buttonCode) {
				case System.Windows.Input.MouseButton.Left:
					inputs[0].u.mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
					break;
				case System.Windows.Input.MouseButton.Right:
					inputs[0].u.mi.dwFlags = MOUSEEVENTF_RIGHTDOWN;
					break;
				case System.Windows.Input.MouseButton.Middle:
					inputs[0].u.mi.dwFlags = MOUSEEVENTF_MIDDLEDOWN;
					break;
				case System.Windows.Input.MouseButton.XButton1:
					inputs[0].u.mi = new MOUSEINPUT {
						dwFlags = MOUSEEVENTF_XDOWN,
						mouseData = XBUTTON1
					};
					break;
				case System.Windows.Input.MouseButton.XButton2:
					inputs[0].u.mi = new MOUSEINPUT {
						dwFlags = MOUSEEVENTF_XDOWN,
						mouseData = XBUTTON2
					};
					break;
			};
			_ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
		}
		private void MouseUpReceived(int buttonCode) {
			//System.Windows.Input.MouseButton pressedButton = ;
			var inputs = new INPUT[1];
			inputs[0].type = INPUT_MOUSE;
			switch ((System.Windows.Input.MouseButton)buttonCode) {
				case System.Windows.Input.MouseButton.Left:
					inputs[0].u.mi.dwFlags = MOUSEEVENTF_LEFTUP;
					break;
				case System.Windows.Input.MouseButton.Right:
					inputs[0].u.mi.dwFlags = MOUSEEVENTF_RIGHTUP;
					break;
				case System.Windows.Input.MouseButton.Middle:
					inputs[0].u.mi.dwFlags = MOUSEEVENTF_MIDDLEUP;
					break;
				case System.Windows.Input.MouseButton.XButton1:
					inputs[0].u.mi = new MOUSEINPUT {
						dwFlags = MOUSEEVENTF_XUP,
						mouseData = XBUTTON1
					};
					break;
				case System.Windows.Input.MouseButton.XButton2:
					inputs[0].u.mi = new MOUSEINPUT {
						dwFlags = MOUSEEVENTF_XUP,
						mouseData = XBUTTON2
					};
					break;
			};
			_ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
		}
		private void MouseScrollReceived(int scrollValue) {
			var inputs = new NativeMethods.INPUT[1];
			inputs[0].type = INPUT_MOUSE;
			inputs[0].u.mi = new MOUSEINPUT {
				dwFlags = MOUSEEVENTF_WHEEL,
				mouseData = scrollValue
			};
			_ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
		}

		private async Task ShareVideoFeed() {
			var cancellationToken = CancellationTokenSource.Token;

			var bitmap = new Bitmap(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
									System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height);
			using var graphics = Graphics.FromImage(bitmap);
			using var memoryStream = new MemoryStream();
			try {
				while (!cancellationToken.IsCancellationRequested) {
					var timer = Stopwatch.StartNew();
					CaptureScreen(graphics, bitmap);

					memoryStream.SetLength(0);
					bitmap.Save(memoryStream, ImageFormat.Jpeg);
					byte[] imageBytes = memoryStream.ToArray();
					this.Dispatcher.Invoke(() => { ConnectionStatus.Content = imageBytes.Length; CaptureTimeLabel.Content = timer.ElapsedMilliseconds + "ms"; });

					timer.Restart();
					var channel = Channel.CreateUnbounded<byte[]>();
					await Connection.SendAsync("UploadFrame", channel.Reader);

					const int chunkSize = 8192;
					int offset = 0;
					while (offset < imageBytes.Length) {
						int count = Math.Min(chunkSize, imageBytes.Length - offset);
						var chunk = new byte[count];
						Array.Copy(imageBytes, offset, chunk, 0, count);
						await channel.Writer.WriteAsync(chunk);
						offset += count;
					}
					channel.Writer.Complete();

					await channel.Reader.Completion;
					this.Dispatcher.Invoke(() => TransferTimeLabel.Content = timer.ElapsedMilliseconds + "ms");
				}
			}
			catch (Exception ex) {
				MessageBox.Show(ex.ToString());
			}
		}

		private async void Button_Click(object sender, RoutedEventArgs e) {
			try {
				await Connection.InvokeAsync("AnnounceShare", User, Passcode);
			}
			catch (Exception ex) {
				MessageBox.Show(ex.Message);
			}
		}

		private async void Button_Click_TakeScreenshot(object sender, RoutedEventArgs e) {

		}

		private static void CaptureScreen(Graphics graphics, Bitmap bitmap) {
			graphics.CopyFromScreen(System.Windows.Forms.Screen.PrimaryScreen.Bounds.X,
									System.Windows.Forms.Screen.PrimaryScreen.Bounds.Y,
									0, 0,
									bitmap.Size,
									CopyPixelOperation.SourceCopy);
		}

		private async void Button_Click_1(object sender, RoutedEventArgs e) {
			Thread.Sleep(3000);
			//SimulateKeyPress('1');
			//SimulateKeyPress('k');
		}
		//public void SimulateKeyPress(ushort keyCode) {
		//	NativeMethods.INPUT[] inputs =
		//	[
		//		new NativeMethods.INPUT {
		//			type = NativeMethods.INPUT_KEYBOARD,
		//			U = new NativeMethods.InputUnion {
		//				ki = new NativeMethods.KEYBDINPUT {
		//					wVk = 0xA0,
		//					dwFlags = NativeMethods.KEYEVENTF_KEYDOWN,
		//					dwExtraInfo = NativeMethods.GetMessageExtraInfo()
		//				}
		//			}
		//		},
		//		new NativeMethods.INPUT {
		//			type = NativeMethods.INPUT_KEYBOARD,
		//			U = new NativeMethods.InputUnion {
		//				ki = new NativeMethods.KEYBDINPUT {
		//					wVk = keyCode,
		//					dwFlags = NativeMethods.KEYEVENTF_KEYDOWN,
		//					dwExtraInfo = NativeMethods.GetMessageExtraInfo()
		//				}
		//			}
		//		},
		//		new NativeMethods.INPUT {
		//			type = NativeMethods.INPUT_KEYBOARD,
		//			U = new NativeMethods.InputUnion {
		//				ki = new NativeMethods.KEYBDINPUT {
		//					wVk = keyCode,
		//					dwFlags = NativeMethods.KEYEVENTF_KEYUP,
		//					dwExtraInfo = NativeMethods.GetMessageExtraInfo()
		//				}
		//			}
		//		},
		//		new NativeMethods.INPUT {
		//			type = NativeMethods.INPUT_KEYBOARD,
		//			U = new NativeMethods.InputUnion {
		//				ki = new NativeMethods.KEYBDINPUT {
		//					wVk = 0xA0,
		//					dwFlags = NativeMethods.KEYEVENTF_KEYUP,
		//					dwExtraInfo = NativeMethods.GetMessageExtraInfo()
		//				}
		//			}
		//		}
		//	];
		//	Debug.WriteLine(NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT))));
		//	//Debug.WriteLine(Marshal.GetLastWin32Error());
		//}

		public void Dispose() {
			//CancellationTokenSource.Cancel();
			CancellationTokenSource.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}