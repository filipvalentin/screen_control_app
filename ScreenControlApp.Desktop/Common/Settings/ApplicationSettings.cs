namespace ScreenControlApp.Desktop.Common.Settings {
	public record ApplicationSettings {
		public string ServerAddress { get; set; } = null!;
		public string HubName { get; set; } = null!;
		public int PreferredScreenId { get; set; }
	}
}
