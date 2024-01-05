using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using SharpEditor.DataManagers;

namespace SharpEditor.Program {

	public static class TemplateImports {

		private static readonly int numberOfTries = 10;
		private static readonly int retryDelay = 100;

		public static bool IsKnownTemplateFileType(string path) {
			return System.IO.Path.GetExtension(path) == ".zip";
		}

		public static void ImportFile(string path) {
			if(System.IO.Path.GetExtension(path) == ".zip") {
				ImportZipArchive(path);
			}
			/*
			else if(System.IO.Path.GetExtension(path) == ".gz") {
				throw new NotImplementedException();
				//ImportGZipArchive(path);
			}
			*/
			else {
				ImportSingleFile(path);
			}
		}

		private static string GetDestination(string relativePath) {
			string templateDir = SharpEditorPathInfo.TemplateDirectory;
			string destination = System.IO.Path.Combine(templateDir, relativePath);
			if (destination.StartsWith(templateDir)) {
				return destination;
			}
			else {
				throw new IOException("Invalid relative path provided for template file destination.");
			}
		}

		private static string GetDestination(ZipArchiveEntry zipEntry) {
			return GetDestination(Path.Combine(zipEntry.FullName.Split('/')));
		}

		private static void ImportSingleFile(string path) {
			string destination = GetDestination(System.IO.Path.GetFileName(path));

			if (File.Exists(destination)) {
				throw new IOException($"Template file {destination} already exists.");
			}

			Console.WriteLine($"Import: {path} to {destination}");
			
			for (int i = 1; i <= numberOfTries; i++) {
				try {
					File.Copy(path, destination, false);
					break; // When done we can break loop
				}
				catch (IOException) when (i < numberOfTries) {
					Thread.Sleep(retryDelay);
				}
			}
		}

		private static void ImportZipArchive(string path) {
			using (FileStream zipToOpen = new FileStream(path, FileMode.Open)) {
				using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read)) {
					
					// First check if any of these files already exist
					foreach(ZipArchiveEntry entry in archive.Entries) {
						string destination = GetDestination(entry);

						if (File.Exists(destination)) {
							throw new IOException($"Template file {destination} already exists.");
						}
					}

					// If there are no filepath clashes, then proceed to copy files
					foreach (ZipArchiveEntry entry in archive.Entries) {
						string destination = GetDestination(entry);

						if (!Directory.Exists(Path.GetDirectoryName(destination))) {
							Console.WriteLine($"Create directory structure up to {Path.GetDirectoryName(destination)}");
							Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? throw new DirectoryNotFoundException($"Could not find directory for entry {entry.FullName}"));
						}

						Console.WriteLine($"Import: archive/{entry.FullName} to {destination}");

						for (int i = 1; i <= numberOfTries; i++) {
							try {
								entry.ExtractToFile(destination, false);
								break; // When done we can break loop
							}
							catch (IOException) when (i < numberOfTries) {
								Thread.Sleep(retryDelay);
							}
						}

					}
				}
			}
		}

	}

}
