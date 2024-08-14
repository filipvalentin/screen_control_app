using Microsoft.AspNetCore.SignalR;

namespace ScreenControlApp.Backend.Hubs {
	
	public class ScreenControlHub : Hub {
		public async Task SendPacket(string user, string message) {
			await Clients.User(user).SendAsync("ReceivePacket", user, message);
		}
	}

}
