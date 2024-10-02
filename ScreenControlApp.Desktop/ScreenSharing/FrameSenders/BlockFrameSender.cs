using Microsoft.AspNetCore.SignalR.Client;
using System.IO;

namespace ScreenControlApp.Desktop.ScreenSharing.FrameSenders {
	public class BlockFrameSender(HubConnection hubConnection, string peerConnectionId) : IFrameSender {
		private HubConnection HubConnection { get; set; } = hubConnection;
		private readonly string peerConnectionId = peerConnectionId; //TODO, when onnection reset, add method to update

		public async Task SendFrame(MemoryStream memoryStream) {

			const int chunkSize = 8192;
			byte[] buffer = new byte[chunkSize];
			int bytesRead;

			await HubConnection.SendAsync("DirectUploadFrame", peerConnectionId, (int)Math.Ceiling((decimal)memoryStream.Length / buffer.Length));

			while ((bytesRead = await memoryStream.ReadAsync(buffer)) > 0) {
				// Create a smaller array only if the last chunk is smaller than the buffer
				if (bytesRead != chunkSize) {
					var temp = buffer;
					buffer = new byte[bytesRead];
					Array.Copy(temp, 0, buffer, 0, bytesRead);
				}
				await HubConnection.SendAsync("DirectUploadFrameChunk", peerConnectionId, buffer);
			}
		}
	}
}