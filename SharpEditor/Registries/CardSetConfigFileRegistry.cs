using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using System.Collections;
using SharpSheets.Cards.CardConfigs;
using SharpSheets.Exceptions;
using SharpEditor.DataManagers;

namespace SharpEditor.Registries {

	public class CardSetConfigFileRegistry : ICardSetConfigRegistry, IEnumerable<CardSetConfig> {

		private class CardSetConfigBuild {

			public readonly CardSetConfig cardSetConfig;
			public readonly FilePath originPath;
			public readonly DateTime buildTime;

			public readonly IReadOnlyList<SharpParsingException> errors;

			public IEnumerable<FilePath> Dependencies { get { return originPath.Yield().Concat(cardSetConfig.archivePaths); } }

			public CardSetConfigBuild(CardSetConfig cardSetConfig, FilePath originPath, DateTime buildTime, IReadOnlyList<SharpParsingException> errors) {
				this.cardSetConfig = cardSetConfig;
				this.originPath = originPath;
				this.buildTime = buildTime;
				this.errors = errors;
			}

		}

		private readonly CardSetConfigParser cardSetConfigParser;

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
		private readonly Dictionary<FilePath, CardSetConfigBuild> configOrigins;

		private string FileSearchPattern { get { return "*" + SharpEditorFileInfo.CardConfigExtension; } }

		public CardSetConfigFileRegistry(string registryPath, CardSetConfigParser cardSetConfigParser) {

			if (!Directory.Exists(registryPath)) {
				throw new ArgumentException("Registry path must be a directory.");
			}

			this.registryPath = registryPath;
			this.cardSetConfigParser = cardSetConfigParser;

			configOrigins = new Dictionary<FilePath, CardSetConfigBuild>();

			fileWatcher = new FileSystemWatcher {
				Path = registryPath,

				// Watch for changes in LastWrite times, and the renaming of files or directories.
				NotifyFilter = NotifyFilters.LastWrite
								 | NotifyFilters.FileName
								 | NotifyFilters.DirectoryName,

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
		}
		public void Stop() {
			fileWatcher.EnableRaisingEvents = false;
			ClearAll();
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
		}

		private void LoadAll() {
			foreach (string filepath in Directory.GetFiles(registryPath, FileSearchPattern, SearchOption.AllDirectories)) {
				LoadFileIntoRegistry(filepath);
			}
		}
		private void ClearAll() {
			foreach (FilePath filepath in configOrigins.Keys) {
				SharpEditorRegistries.ClearRegistryErrors(filepath.Path);
			}

			configOrigins.Clear();
		}

		private static readonly int RETRIES = 3;

		private CardSetConfigBuild? LoadFileIntoRegistry(string filepath) {
			if (File.Exists(filepath)) {
				//Console.WriteLine($"Load {filepath} into CardConfig registry.");

				FilePath originPath = new FilePath(filepath);
				// Just use "originPath.GetDirectory() ?? throw new Exception(...)"?
				DirectoryPath sourcePath = new DirectoryPath(System.IO.Path.GetFullPath(SharpEditorPathInfo.GetDirectoryPathOrFallback(filepath)));

				try {
					DateTime buildTime = DateTime.UtcNow;

					// TODO This isn't working
					string? configText = null;
					for (int i = 0; i < RETRIES; i++) {
						try {
							string fileText = SharpEditorRegistries.FileReader.ReadAllText(filepath);
							configText = fileText;
						}
						catch(IOException e) {
							if (i == RETRIES - 1) {
								SharpEditorRegistries.LogRegistryErrors(filepath, e.Yield());
								return null;
							}
							else {
								System.Threading.Thread.Sleep(250);
							}
						}
					}

					if(configText is null) {
						return null;
					}

					CardSetConfig? cardSetConfig = cardSetConfigParser.ParseContent(originPath, sourcePath, configText, out CompilationResult compilationResult);

					// TODO LogRegistryError for CompilationResult errors?
					List<Exception> configExceptions = new List<Exception>(compilationResult.errors);

					if (cardSetConfig != null) {
						CardSetConfigBuild build = new CardSetConfigBuild(cardSetConfig, originPath, buildTime, compilationResult.errors);

						configOrigins[originPath] = build;

						return build;
					}
					else {
						configExceptions.Add(new NullReferenceException("Could not parse card configuration: " + filepath)); // TODO What kind of exception?
					}

					SharpEditorRegistries.LogRegistryErrors(filepath, configExceptions);
					Console.WriteLine(filepath + " registered");
				}
				catch (Exception e) {
					SharpEditorRegistries.LogRegistryErrors(filepath, e.Yield());
					Console.WriteLine(filepath + " not registered");
					Console.WriteLine(e.StackTrace);
				}
			}

			// If something went wrong, return null
			return null;
		}

		private void RemoveFileFromRegistry(FilePath filepath) {
			configOrigins.Remove(filepath);
		}

		private void RemoveFileFromRegistry(string filepath) {
			RemoveFileFromRegistry(new FilePath(filepath));
		}

		private void OnFileChanged(object source, FileSystemEventArgs e) {
			//Console.WriteLine($"Update: {e.FullPath}");
			RemoveFileFromRegistry(e.FullPath);
			LoadFileIntoRegistry(e.FullPath);
		}

		private void OnFileDeleted(object source, FileSystemEventArgs e) {
			//Console.WriteLine($"Delete: {e.FullPath}");
			RemoveFileFromRegistry(e.FullPath);
		}

		private void OnRenamed(object source, RenamedEventArgs e) {
			//Console.WriteLine($"Rename: {e.OldFullPath} to {e.FullPath}");
			RemoveFileFromRegistry(e.OldFullPath);
			LoadFileIntoRegistry(e.FullPath);
		}

		public CardSetConfig? GetSetConfig(DirectoryPath source, string path, out List<SharpParsingException> buildErrors) {

			FilePath? resolvedPath = CardSetConfigRegistry.ResolveConfigPath(source, path, new DirectoryPath(registryPath));

			if(resolvedPath != null) {
				CardSetConfigBuild? build;
				if (configOrigins.TryGetValue(resolvedPath, out CardSetConfigBuild? existingBuild) && existingBuild.Dependencies.All(f => File.GetLastWriteTimeUtc(f.Path) < existingBuild.buildTime)) {
					build = existingBuild;
				}
				else {
					build = LoadFileIntoRegistry(resolvedPath.Path);
				}

				buildErrors = build?.errors.ToList() ?? new List<SharpParsingException>() { new SharpParsingException(null, "Could build configuration file.") };
				return build?.cardSetConfig;
			}
			else {
				buildErrors = new List<SharpParsingException>() { new SharpParsingException(null, "Could not find configuration file.") };
				return null;
			}

		}

		public IEnumerator<CardSetConfig> GetEnumerator() {
			return configOrigins.Values.Select(b => b.cardSetConfig).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

	}
	
}
