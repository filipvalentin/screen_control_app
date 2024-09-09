using System.IO;

namespace ScreenControlApp.Desktop.ScreenSharing.Util {
	public interface IFrameProvider : IDisposable {
		void CaptureFrame(MemoryStream memoryStream);
	}
}
