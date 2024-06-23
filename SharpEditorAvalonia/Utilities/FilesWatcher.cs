using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditorAvalonia.Utilities {

	public enum FileWatcherEventType {
		Created,
		Changed,
		Deleted,
		Renamed
	}

	public class FileWatcherEventArgs : EventArgs {
		public FileWatcherEventType Type { get; }
		public IReadOnlyList<string> Files { get; }
		public FileWatcherEventArgs(FileWatcherEventType type, IEnumerable<string> files) {
			Type = type;
			Files = files.ToList();
		}
		public FileWatcherEventArgs(FileWatcherEventType type, params string[] files) : this(type, (IEnumerable<string>)files) { }
	}

	public class FileWatcherRenameEventArgs : FileWatcherEventArgs {
		public IReadOnlyList<(string oldName, string newName)> Renamed { get; }
		public FileWatcherRenameEventArgs(IEnumerable<(string oldName, string newName)> files) : base(FileWatcherEventType.Renamed, files.Select(f => f.newName)) {
			Renamed = files.ToList();
		}
		public FileWatcherRenameEventArgs(params (string oldName, string newName)[] files) : this((IEnumerable<(string, string)>)files) { }
	}

	public class FilesWatcher {

		private readonly FileSystemWatcher fileWatcher;

		public bool EnableRaisingEvents {
			get => fileWatcher.EnableRaisingEvents;
			set => fileWatcher.EnableRaisingEvents = value;
		}

		public string Path {
			get => fileWatcher.Path;
			set => fileWatcher.Path = value;
		}

		public bool IncludeSubdirectories {
			get => fileWatcher.IncludeSubdirectories;
			set => fileWatcher.IncludeSubdirectories = value;
		}

		public System.Collections.ObjectModel.Collection<string> Filters => fileWatcher.Filters;

		private TimeSpan interval = TimeSpan.Zero;
		public TimeSpan Interval {
			get => interval;
			set {
				interval = value;
				createdThrottle.Interval = interval;
				changedThrottle.Interval = interval;
				deletedThrottle.Interval = interval;
				renamedThrottle.Interval = interval;
			}
		}

		private readonly ThrottleTimer<string> createdThrottle;
		public EventHandler<FileWatcherEventArgs>? Created;
		private readonly ThrottleTimer<string> changedThrottle;
		public EventHandler<FileWatcherEventArgs>? Changed;
		private readonly ThrottleTimer<string> deletedThrottle;
		public EventHandler<FileWatcherEventArgs>? Deleted;
		private readonly ThrottleTimer<(string oldName, string newName)> renamedThrottle;
		public EventHandler<FileWatcherRenameEventArgs>? Renamed;

		public FilesWatcher() {
			fileWatcher = new FileSystemWatcher {
				// Watch for changes in LastWrite times, and the renaming of files or directories.
				NotifyFilter = NotifyFilters.LastWrite
								 | NotifyFilters.FileName
								 | NotifyFilters.DirectoryName
								 | NotifyFilters.Attributes
								 | NotifyFilters.Size
			};

			fileWatcher.Created += OnFileCreated;
			fileWatcher.Changed += OnFileChanged;
			fileWatcher.Deleted += OnFileDeleted;
			fileWatcher.Renamed += OnRenamed;

			createdThrottle = new ThrottleTimer<string>() { Interval = interval };
			changedThrottle = new ThrottleTimer<string>() { Interval = interval };
			deletedThrottle = new ThrottleTimer<string>() { Interval = interval };
			renamedThrottle = new ThrottleTimer<(string, string)>() { Interval = interval };

			createdThrottle.ThrottledTick += (s, e) => { Created?.Invoke(s, new FileWatcherEventArgs(FileWatcherEventType.Created, e)); };
			changedThrottle.ThrottledTick += (s, e) => { Changed?.Invoke(s, new FileWatcherEventArgs(FileWatcherEventType.Changed, e)); };
			deletedThrottle.ThrottledTick += (s, e) => { Deleted?.Invoke(s, new FileWatcherEventArgs(FileWatcherEventType.Deleted, e)); };
			renamedThrottle.ThrottledTick += (s, e) => { Renamed?.Invoke(s, new FileWatcherRenameEventArgs(e)); };
		}

		private void OnFileCreated(object source, FileSystemEventArgs e) {
			if (interval.Ticks > 0) {
				createdThrottle.Enqueue(e.FullPath);
			}
			else {
				Created?.Invoke(source, new FileWatcherEventArgs(FileWatcherEventType.Created, e.FullPath));
			}
		}

		private void OnFileChanged(object source, FileSystemEventArgs e) {
			if (interval.Ticks > 0) {
				changedThrottle.Enqueue(e.FullPath);
			}
			else {
				Changed?.Invoke(source, new FileWatcherEventArgs(FileWatcherEventType.Changed, e.FullPath));
			}
		}

		private void OnFileDeleted(object source, FileSystemEventArgs e) {
			if (interval.Ticks > 0) {
				deletedThrottle.Enqueue(e.FullPath);
			}
			else {
				Deleted?.Invoke(source, new FileWatcherEventArgs(FileWatcherEventType.Deleted, e.FullPath));
			}
		}

		private void OnRenamed(object source, RenamedEventArgs e) {
			if (interval.Ticks > 0) {
				renamedThrottle.Enqueue((e.OldFullPath, e.FullPath));
			}
			else {
				Renamed?.Invoke(source, new FileWatcherRenameEventArgs((e.OldFullPath, e.FullPath)));
			}
		}

		// https://stackoverflow.com/a/28017571/11002708
		private class ThrottleTimer<T> : DispatcherTimer {
			private bool flag = false;
			private readonly Queue<T> queue = new Queue<T>();

			public EventHandler<IReadOnlyList<T>>? ThrottledTick;

			public ThrottleTimer() {
				Tick += (s, e) => {
					List<T> values;
					lock (queue) {
						flag = false;
						Stop();
						values = new List<T>(queue);
						queue.Clear();
					}
					ThrottledTick?.Invoke(s, values);
				};
			}

			public void Enqueue(T value) {
				if (flag) {
					lock (queue) {
						queue.Enqueue(value);
					}
				}
				else {
					lock (queue) {
						flag = true;
						Start();
						queue.Enqueue(value);
					}
				}
			}

		}

	}

}
