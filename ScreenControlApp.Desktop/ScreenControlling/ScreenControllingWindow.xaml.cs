﻿using Microsoft.AspNetCore.SignalR.Client;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Threading;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace ScreenControlApp.Desktop.ScreenControlling {
	/// <summary>
	/// Interaction logic for ScreenControllingWindow.xaml
	/// </summary>
	public partial class ScreenControllingWindow : Window, IDisposable {
		private HubConnection Connection { get; set; } = null!;
		private string User { get; set; } = null!;
		private string Passcode { get; set; } = null!;
		private string PeerConnectionId { get; set; } = null!;
		private double PeerScreenWidth { get; set; }
		private double PeerScreenHeight { get; set; }
		private readonly BlockingCollection<MemoryStream> FrameBuffer = new(24 * 5);

		private readonly CancellationTokenSource CancellationTokenSource = new();

		private readonly TaskCompletionSource<string> PeerConnectionIdCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource<(double, double)> PeerScreenSizeCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource IsInitializedCompletionSource = new();

		public ScreenControllingWindow(string user, string passcode) {
			InitializeComponent();
			//Image.Width = this.Width;
			//Image.Height=this.Height;
			User = user;
			Passcode = passcode;

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

			await Connection.InvokeAsync("AnnounceControl", User, Passcode);

			PeerConnectionId = await PeerConnectionIdCompletionSource.Task;
			(PeerScreenWidth, PeerScreenHeight) = await PeerScreenSizeCompletionSource.Task;


			//var backgroundThread1 = new Thread(async () => await RetrieveFrames()) {
			//	IsBackground = true
			//};
			//backgroundThread1.Start();
			_ = Task.Factory.StartNew(RetrieveFrames, TaskCreationOptions.LongRunning);
			_ = Task.Factory.StartNew(DisplayFrames, TaskCreationOptions.LongRunning);

			IsInitializedCompletionSource.SetResult();
		}

		private async Task InitializeSignalR() {
			try {
				Connection = new HubConnectionBuilder()
					.WithUrl("http://localhost:5026/screenControlHub")
					.Build();

				Connection.Closed += async (obj) => {
					await Task.Delay(new Random().Next(0, 5) * 1000);
					await Connection.StartAsync();
				};
				//Connection.On<string, string>("ReceivePacket", (user, message) => {
				//	this.Dispatcher.Invoke(() => {
				//		var newMessage = $"control {user}: {message}";
				//		MessageBox.Show(newMessage);
				//	});
				//});
				Connection.On<string>("FailedConnection", (message) => {
					MessageBox.Show($"Couldn't connect: {message}");
				});
				Connection.On<string>("ReceiveConnectionToControl", (peerId) => {
					PeerConnectionIdCompletionSource.SetResult(peerId);
					this.Dispatcher.Invoke(() => {
						ConnectionStatus.Content = "Connected";
					});
				});
				Connection.On<double, double>("ReceiveScreenSize", (double width, double height) => {
					PeerScreenSizeCompletionSource.SetResult((width, height));
				});
				Connection.On<string>("Error", (error) => { MessageBox.Show($"Error: {error}"); });

				await Connection.StartAsync();
			}
			catch (Exception ex) {
				MessageBox.Show(ex.Message);
				CancellationTokenSource.Cancel();
			}
		}

		private async Task RetrieveFrames() {
			var token = CancellationTokenSource.Token;
			await IsInitializedCompletionSource.Task;
			try {
				//while (!IsInitialized) {
				//	await Task.Delay(1000);
				//}

				this.Dispatcher.Invoke(() => ConnectingStatusLabel.Content = string.Empty);

				var timer = Stopwatch.StartNew();
				while (!token.IsCancellationRequested) {
					timer.Restart();

					var channel = await Connection.StreamAsChannelAsync<byte[]>("DownloadFrame", PeerConnectionId, token);
					var memoryStream = new MemoryStream();
					memoryStream.SetLength(0); // Reset the memory stream

					while (await channel.WaitToReadAsync()) {
						await foreach (var chunk in channel.ReadAllAsync()) {
							memoryStream.Write(chunk, 0, chunk.Length);
						}
					}

					if (memoryStream.Length == 0) {
						Dispatcher.Invoke(() => TransferStatus.Content = "Buffer is empty");
						await Task.Delay(500);
						continue;
					}

					memoryStream.Position = 0;
					FrameBuffer.Add(memoryStream);

					timer.Stop();
					Dispatcher.Invoke(() => TransferTimeLabel.Content = timer.ElapsedMilliseconds + "ms");
				}
			}
			catch (Exception ex) {
				MessageBox.Show(ex.ToString());
			}
		}

		private async Task DisplayFrames() {
			try {
				var token = CancellationTokenSource.Token;
				var timer = Stopwatch.StartNew();
				while (!token.IsCancellationRequested) {
					timer.Restart();
					using MemoryStream memoryStream = FrameBuffer.Take();
					var bitmapImage = new BitmapImage();
					bitmapImage.BeginInit();
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.StreamSource = memoryStream;
					bitmapImage.EndInit();
					bitmapImage.Freeze();
					this.Dispatcher.Invoke(() => {
						Image.Source = bitmapImage;
						RenderFrameBufferLabel.Content = FrameBuffer.Count;
					});
					timer.Stop();
					Dispatcher.Invoke(() => RenderTimeLabel.Content = timer.ElapsedMilliseconds + "ms + ");
					await Task.Delay(24);
				}
			}
			catch (Exception e) {
				MessageBox.Show(e.ToString());
			}
		}


		//private static BitmapImage BitmapToImageSource(Bitmap bitmap) {
		//	using var memoryStream = new MemoryStream();
		//	bitmap.Save(memoryStream, ImageFormat.Jpeg);
		//	memoryStream.Position = 0;
		//	var bitmapImage = new BitmapImage();
		//	bitmapImage.BeginInit();
		//	bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		//	bitmapImage.StreamSource = memoryStream;
		//	bitmapImage.EndInit();
		//	return bitmapImage;
		//}


		private async void Button_Click(object sender, RoutedEventArgs e) {
			try {
				await Connection.InvokeAsync("AnnounceControl", User, Passcode);
			}
			catch (Exception ex) {
				MessageBox.Show(ex.Message);
			}
		}

		public struct KeyboardInput {
			public enum ModifierKey {
				NONE, SHIFT, CTRL, ALT,
			}

			public ushort Key { get; set; }
			public ModifierKey SpecialKeyFlag { get; set; }
		}
		//public enum MouseDown

		private async void VideoFeed_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) {
			if (!IsInitializedCompletionSource.Task.IsCompleted)
				return;

			var position = e.GetPosition(Image);

			double normalizedX = Math.Clamp(position.X / Image.ActualWidth, 0, 1);
			double normalizedY = Math.Clamp(position.Y / Image.ActualHeight, 0, 1);

			//SWidth.Content = normalizedX;
			//SHeight.Content = normalizedY;

			await Connection.SendAsync("SendMouseMove", PeerConnectionId, normalizedX, normalizedY);
		}

		public class KeyboardSimulator {
			[StructLayout(LayoutKind.Sequential)]
			private struct INPUT {
				public uint type;
				public InputUnion u;
			}

			[StructLayout(LayoutKind.Explicit)]
			private struct InputUnion {
				[FieldOffset(0)] public MOUSEINPUT mi;
				[FieldOffset(0)] public KEYBDINPUT ki;
				[FieldOffset(0)] public HARDWAREINPUT hi;
			}

			[StructLayout(LayoutKind.Sequential)]
			private struct MOUSEINPUT {
				public int dx;
				public int dy;
				public uint mouseData;
				public uint dwFlags;
				public uint time;
				public IntPtr dwExtraInfo;
			}

			[StructLayout(LayoutKind.Sequential)]
			private struct KEYBDINPUT {
				public ushort wVk;
				public ushort wScan;
				public uint dwFlags;
				public uint time;
				public IntPtr dwExtraInfo;
			}

			[StructLayout(LayoutKind.Sequential)]
			private struct HARDWAREINPUT {
				public uint uMsg;
				public ushort wParamL;
				public ushort wParamH;
			}

			private const uint INPUT_KEYBOARD = 1;
			private const uint KEYEVENTF_KEYUP = 0x0002;

			[DllImport("user32.dll", SetLastError = true)]
			private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

			public static void SendKey(ushort keyCode, int duration = 100) {
				INPUT[] inputs = new INPUT[2];

				// Key down event
				inputs[0].type = INPUT_KEYBOARD;
				inputs[0].u.ki.wVk = keyCode;
				inputs[0].u.ki.wScan = 0;
				inputs[0].u.ki.dwFlags = 0;
				inputs[0].u.ki.time = 0;
				inputs[0].u.ki.dwExtraInfo = IntPtr.Zero;

				// Key up event
				inputs[1].type = INPUT_KEYBOARD;
				inputs[1].u.ki.wVk = keyCode;
				inputs[1].u.ki.wScan = 0;
				inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP;
				inputs[1].u.ki.time = 0;
				inputs[1].u.ki.dwExtraInfo = IntPtr.Zero;

				// Send key down event
				SendInput(1, new INPUT[] { inputs[0] }, Marshal.SizeOf(typeof(INPUT)));

				// Wait for the specified duration
				System.Threading.Thread.Sleep(duration);

				// Send key up event
				SendInput(1, new INPUT[] { inputs[1] }, Marshal.SizeOf(typeof(INPUT)));
			}
		}
		private async void VideoFeed_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
			await Connection.SendAsync("SendMouseDown", PeerConnectionId, (int)e.ChangedButton);
		}

		private async void VideoFeed_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e) {
			await Connection.SendAsync("SendMouseUp", PeerConnectionId, (int)e.ChangedButton);
		}

		private async void VideoFeed_MouseScroll(object sender, System.Windows.Input.MouseWheelEventArgs e) {
			await Connection.SendAsync("SendMouseScroll", PeerConnectionId, e.Delta);
		}

		private async void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
			if (!IsInitializedCompletionSource.Task.IsCompleted) return;
			await Connection.SendAsync("SendKeyDown", PeerConnectionId, (int)e.Key);
		}

		private async void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e) {
			if (!IsInitializedCompletionSource.Task.IsCompleted) return;
			await Connection.SendAsync("SendKeyUp", PeerConnectionId, (int)e.Key);
		}

		public void Dispose() {
			CancellationTokenSource.Dispose();
			GC.SuppressFinalize(this);
		}

		private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
			KeyboardSimulator.SendKey(0x41, 10000);
		}
	}
}
