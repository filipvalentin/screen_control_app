using Microsoft.AspNetCore.SignalR;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Diagnostics;

namespace ScreenControlApp.Backend.Hubs {

	public class ScreenControlHub : Hub {
		private static readonly Dictionary<string, (string passcode, string shareConnectionId, DateTime arriveTime)> announced = []; //add time to tuple -> check and delete announce if time expires
																																	 //private readonly Dictionary<string, string> connectedHosts = []; //maps sharing->controlling
		public async Task SendPacket(string user, string message) {
			await Clients.Client(user).SendAsync("ReceivePacket", user, message);
		}
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
			//connectedHosts.Add(value.hostId, hostId);

			await Clients.Client(Context.ConnectionId).SendAsync("ReceiveConnectionToControl", value.shareConnectionId);
			await Clients.Client(value.shareConnectionId).SendAsync("ReceiveConnectionToShare", Context.ConnectionId);
		}
	}

}
