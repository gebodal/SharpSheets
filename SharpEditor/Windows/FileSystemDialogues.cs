using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditor.Windows {

	public static class FileSystemDialogues {

		public static async Task<string[]> OpenFilePicker(IStorageProvider storageProvider, string title, bool allowMultiple, string? startingLocation, string? filter) {

			IStorageFolder? startLocation = null;
			if (startingLocation is not null) {
				startLocation = await storageProvider.TryGetFolderFromPathAsync(startingLocation);
			}

			// Start async operation to open the dialog.
			IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions() {
				Title = title,
				AllowMultiple = allowMultiple, 
				FileTypeFilter = ParseFilter(filter),
				SuggestedStartLocation = startLocation
			});

			return files.Select(f => f.Path.LocalPath).ToArray();
		}

		public static async Task<string[]> OpenFilePicker(this Visual visual, string title, bool allowMultiple, string? startingLocation, string? filter) {
			// Get top level from the provided visual
			TopLevel? topLevel = TopLevel.GetTopLevel(visual);

			if (topLevel is null) {
				return Array.Empty<string>();
			}

			return await OpenFilePicker(topLevel.StorageProvider, title, allowMultiple, startingLocation, filter);
		}

		public static async Task<string?> SaveFilePicker(IStorageProvider storageProvider, string title, string? suggestedName, string? defaultExtension, string? startingLocation, string? filter, bool showOverwritePrompt = true) {

			IStorageFolder? startLocation = null;
			if (startingLocation is not null) {
				startLocation = await storageProvider.TryGetFolderFromPathAsync(startingLocation);
			}

			// Start async operation to open the dialog.
			IStorageFile? file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions() {
				Title = title,
				DefaultExtension = defaultExtension,
				FileTypeChoices = ParseFilter(filter),
				ShowOverwritePrompt = showOverwritePrompt,
				SuggestedFileName = suggestedName,
				SuggestedStartLocation = startLocation
			});

			return file?.Path.LocalPath;
		}

		public static async Task<string?> SaveFilePicker(this Visual visual, string title, string? suggestedName, string? defaultExtension, string? startingLocation, string? filter, bool showOverwritePrompt = true) {
			// Get top level from the provided visual
			TopLevel? topLevel = TopLevel.GetTopLevel(visual);

			if (topLevel is null) {
				return null;
			}

			return await SaveFilePicker(topLevel.StorageProvider, title, suggestedName, defaultExtension, startingLocation, filter, showOverwritePrompt: showOverwritePrompt);
		}

		public static async Task<string[]> OpenDirectoryPicker(IStorageProvider storageProvider, string title, bool allowMultiple, string? startingLocation) {

			IStorageFolder? startLocation = null;
			if (startingLocation is not null) {
				startLocation = await storageProvider.TryGetFolderFromPathAsync(startingLocation);
			}

			// Start async operation to open the dialog.
			IReadOnlyList<IStorageFolder> folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions() {
				Title = title,
				AllowMultiple = allowMultiple,
				SuggestedStartLocation = startLocation
			});

			return folders.Select(f => f.Path.LocalPath).ToArray();
		}

		public static async Task<string[]> OpenDirectoryPicker(this Visual visual, string title, bool allowMultiple, string? startingLocation) {
			// Get top level from the provided visual
			TopLevel? topLevel = TopLevel.GetTopLevel(visual);

			if (topLevel is null) {
				return Array.Empty<string>();
			}

			return await OpenDirectoryPicker(topLevel.StorageProvider, title, allowMultiple, startingLocation);
		}

		private static IReadOnlyList<FilePickerFileType>? ParseFilter(string? filter) {
			if (filter is not null) {
				List<FilePickerFileType> fileTypeFilter = new List<FilePickerFileType>();

				// e.g. "First Type|*.txt;*.tx|Second Type|*.png;*.jpg"
				string[] parts = filter.Split('|');
				for (int i = 0; i < parts.Length; i += 2) {
					string name = parts[i];
					string[] types = parts[i + 1].Split(';');

					fileTypeFilter.Add(new FilePickerFileType(name) {
						Patterns = types
					});
				}

				return fileTypeFilter;
			}
			else {
				return null;
			}
		}

	}

}
