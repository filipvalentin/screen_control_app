using Microsoft.AspNetCore.SignalR.Client;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Threading;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace ScreenControlApp.Desktop {
	/// <summary>
	/// Interaction logic for ScreenControlWindow.xaml
	/// </summary>
	public partial class ScreenControlWindow : Window {
		private HubConnection Connection { get; set; } = null!;
		//private bool IsClosed { get; set; }
		private string User { get; set; }
		private string Passcode { get; set; }
		private string? PeerId { get; set; } = null;
		private new bool IsInitialized { get; set; }
		private readonly CancellationTokenSource CancellationTokenSource = new();
		private readonly BlockingCollection<MemoryStream> FrameBuffer = new(24 * 5);
		public ScreenControlWindow(string user, string passcode) {
			InitializeComponent();
			
			User = user;
			Passcode = passcode;

			this.Closed += (sender, args) => {
				//IsClosed = true;
				CancellationTokenSource.Cancel();
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

			_ = Task.Run(RetrieveFrames);
			_ = Task.Run(DisplayFrames);

			IsInitialized = true;
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

				Connection.On<string, string>("ReceivePacket", (user, message) => {
					this.Dispatcher.Invoke(() => {
						var newMessage = $"control {user}: {message}";
						MessageBox.Show(newMessage);
					});
				});

				Connection.On<string>("FailedConnection", (message) => {
					MessageBox.Show($"Couldn't connect: {message}");
				});

				Connection.On<string>("ReceiveConnectionToControl", (peerId) => {
					PeerId = peerId;
					this.Dispatcher.Invoke(() => {
						ConnectionStatus.Content = "Connected";

						//MessageBox.Show($"control received connection {peerId}");
					});
				});

				Connection.On<string>("Error", (error) => { MessageBox.Show($"Error: {error}"); });

				await Connection.StartAsync();
			}
			catch (Exception ex) {
				//if (IsClosed)
				//	CancellationTokenSource.Cancel();
				MessageBox.Show(ex.Message);
				CancellationTokenSource.Cancel();
			}
		}

		private async Task RetrieveFrames() {
			var token = CancellationTokenSource.Token;
			try {
				while (!IsInitialized) {
					await Task.Delay(1000);
				}
				this.Dispatcher.Invoke(() => ConnectingStatusLabel.Content = string.Empty);

				var timer = Stopwatch.StartNew();
				while (!token.IsCancellationRequested) {
					timer.Restart();

					var channel = await Connection.StreamAsChannelAsync<byte[]>("DownloadFrame", PeerId, token);
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
					this.Dispatcher.Invoke(() => {
						var bitmapImage = new BitmapImage();
						bitmapImage.BeginInit();
						bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
						bitmapImage.StreamSource = memoryStream;
						bitmapImage.EndInit();
						bitmapImage.Freeze();
						Image.Source = bitmapImage;
						RenderFrameBufferLabel.Content = FrameBuffer.Count;
					});
					timer.Stop();
					Dispatcher.Invoke(() => RenderTimeLabel.Content = timer.ElapsedMilliseconds + "ms");
					await Task.Delay(12);
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


	}
}
