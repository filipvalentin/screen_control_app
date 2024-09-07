using System.IO;
using System.Text;
using System.Text.Json;

namespace ScreenControlApp.Desktop.Common.Settings {
	class ApplicationSettingsLoader {
		private readonly JsonSerializerOptions Options = new() {
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = true
		};
		public ApplicationSettings Load(string path) {
			var json = File.ReadAllText(path, Encoding.UTF8);
			var obj = JsonSerializer.Deserialize<ApplicationSettings>(json, Options);
			return obj!;
		}
		public void Save(ApplicationSettings settings, string path) {

			var json = JsonSerializer.Serialize(settings, Options);
			File.WriteAllText(path, json);
		}
	}
}
