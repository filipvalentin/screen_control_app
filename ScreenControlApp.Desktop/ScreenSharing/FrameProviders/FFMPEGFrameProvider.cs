using System.Diagnostics;
using System.IO;

namespace ScreenControlApp.Desktop.ScreenSharing.FrameProviders {
	public class FFMPEGFrameProvider : IFrameProvider {
		private readonly Process ffmpegProcess;
		private readonly Stream inputStream;
		private readonly byte[] framePixels;
		public FFMPEGFrameProvider(string ffmpegPath, Screen sharedScreen, CancellationToken cancellationToken) {
			inputStream = new MemoryStream();

			framePixels = new byte[sharedScreen.Bounds.Width * sharedScreen.Bounds.Height * 3];// + (sharedScreen.Bounds.Width / 2 * sharedScreen.Bounds.Height / 2) * 2

			ffmpegProcess = new Process();
			ffmpegProcess.StartInfo.FileName = ffmpegPath;
			ffmpegProcess.StartInfo.Arguments = "-filter_complex ddagrab=0,hwdownload,format=bgra -tune zerolatency -pix_fmt yuv420p -c:v libx264 -preset veryfast -crf 20 -f matroska -";//-pix_fmt rgb24
			ffmpegProcess.StartInfo.RedirectStandardInput = true;
			ffmpegProcess.StartInfo.RedirectStandardOutput = true;
			ffmpegProcess.StartInfo.UseShellExecute = false;
			ffmpegProcess.Start();
			ThreadPool.QueueUserWorkItem(_ => {
				inputStream.CopyTo(ffmpegProcess.StandardInput.BaseStream);
			});

			cancellationToken.Register(() => {
				SendCloseSignal();
			});
		}

		public void CaptureFrame(MemoryStream memoryStream) {
			var read = 0;
			while (true) {
				var toRead = Math.Min(4096 * 10, framePixels.Length - read);
				if (toRead < 4096) {
					Debug.WriteLine(toRead);
				}
				var justRead = ffmpegProcess.StandardOutput.BaseStream.Read(framePixels, read, toRead);
				if (justRead < 0) break;
				read += justRead;
				if (read == framePixels.Length) {
					read = 0;
					memoryStream.Write(framePixels, 0, framePixels.Length);
				}
			}
		}

		private void SendCloseSignal() {
			if (!ffmpegProcess.HasExited) {
				ffmpegProcess.StandardInput.BaseStream.WriteByte(0x03); // Send Ctrl+C
				ffmpegProcess.StandardInput.BaseStream.Flush();
			}
		}

		public void Dispose() {
			inputStream.Dispose();
			ffmpegProcess.WaitForExit();
			ffmpegProcess.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
