using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditor.DataManagers {

	public static class SharpEditorData {

		public static string GetEditorName() {
			return "SharpEditor";
		}

		public static string GetCreatorName() {
			return "SharpSheets";
		}

		public static string GetVersionString() {
			Version sharpSheetsVersion = typeof(SharpEditorData).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
			return $"{sharpSheetsVersion.Major}.{sharpSheetsVersion.Minor}.{sharpSheetsVersion.Build}.{sharpSheetsVersion.Revision}";
		}

		public static string GetDisplayVersionString() {
			Version sharpSheetsVersion = typeof(SharpEditorData).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
			StringBuilder sb = new StringBuilder();
			sb.Append($"{sharpSheetsVersion.Major}.{sharpSheetsVersion.Minor}");
			if(sharpSheetsVersion.Build > 0 || sharpSheetsVersion.Revision > 0) {
				sb.Append($".{sharpSheetsVersion.Build}");
			}
			if (sharpSheetsVersion.Revision > 0) {
				sb.Append($".{sharpSheetsVersion.Revision}");
			}
			return sb.ToString();
		}

	}

}
