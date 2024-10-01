using Microsoft.AspNetCore.SignalR.Client;
using System.IO;
using System.Threading.Channels;

namespace ScreenControlApp.Desktop.ScreenSharing.FrameSenders {
	public class ChannelFrameSender(HubConnection connection) : IFrameSender {
		HubConnection Connection { get; set; } = connection;

		public async Task SendFrame(MemoryStream memoryStream) {
			byte[] frameBytes = memoryStream.ToArray();

			var channel = Channel.CreateUnbounded<byte[]>();
			await Connection.SendAsync("UploadFrame", channel.Reader);

			const int chunkSize = 8192;
			int offset = 0;
			while (offset < frameBytes.Length) {
				int count = Math.Min(chunkSize, frameBytes.Length - offset);
				var chunk = new byte[count];//TODO! move allocations
				Array.Copy(frameBytes, offset, chunk, 0, count);
				await channel.Writer.WriteAsync(chunk);
				offset += count;
			}
			channel.Writer.Complete();

			await channel.Reader.Completion;
		}
	}
}