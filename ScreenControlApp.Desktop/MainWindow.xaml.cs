using System.Windows;
using ScreenControlApp.Desktop.ScreenControlling;
using ScreenControlApp.Desktop.ScreenSharing;
using System.Windows.Media.Imaging;
using ScreenControlApp.Desktop.Common.Settings;
using ScreenControlApp.Desktop.Common;
using System.Net.Http;
using System.Text;
using System.Windows.Forms;
using ScreenControlApp.Desktop.Common.Displays;
using System.Media;
using System.Net.Http.Json;
using Microsoft.Win32;

namespace ScreenControlApp.Desktop {

	public partial class MainWindow : Window {
		private ApplicationSettings Settings { get; set; } = null!;
		private bool IsSettingsPageDisplayed = false;
		private CancellationTokenSource CancellationTokenSource { get; set; } = new();
		private List<DisplayInfo> Displays { get; set; }

		private readonly HttpClient httpClient;
		private string HostId { get; set; } = null!;
		private readonly System.Windows.Threading.DispatcherTimer KeepAliveHostIdTimer = new();
		private readonly TaskCompletionSource<string> ReceivedHostIdTaskCompletionSource = new();

		public MainWindow() {
			httpClient = new HttpClient();

			this.Closed += OnWindowClosed;

			InitializeComponent();

			SharingSide_Passcode_TextBox.Text = GeneratePasskey();
			Displays = DisplayInfoRetriever.GetMonitorsInfo();//TODO: detect monitor number change? https://codetips.nl/detectmonitor.html
			SystemEvents.DisplaySettingsChanging += OnMonitorConnected;
		}

		private void OnMonitorConnected(object? sender, EventArgs e) {
			Displays = DisplayInfoRetriever.GetMonitorsInfo();
			this.Dispatcher.Invoke(() => SetUpDisplayInformation());
		}

		private static string GeneratePasskey() {
			StringBuilder s = new();
			Random r = new();
			for (int i = 0; i < 6; i++) {
				s.Append(r.Next(9));
			}
			return s.ToString();
		}

		private async void OnWindowClosed(object? sender, EventArgs e) {
			KeepAliveHostIdTimer.Stop();

			await ReturnHostId();

			httpClient.Dispose();
		}

		private async Task ReturnHostId() {
			try {
				await httpClient.DeleteAsync($"{Settings.ServerAddress}api/hostIds/{SharingSide_HubId_TextBox.Text}");
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

		#region Settings_Panel
		private void Settings_Panel_SaveSettings_Button_Click(object sender, RoutedEventArgs e) {
			Settings.ServerAddress = Settings_Panel_ServerAddress_TextBox.Text;
			Settings.HubName = Settings_Panel_HubName_TextBox.Text;
			Settings.PreferredScreenId = Settings_Panel_ScreenSelector_ComboBox.SelectedIndex;

			try {
				var loader = new ApplicationSettingsLoader();
				loader.Save(Settings, "client-settings.json");
				Settings_Panel_SavedSettings_Label.Visibility = Visibility.Visible;
			}
			catch (Exception ex) {
				System.Windows.MessageBox.Show(ex.Message);
				CancellationTokenSource.Cancel();
			}
		}

		private void Settings_Panel_ViewSelectedScreen_Button_Click(object sender, RoutedEventArgs e) {
			var window = new ViewSelectedScreenWindow((DisplayInfo)Settings_Panel_ScreenSelector_ComboBox.SelectedItem);
			window.Show();
			window.Closed += Settings_Panel_ViewSelectedScreen_DisplayTimer_Tick;

			this.Dispatcher.Invoke(() => Settings_Panel_ViewSelectedScreen_Button.IsEnabled = false);
		}
		private void Settings_Panel_ViewSelectedScreen_DisplayTimer_Tick(object? sender, EventArgs e) {
			this.Dispatcher.Invoke(() => Settings_Panel_ViewSelectedScreen_Button.IsEnabled = true);
		}

		#endregion

		#region HostId_IsInUse
		private void StartKeepAliveHostIdTimer() {
			//await ReceivedHostIdTaskCompletionSource.Task;
			KeepAliveHostIdTimer.Interval = TimeSpan.FromMinutes(5);
			KeepAliveHostIdTimer.Tick += KeepAliveHostIdTimerTick;
			KeepAliveHostIdTimer.Start();
		}
		private async void KeepAliveHostIdTimerTick(object? sender, EventArgs e) {
			try {
				HttpResponseMessage response = await httpClient.PostAsJsonAsync(Settings.ServerAddress + "api/hostIds/keepAlive", new { });
				response.EnsureSuccessStatusCode();
			}
			catch (HttpRequestException ex) {
				System.Windows.MessageBox.Show("Error while contacting the server: " + ex.ToString());
				CancellationTokenSource.Cancel();
			}
		}
		#endregion

		#region MainWindow_LoadingProcedure
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


				var hostId = await RequestHostId();
				if (hostId == null) {
					this.Close();
					return;
				}
				HostId = hostId;
				//= await ReceivedHostIdTaskCompletionSource.Task;
				//_ = Task.Factory.StartNew(StartKeepAliveHostIdTimer, TaskCreationOptions.LongRunning);
				StartKeepAliveHostIdTimer();

				this.Dispatcher.Invoke(() => {
					SharingSide_HubId_TextBox.Text = HostId;
					Loading_ProgressBar.Value += 25;
				});

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
			Settings_Panel_ScreenSelector_ComboBox.ItemsSource = Displays;
		}

		private void LoadSettings() {
			try {
				var loader = new ApplicationSettingsLoader();
				Settings = loader.Load("client-settings.json");
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
		private async Task<string?> RequestHostId() {
			try {
				HttpResponseMessage response = await httpClient.GetAsync(Settings.ServerAddress + "api/hostIds");
				response.EnsureSuccessStatusCode();
				string generatedHostId = await response.Content.ReadAsStringAsync();
				//ReceivedHostIdTaskCompletionSource.SetResult(generatedHostId);
				return generatedHostId;
			}
			catch (HttpRequestException e) {
				System.Windows.MessageBox.Show("Error while connecting to the server: " + e.ToString());
				CancellationTokenSource.Cancel();
				return null;
			}
		}
		#endregion
	}
}