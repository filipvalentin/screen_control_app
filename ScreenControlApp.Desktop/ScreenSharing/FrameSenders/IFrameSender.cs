using System.IO;

namespace ScreenControlApp.Desktop.ScreenSharing.FrameSenders {
	public interface IFrameSender {
		public Task SendFrame(MemoryStream memoryStream);
	}
}