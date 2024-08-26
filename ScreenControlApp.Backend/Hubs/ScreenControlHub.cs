using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ScreenControlApp.Backend.Hubs {

	public class ScreenControlHub : Hub {
		private static readonly Dictionary<string, (string passcode, string shareConnectionId, DateTime arriveTime)> announced = []; //add time to tuple -> check and delete announce if time expires
		private static readonly Dictionary<string, ConcurrentQueue<byte[]>> streamBuffer = [];

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

		public async Task UploadFrame(ChannelReader<byte[]> stream) {
			try {
				var memoryStream = new MemoryStream();
				while (await stream.WaitToReadAsync()) {
					await foreach (var chunk in stream.ReadAllAsync()) {
						memoryStream.Write(chunk, 0, chunk.Length);
					}
				}
				memoryStream.Position = 0;

				var arr = memoryStream.ToArray();
				if (!streamBuffer.TryGetValue(Context.ConnectionId, out var queue)) {
					queue = new ConcurrentQueue<byte[]>();
					streamBuffer[Context.ConnectionId] = queue;
				}
				queue.Enqueue(arr);
				Console.WriteLine(queue.Count);
			}
			catch (Exception ex) {
				Console.WriteLine(ex);
			}
		}

		public ChannelReader<byte[]> DownloadFrame(string connectionId, CancellationToken cancellationToken) {
			var channel = Channel.CreateUnbounded<byte[]>();

			// We don't want to await WriteItemsAsync, otherwise we'd end up waiting 
			// for all the items to be written before returning the channel back to
			// the client.
			_ = WriteItemsAsync(channel.Writer, Clients.Caller, connectionId, cancellationToken);

			return channel.Reader;
		}
		private static async Task WriteItemsAsync(ChannelWriter<byte[]> writer, IClientProxy caller, string connectionId, CancellationToken cancellationToken) {
			Exception? localException = null;
			try {
				if (string.IsNullOrWhiteSpace(connectionId)) {
					await caller.SendAsync("Error", "No connection id provided", cancellationToken);
					return;
				}
				if (!streamBuffer.TryGetValue(connectionId, out var queue)) {
					await caller.SendAsync("Error", "Connection not found", cancellationToken);
					return;
				}
				if (!queue.TryDequeue(out var buffer)) {
					return;
				}

				// Write the buffer in chunks
				const int chunkSize = 8192; // Adjust the chunk size as needed
				int offset = 0;
				while (offset < buffer.Length) {
					int count = Math.Min(chunkSize, buffer.Length - offset);
					var chunk = new byte[count];
					Array.Copy(buffer, offset, chunk, 0, count);
					await writer.WriteAsync(chunk, cancellationToken);
					offset += count;
				}
			}
			catch (Exception ex) {
				localException = ex;
			}
			finally {
				writer.Complete(localException);
			}
		}

		public async Task SendKeyboardInput(string connectionId, ushort key) {
			//await Clients.Client(Context.ConnectionId).SendAsync("ReceiveKeyboardInput", key);
		}

		public async Task SendMouseDown(string connectionId, int buttonCode) {
			await Clients.Client(connectionId).SendAsync("ReceiveMouseDown", buttonCode);
		}
		public async Task SendMouseUp(string connectionId, int buttonCode) {
			await Clients.Client(connectionId).SendAsync("ReceiveMouseUp", buttonCode);
		}
		public async Task SendMouseMove(string connectionId, double x, double y) {
			await Clients.Client(connectionId).SendAsync("ReceiveMouseMove", x, y);
		}
		public async Task SendMouseScroll(string connectionId, int scrollValue) {
			await Clients.Client(connectionId).SendAsync("ReceiveMouseScroll", scrollValue);
		}


	}




}
