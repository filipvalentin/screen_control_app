using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Channels;

namespace ScreenControlApp.Backend.Hubs {

	public class ScreenControlHub : Hub {
		private static readonly Dictionary<string, (string passcode, string shareConnectionId, DateTime arriveTime)> announced = []; //add time to tuple -> check and delete announce if time expires
		private static readonly Dictionary<string, ConcurrentQueue<byte[]>> streamBuffer = [];
		//private readonly Dictionary<string, string> connectedHosts = []; //maps sharing->controlling
		//public async Task SendPacket(string user, string message) {
		//	await Clients.Client(user).SendAsync("ReceivePacket", user, message);
		//}
		public void AnnounceShare(string hostUserId, string passcode) {
			announced.Add(hostUserId, (passcode, Context.ConnectionId!, DateTime.Now));
		}
		public async Task AnnounceControl(string hostUserId, string passcode) {
			if (!announced.TryGetValue(hostUserId, out (string passcode, string shareConnectionId, DateTime arriveTime) value)) {
				await Clients.Caller.SendAsync("FailedConnection", $"host {hostUserId} is not awaiting any connections");
				return;
			}
			if (value.passcode != passcode) {
				await Clients.Caller.SendAsync("FailedConnection", "wrong passcode");
				return;
			}
			announced.Remove(hostUserId);
			streamBuffer.Add(value.shareConnectionId, new());
			//connectedHosts.Add(value.hostId, hostId);

			await Clients.Client(Context.ConnectionId).SendAsync("ReceiveConnectionToControl", value.shareConnectionId);
			await Clients.Client(value.shareConnectionId).SendAsync("ReceiveConnectionToShare", Context.ConnectionId);
		}

		public async Task UploadFrame(ChannelReader<byte> stream) {
			var memoryStream = new MemoryStream();
			while (await stream.WaitToReadAsync()) {
				await foreach (var b in stream.ReadAllAsync()) {
					memoryStream.WriteByte(b);
				}
			}
			memoryStream.Position = 0;
			//await Task.Run(() => {
			var arr = memoryStream.ToArray();
			streamBuffer[Context.ConnectionId].Enqueue(arr);
			//});
		}

		public ChannelReader<byte> DownloadFrame(string connectionId, CancellationToken cancellationToken) {
			var channel = Channel.CreateUnbounded<byte>();

			// We don't want to await WriteItemsAsync, otherwise we'd end up waiting 
			// for all the items to be written before returning the channel back to
			// the client.
			_ = WriteItemsAsync(channel.Writer, Clients.Caller, connectionId, cancellationToken);

			return channel.Reader;
		}

		private static async Task WriteItemsAsync(ChannelWriter<byte> writer, IClientProxy caller, string connectionId, CancellationToken cancellationToken) {
			Exception? localException = null;
			try {
				if (!streamBuffer.TryGetValue(connectionId, out var queue)) {
					await caller.SendAsync("Error", "Connection not found", cancellationToken);
					return;
				}
				if (queue.IsEmpty) {
					return;
				}
				var buffer = queue.First();

				foreach (byte b in buffer) { 
					await writer.WriteAsync(b, cancellationToken);
				}
			}
			catch (Exception ex) {
				localException = ex;
			}
			finally {
				writer.Complete(localException);
			}
		}
	}




}
