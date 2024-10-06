using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using SharpSheets.Exceptions;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Cards.CardConfigs {

	public interface ICardSetConfigRegistry {
		//string[] GetAllConfigNames();

		CardSetConfig? GetSetConfig(DirectoryPath source, string path, out List<SharpParsingException> buildErrors);
		//bool IsConfig(string path);
	}

	public static class CardSetConfigRegistry {

		public static FilePath? ResolveConfigPath(DirectoryPath source, string proposedPath, DirectoryPath defaultConfigsPath) {
			if (TryConfigPath(Path.Combine(source.Path, proposedPath), out string? fromSourcePath)) {
				return new FilePath(fromSourcePath);
			}
			else if (defaultConfigsPath != null && TryConfigPath(Path.Combine(defaultConfigsPath.Path, proposedPath), out string? fromConfigsDirPath)) {
				return new FilePath(fromConfigsDirPath);
			}
			else {
				return null;
			}
		}

		private static bool TryConfigPath(string path, [MaybeNullWhen(false)] out string finalPath) {
			if (File.Exists(path)) {
				finalPath = path;
				return true;
			}

			if (!Path.HasExtension(path) && Path.GetDirectoryName(path) is string dir) {
				string[] currentDirMatches = Directory.GetFiles(dir, Path.GetFileNameWithoutExtension(path) + ".*", SearchOption.AllDirectories);
				if (currentDirMatches.Length > 0) {
					finalPath = currentDirMatches[0];
					return true;
				}
			}
			// else // If it has an extension and doesn't exist, no need for else, just fail

			finalPath = null;
			return false;
		}

		#region Simple Registry
		public static ICardSetConfigRegistry SimpleFileRegistry(DirectoryPath defaultConfigsPath, CardSetConfigParser parser, IFileReader fileReader) {
			return new SimpleCardSetConfigFileRegistry(defaultConfigsPath, parser, fileReader);
		}

		private class SimpleCardSetConfigFileRegistry : ICardSetConfigRegistry {

			public readonly DirectoryPath defaultConfigsPath;
			private readonly CardSetConfigParser parser;
			private readonly IFileReader fileReader;
			private readonly Dictionary<string, CardSetConfig> alreadySeen;

			public SimpleCardSetConfigFileRegistry(DirectoryPath defaultConfigsPath, CardSetConfigParser parser, IFileReader fileReader) {
				this.defaultConfigsPath = defaultConfigsPath;
				this.parser = parser;
				this.fileReader = fileReader;
				alreadySeen = new Dictionary<string, CardSetConfig>(StringComparer.InvariantCultureIgnoreCase);
			}

			public CardSetConfig? GetSetConfig(DirectoryPath source, string path, out List<SharpParsingException> buildErrors) {
				FilePath? configPath = ResolveConfigPath(source, path, defaultConfigsPath);
				if (configPath != null && configPath.Exists && configPath.GetDirectory() is DirectoryPath configSource) {
					if (alreadySeen.TryGetValue(configPath.FileNameWithoutExtension, out CardSetConfig? existing)) {
						buildErrors = new List<SharpParsingException>();
						return existing;
					}
					else {
						try {
							string configText = fileReader.ReadAllText(configPath.Path);
							CardSetConfig? config = parser.ParseContent(configPath, configSource, configText, out CompilationResult compilationResult);
							buildErrors = compilationResult.errors.ToList();
							if (config != null) {
								alreadySeen.Add(config.Name, config);
							}
							return config;
						}
						catch (IOException e) {
							buildErrors = new List<SharpParsingException>() { new SharpParsingException(null, e.Message, e) };
							return null;
						}
					}
				}
				else {
					buildErrors = new List<SharpParsingException>() { new SharpParsingException(null, "Could not find configuration file.") };
					return null;
				}
			}

			/*
			public string[] GetAllNames() {
				return values.Keys.ToArray();
			}

			public bool IsConfig(string path) {
				return values.ContainsKey(path);
			}
			*/
		}
		#endregion

		#region Read-Only
		public static ICardSetConfigRegistry ReadOnly(IEnumerable<CardSetConfig> values) {
			return new ReadOnlyCardConfigRegistry(values);
		}

		private class ReadOnlyCardConfigRegistry : ICardSetConfigRegistry {

			private readonly IReadOnlyDictionary<string, CardSetConfig> values;

			public ReadOnlyCardConfigRegistry(IEnumerable<CardSetConfig> values) {
				this.values = values.ToDictionary(p => p.Name, StringComparer.InvariantCultureIgnoreCase);
			}

			public CardSetConfig? GetSetConfig(DirectoryPath source, string name, out List<SharpParsingException> buildErrors) {
				buildErrors = new List<SharpParsingException>();
				return values.GetValueOrFallback(name, null);
			}

			/*
			public string[] GetAllNames() {
				return values.Keys.ToArray();
			}
			
			public bool IsConfig(string path) {
				return values.ContainsKey(path);
			}
			*/
		}
		#endregion
	}
}
