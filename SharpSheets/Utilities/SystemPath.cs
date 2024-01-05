using System;
using System.IO;

namespace SharpSheets.Utilities {

	public class SystemPath {

		public bool Exists { get { return IsFile || IsDirectory; } }
		public virtual bool IsFile { get { return File.Exists(Path); } }
		public virtual bool IsDirectory { get { return Directory.Exists(Path); } }
		public string Path { get; private set; }
		public string Extension { get { return System.IO.Path.GetExtension(Path); } }

		public SystemPath(string path) {
			if (string.IsNullOrWhiteSpace(path)) {
				Path = Directory.GetCurrentDirectory();
			}
			else {
				Path = System.IO.Path.GetFullPath(path);
			}
		}

		public SystemPath(params string[] paths) : this(System.IO.Path.Combine(paths)) { }

		public static SystemPath operator +(SystemPath a, string b) {
			return new SystemPath(a.Path, b);
		}

		public SystemPath Append(string other) {
			return new SystemPath(Path, other);
		}

		public bool HasExtension(string extension) {
			return Path.EndsWith(extension);
		}

		public DirectoryPath? GetDirectory() {
			string? dirPath = System.IO.Path.GetDirectoryName(Path);
			if (string.IsNullOrEmpty(dirPath)) {
				return null;
			}
			else {
				return new DirectoryPath(dirPath);
			}
		}

		public override string ToString() {
			return Path;
		}

		public override bool Equals(object? obj) {
			if(obj is SystemPath sysPath) {
				return StringComparer.OrdinalIgnoreCase.Equals(Path, sysPath.Path); // This is apparently the best comparer for filepaths
			}
			else {
				return false;
			}
		}

		public override int GetHashCode() {
			return Path.GetHashCode();
		}
	}

	public class FilePath : SystemPath {
		public override bool IsDirectory { get { return false; } }
		public FilePath(string path) : base(path) { }
		public FilePath(params string[] paths) : base(paths) { }

		public string FileName { get { return System.IO.Path.GetFileName(Path); } }
		public string FileNameWithoutExtension { get { return System.IO.Path.GetFileNameWithoutExtension(Path); } }
	}
	public class DirectoryPath : SystemPath {
		public override bool IsFile { get { return false; } }
		public DirectoryPath(string path) : base(path) { }
		public DirectoryPath(params string[] paths) : base(paths) { }

		public static DirectoryPath GetCurrentDirectory() {
			return new DirectoryPath(Directory.GetCurrentDirectory());
		}
	}

}
