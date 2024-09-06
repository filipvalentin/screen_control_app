using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using ScreenControlApp.Desktop.ScreenControlling;
using ScreenControlApp.Desktop.ScreenSharing;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static ScreenControlApp.Desktop.ScreenSharing.NativeMethods;
using System.Windows.Media.Imaging;
using ScreenControlApp.Desktop.Common.Settings;
using ScreenControlApp.Desktop.Common;

namespace ScreenControlApp.Desktop {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		private ApplicationSettings Settings { get; set; }

		public MainWindow() {
			InitializeComponent();
			//Application.Current.MainWindow.WindowState = WindowState.Maximized;

			shareHost_HostId.Text = "123-456-789";
			shareHost_Passcode.Text = "1234";
		}


		private void ControlHost_Button_Click(object sender, RoutedEventArgs e) {
			Window window = new ScreenControllingWindow(Settings, controlHost_HostId.Text, controlHost_Passcode.Text);
			Visibility = Visibility.Hidden;
			window.Show();
			window.Closing += (sender, args) => {
				Visibility = Visibility.Visible;
			};
		}

		private void ShareHost_Button_Click(object sender, RoutedEventArgs e) {
			Window window = new ScreenSharingWindow(Settings, shareHost_HostId.Text, shareHost_Passcode.Text);
			Visibility = Visibility.Hidden;
			window.Show();
			window.Closing += (sender, args) => {
				Visibility = Visibility.Visible;
			};
		}


		private static bool IsSettingsPageDisplayed = false;
		private void NavBar_SettingsButton_Click(object sender, RoutedEventArgs e) {
			if (!IsSettingsPageDisplayed) {
				SCA_Hub_Controls_Panel.Visibility = Visibility.Collapsed;//TODO:Rename panels to mainpanel
				SCA_Hub_Settings_Panel.Visibility = Visibility.Visible;
				NavBar_SettingsButton_Image.Source = new BitmapImage(new Uri(@"pack://application:,,,/ScreenControlApp.Desktop;component/Images/back.png"));
			}
			else {
				SCA_Hub_Controls_Panel.Visibility = Visibility.Visible;
				SCA_Hub_Settings_Panel.Visibility = Visibility.Collapsed;
				NavBar_SettingsButton_Image.Source = new BitmapImage(new Uri(@"pack://application:,,,/ScreenControlApp.Desktop;component/Images/cog.png"));
			}
			IsSettingsPageDisplayed = !IsSettingsPageDisplayed;
		}

		private void Window_Loaded(object sender, RoutedEventArgs e) {
			SetUpDisplayInformation();
			Loading_ProgressBar.Value += 50;
			LoadSettings();
			Loading_ProgressBar.Value += 25;
			PopulateSettings();
			Loading_ProgressBar.Value += 25;

			SCA_Loading_Panel.Visibility = Visibility.Collapsed;
			SCA_Bottom_Ribbon_Panel.Visibility = Visibility.Visible;
			SCA_MainContent_Panel.Visibility = Visibility.Visible;
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
			Settings_Panel_ScreenSelector_ComboBox.SelectedIndex = Settings.PreferredScreenId;
		}

		private void SetUpDisplayInformation() {
			Settings_Panel_ScreenSelector_ComboBox.ItemsSource = DisplayInformation.GetMonitorsInfo();
		}

		private void Settings_Panel_SaveSettings_Button_Click(object sender, RoutedEventArgs e) {
			Settings.ServerAddress = Settings_Panel_ServerAddress_TextBox.Text;
			Settings.PreferredScreenId = Settings_Panel_ScreenSelector_ComboBox.SelectedIndex;

			try {
				var loader = new ApplicationSettingsLoader();
				loader.Save(Settings, "settings.json");
			}
			catch (Exception ex) {
				System.Windows.MessageBox.Show(ex.Message);
			}
		}
	}
}