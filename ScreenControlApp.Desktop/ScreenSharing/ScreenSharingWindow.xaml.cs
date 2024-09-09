using Microsoft.AspNetCore.SignalR.Client;
using ScreenControlApp.Desktop.Common.Settings;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Input;
using static ScreenControlApp.Desktop.ScreenSharing.NativeMethods;
using MessageBox = System.Windows.MessageBox;

namespace ScreenControlApp.Desktop.ScreenSharing {

	public partial class ScreenSharingWindow : Window, IDisposable {
		private ApplicationSettings Settings { get; set; }
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

		private Screen SharedScreen { get; set; } = null!;

		public ScreenSharingWindow(ApplicationSettings settings, string user, string passcode) {
			InitializeComponent();

			Settings = settings;
			User = user;
			Passcode = passcode;
			SharedScreen = Screen.AllScreens[settings.PreferredScreenId];
			//TODO place window on preferred screen

			this.Closed += (sender, args) => {
				CancellationTokenSource.Cancel();
				Dispose();
			};

			_ = Task.Run(InitializeWindowState);
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
					.WithUrl(Settings.ServerAddress)
					.Build();

				Connection.Closed += async (obj) => {
					await Task.Delay(new Random().Next(0, 5) * 1000);
					await Connection.StartAsync();//TODO: RESET CONNECTION IDS
				};

				Connection.On<string>("FailedConnection", (message) => {
					MessageBox.Show($"Couldn't connect: {message}");
				});
				Connection.On<string>("FailedTransfer", (message) => {
					MessageBox.Show($"Couldn't transfer: {message}");
				});
				Connection.On<string>("ReceiveConnectionToShare", (peerId) => {
					PeerConnectionIdCompletionSource.SetResult(peerId);
				});
				Connection.On<double, double>("ReceiveMouseMove", MouseMoveReceived);
				Connection.On<int>("ReceiveMouseDown", MouseDownReceived);
				Connection.On<int>("ReceiveMouseUp", MouseUpReceived);
				Connection.On<int>("ReceiveMouseScroll", MouseScrollReceived);
				Connection.On<int>("ReceiveKeyDown", KeyDownReceived);
				Connection.On<int>("ReceiveKeyUp", KeyUpReceived);
				await Connection.StartAsync();
			}
			catch (Exception ex) {
				MessageBox.Show(ex.Message);
			}
		}

		#region Controlling_Methods
		private void MouseMoveReceived(double normalizedX, double normalizedY) {
			int targetX = SharedScreen.Bounds.Left + (int)(normalizedX * SharedScreen.Bounds.Width);
			int targetY = SharedScreen.Bounds.Top + (int)(normalizedY * SharedScreen.Bounds.Height);

			int screenNormalizedX = ((targetX - SystemInformation.VirtualScreen.Left) * 65535) / SystemInformation.VirtualScreen.Width;
			int screenNormalizedY = ((targetY - SystemInformation.VirtualScreen.Top) * 65535) / SystemInformation.VirtualScreen.Height;

			var inputs = new NativeMethods.INPUT[1];
			inputs[0].type = INPUT_MOUSE;
			inputs[0].u.mi.dx = screenNormalizedX;
			inputs[0].u.mi.dy = screenNormalizedY;
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

		private async void KeyDownReceived(int keycode) {
			var inputs = new NativeMethods.INPUT[1];
			inputs[0].type = INPUT_KEYBOARD;
			inputs[0].u.ki.wVk = NativeMethods.MapKeyToVirtualKey((Key)keycode);
			inputs[0].u.ki.dwFlags = KEYEVENTF_KEYDOWN;
			inputs[0].u.ki.dwExtraInfo = NativeMethods.GetMessageExtraInfo();
			_ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
		}
		private void KeyUpReceived(int keycode) {
			var inputs = new NativeMethods.INPUT[1];
			inputs[0].type = INPUT_KEYBOARD;
			inputs[0].u.ki.wVk = NativeMethods.MapKeyToVirtualKey((Key)keycode);
			inputs[0].u.ki.dwFlags = KEYEVENTF_KEYUP;
			inputs[0].u.ki.dwExtraInfo = NativeMethods.GetMessageExtraInfo();
			_ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
		}
		#endregion

		#region VideoFeed_Methods
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
					this.Dispatcher.Invoke(() => { /*ConnectionStatus.Content = imageBytes.Length;*/ CaptureTimeLabel.Content = timer.ElapsedMilliseconds + "ms"; });

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

		private static void CaptureScreen(Graphics graphics, Bitmap bitmap) {
			graphics.CopyFromScreen(System.Windows.Forms.Screen.PrimaryScreen.Bounds.X,
									System.Windows.Forms.Screen.PrimaryScreen.Bounds.Y,
									0, 0,
									bitmap.Size,
									CopyPixelOperation.SourceCopy);
		}
		#endregion

		private void Disconnect_Button_Click(object sender, RoutedEventArgs e) {
			CancellationTokenSource.Cancel();
		}

		private void Settings_Button_Click(object sender, RoutedEventArgs e) {
			//open window with screen
		}

		private void MoveWindowArea_StackPanel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
			if (e.ChangedButton == MouseButton.Left)
				this.DragMove();
		}

		public void Dispose() {
			//CancellationTokenSource.Cancel();
			CancellationTokenSource.Dispose();
			GC.SuppressFinalize(this);
		}

	}
}