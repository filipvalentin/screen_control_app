using Microsoft.AspNetCore.SignalR.Client;
using ScreenControlApp.Desktop.Common;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;

namespace ScreenControlApp.Desktop {
	/// <summary>
	/// Interaction logic for ScreenSharingWindow.xaml
	/// </summary>
	public partial class ScreenSharingWindow : Window {
		private HubConnection Connection { get; set; }
		private bool IsClosed { get; set; }
		private string User { get; set; }
		private string Passcode { get; set; }
		private string? PeerId { get; set; } = null;

		private readonly CancellationTokenSource cancellationTokenSource = new();
		public ScreenSharingWindow(string user, string passcode) {
			User = user;
			Passcode = passcode;

			InitializeComponent();

			this.Closed += (sender, args) => {
				IsClosed = true;
				cancellationTokenSource.Cancel();
			};

			InitializeSignalR();
			Connection.InvokeAsync("AnnounceShare", User, Passcode);
		}

		private async void InitializeSignalR() {
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
						var newMessage = $"sharing {user}: {message}";
						MessageBox.Show(newMessage);
					});
				});
				Connection.On<string>("FailedConnection", (message) => {
					MessageBox.Show($"Couldn't connect: {message}");
				});
				Connection.On<string>("FailedTransfer", (message) => {
					MessageBox.Show($"Couldn't transfer: {message}");
				});
				Connection.On<string>("ReceiveConnectionToShare", (peerId) => {
					PeerId = peerId;
					this.Dispatcher.Invoke(() => {
						ConnectionStatus.Content = "Connected";

						//MessageBox.Show($"share received connection {peerId}");
					});
				});

				await Connection.StartAsync();

				test.Text = Connection.ConnectionId;
			}
			catch (Exception ex) {

				if (IsClosed)
					return;
				MessageBox.Show(ex.Message);
				throw ex;
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

			var cancellationToken = cancellationTokenSource.Token;

			_ = Task.Run(async () => {
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
			});
		}


		private void CaptureScreen(Graphics graphics, Bitmap bitmap) {
			graphics.CopyFromScreen(System.Windows.Forms.Screen.PrimaryScreen.Bounds.X,
									System.Windows.Forms.Screen.PrimaryScreen.Bounds.Y,
									0, 0,
									bitmap.Size,
									CopyPixelOperation.SourceCopy);
		}

		//private static BitmapSource CaptureScreen(Graphics graphics, Bitmap bitmap) {
		//	graphics.CopyFromScreen(System.Windows.Forms.Screen.PrimaryScreen.Bounds.X,
		//							System.Windows.Forms.Screen.PrimaryScreen.Bounds.Y,
		//							0, 0,
		//							bitmap.Size,
		//							CopyPixelOperation.SourceCopy);

		//	IntPtr hBitmap = bitmap.GetHbitmap();
		//	try {
		//		return Imaging.CreateBitmapSourceFromHBitmap(
		//			hBitmap,
		//			IntPtr.Zero,
		//			Int32Rect.Empty,
		//			BitmapSizeOptions.FromEmptyOptions());
		//	}
		//	finally {
		//		// Release the HBitmap to avoid memory leaks
		//		ScreenCapture.DeleteObject(hBitmap);
		//	}
		//}


		//public class ScreenshotHelper {
		//public Bitmap CaptureScreen() {
		//	// Capture the entire screen.
		//	Rectangle screenSize = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
		//	Bitmap bitmap = new Bitmap(screenSize.Width, screenSize.Height);

		//	using (Graphics g = Graphics.FromImage(bitmap)) {
		//		g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
		//	}

		//	return bitmap;
		//}

		//public BitmapImage BitmapToImageSource(Bitmap bitmap) {
		//	using (MemoryStream memory = new MemoryStream()) {
		//		bitmap.Save(memory, ImageFormat.Bmp);
		//		memory.Position = 0;

		//		BitmapImage bitmapImage = new BitmapImage();
		//		bitmapImage.BeginInit();
		//		bitmapImage.StreamSource = memory;
		//		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		//		bitmapImage.EndInit();

		//		return bitmapImage;
		//	}
		//}
	}
}



