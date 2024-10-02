using Microsoft.AspNetCore.SignalR.Client;
using ScreenControlApp.Desktop.Common.Settings;
using ScreenControlApp.Desktop.ScreenSharing.FrameProviders;
using ScreenControlApp.Desktop.ScreenSharing.FrameSenders;
using ScreenControlApp.Desktop.ScreenSharing.Util;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;

namespace ScreenControlApp.Desktop.ScreenSharing {

	public partial class ScreenSharingWindow : Window, IDisposable {
		private ApplicationSettings Settings { get; set; }
		private HubConnection HubConnection { get; set; } = null!;
		private string User { get; set; } = null!;
		private string Passcode { get; set; } = null!;
		private string PeerConnectionId { get; set; } = null!;
		private Screen SharedScreen { get; set; } = null!;

		private readonly CancellationTokenSource CancellationTokenSource = new();

		private readonly TaskCompletionSource<string> PeerConnectionIdCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource IsInitializedCompletionSource = new();

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

			await HubConnection.InvokeAsync("AnnounceShare", User, Passcode);

			PeerConnectionId = await PeerConnectionIdCompletionSource.Task;

			IsInitializedCompletionSource.SetResult();

			_ = Task.Factory.StartNew(ShareVideoFeed, TaskCreationOptions.LongRunning);
		}
		private async Task InitializeSignalR() {
			try {
				HubConnection = new HubConnectionBuilder()
					.WithUrl(Settings.ServerAddress + Settings.HubName)
					.Build();

				HubConnection.Closed += async (obj) => {
					await Task.Delay(new Random().Next(0, 5) * 1000);
					try {
						await HubConnection.StartAsync();
						this.Dispatcher.Invoke(() => UpdateConnectionStatus(true));
					}
					catch (Exception e) {
						this.Dispatcher.Invoke(() => UpdateConnectionStatus(false));
						MessageBox.Show("Couldn't reconnect: " + e.ToString());
						//handle this better
					}
					//TODO: RESET CONNECTION IDS
				};

				HubConnection.On<string>("FailedConnection", (message) => {
					MessageBox.Show($"Couldn't connect: {message}");
				});
				HubConnection.On<string>("FailedTransfer", (message) => {
					MessageBox.Show($"Couldn't transfer: {message}");
				});
				HubConnection.On<string>("ReceiveConnectionToShare", (peerId) => {
					PeerConnectionIdCompletionSource.SetResult(peerId);
				});
				HubConnection.On<double, double>("ReceiveMouseMove", MouseMoveReceived);
				HubConnection.On<int>("ReceiveMouseDown", MouseDownReceived);
				HubConnection.On<int>("ReceiveMouseUp", MouseUpReceived);
				HubConnection.On<int>("ReceiveMouseScroll", MouseScrollReceived);
				HubConnection.On<int>("ReceiveKeyDown", KeyDownReceived);
				HubConnection.On<int>("ReceiveKeyUp", KeyUpReceived);
				await HubConnection.StartAsync();
			}
			catch (Exception ex) {
				MessageBox.Show(ex.Message);
			}
		}
		private void UpdateConnectionStatus(bool isConnected) {
			if (isConnected) {
				StatusIndicator.Fill = new SolidColorBrush(Colors.Green);
			}
			else {
				StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
			}
		}

		#region Controlling_Methods
		private void MouseMoveReceived(double normalizedX, double normalizedY) {
			int targetX = SharedScreen.Bounds.Left + (int)(normalizedX * SharedScreen.Bounds.Width);
			int targetY = SharedScreen.Bounds.Top + (int)(normalizedY * SharedScreen.Bounds.Height);

			int screenNormalizedX = ((targetX - SystemInformation.VirtualScreen.Left) * 65535) / SystemInformation.VirtualScreen.Width;
			int screenNormalizedY = ((targetY - SystemInformation.VirtualScreen.Top) * 65535) / SystemInformation.VirtualScreen.Height;

			var inputs = new NativeMethods.INPUT[1];
			inputs[0].type = NativeMethods.INPUT_MOUSE;
			inputs[0].u.mi.dx = screenNormalizedX;
			inputs[0].u.mi.dy = screenNormalizedY;
			inputs[0].u.mi.dwFlags = NativeMethods.MOUSEEVENTF_ABSOLUTE | NativeMethods.MOUSEEVENTF_MOVE | NativeMethods.MOUSEEVENTF_VIRTUALDESK;
			_ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
		}
		private void MouseDownReceived(int buttonCode) {
			var inputs = new NativeMethods.INPUT[1];
			inputs[0].type = NativeMethods.INPUT_MOUSE;
			//MessageBox.Show(buttonCode.ToString());
			switch ((System.Windows.Input.MouseButton)buttonCode) {
				case System.Windows.Input.MouseButton.Left:
					inputs[0].u.mi.dwFlags = NativeMethods.MOUSEEVENTF_LEFTDOWN;
					break;
				case System.Windows.Input.MouseButton.Right:
					inputs[0].u.mi.dwFlags = NativeMethods.MOUSEEVENTF_RIGHTDOWN;
					break;
				case System.Windows.Input.MouseButton.Middle:
					inputs[0].u.mi.dwFlags = NativeMethods.MOUSEEVENTF_MIDDLEDOWN;
					break;
				case System.Windows.Input.MouseButton.XButton1:
					inputs[0].u.mi = new NativeMethods.MOUSEINPUT {
						dwFlags = NativeMethods.MOUSEEVENTF_XDOWN,
						mouseData = NativeMethods.XBUTTON1
					};
					break;
				case System.Windows.Input.MouseButton.XButton2:
					inputs[0].u.mi = new NativeMethods.MOUSEINPUT {
						dwFlags = NativeMethods.MOUSEEVENTF_XDOWN,
						mouseData = NativeMethods.XBUTTON2
					};
					break;
			};
			_ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
		}
		private void MouseUpReceived(int buttonCode) {
			//System.Windows.Input.MouseButton pressedButton = ;
			var inputs = new NativeMethods.INPUT[1];
			inputs[0].type = NativeMethods.INPUT_MOUSE;
			switch ((System.Windows.Input.MouseButton)buttonCode) {
				case System.Windows.Input.MouseButton.Left:
					inputs[0].u.mi.dwFlags = NativeMethods.MOUSEEVENTF_LEFTUP;
					break;
				case System.Windows.Input.MouseButton.Right:
					inputs[0].u.mi.dwFlags = NativeMethods.MOUSEEVENTF_RIGHTUP;
					break;
				case System.Windows.Input.MouseButton.Middle:
					inputs[0].u.mi.dwFlags = NativeMethods.MOUSEEVENTF_MIDDLEUP;
					break;
				case System.Windows.Input.MouseButton.XButton1:
					inputs[0].u.mi = new NativeMethods.MOUSEINPUT {
						dwFlags = NativeMethods.MOUSEEVENTF_XUP,
						mouseData = NativeMethods.XBUTTON1
					};
					break;
				case System.Windows.Input.MouseButton.XButton2:
					inputs[0].u.mi = new NativeMethods.MOUSEINPUT {
						dwFlags = NativeMethods.MOUSEEVENTF_XUP,
						mouseData = NativeMethods.XBUTTON2
					};
					break;
			};
			_ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
		}
		private void MouseScrollReceived(int scrollValue) {
			var inputs = new NativeMethods.INPUT[1];
			inputs[0].type = NativeMethods.INPUT_MOUSE;
			inputs[0].u.mi = new NativeMethods.MOUSEINPUT {
				dwFlags = NativeMethods.MOUSEEVENTF_WHEEL,
				mouseData = scrollValue
			};
			_ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
		}

		private async void KeyDownReceived(int keycode) {
			var inputs = new NativeMethods.INPUT[1];
			inputs[0].type = NativeMethods.INPUT_KEYBOARD;
			inputs[0].u.ki.wVk = NativeMethods.MapKeyToVirtualKey((Key)keycode);
			inputs[0].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYDOWN;
			inputs[0].u.ki.dwExtraInfo = NativeMethods.GetMessageExtraInfo();
			_ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
		}
		private void KeyUpReceived(int keycode) {
			var inputs = new NativeMethods.INPUT[1];
			inputs[0].type = NativeMethods.INPUT_KEYBOARD;
			inputs[0].u.ki.wVk = NativeMethods.MapKeyToVirtualKey((Key)keycode);
			inputs[0].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;
			inputs[0].u.ki.dwExtraInfo = NativeMethods.GetMessageExtraInfo();
			_ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
		}

		#endregion

		#region VideoFeed_Methods
		private async Task ShareVideoFeed() {
			var cancellationToken = CancellationTokenSource.Token;

			using var frameProvider = new DDAPIFrameProvider(cancellationToken);//new FFMPEGFrameProvider("E:\\Utilitare\\ShareX\\ffmpeg.exe", SharedScreen, cancellationToken); //new GDIFrameProvider(SharedScreen);////
			var frameSender = new ChannelFrameSender(HubConnection);//new BlockFrameSender(HubConnection, PeerConnectionId);

			using var memoryStream = new MemoryStream();
			try {
				while (!cancellationToken.IsCancellationRequested) {
					var timer = Stopwatch.StartNew();

					frameProvider.CaptureFrame(memoryStream);
					this.Dispatcher.Invoke(() => CaptureTimeLabel.Content = timer.ElapsedMilliseconds + "ms");

					memoryStream.Position = 0;
					timer.Restart();

					await frameSender.SendFrame(memoryStream);
					this.Dispatcher.Invoke(() => TransferTimeLabel.Content = timer.ElapsedMilliseconds + "ms");

					await Task.Delay(1000 / 24);
				}
			}
			catch (Exception ex) {
				MessageBox.Show(ex.ToString());
			}
		}
		#endregion

		private void Disconnect_Button_Click(object sender, RoutedEventArgs e) {
			this.Close();
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