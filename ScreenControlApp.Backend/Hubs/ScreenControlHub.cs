using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;

namespace ScreenControlApp.Backend.Hubs {
	
	public class ScreenControlHub : Hub {
		private readonly Dictionary<string, (string passcode ,string hostId)> announced = [];
		public async Task SendPacket(string user, string message) {
			await Clients.Client(user).SendAsync("ReceivePacket", user, message);
		}
		public void AnnounceShare(string hostId, string passcode) {
			announced.Add(hostId, (passcode, Context.UserIdentifier!));
		}
		public async Task AnnounceControl(string hostId, string passcode) {
			if (!announced.TryGetValue(hostId, out (string passcode, string hostId) value)) {
				await Clients.Caller.SendAsync("FailedConnection", $"host {hostId} is not awaiting any connections");
				return;
			}
			if(value.passcode != passcode) {
				await Clients.Caller.SendAsync("FailedConnection", "wrong passcode");
					return;
			}
			announced.Remove(hostId);

			await Clients.Client(value.hostId).SendAsync("ReceiveConnection", Context.ConnectionId);
			await Clients.Client(Context.ConnectionId).SendAsync("ReceiveConnection", value.hostId);
		}
	}

}
