using System.Text;

namespace ScreenControlApp.Backend.Services {
	public static class HostIdGeneratorService {
		private static readonly List<string> UsedHostIds = [];

		public static string Get() {
			int tries = 0;
			string id;
			while (true) {
				id = GenerateHostId(3, 3 + tries);
				if (UsedHostIds.Contains(id))
					tries++;
				else break;
			}
			UsedHostIds.Add(id);
			return id;
		}

		// Generates IDs composed of digits: 123-456-789
		private static string GenerateHostId(int sections, int sectionLength) {
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
	}
}
