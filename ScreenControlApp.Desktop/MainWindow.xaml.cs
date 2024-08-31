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

		private void StackPanel_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) {
			//SWidth.Content
			SWidth.Content = e.GetPosition(null).X;
			SHeight.Content = e.GetPosition(null).Y;
		}
	}
}