using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenControlApp.Desktop.Common.Displays {
	public record Resolution(int Width, int Height);
	public record DisplayInfo(Screen Screen, string DeviceName, Resolution RealResolution, Resolution ScaledResolution, bool IsPrimary) {
		public override string ToString() {
			StringBuilder sb = new();
			sb.Append($"Device: {DeviceName}; ");
			sb.Append($"Real Resolution: {RealResolution.Width}x{RealResolution.Height}; ");
			sb.Append($"Scaled Resolution: {ScaledResolution.Width}x{ScaledResolution.Height}");
			if (IsPrimary)
				sb.Append("; Primary");
			return sb.ToString();
		}
	};
}
