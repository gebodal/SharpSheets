using Avalonia.Media;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharpEditor.DataManagers {

	public static class SharpConfigManager {

		public static string ConfigDir => GetCurrentConfigDir();

		private static readonly JsonSerializerOptions jsonSerializeoptions = new JsonSerializerOptions() {
			IncludeFields = false,
			WriteIndented = true,
			Converters = {
				new AvaloniaColorJsonConverter(),
				new FontStyleJsonConverter(),
				new FontWeightJsonConverter()
			}
		};

		static SharpConfigManager() {
			string configDirPath = GetCurrentConfigDir();
			if (!Directory.Exists(configDirPath)) {
				Console.WriteLine($"Create config directory: {configDirPath}");
				Directory.CreateDirectory(configDirPath);
			}
		}

		private static string GetCurrentConfigDir() {
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string configDirBasename = SharpEditorData.GetEditorName();
			return Path.Combine(appData, configDirBasename);
		}

		private static string GetCurrentConfigName(ConfigName configName, string? suffix = null) {
			return configName.BaseName + SharpEditorData.GetVersionString() + (suffix is not null ? suffix : "") + configName.Extension;
		}

		private static string GetConfigPath(string filename) {
			return Path.Join(GetCurrentConfigDir(), filename);
		}

		public static string GetCurrentConfigPath(ConfigName configName) {
			return GetConfigPath(GetCurrentConfigName(configName));
		}

		public static string GetSuffixedCurrentConfigPath(ConfigName configName, string suffix) {
			return GetConfigPath(GetCurrentConfigName(configName, suffix));
		}

		private static string? GetOldConfigPath(ConfigName configName) {
			string configDir = ConfigDir;
			string baseName = configName.BaseName;
			string extension = configName.Extension;

			IEnumerable<(string path, Version version)> GetVersions() {
				foreach (string path in Directory.GetFiles(configDir, $"{baseName}*{extension}", SearchOption.TopDirectoryOnly)) {
					string filename = Path.GetFileName(path);
					string versionStr = filename[baseName.Length..^extension.Length];
					if (Version.TryParse(versionStr, out Version? parsed)) {
						yield return (path, parsed);
					}
				}
			}

			(string path, Version version)[] existing = GetVersions().ToArray();

			if (existing.Length > 0) {
				return existing.MaxBy(pv => pv.version).path;
			}
			else {
				return null;
			}
		}

		public static void Save<T>(T value, ConfigName configName) {
			string configPath = GetCurrentConfigPath(configName);
			try {
				string jsonText = JsonSerializer.Serialize(value, jsonSerializeoptions);
				File.WriteAllText(configPath, jsonText, System.Text.Encoding.UTF8);
				//Console.WriteLine($"Save {configName.BaseName} config to: {configPath}");
				return;
			}
			catch (IOException) {
				Console.WriteLine($"Could not save settings to: {configPath}");
				return;
			}
			catch (SystemException) {
				Console.WriteLine($"Could not save settings to: {configPath}");
				return;
			}
		}

		public static T? Load<T>(ConfigName configName, out bool latest) where T : class {

			// Does the config file exist?
			// If yes, load it
			// If not, check for an earlier version, load that if it exists
			// Otherwise, return nothing

			string configPath = GetCurrentConfigPath(configName);

			if (File.Exists(configPath)) {
				Console.WriteLine($"Load existing {configName.BaseName} config.");
				T? loaded = LoadData<T>(configPath);
				if (loaded is null) {
					Console.WriteLine($"Could not load existing {configName.BaseName} config.");
				}
				latest = true;
				return loaded;
			}
			else if (GetOldConfigPath(configName) is string oldConfigPath) {
				Console.WriteLine($"Load old {configName.BaseName} config.");
				T? loaded = LoadData<T>(oldConfigPath);
				if (loaded is null) {
					Console.WriteLine($"Could not load old {configName.BaseName} config.");
				}
				latest = false;
				return loaded;
			}
			else {
				Console.WriteLine($"No {configName.BaseName} config found.");
				latest = false;
				return null;
			}

		}

		private static T? LoadData<T>(string path) where T : class {
			//Console.WriteLine($"Load {path}");
			try {
				string jsonText = File.ReadAllText(path, System.Text.Encoding.UTF8);
				return JsonSerializer.Deserialize<T>(jsonText, jsonSerializeoptions);
			}
			catch (IOException) {
				return null;
			}
			catch (SystemException) {
				return null;
			}
			catch (JsonException) {
				return null;
			}
		}

		public static bool SaveBackup(ConfigName configName) {
			string configPath = GetCurrentConfigPath(configName);

			if (File.Exists(configPath)) {
				Console.WriteLine($"Make backup of existing {configName.BaseName} config.");

				string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff");
				string backupConfigPath = GetSuffixedCurrentConfigPath(configName, timestamp);

				try {
					File.Copy(configPath, backupConfigPath, true);
				}
				catch (Exception) {
					Console.WriteLine($"Error copying {configName.BaseName} config backup.");
				}

				return File.Exists(backupConfigPath);
			}
			else {
				Console.WriteLine($"No existing {configName.BaseName} config found to backup.");

				return false;
			}
		}

	}

	public class ConfigName {

		public string BaseName { get; }
		public string Extension { get; }

		public ConfigName(string baseName, string extension) {
			BaseName = baseName;
			Extension = extension;
		}

	}

	public class AvaloniaColorJsonConverter : JsonConverter<Color> {
		public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			return Color.Parse(reader.GetString()!);
		}

		public override void Write(Utf8JsonWriter writer, Color color, JsonSerializerOptions options) {
			uint rgb = color.ToUInt32();
			writer.WriteStringValue($"#{rgb.ToString("x8", CultureInfo.InvariantCulture)}");
		}
	}

	public class FontWeightJsonConverter : JsonConverter<FontWeight> {
		public override FontWeight Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			return Enum.Parse<FontWeight>(reader.GetString()!, true);
		}

		public override void Write(Utf8JsonWriter writer, FontWeight fontWeight, JsonSerializerOptions options) {
			writer.WriteStringValue(fontWeight.ToString());
		}
	}

	public class FontStyleJsonConverter : JsonConverter<FontStyle> {
		public override FontStyle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			return Enum.Parse<FontStyle>(reader.GetString()!, true);
		}

		public override void Write(Utf8JsonWriter writer, FontStyle fontStyle, JsonSerializerOptions options) {
			writer.WriteStringValue(fontStyle.ToString());
		}
	}

}
