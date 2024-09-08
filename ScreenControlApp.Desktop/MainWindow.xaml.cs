using System.Windows;
using ScreenControlApp.Desktop.ScreenControlling;
using ScreenControlApp.Desktop.ScreenSharing;
using System.Windows.Media.Imaging;
using ScreenControlApp.Desktop.Common.Settings;
using ScreenControlApp.Desktop.Common;
using System.Net.Http;

namespace ScreenControlApp.Desktop {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		private ApplicationSettings Settings { get; set; } = null!;
		private bool IsSettingsPageDisplayed = false;
		private CancellationTokenSource CancellationTokenSource { get; set; } = new();

		public MainWindow() {
			InitializeComponent();
			//Application.Current.MainWindow.WindowState = WindowState.Maximized;

			SharingSide_Passcode_TextBox.Text = "123";//generate one

			//var a = new QuickControlsWindow();
			//a.Show();
			this.Closed += ReturnHostId;
		}

		private async void ReturnHostId(object? sender, EventArgs e) {
			using HttpClient client = new();
			try {
				_ = await client.DeleteAsync(Settings.ServerAddress + "api/hostIds/" + SharingSide_HubId_TextBox.Text);
			}
			catch (HttpRequestException) {
				return;
			}
		}

		private void ControlHost_Button_Click(object sender, RoutedEventArgs e) {
			Window window = new ScreenControllingWindow(Settings, ControllingSide_HostId_TextBox.Text, ControllingSide_Passcode_TextBox.Text);
			Visibility = Visibility.Hidden;
			window.Show();
			window.Closing += (sender, args) => {
				Visibility = Visibility.Visible;
			};
		}

		private void ShareHost_Button_Click(object sender, RoutedEventArgs e) {
			Window window = new ScreenSharingWindow(Settings, SharingSide_HubId_TextBox.Text, SharingSide_Passcode_TextBox.Text);
			Visibility = Visibility.Hidden;
			window.Show();
			window.Closing += (sender, args) => {
				Visibility = Visibility.Visible;
			};
		}

		private void NavBar_SettingsButton_Click(object sender, RoutedEventArgs e) {
			if (!IsSettingsPageDisplayed) {
				SCA_Hub_Controls_Panel.Visibility = Visibility.Collapsed;//TODO:Rename panels to mainpanel
				SCA_Hub_Settings_Panel.Visibility = Visibility.Visible;
				Settings_Panel_SavedSettings_Label.Visibility = Visibility.Collapsed;
				NavBar_SettingsButton_Image.Source = new BitmapImage(new Uri(@"pack://application:,,,/ScreenControlApp.Desktop;component/Images/back.png"));
			}
			else {
				SCA_Hub_Controls_Panel.Visibility = Visibility.Visible;
				SCA_Hub_Settings_Panel.Visibility = Visibility.Collapsed;
				NavBar_SettingsButton_Image.Source = new BitmapImage(new Uri(@"pack://application:,,,/ScreenControlApp.Desktop;component/Images/cog.png"));
			}
			IsSettingsPageDisplayed = !IsSettingsPageDisplayed;
		}

		private void Settings_Panel_SaveSettings_Button_Click(object sender, RoutedEventArgs e) {
			Settings.ServerAddress = Settings_Panel_ServerAddress_TextBox.Text;
			Settings.HubName = Settings_Panel_HubName_TextBox.Text;
			Settings.PreferredScreenId = Settings_Panel_ScreenSelector_ComboBox.SelectedIndex;

			try {
				var loader = new ApplicationSettingsLoader();
				loader.Save(Settings, "settings.json");
				Settings_Panel_SavedSettings_Label.Visibility = Visibility.Visible;
			}
			catch (Exception ex) {
				System.Windows.MessageBox.Show(ex.Message);
				CancellationTokenSource.Cancel();
			}
		}


		#region MainWindow_LoadingOverlay
		private void Window_Loaded(object sender, RoutedEventArgs e) {
			_ = Task.Run(async () => {
				this.Dispatcher.Invoke(() => {
					SetUpDisplayInformation();
					Loading_ProgressBar.Value += 25;
				});

				LoadSettings();
				this.Dispatcher.Invoke(() => Loading_ProgressBar.Value += 25);

				this.Dispatcher.Invoke(() => {
					PopulateSettings();
					Loading_ProgressBar.Value += 25;
				});

				await RequestHostId();
				this.Dispatcher.Invoke(() => Loading_ProgressBar.Value += 25);

				if (CancellationTokenSource.IsCancellationRequested)
					System.Windows.Application.Current.Shutdown(); //TODO, does this even work?
				else
					this.Dispatcher.Invoke(() => {
						SCA_Loading_Panel.Visibility = Visibility.Collapsed;
						SCA_Bottom_Ribbon_Panel.Visibility = Visibility.Visible;
						SCA_MainContent_Panel.Visibility = Visibility.Visible;
					});
			});
		}
		private void SetUpDisplayInformation() {
			Settings_Panel_ScreenSelector_ComboBox.ItemsSource = DisplayInformation.GetMonitorsInfo();
		}

		private void LoadSettings() {
			try {
				var loader = new ApplicationSettingsLoader();
				Settings = loader.Load("settings.json");
			}
			catch (Exception ex) {
				System.Windows.MessageBox.Show(ex.Message);
			}
		}

		private void PopulateSettings() {
			Settings_Panel_ServerAddress_TextBox.Text = Settings.ServerAddress;
			Settings_Panel_HubName_TextBox.Text = Settings.HubName;
			if (Screen.AllScreens.Length - 1 < Settings.PreferredScreenId) {
				var screenSelections = (List<string>)Settings_Panel_ScreenSelector_ComboBox.ItemsSource;
				int index = screenSelections.FindIndex(screenSelection => screenSelection.Contains("Primary"));
				Settings_Panel_ScreenSelector_ComboBox.SelectedIndex = Settings.PreferredScreenId = index == -1 ? 0 : index;
			}
			else
				Settings_Panel_ScreenSelector_ComboBox.SelectedIndex = Settings.PreferredScreenId;
		}

		private async Task RequestHostId() {
			using HttpClient client = new();
			try {
				HttpResponseMessage response = await client.GetAsync(Settings.ServerAddress + "api/hostIds");
				response.EnsureSuccessStatusCode();
				string generatedHostId = await response.Content.ReadAsStringAsync();
				this.Dispatcher.Invoke(() => SharingSide_HubId_TextBox.Text = generatedHostId);
			}
			catch (HttpRequestException e) {
				System.Windows.MessageBox.Show("Error while connecting to the server: " + e.ToString());
				CancellationTokenSource.Cancel();
			}
		}
		#endregion


	}
}