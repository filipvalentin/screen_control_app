using System.Text;
using System.Windows;

namespace ScreenControlApp.Desktop {
	/// <summary>
	/// Interaction logic for ViewSelectedScreenWindow.xaml
	/// </summary>
	public partial class ViewSelectedScreenWindow : Window {
		private int secondsUntilClose = 5;
		private readonly System.Windows.Threading.DispatcherTimer timer = new();
		public ViewSelectedScreenWindow(string screenInfo) {
			InitializeComponent();

			var split = screenInfo.Split("; ");
			StringBuilder sb = new();
			foreach (string s in split) {
				sb.Append(s).AppendLine();
			}
			ScreenInfo_TextBlock.Text = sb.ToString();

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
