using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditor.DataManagers {

	public static class SharpEditorPathInfo {

		#region Template Directory

		public static string GetDefaultApplicationTemplateDirectory() {
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SharpEditorData.GetEditorName());
		}

		public static bool IsTemplateDirectorySet {
			get {
				return !string.IsNullOrWhiteSpace(SharpEditor.Properties.Settings.Default.TemplateDirectory);
			}
		}

		public static string TemplateDirectory {
			get {
				string templateDirectory = SharpEditor.Properties.Settings.Default.TemplateDirectory;
				return string.IsNullOrWhiteSpace(templateDirectory) ? GetDefaultApplicationTemplateDirectory() : templateDirectory;
			}
			set {
				string oldTemplateDirectory = SharpEditor.Properties.Settings.Default.TemplateDirectory ?? "";
				string newTemplateDirectory = string.IsNullOrWhiteSpace(value) ? "" : Path.GetFullPath(value);

				if(!string.Equals(newTemplateDirectory, oldTemplateDirectory, StringComparison.OrdinalIgnoreCase)) { // Best comparison for paths
					SharpEditor.Properties.Settings.Default.TemplateDirectory = newTemplateDirectory;
					SharpEditor.Properties.Settings.Default.Save();

					lastTemplateDirectory = null;

					TemplateDirectoryChanged?.Invoke();
				}
			}
		}

		public delegate void TemplateDirectoryChange();
		public static event TemplateDirectoryChange? TemplateDirectoryChanged;

		#endregion

		#region Last Used Locations

		public static string? LastFileDirectory {
			get {
				string lastFileDirectory = SharpEditor.Properties.Settings.Default.LastFileDirectory;
				return string.IsNullOrWhiteSpace(lastFileDirectory) ? null : lastFileDirectory;
			}
			set {
				SharpEditor.Properties.Settings.Default.LastFileDirectory = string.IsNullOrWhiteSpace(value) ? "" : value;
				SharpEditor.Properties.Settings.Default.Save();
			}
		}

		private static string? lastTemplateDirectory = null;
		public static string? LastTemplateDirectory {
			get {
				if (string.IsNullOrWhiteSpace(lastTemplateDirectory)) {
					lastTemplateDirectory = TemplateDirectory;
				}
				return lastTemplateDirectory;
			}
			set {
				if (value != null && Path.GetFullPath(value).StartsWith(TemplateDirectory)) {
					lastTemplateDirectory = value;
				}
				else {
					lastTemplateDirectory = null;
				}
				//lastTemplateDirectory = string.IsNullOrWhiteSpace(value) ? null : value;
			}
		}

		#endregion

		#region Accessing Directories

		/// <summary>
		/// Returns the directory information for <paramref name="path"/> or throws an exception if such information cannot be found or the directory does not exist.
		/// </summary>
		/// <param name="path">The base path for which the directory information is to be found.</param>
		/// <returns>Returns the directory information for <paramref name="path"/>.</returns>
		/// <exception cref="DirectoryNotFoundException">Thrown if the directory information cannot be found or the directory does not exist.</exception>
		public static string GetExistingDirectoryPath(string path) {
			string dirPath = Path.GetDirectoryName(path) ?? throw new DirectoryNotFoundException($"Could not find directory for {path ?? "NULL."}");
			if(!Directory.Exists(dirPath)) { throw new DirectoryNotFoundException($"The directory {dirPath} does not exist."); }
			return dirPath;
		}

		public static string GetDirectoryPathOrFallback(string? path) {
			return Path.GetDirectoryName(path) ?? Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		}

		#endregion

	}

}
