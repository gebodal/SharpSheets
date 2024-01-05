using System;

namespace GeboPdf.Utilities {

	public static class GeboData {

		public static string GetProducerString() {
			Version geboPdfVersion = typeof(GeboData).Assembly.GetName().Version ?? new Version(0, 0, 0, 0); // TODO Throw error here if no version found?
			return $"GeboPDF {geboPdfVersion.Major}.{geboPdfVersion.Minor}.{geboPdfVersion.Build}.{geboPdfVersion.Revision}";
		}

		public static string MakeKeywordsString(string[] keywords) {
			return string.Join("; ", keywords);
		}

	}

}
