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

namespace ScreenControlApp.Desktop {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		public MainWindow() {
			InitializeComponent();
			//Application.Current.MainWindow.WindowState = WindowState.Maximized;

			shareHost_HostId.Text = "123-456-789";
			shareHost_Passcode.Text = "1234";
		}


		private void ControlHost_Button_Click(object sender, RoutedEventArgs e) {
			Window window = new ScreenControllingWindow(controlHost_HostId.Text, controlHost_Passcode.Text);
			Visibility = Visibility.Hidden;
			window.Show();
			window.Closing += (sender, args) => {
				Visibility = Visibility.Visible;
			};
		}

		private void ShareHost_Button_Click(object sender, RoutedEventArgs e) {
			Window window = new ScreenSharingWindow(shareHost_HostId.Text, shareHost_Passcode.Text);
			Visibility = Visibility.Hidden;
			window.Show();
			window.Closing += (sender, args) => {
				Visibility = Visibility.Visible;
			};
		}

		private void SaveSettings_Button_Click(object sender, RoutedEventArgs e) {

		}

		private static bool IsSettingsPageDisplayed = false;
		private void NavBar_SettingsButton_Click(object sender, RoutedEventArgs e) {
			if (!IsSettingsPageDisplayed) {
				SCA_Hub_Controls_Panel.Visibility = Visibility.Collapsed;
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
	}
}