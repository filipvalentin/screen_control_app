using Microsoft.AspNetCore.SignalR.Client;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Threading;
using System.Diagnostics;
using System.Collections.Concurrent;
using ScreenControlApp.Desktop.Common.Settings;
using System.Windows.Media;
using ScreenControlApp.Desktop.ScreenControlling.Util;

namespace ScreenControlApp.Desktop.ScreenControlling {

	public partial class ScreenControllingWindow : Window, IDisposable {
		private ApplicationSettings Settings { get; set; }
		private HubConnection Connection { get; set; } = null!;
		private string User { get; set; } = null!;
		private string Passcode { get; set; } = null!;
		private string PeerConnectionId { get; set; } = null!;
		private readonly BlockingCollection<MemoryStream> FrameBuffer = new(24 * 5);

		private readonly CancellationTokenSource CancellationTokenSource = new();

		private readonly TaskCompletionSource<string> PeerConnectionIdCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource IsInitializedCompletionSource = new();

		public ScreenControllingWindow(ApplicationSettings settings, string user, string passcode) {
			InitializeComponent();

			Settings = settings;
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

			_ = Task.Factory.StartNew(RetrieveFrames, TaskCreationOptions.LongRunning);
			_ = Task.Factory.StartNew(DisplayFrames, TaskCreationOptions.LongRunning);

			IsInitializedCompletionSource.SetResult();
		}

		private async Task InitializeSignalR() {
			try {
				Connection = new HubConnectionBuilder()
					.WithUrl(Settings.ServerAddress + Settings.HubName)
					.Build();

				Connection.Closed += async (obj) => {
					await Task.Delay(new Random().Next(0, 5) * 1000);
					try {
						await Connection.StartAsync();
						this.Dispatcher.Invoke(() => UpdateConnectionStatus(true));
					}
					catch (Exception e) {
						this.Dispatcher.Invoke(() => UpdateConnectionStatus(false));
						MessageBox.Show("Couldn't reconnect: " + e.ToString());
						//handle this better
					}
				};
				Connection.On<string>("FailedConnection", (message) => {
					MessageBox.Show($"Couldn't connect: {message}");
				});
				Connection.On<string>("ReceiveConnectionToControl", (peerId) => {
					PeerConnectionIdCompletionSource.SetResult(peerId);
					this.Dispatcher.Invoke(() => UpdateConnectionStatus(true));
				});
				Connection.On<string>("Error", (error) => { MessageBox.Show($"Error: {error}"); });

				await Connection.StartAsync();
			}
			catch (Exception ex) {
				MessageBox.Show(ex.Message);
				CancellationTokenSource.Cancel();
			}
		}

		private void UpdateConnectionStatus(bool isConnected) {
			if (isConnected) {
				StatusIndicator.Fill = new SolidColorBrush(Colors.Green);
				StatusIndicator.ToolTip = "Connection status: Connected";
			}
			else {
				StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
				StatusIndicator.ToolTip = "Connection status: Not connected";
			}
		}

		private async Task RetrieveFrames() {
			var token = CancellationTokenSource.Token;
			await IsInitializedCompletionSource.Task;
			var frameRetriever = new ChannelFrameRetriever(Connection, PeerConnectionId, token);
			try {
				var timer = Stopwatch.StartNew();
				while (!token.IsCancellationRequested) {
					timer.Restart();

					var memoryStream = await frameRetriever.RetrieveAsync();

					if (memoryStream.Length == 0) {
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
					Dispatcher.Invoke(() => RenderTimeLabel.Content = timer.ElapsedMilliseconds + "ms");
					await Task.Delay(24);
				}
			}
			catch (Exception e) {
				MessageBox.Show(e.ToString());
			}
		}

		private async void Button_Click(object sender, RoutedEventArgs e) {
			try {
				await Connection.InvokeAsync("AnnounceControl", User, Passcode);
			}
			catch (Exception ex) {
				MessageBox.Show(ex.Message);
			}
		}


		private async void VideoFeed_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) {
			if (!IsInitializedCompletionSource.Task.IsCompleted)
				return;

			var position = e.GetPosition(Image);

			double normalizedX = Math.Clamp(position.X / Image.ActualWidth, 0, 1);
			double normalizedY = Math.Clamp(position.Y / Image.ActualHeight, 0, 1);

			await Connection.SendAsync("SendMouseMove", PeerConnectionId, normalizedX, normalizedY);
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
	}
}
