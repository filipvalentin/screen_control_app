using Microsoft.AspNetCore.SignalR.Client;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Threading;

namespace ScreenControlApp.Desktop {
	/// <summary>
	/// Interaction logic for ScreenControlWindow.xaml
	/// </summary>
	public partial class ScreenControlWindow : Window {
		private HubConnection Connection { get; set; }
		private bool IsClosed { get; set; }
		private string User { get; set; }
		private string Passcode { get; set; }
		private string? PeerId { get; set; } = null;
		private readonly CancellationTokenSource CancellationTokenSource = new();

		public ScreenControlWindow(string user, string passcode) {
			User = user;
			Passcode = passcode;

			InitializeComponent();

			this.Closed += (sender, args) => {
				IsClosed = true;
			};

			InitializeSignalR();
			Connection.InvokeAsync("AnnounceControl", User, Passcode);
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

				test.Text = Connection.ConnectionId;
				var token = CancellationTokenSource.Token;
				_ = Task.Run(async () => {
					while (PeerId == null) {
						await Task.Delay(1000);
					}
					try {
						while (!token.IsCancellationRequested) {
							var channel = await Connection.StreamAsChannelAsync<byte>("DownloadFrame", PeerId, token);
							using var memoryStream = new MemoryStream();
							while (await channel.WaitToReadAsync()) {
								await foreach (var b in channel.ReadAllAsync()) {
									memoryStream.WriteByte(b);
								}
							}
							if (memoryStream.Length == 0) {
								Dispatcher.Invoke(() => TransferStatus.Content = "Buffer is empty");
								await Task.Delay(500);
								continue;
							}
							Dispatcher.Invoke(() => ConnectionStatus.Content = memoryStream.Length);
							memoryStream.Position = 0;

							await Dispatcher.InvokeAsync(() => {
								var bitmapImage = new BitmapImage();
								bitmapImage.BeginInit();
								bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
								bitmapImage.StreamSource = memoryStream;
								bitmapImage.EndInit();

								Image.Source = bitmapImage;
								ConnectionStatus.Content = memoryStream.Length;
							});

							await Task.Delay(1000);
						}
					} catch(Exception ex) { 
						MessageBox.Show(ex.ToString());
					}
				});
			}
			catch (Exception ex) {
				if (IsClosed)
					return;
				MessageBox.Show(ex.Message);
				throw ex;//handle this
			}
		}

		private static BitmapImage BitmapToImageSource(Bitmap bitmap) {
			using var memoryStream = new MemoryStream();
			bitmap.Save(memoryStream, ImageFormat.Jpeg);
			memoryStream.Position = 0;
			var bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.StreamSource = memoryStream;
			bitmapImage.EndInit();
			return bitmapImage;
		}


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
