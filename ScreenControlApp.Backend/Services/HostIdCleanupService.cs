namespace ScreenControlApp.Backend.Services {
	public class HostIdCleanupService : IHostedService, IDisposable {
		private Timer timer = null!;

		public Task StartAsync(CancellationToken cancellationToken) {
			timer = new Timer(CleanupHostIds, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
			return Task.CompletedTask;
		}

		private void CleanupHostIds(object? state) {
			HostIdGeneratorService.CleanupExpiredHostIds();
		}

		public Task StopAsync(CancellationToken cancellationToken) {
			timer.Change(Timeout.Infinite, 0);
			return Task.CompletedTask;
		}

		public void Dispose() {
			timer.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
