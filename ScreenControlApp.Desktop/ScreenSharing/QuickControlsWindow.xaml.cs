using System.Windows;
using System.Windows.Input;

namespace ScreenControlApp.Desktop.ScreenSharing {
	public partial class QuickControlsWindow : Window {
		public QuickControlsWindow() {
			InitializeComponent();
		}

		private void StackPanel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
			if (e.ChangedButton == MouseButton.Left)
				this.DragMove();
		}

		private void StackPanel_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) {

		}

		private void StackPanel_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) {

		}
	}
}
