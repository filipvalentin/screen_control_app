//using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Client;

//using Microsoft.AspNetCore.SignalR.Client;
using System.Windows;

namespace ScreenControlApp.Desktop {
	/// <summary>
	/// Interaction logic for ScreenSharingWindow.xaml
	/// </summary>
	public partial class ScreenSharingWindow : Window {
		private HubConnection? Connection { get; set; }
		private bool IsClosed { get; set; }
		private string User { get; set; }
		private string Passcode { get; set; }

		public ScreenSharingWindow(string user, string passcode) {
			User = user;
			Passcode = passcode;

			InitializeComponent();

			this.Closed += (sender, args) => {
				IsClosed = true;
			};

			InitializeSignalR();
		}

		private async void InitializeSignalR() {
			try {
				//Connection = new HubConnection("http://localhost:5026/screenControlHub");
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

				await Connection.StartAsync();
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
				await Connection.InvokeAsync("SendPacket", User, test.Text);
			}
			catch (Exception ex) {
				MessageBox.Show(ex.Message);
			}
		}
	}
}
