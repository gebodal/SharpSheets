using SharpEditor.Utilities;
using SharpSheets.Fonts;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditor.DataManagers {

	public static class SharpSheetsStateManager {

		private static string? registeredTemplatePath = null;

		private static readonly FilesWatcher filesWatcher;
		private static readonly IReadOnlyCollection<string> FileFilters = new List<string>() { "*.ttf", "*.otf", "*.ttc", "*.otc" };

		public static void Initialise() { } // Dummy method to force static initialisation

		static SharpSheetsStateManager() {
			filesWatcher = new FilesWatcher {
				IncludeSubdirectories = true,
				Interval = TimeSpan.FromSeconds(0.5)
			};

			filesWatcher.Filters.AddRange(FileFilters);

			filesWatcher.Changed += OnFileChange;
			filesWatcher.Created += OnFileChange;
			filesWatcher.Deleted += OnFileChange;
			filesWatcher.Renamed += OnFileRename;

			SetTemplatePath(SharpEditorPathInfo.TemplateDirectory);

			SharpEditorPathInfo.TemplateDirectoryChanged += OnTemplateDirectoryChanged;
		}

		private static void OnTemplateDirectoryChanged() {
			SetTemplatePath(SharpEditorPathInfo.TemplateDirectory);
		}

		[MemberNotNull(nameof(registeredTemplatePath))]
		private static void SetTemplatePath(string newPath) {
			filesWatcher.EnableRaisingEvents = false;

			if(registeredTemplatePath != null) {
				FontPathRegistry.RemoveFontSource(registeredTemplatePath);
			}

			registeredTemplatePath = Path.GetFullPath(newPath);
			FontPathRegistry.AddFontSource(registeredTemplatePath);
			filesWatcher.Path = registeredTemplatePath;

			filesWatcher.EnableRaisingEvents = true;
		}

		private static void OnFileChange(object? source, FileWatcherEventArgs e) {
			//Console.WriteLine($"Detect file change in {nameof(SharpSheetsStateManager)}.");
			OnTemplateDirectoryChanged();
		}
		private static void OnFileRename(object? source, FileWatcherRenameEventArgs e) {
			//Console.WriteLine($"Detect file rename in {nameof(SharpSheetsStateManager)}.");
			OnTemplateDirectoryChanged();
		}

	}


	/*
	public static class SharpSheetsStateManager {

		private static string? registeredTemplatePath = null;

		private static readonly FileSystemWatcher fileWatcher;
		private static readonly IReadOnlyCollection<string> FileFilters = new List<string>() { "*.ttf", "*.otf", "*.ttc", "*.otc" };

		public static void Initialise() { } // Dummy method to force static initialisation

		static SharpSheetsStateManager() {
			fileWatcher = new FileSystemWatcher {
				// Watch for changes in LastWrite times, and the renaming of files or directories.
				NotifyFilter = NotifyFilters.LastWrite
								 | NotifyFilters.FileName
								 | NotifyFilters.DirectoryName
								 | NotifyFilters.Attributes
								 | NotifyFilters.Size,

				IncludeSubdirectories = true
			};

			fileWatcher.Filters.AddRange(FileFilters);
			
			fileWatcher.Changed += OnFileChange;
			fileWatcher.Created += OnFileChange;
			fileWatcher.Deleted += OnFileChange;
			fileWatcher.Renamed += OnFileChange;

			SetTemplatePath(SharpEditorPathInfo.TemplateDirectory);

			SharpEditorPathInfo.TemplateDirectoryChanged += OnTemplateDirectoryChanged;
		}

		private static void OnTemplateDirectoryChanged() {
			SetTemplatePath(SharpEditorPathInfo.TemplateDirectory);
		}

		[MemberNotNull(nameof(registeredTemplatePath))]
		private static void SetTemplatePath(string newPath) {
			fileWatcher.EnableRaisingEvents = false;

			if(registeredTemplatePath != null) {
				FontPathRegistry.RemoveFontSource(registeredTemplatePath);
			}

			registeredTemplatePath = Path.GetFullPath(newPath);
			FontPathRegistry.AddFontSource(registeredTemplatePath);
			fileWatcher.Path = registeredTemplatePath;

			fileWatcher.EnableRaisingEvents = true;
		}

		private static void OnFileChange(object source, FileSystemEventArgs e) {
			Console.WriteLine($"Detect file change in {nameof(SharpSheetsStateManager)}.");
			OnTemplateDirectoryChanged();
		}

	}
	*/




}
