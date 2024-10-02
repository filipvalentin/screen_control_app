using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.IO;
using System.Threading.Channels;

namespace ScreenControlApp.Desktop.ScreenSharing.FrameSenders {
	public class ChannelFrameSender(HubConnection connection) : IFrameSender {
		HubConnection Connection { get; set; } = connection;

		public async Task SendFrame(MemoryStream memoryStream) {

			var channel = Channel.CreateUnbounded<byte[]>();
			await Connection.SendAsync("UploadFrame", channel.Reader);

			const int chunkSize = 8192;
			byte[] buffer = new byte[chunkSize];
			int bytesRead;

			while ((bytesRead = memoryStream.Read(buffer, 0, chunkSize)) > 0) {
				var chunk = new byte[bytesRead];  // Always create a new array for each chunk
				Array.Copy(buffer, chunk, bytesRead);
				await channel.Writer.WriteAsync(chunk);
			}

			channel.Writer.Complete();
			await channel.Reader.Completion;
		}
	}

}