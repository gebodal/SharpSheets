using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpEditor.Utilities {

	public class NavigationService<T> : INotifyPropertyChanged where T : Control {

		private readonly List<T> history;

		private int index;

		public T? Current {
			get {
				if (history.Count == 0) { return null; }
				else { return history[index]; }
			}
		}

		public bool CanGoBack { get { return history.Count > 0 && index > 0; } }
		public bool CanGoForward { get { return history.Count > 0 && index < history.Count - 1; } }

		public event EventHandler? NavigationFinished;

		public NavigationService() {
			this.history = new List<T>();
			this.index = -1;
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public void Navigate(T content) {
			if (history.Count == 0) {
				history.Add(content);
				index = 0;
			}
			else {

				if (index < history.Count - 1) {
					history.RemoveRange(index + 1, history.Count - index - 1);
				}

				history.Add(content);
				index = history.Count - 1;
			}

			OnPropertyChanged(nameof(CanGoBack));
			OnPropertyChanged(nameof(CanGoForward));
			NavigationFinished?.Invoke(this, new EventArgs());
		}

		public bool GoForward() {
			if (history.Count == 0) {
				return false;
			}
			else if (index < history.Count - 1) {
				index++;
				OnPropertyChanged(nameof(CanGoBack));
				OnPropertyChanged(nameof(CanGoForward));
				NavigationFinished?.Invoke(this, new EventArgs());
				return true;
			}
			else {
				return false;
			}
		}

		public bool GoBack() {
			if(history.Count == 0) {
				return false;
			}
			else if(index > 0) {
				index--;
				OnPropertyChanged(nameof(CanGoBack));
				OnPropertyChanged(nameof(CanGoForward));
				NavigationFinished?.Invoke(this, new EventArgs());
				return true;
			}
			else {
				return false;
			}
		}

	}

}
