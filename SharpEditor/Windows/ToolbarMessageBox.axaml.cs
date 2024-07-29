using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Timers;

namespace SharpEditor.Windows {

	public partial class ToolbarMessageBox : UserControl {

		private readonly object entriesLock = new object();
		private ObservableCollection<ToolbarMessage> PopupEntries { get; } = new ObservableCollection<ToolbarMessage>();

		private readonly Timer timer;

		public ToolbarMessageBox() {
			InitializeComponent();
			this.DataContext = this;

			MessagePopupListBox.ItemsSource = PopupEntries;

			timer = new Timer {
				Interval = 1000,
				AutoReset = true
			};
			timer.Elapsed += CleanUp;
		}

		private void UpdateMessage() {
			string message = "";
			lock (entriesLock) {
				if (PopupEntries.Count > 0) {
					int highestPriority = int.MinValue;

					for (int i = PopupEntries.Count - 1; i >= 0; i--) {
						if (PopupEntries[i].Priority > highestPriority && !string.IsNullOrWhiteSpace(PopupEntries[i].Message)) {
							highestPriority = PopupEntries[i].Priority;
							message = PopupEntries[i].Message;
						}
					}
				}
			}

			Dispatcher.UIThread.Invoke(() => {
				bool hasMessage = !string.IsNullOrEmpty(message);
				MessageText.Text = message;
				MessagePanel.IsVisible = hasMessage;
				MessagePopup.IsOpen &= hasMessage;
			});
		}

		public void Start() {
			timer.Start();
		}
		public void Stop() {
			timer.Stop();
		}

		public ToolbarMessage LogMessage(string message, TimeSpan duration, int priority) {
			ToolbarMessage newEntry = new ToolbarMessage(message, DateTime.UtcNow, duration, priority);
			lock (entriesLock) {
				PopupEntries.Add(newEntry);
			}
			UpdateMessage();
			return newEntry;
		}

		public ToolbarMessage LogMessage(string message, int seconds, int priority) {
			return LogMessage(message, new TimeSpan(0, 0, seconds), priority);
		}

		public bool RemoveMessage(ToolbarMessage? message) {
			if (message == null) { return false; }

			bool result;
			lock (entriesLock) {
				result = PopupEntries.Remove(message);
			}
			UpdateMessage();
			return result;
		}

		private void CleanUp(object? sender, ElapsedEventArgs e) {
			if (PopupEntries.Count == 0) { return; }
			DateTime now = DateTime.UtcNow;
			lock (entriesLock) {
				while (PopupEntries.Count > 0 && PopupEntries[0].Time < now - PopupEntries[0].Duration) {
					PopupEntries.RemoveAt(0);
				}
			}
			UpdateMessage();
		}

		void OnPointerEnter(object? sender, PointerEventArgs args) {
			//Console.WriteLine("OnPointerEnter");
			if (PopupEntries.Count > 0) {
				MessagePopup.IsOpen = true;
				args.Handled = true;
			}
			else {
				MessagePopup.IsOpen = false;
			}
		}

		private void OnPointerExit(object? sender, PointerEventArgs e) {
			//Console.WriteLine("OnPointerExit");
			MessagePopup.IsOpen = false;
		}

	}

	public class ToolbarMessage {

		public string Message { get; }
		public DateTime Time { get; }
		public TimeSpan Duration { get; }
		public int Priority { get; }

		public ToolbarMessage(string message, DateTime time, TimeSpan duration, int priority) {
			Message = message;
			Time = time;
			Duration = duration;
			Priority = priority;
		}

	}

}