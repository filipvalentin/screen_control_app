using System.IO;

namespace ScreenControlApp.Desktop.ScreenSharing.FrameProviders {
	public interface IFrameProvider : IDisposable {
		void CaptureFrame(MemoryStream memoryStream);
	}
}
