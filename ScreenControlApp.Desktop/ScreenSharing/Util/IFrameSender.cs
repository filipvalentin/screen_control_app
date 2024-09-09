using System.IO;

namespace ScreenControlApp.Desktop.ScreenSharing.Util {
	public interface IFrameSender {
		public Task SendFrame(MemoryStream memoryStream);
	}
}