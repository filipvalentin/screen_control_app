using ScreenControlApp.Desktop.Common.Displays;
using System.Text;
using System.Windows;

namespace ScreenControlApp.Desktop {
	/// <summary>
	/// Interaction logic for ViewSelectedScreenWindow.xaml
	/// </summary>
	public partial class ViewSelectedScreenWindow : Window {
		private int secondsUntilClose = 5;
		private readonly System.Windows.Threading.DispatcherTimer timer = new();
		public ViewSelectedScreenWindow(DisplayInfo displayInfo) {
			InitializeComponent();

			SetWindowPosition(displayInfo);
			SetDisplayInfo(displayInfo.ToString());

			SetTimer();
		}

		private void SetWindowPosition(DisplayInfo displayInfo) {
			this.Left = displayInfo.Screen.Bounds.Left + displayInfo.ScaledResolution.Width / 2 - this.Width / 2;
			this.Top = displayInfo.Screen.Bounds.Top + displayInfo.ScaledResolution.Height / 2 - this.Height / 2;
		}
		private void SetDisplayInfo(string screenInfo) {
			var split = screenInfo.Split("; ");
			StringBuilder sb = new();
			foreach (string s in split) {
				sb.Append(s).AppendLine();
			}
			ScreenInfo_TextBlock.Text = sb.ToString();
		}

		private void SetTimer() {
			timer.Tick += AutoCloseWindowTimerTick;
			timer.Interval = TimeSpan.FromSeconds(1);
			timer.Start();
		}

		private void AutoCloseWindowTimerTick(object? sender, EventArgs e) {
			if (secondsUntilClose > 0) {
				string message = $"Click to close this popup or wait {secondsUntilClose} seconds";
				this.Dispatcher.Invoke(() => AutoClose_Label.Content = message);
				secondsUntilClose--;
			}
			else {
				timer.Stop();
				this.Close();
			}
		}

		private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
			this.Close();
		}
	}
}
