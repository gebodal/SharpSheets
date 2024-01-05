using SharpSheets.Markup.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using SharpSheets.Markup.Patterns;
using SharpSheets.Exceptions;
using System.Threading;
using SharpEditor.DataManagers;

namespace SharpEditor.Registries {

	public class MarkupFileRegistry : IMarkupRegistry {

		private readonly MarkupPatternParser markupPatternParser;

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
		private readonly PatternNameDictionary<MarkupPattern> patterns;
		private readonly Dictionary<string, List<string>> origins; // Key: filepath, Values: pattern full names

		private string FileSearchPattern { get { return "*" + SharpEditorFileInfo.MarkupExtension; } }

		private static readonly int numberOfTries = 3;
		private static readonly int retryDelay = 50;

		public MarkupFileRegistry(string registryPath, MarkupPatternParser markupPatternParser) {

			if (!Directory.Exists(registryPath)) {
				throw new ArgumentException("Registry path must be a directory.");
			}

			this.registryPath = registryPath;
			this.markupPatternParser = markupPatternParser;

			patterns = new PatternNameDictionary<MarkupPattern>();
			origins = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); // This is apparently the best comparer for filepaths

			fileWatcher = new FileSystemWatcher {
				Path = registryPath,

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

			/*
			Console.WriteLine("Names");
			foreach (string i in patterns.Names(new HashSet<string>())) {
				Console.WriteLine(i);
			}
			Console.WriteLine("\nMinimal names");
			foreach(string i in patterns.MinimalNames(new HashSet<string>())) {
				Console.WriteLine(i);
			}
			Console.WriteLine("\nValid names");
			foreach (string i in patterns.ValidNames(new HashSet<string>())) {
				Console.WriteLine(i);
			}
			Console.WriteLine();
			*/
		}
		private void ClearAll() {
			foreach(string filepath in origins.Keys) {
				SharpEditorRegistries.ClearRegistryErrors(filepath);
			}

			patterns.Clear();
			origins.Clear();
		}

		private void LoadFileIntoRegistry(string filepath) {
			if (File.Exists(filepath)) {
				string fullPath = System.IO.Path.GetFullPath(filepath);
				origins[fullPath] = new List<string>();
				FilePath originPath = new FilePath(filepath);
				DirectoryPath sourcePath = new DirectoryPath(System.IO.Path.GetFullPath(SharpEditorPathInfo.GetDirectoryPathOrFallback(filepath)));

				try {
					string? fileText = null;
					for (int i = 1; i <= numberOfTries; i++) {
						try {
							fileText = SharpEditorRegistries.FileReader.ReadAllText(filepath);
							break; // When done we can break loop
						}
						catch (IOException) when (i < numberOfTries) {
							Thread.Sleep(retryDelay);
						}
					}
					if(fileText is null) {
						throw new IOException($"Could not read file {filepath}");
					}
					
					List<MarkupPattern> filePatterns = markupPatternParser.ParseContent(originPath, sourcePath, fileText, out CompilationResult result);
					foreach (MarkupPattern pattern in filePatterns) { // TODO Better error handling here?
						if (pattern != null && pattern.ValidPattern) {
							foreach (List<string> filelist in origins.Values) { filelist.Remove(pattern.Name); }
							origins[fullPath].Add(pattern.FullName);

							//Console.WriteLine($"Loaded {pattern.FullName} into registry");
							patterns[PatternName.Parse(pattern.FullName)] = pattern;
						}
					}

					SharpEditorRegistries.LogRegistryErrors(filepath, result?.errors ?? Enumerable.Empty<SharpParsingException>());
				}
				catch (Exception e) {
					SharpEditorRegistries.LogRegistryErrors(filepath, e.Yield());
				}
			}
		}

		private void RemoveFileFromRegistry(string filepath) {
			string fullPath = System.IO.Path.GetFullPath(filepath);
			if (origins.TryGetValue(fullPath, out List<string>? fullNames)) {
				foreach(string fullName in fullNames) {
					patterns.Remove(PatternName.Parse(fullName));
				}
			}
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
			// There is probably a better way of doing this?
		}

		public delegate void UpdateNameListHandler();
		public event UpdateNameListHandler? RegistryChanged;

		#region IMarkupRegistry

		public IEnumerable<MarkupPattern> GetPatterns() {
			return patterns.Values;
		}

		public MarkupPattern? GetPattern(PatternName name) {
			//return patterns.GetValueOrDefault(name, null);
			if (patterns.TryGetValue(name, out MarkupPattern? pattern)) {
				return pattern;
			}
			else {
				return null;
			}
		}

		public IEnumerable<string> GetValidNames(HashSet<string> reservedNames) {
			return patterns.ValidNames(reservedNames);
		}

		public IEnumerable<string> GetMinimalNames(HashSet<string> reservedNames) {
			return patterns.MinimalNames(reservedNames);
		}

		#endregion
	}

}
