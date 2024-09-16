using System.Drawing.Imaging;
using System.IO;

namespace ScreenControlApp.Desktop.ScreenSharing.FrameProviders {
	public class GDIFrameProvider : IFrameProvider {
		private Bitmap Bitmap { get; set; } = null!;
		private Graphics Graphics { get; set; } = null!;
		private Screen Screen { get; set; } = null!;

		public GDIFrameProvider(Screen screenToCapture) {
			Screen = screenToCapture;
			Bitmap = new Bitmap(Screen.Bounds.Width, Screen.Bounds.Height);
			Graphics = Graphics.FromImage(Bitmap);
		}

		public void CaptureFrame(MemoryStream memoryStream) {
			Graphics.CopyFromScreen(Screen.Bounds.X, Screen.Bounds.Y,
									0, 0,
									Bitmap.Size,
									CopyPixelOperation.SourceCopy);
			memoryStream.SetLength(0);
			Bitmap.Save(memoryStream, ImageFormat.Jpeg);

		}

		public void Dispose() {
			Graphics.Dispose();
			GC.SuppressFinalize(this);
		}
	}

}