using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SharpEditor.DataManagers;

namespace SharpEditor.Registries {

	public class TemplateRegistry {

		public IReadOnlyDictionary<string, string[]> Registered { get { return origins; } }

		private string registryPath;
		public string Path {
			get {
				return registryPath;
			}
			set {
				registryPath = System.IO.Path.GetFullPath(value);
				fileWatcher.Path = registryPath;
			}
		}

		private readonly FileSystemWatcher fileWatcher;
		private readonly Dictionary<string, string[]> origins; // Key: filepath, Values: template name

		private string FileSearchPattern { get { return "*" + SharpEditorFileInfo.SharpConfigExtension; } }

		private static readonly int numberOfTries = 3;
		private static readonly int retryDelay = 50;

		private static readonly char[] pathSeparators = new char[] {
			  System.IO.Path.DirectorySeparatorChar,
			  System.IO.Path.AltDirectorySeparatorChar
			};

		public TemplateRegistry(string registryPath) {

			this.registryPath = System.IO.Path.GetFullPath(registryPath);

			if (!Directory.Exists(this.registryPath)) {
				throw new ArgumentException("Registry path must be a directory.");
			}

			origins = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase); // This is apparently the best comparer for filepaths

			fileWatcher = new FileSystemWatcher {
				Path = this.registryPath,

				// Watch for changes in LastWrite times, and the renaming of files or directories.
				NotifyFilter = NotifyFilters.LastWrite
								 | NotifyFilters.FileName
								 | NotifyFilters.DirectoryName
								 | NotifyFilters.Attributes,

				Filter = FileSearchPattern,
				IncludeSubdirectories = true
			};

			fileWatcher.Changed += OnFileChanged;
			fileWatcher.Created += OnFileChanged;
			fileWatcher.Deleted += OnFileDeleted;
			fileWatcher.Renamed += OnRenamed;
		}

		public void Start() {
			LoadAll();
			fileWatcher.EnableRaisingEvents = true;
			if (RegistryChanged != null) RegistryChanged.Invoke();
		}
		public void Stop() {
			fileWatcher.EnableRaisingEvents = false;
			ClearAll();
			if (RegistryChanged != null) RegistryChanged.Invoke();
		}

		public void Refresh() {
			if (fileWatcher.EnableRaisingEvents) {
				fileWatcher.EnableRaisingEvents = false;
				ClearAll();
				LoadAll();
				fileWatcher.EnableRaisingEvents = true;
			}
			else {
				ClearAll();
				LoadAll();
			}
			if (RegistryChanged != null) RegistryChanged.Invoke();
		}

		private void LoadAll() {
			foreach (string filepath in Directory.GetFiles(registryPath, FileSearchPattern, SearchOption.AllDirectories)) {
				LoadFileIntoRegistry(filepath);
			}
		}
		private void ClearAll() {
			foreach (string filepath in origins.Keys) {
				SharpEditorRegistries.ClearRegistryErrors(filepath);
			}

			origins.Clear();
		}

		private static readonly Regex nameSplitRegex = new Regex(@"^\#\=\s*(?<name>[a-z0-9]+(\.[a-z0-9]+)*)", RegexOptions.IgnoreCase);
		private static readonly char[] nameSeparators = new char[] { '.' };

		private static readonly Regex nameRemoveRegex = new Regex(@"^\#\=\s*(?<name>[a-z0-9]+(\.[a-z0-9]+)*)\s*", RegexOptions.IgnoreCase);
		private static readonly Regex pathSeparatorReplaceRegex = new Regex(@"\\"); // new Regex(@"(?<!\\)\\\\|(?<!\\)\\");

		private void LoadFileIntoRegistry(string filepath) {
			if (File.Exists(filepath)) {
				string fullPath = System.IO.Path.GetFullPath(filepath);
				string fileName = System.IO.Path.GetFileName(fullPath);

				//Console.WriteLine($"Template: {fileName} at {fullPath}");

				try {
					string? firstLine = null;
					for (int i = 1; i <= numberOfTries; i++) {
						try {
							using (StreamReader reader = SharpEditorRegistries.FileReader.OpenFile(fullPath)) {
								firstLine = reader.ReadLine();
							}
							break; // When done we can break loop
						}
						catch (IOException) when (i < numberOfTries) {
							Thread.Sleep(retryDelay);
						}
					}

					if (!string.IsNullOrWhiteSpace(firstLine) && nameSplitRegex.Match(firstLine) is Match match && match.Success) {
						origins[fullPath] = match.Groups["name"].Value.Split(nameSeparators);
					}
					else {
						string relativeTemplatePath = Regex.Replace(fullPath, @"^" + Regex.Escape(this.registryPath), "").TrimStart(pathSeparators);
						string relativeName = System.IO.Path.ChangeExtension(relativeTemplatePath, null);
						
						//Console.WriteLine("Relative path: " + relativeName);
						
						origins[fullPath] = relativeName.Split(pathSeparators);
					}
				}
				catch (Exception e) {
					SharpEditorRegistries.LogRegistryErrors(filepath, e.Yield());
				}
			}
		}

		public static string GetTemplateContent(string filepath) {
			string fileText = nameRemoveRegex.Replace(File.ReadAllText(filepath, System.Text.Encoding.UTF8), "");
			string sourcePath = pathSeparatorReplaceRegex.Replace(System.IO.Path.GetDirectoryName(filepath) ?? "", "/");
			fileText = fileText.Replace(SharpSheets.Parsing.ValueParsing.SourceKeyword, sourcePath);
			return fileText;
		}

		private void RemoveFileFromRegistry(string filepath) {
			string fullPath = System.IO.Path.GetFullPath(filepath);
			origins.Remove(fullPath);
		}

		private void OnFileChanged(object source, FileSystemEventArgs e) {
			//Console.WriteLine($"Update: {e.FullPath}");
			RemoveFileFromRegistry(e.FullPath);
			LoadFileIntoRegistry(e.FullPath);
			if (RegistryChanged != null) RegistryChanged.Invoke();
		}

		private void OnFileDeleted(object source, FileSystemEventArgs e) {
			//Console.WriteLine($"Delete: {e.FullPath}");
			RemoveFileFromRegistry(e.FullPath);
			if (RegistryChanged != null) RegistryChanged.Invoke();
		}

		private void OnRenamed(object source, RenamedEventArgs e) {
			//Console.WriteLine($"Rename: {e.OldFullPath} to {e.FullPath}");
			RemoveFileFromRegistry(e.OldFullPath);
			LoadFileIntoRegistry(e.FullPath);
			if (RegistryChanged != null) RegistryChanged.Invoke();
		}

		public delegate void UpdateTemplateListHandler();
		public event UpdateTemplateListHandler? RegistryChanged;

	}

}
