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
using System.IO.Pipelines;

namespace ScreenControlApp.Desktop.ScreenControlling {

	public partial class ScreenControllingWindow : Window, IDisposable {
		private ApplicationSettings Settings { get; set; }
		private HubConnection HubConnection { get; set; } = null!;
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

			await HubConnection.InvokeAsync("AnnounceControl", User, Passcode);

			PeerConnectionId = await PeerConnectionIdCompletionSource.Task;

			_ = Task.Factory.StartNew(RetrieveFrames, TaskCreationOptions.LongRunning);
			//_ = Task.Factory.StartNew(DisplayFrames, TaskCreationOptions.LongRunning);

			IsInitializedCompletionSource.SetResult();
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
				};
				HubConnection.On<string>("FailedConnection", (message) => {
					MessageBox.Show($"Couldn't connect: {message}");
				});
				HubConnection.On<string>("ReceiveConnectionToControl", (peerId) => {
					PeerConnectionIdCompletionSource.SetResult(peerId);
					this.Dispatcher.Invoke(() => UpdateConnectionStatus(true));
				});
				HubConnection.On<string>("Error", (error) => { MessageBox.Show($"Error: {error}"); });

				await HubConnection.StartAsync();
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
			//var frameRetriever = new ChannelFrameRetriever(HubConnection, PeerConnectionId, token);
			try {
				var timer = Stopwatch.StartNew();
				//while (!token.IsCancellationRequested) {
				//	timer.Restart();

				//	var memoryStream = await frameRetriever.RetrieveAsync();

				//	if (memoryStream.Length == 0) {
				//		await Task.Delay(500);
				//		continue;
				//	}

				//	memoryStream.Position = 0;
				//	FrameBuffer.Add(memoryStream);

				//timer.Stop();
				//Dispatcher.Invoke(() => TransferTimeLabel.Content = timer.ElapsedMilliseconds + "ms");
				//}

				using Process ffmpegDecoder = new Process {
					StartInfo = new ProcessStartInfo {
						FileName = "ffmpeg.exe",
						//Arguments = "-fflags nobuffer -f mpegts -i pipe:0 -vf fps=24 -f image2pipe -vcodec bmp pipe:1",
						Arguments = "-f mpegts -i pipe:0 -vf fps=24 R:/frame_%04d.png",
						UseShellExecute = false,
						RedirectStandardInput = true,
						RedirectStandardOutput = true,
						CreateNoWindow = true
					}
				};

				// Start FFmpeg process
				ffmpegDecoder.Start();

				MemoryStream videoStream = new();

				HubConnection.On<byte[]>("ReceiveVideoStream", async (videoBytes) =>
				{
					await ffmpegDecoder.StandardInput.BaseStream.WriteAsync(videoBytes, 0, videoBytes.Length);
					await ffmpegDecoder.StandardInput.BaseStream.FlushAsync(); // Flush to ensure data is sent
				});

				// Write received video stream to FFmpeg stdin
				await videoStream.CopyToAsync(ffmpegDecoder.StandardInput.BaseStream);
				//ffmpegDecoder.StandardInput.Close();

				// Continuously read the output of FFmpeg (which will be raw bitmap data)
				var bitmapReader = ffmpegDecoder.StandardOutput.BaseStream;

				while (true) {
					// Read and convert bitmap from FFmpeg output

					Bitmap bitmap = ReadBitmapFromStream(bitmapReader);
					timer.Stop();
					Dispatcher.Invoke(() => TransferTimeLabel.Content = timer.ElapsedMilliseconds + "ms");
					if (bitmap != null) {
						// Call method to display frame in WPF
						ShowFrame(bitmap);
					}
					else {
						MessageBox.Show("error");
						break;
					}
				}

			}
			catch (Exception ex) {
				MessageBox.Show(ex.ToString());
			}
		}
		private Bitmap ReadBitmapFromStream(Stream stream) {
			try {
				// Create a memory stream to hold the incoming bitmap data
				using (MemoryStream ms = new()) {
					byte[] buffer = new byte[4096];
					int bytesRead;

					// Read until you have the entire BMP image data
					while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0) {
						ms.Write(buffer, 0, bytesRead);

						// BMP images have a header. Check for end of image based on known file size or image structure.
						// Optional: You may need to implement logic here to stop reading when the full bitmap is captured.
					}

					ms.Position = 0;  // Reset memory stream position to the beginning
					return new Bitmap(ms);  // Create Bitmap from memory stream
				}
			}
			catch (Exception ex) {
				Console.WriteLine("Error reading bitmap from stream: " + ex.Message);
				return null;
			}
		}
		private void ShowFrame(Bitmap bitmap) {
			var timer = Stopwatch.StartNew();
			using MemoryStream memoryStream = new();
			BitmapImage bitmapImage = new BitmapImage();
			using (MemoryStream memory = new MemoryStream()) {
				bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
				memory.Position = 0;
				bitmapImage.BeginInit();
				bitmapImage.StreamSource = memory;
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.EndInit();
				bitmapImage.Freeze();
			}
			this.Dispatcher.Invoke(() => {
				Image.Source = bitmapImage;
				RenderFrameBufferLabel.Content = FrameBuffer.Count;
			});
			timer.Stop();
			Dispatcher.Invoke(() => RenderTimeLabel.Content = timer.ElapsedMilliseconds + "ms");
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
				await HubConnection.InvokeAsync("AnnounceControl", User, Passcode);
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

			await HubConnection.SendAsync("SendMouseMove", PeerConnectionId, normalizedX, normalizedY);
		}

		private async void VideoFeed_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
			await HubConnection.SendAsync("SendMouseDown", PeerConnectionId, (int)e.ChangedButton);
		}

		private async void VideoFeed_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e) {
			await HubConnection.SendAsync("SendMouseUp", PeerConnectionId, (int)e.ChangedButton);
		}

		private async void VideoFeed_MouseScroll(object sender, System.Windows.Input.MouseWheelEventArgs e) {
			await HubConnection.SendAsync("SendMouseScroll", PeerConnectionId, e.Delta);
		}

		private async void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
			if (!IsInitializedCompletionSource.Task.IsCompleted) return;
			await HubConnection.SendAsync("SendKeyDown", PeerConnectionId, (int)e.Key);
		}

		private async void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e) {
			if (!IsInitializedCompletionSource.Task.IsCompleted) return;
			await HubConnection.SendAsync("SendKeyUp", PeerConnectionId, (int)e.Key);
		}

		public void Dispose() {
			CancellationTokenSource.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
