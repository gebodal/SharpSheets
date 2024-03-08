using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeboPdf.Fonts.TrueType {

	internal class ResourceFileReading {

		private static string GetResourceName(string filename) {
			return typeof(ResourceFileReading).Assembly.GetManifestResourceNames().First(r => r.EndsWith(filename));
		}

		public static string[][] ReadResourceFile(string filename) {
			List<string[]> resource = new List<string[]>();

			using (Stream fileStream = typeof(ResourceFileReading).Assembly.GetManifestResourceStream(GetResourceName(filename))!) {
				using (StreamReader reader = new StreamReader(fileStream)) {
					while (reader.Peek() >= 0) {
						string? line = reader.ReadLine()?.Trim();

						if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) {
							continue;
						}

						string[] parts = line.Split(';');
						resource.Add(parts);
					}
				}
			}

			return resource.ToArray();
		}

	}

}
