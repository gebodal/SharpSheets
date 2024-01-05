using System;
using System.Collections.Generic;
using System.IO;

namespace SharpSheets.Utilities {
	public static class FileUtils {

		public static string IndexedFilename(string stub, string extension) {
			if (extension.StartsWith(".")) { extension = extension.Substring(1); }
			int i = 0;
			string filename = $"{stub}.{extension}";
			if (!File.Exists(filename)) {
				return filename;
			}
			do {
				i++;
				filename = $"{stub}{i}.{extension}";
			} while (File.Exists(filename));
			return filename;
		}

		/// <summary></summary>
		/// <exception cref="IOException"></exception>
		/// <exception cref="UnauthorizedAccessException"></exception>
		public static IEnumerable<string> GetAllFiles(string path) {

			if (!Directory.Exists(path)) {
				throw new DirectoryNotFoundException($"No such directory: {path}");
			}

			Queue<string> queue = new Queue<string>();
			queue.Enqueue(path);

			while (queue.Count > 0) {
				#pragma warning disable GJT0001 // Unhandled thrown exception from statement
				path = queue.Dequeue();
				#pragma warning restore GJT0001 // Unhandled thrown exception from statement

				foreach (string subDir in Directory.GetDirectories(path)) {
					queue.Enqueue(subDir);
				}

				string[] files = Directory.GetFiles(path);

				if (files != null) {
					for (int i = 0; i < files.Length; i++) {
						yield return files[i];
					}
				}
			}
		}

		// https://stackoverflow.com/a/32113484/11002708
		/// <summary></summary>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="UriFormatException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public static string GetRelativePath(string fromPath, string toPath) {
			if (string.IsNullOrEmpty(fromPath)) {
				throw new ArgumentNullException(nameof(fromPath));
			}

			if (string.IsNullOrEmpty(toPath)) {
				throw new ArgumentNullException(nameof(toPath));
			}

			Uri fromUri = new Uri(AppendDirectorySeparatorChar(fromPath));
			Uri toUri = new Uri(AppendDirectorySeparatorChar(toPath));

			if (fromUri.Scheme != toUri.Scheme) {
				return toPath;
			}

			Uri relativeUri = fromUri.MakeRelativeUri(toUri);
			string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

			if (string.Equals(toUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)) {
				relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
			}

			return relativePath;
		}

		private static string AppendDirectorySeparatorChar(string path) {
			// Append a slash only if the path is a directory and does not have a slash.
			if (!Path.HasExtension(path) && !path.EndsWith(Path.DirectorySeparatorChar.ToString())) {
				return path + Path.DirectorySeparatorChar;
			}
			else {
				return path;
			}
		}

	}
}
