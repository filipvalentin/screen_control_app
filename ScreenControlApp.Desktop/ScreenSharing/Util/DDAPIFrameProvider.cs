
using HPPH;
using ScreenCapture.NET;
using System.Drawing.Imaging;
using System.IO;

namespace ScreenControlApp.Desktop.ScreenSharing.Util {
	public class DDAPIFrameProvider : IFrameProvider {
		private IScreenCaptureService ScreenCaptureService { get; set; }
		private ICaptureZone FullScreenCaptureZone { get; set; }
		private IScreenCapture ScreenCapture { get; set; }
		private readonly ManualResetEvent ThreadWaitHandle = new(false);
		Thread CaptureScreenThread { get; set; }
		private CancellationToken CancellationToken { get; set; }

		public DDAPIFrameProvider(CancellationToken token) {
			CancellationToken = token;

			ScreenCaptureService = new DX11ScreenCaptureService();

			// Get all available graphics cards
			IEnumerable<GraphicsCard> graphicsCards = ScreenCaptureService.GetGraphicsCards();

			// Get the displays from the graphics card(s) you are interested in
			IEnumerable<Display> displays = ScreenCaptureService.GetDisplays(graphicsCards.First());

			// Create a screen-capture for all screens you want to capture
			ScreenCapture = ScreenCaptureService.GetScreenCapture(displays.First());

			// Register the regions you want to capture on the screen
			// Capture the whole screen
			FullScreenCaptureZone = ScreenCapture.RegisterCaptureZone(0, 0, ScreenCapture.Display.Width, ScreenCapture.Display.Height);

			CaptureScreenThread = new Thread(() => {
				while (!CancellationToken.IsCancellationRequested) {
					int index = WaitHandle.WaitAny([ThreadWaitHandle, CancellationToken.WaitHandle]);
					if (index == 1) break; // Cancellation requested

					// Capture the screen
					// This should be done in a loop on a separate thread as CaptureScreen blocks if the screen is not updated (still image).
					ScreenCapture.CaptureScreen();
				}
			});
			CaptureScreenThread.Start();
		}

		public void CaptureFrame(MemoryStream memoryStream) {
			memoryStream.SetLength(0);
			ThreadWaitHandle.Set();

			//Lock the zone to access the data. Remember to dispose the returned disposable to unlock again.
			using (FullScreenCaptureZone.Lock()) {
				// Get the image captured for the zone
				IImage image = FullScreenCaptureZone.Image;

				var bitmap = HPPH.System.Drawing.ImageExtension.ToBitmap(image);
				bitmap.Save(memoryStream, ImageFormat.Jpeg);
			}
		}

		public void Dispose() {
			ThreadWaitHandle.Set();
			CaptureScreenThread.Join();
			ScreenCaptureService.Dispose();
			ScreenCapture.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
