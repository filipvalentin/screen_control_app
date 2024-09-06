using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenControlApp.Desktop.Common.Settings {
	public record ApplicationSettings {
		public string ServerAddress { get; set; } = null!;
		public int PreferredScreenId { get; set; }
	}
}
