using Microsoft.AspNetCore.SignalR.Client;
using System.IO;
using System.Threading.Channels;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace ScreenControlApp.Desktop.ScreenSharing.FrameSenders {
	public class BlockFrameSender(HubConnection connection) : IFrameSender {
		HubConnection Connection { get; set; } = connection;

		public async Task SendFrame(MemoryStream memoryStream) {

			const int chunkSize = 8192;
			byte[] buffer = new byte[chunkSize];
			int bytesRead;

			await Connection.SendAsync("DirectUploadFrame", Math.Ceiling((decimal)memoryStream.Length / buffer.Length));

			while ((bytesRead = await memoryStream.ReadAsync(buffer)) > 0) {
				// Create a smaller array only if the last chunk is smaller than the buffer
				if (bytesRead != chunkSize) {
					var temp = buffer;
					buffer = new byte[bytesRead];
					Array.Copy(temp, 0, buffer, 0, bytesRead);
				}
				await Connection.SendAsync("UploadFrameChunk", buffer);
			}
		}
	}
}