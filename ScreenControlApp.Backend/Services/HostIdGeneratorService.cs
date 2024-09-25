using System.Text;

namespace ScreenControlApp.Backend.Services {
	public static class HostIdGeneratorService {
		private static readonly Dictionary<string, DateTime> UsedHostIds = [];

		public static string Get() {
			int tries = 0;
			string id;
			while (true) {
				id = Generate(3, 3 + tries);
				if (UsedHostIds.ContainsKey(id))
					tries++;
				else break;
			}
			UsedHostIds[id] = DateTime.UtcNow;
			return id;
		}

		// Generates IDs composed of digits: 123-456-789
		private static string Generate(int sections, int sectionLength) {
			StringBuilder sb = new();
			Random random = new();
			for (int i = 0; i < sections; i++) {
				for (int j = 0; j < sectionLength; j++) {
					sb.Append(random.Next(10));
				}
				sb.Append('-');
			}
			sb.Remove(sb.Length - 1, 1);
			return sb.ToString();
		}
		public static void Return(string hostId) {
			UsedHostIds.Remove(hostId);
		}

		public static void UpdateAlive(string hostId) {
			if (UsedHostIds.ContainsKey(hostId)) {
				UsedHostIds[hostId] = DateTime.UtcNow;
			}
		}

		public static void CleanupExpiredHostIds() {
			var now = DateTime.UtcNow;
			var expiredKeys = new List<string>();

			foreach (var kvp in UsedHostIds) {
				if ((now - kvp.Value).TotalMinutes > 5) {
					expiredKeys.Add(kvp.Key);
				}
			}

			foreach (var key in expiredKeys) {
				UsedHostIds.Remove(key);
			}
		}


	}
}
