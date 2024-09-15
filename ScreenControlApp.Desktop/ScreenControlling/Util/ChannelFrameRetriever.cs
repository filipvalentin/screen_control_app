using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenControlApp.Desktop.ScreenControlling.Util {
	public class ChannelFrameRetriever(HubConnection connection, string PeerConnectionId, CancellationToken cancellationToken) {
		private readonly HubConnection Connection = connection;
		private readonly CancellationToken CancellationToken = cancellationToken;
		private readonly string PeerConnectionId = PeerConnectionId;

		public async Task<MemoryStream> RetrieveAsync() {
			var channel = await Connection.StreamAsChannelAsync<byte[]>("DownloadFrame", PeerConnectionId, CancellationToken);
			var memoryStream = new MemoryStream(); //TODO maybe a pool?
			while (await channel.WaitToReadAsync()) {
				await foreach (var chunk in channel.ReadAllAsync()) {
					memoryStream.Write(chunk, 0, chunk.Length);
				}
			}
			return memoryStream;
		}
	}
}
