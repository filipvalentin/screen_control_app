using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows;

namespace ScreenControlApp.Desktop.Common {
	public static class ScreenCapture {
		[System.Runtime.InteropServices.DllImport("gdi32.dll")]
		public static extern bool DeleteObject(IntPtr hObject); //make private, gather in 1 class

		public static BitmapSource GetBitmapSource() {
			var left = Screen.AllScreens.Min(screen => screen.Bounds.X);
			var top = Screen.AllScreens.Min(screen => screen.Bounds.Y);
			var right = Screen.AllScreens.Max(screen => screen.Bounds.X + screen.Bounds.Width);
			var bottom = Screen.AllScreens.Max(screen => screen.Bounds.Y + screen.Bounds.Height);
			var width = right - left;
			var height = bottom - top;

			using var screenBmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
			using var bmpGraphics = System.Drawing.Graphics.FromImage(screenBmp);
			bmpGraphics.CopyFromScreen(left, top, 0, 0, new System.Drawing.Size(width, height));
			IntPtr hBitmap = screenBmp.GetHbitmap();
			try {
				return Imaging.CreateBitmapSourceFromHBitmap(
					hBitmap, IntPtr.Zero, Int32Rect.Empty,
					BitmapSizeOptions.FromEmptyOptions());
			}
			finally {
				DeleteObject(hBitmap);
			}
		}
	}


}
