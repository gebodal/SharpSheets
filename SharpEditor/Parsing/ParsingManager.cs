using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using SharpSheets.Utilities;
using SharpSheets.Parsing;
using System.IO;
using SharpSheets.Exceptions;
using System.Diagnostics.CodeAnalysis;
using SharpEditor.DataManagers;
using AvaloniaEdit.Utils;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia;
using SharpEditor.Utilities;

namespace SharpEditor {

	public sealed class ResultEntry {
		public CompilationResult results;
		public ITextSource snapshot;
		public object? content;

		public ResultEntry(CompilationResult results, ITextSource snapshot, object? content) {
			this.results = results;
			this.snapshot = snapshot;
			this.content = content;
		}
	}

	public enum ParseState { NONE, SUCCESS, ERROR, TERMINATED }
	public delegate void ParsingEventHandler(ParseState state);

	public sealed class ParsingManager : ITextViewConnect {
		
		private readonly TextArea textArea;
		public TextDocument Document { get; private set; }

		private readonly IVisualLineTransformer colorizingLineTransformer;
		private readonly IBackgroundRenderer backgroundRenderer;
		private readonly IBackgroundRenderer errorRenderer;
		private readonly IBackgroundRenderer sameTokenRenderer;

		private ParsingManager(TextArea textArea) {
			this.textArea = textArea ?? throw new ArgumentNullException(nameof(textArea));
			this.Document = textArea.Document;

			IBrush ownerBrush = Application.Current?.GetResource<IBrush>(SharpEditorThemeManager.EditorOwnerBrush) ?? throw new InvalidOperationException();
			IBrush childBrush = Application.Current?.GetResource<IBrush>(SharpEditorThemeManager.EditorChildBrush) ?? throw new InvalidOperationException();
			IBrush unusedBrush = Application.Current?.GetResource<IBrush>(SharpEditorThemeManager.EditorUnusedBrush) ?? throw new InvalidOperationException();
			IBrush sameTokenBrush = Application.Current?.GetResource<IBrush>(SharpEditorThemeManager.EditorSameTokenBrush) ?? throw new InvalidOperationException();
			IBrush error1Brush = Application.Current?.GetResource<IBrush>(SharpEditorThemeManager.EditorErrorBrush1) ?? throw new InvalidOperationException();
			IBrush error2Brush = Application.Current?.GetResource<IBrush>(SharpEditorThemeManager.EditorErrorBrush2) ?? throw new InvalidOperationException();

			colorizingLineTransformer = new ParsingStateColorizingTransformer(this, textArea, ownerBrush, childBrush, unusedBrush);
			backgroundRenderer = new ParsingStateUnderlineRenderer(this);
			errorRenderer = new ParsingErrorBackgroundRenderer(this, error1Brush, error2Brush);
			sameTokenRenderer = new ParsingStateSameTokenRenderer(this, textArea, sameTokenBrush);

			InitializeParsingManager();
		}

		#region Installation
		public static ParsingManager Install(TextArea textArea) {
			ParsingManager manager = new ParsingManager(textArea);
			manager.textArea.TextView.BackgroundRenderers.Add(manager.backgroundRenderer);
			manager.textArea.TextView.BackgroundRenderers.Add(manager.errorRenderer);
			manager.textArea.TextView.BackgroundRenderers.Add(manager.sameTokenRenderer);
			manager.textArea.TextView.LineTransformers.Add(manager.colorizingLineTransformer);
            AvaloniaEdit.Utils.IServiceContainer? services = manager.textArea.Document.GetService<AvaloniaEdit.Utils.IServiceContainer>();
			if (services != null) {
				services.AddService(typeof(ParsingManager), manager);
			}

			manager.InstallParsing();

			Console.WriteLine("Parsing Manager Installed");

			return manager;
		}

		public static void Uninstall(ParsingManager manager) {
			manager.textArea.TextView.BackgroundRenderers.Remove(manager.sameTokenRenderer);
			manager.textArea.TextView.BackgroundRenderers.Remove(manager.errorRenderer);
			manager.textArea.TextView.BackgroundRenderers.Remove(manager.backgroundRenderer);
			manager.textArea.TextView.LineTransformers.Remove(manager.colorizingLineTransformer);
			AvaloniaEdit.Utils.IServiceContainer? services = manager.textArea.Document.GetService<AvaloniaEdit.Utils.IServiceContainer>();
			if (services != null) {
				services.RemoveService(typeof(ParsingManager));
			}

			manager.UninstallParsing();

			Console.WriteLine("Parsing Manager Uninstalled");
		}
		#endregion

		#region Parsing State
		private IParsingState? parsingState;
		private List<IVisualLineTransformer>? parsingStateLineTransformers;
		public event EventHandler? ParseStateChanged;

		public void SetParsingState(IParsingState? state) {
			if (this.parsingState != null) {
				this.parsingState.StateChanged -= OnParseStateChanged;
				this.parsingState.RedrawSegment -= Redraw;
				if (parsingStateLineTransformers is not null) {
					foreach (IVisualLineTransformer transformer in parsingStateLineTransformers) {
						textArea.TextView.LineTransformers.Remove(transformer);
					}
				}
				parsingStateLineTransformers = null;
			}
			this.parsingState = state;
			if (this.parsingState != null) {
				this.parsingState.StateChanged += OnParseStateChanged;
				this.parsingState.RedrawSegment += Redraw;
				parsingStateLineTransformers = this.parsingState.GetLineTransformers().ToList();
				foreach(IVisualLineTransformer transformer in parsingStateLineTransformers) {
					textArea.TextView.LineTransformers.Add(transformer);
				}
			}
		}

		public IParsingState? GetParsingState() {
			return this.parsingState;
		}

		private void OnParseStateChanged(object? sender, EventArgs e) {
			ParseStateChanged?.Invoke(sender, e);
		}

		public string GetCurrentExtension() {
			return parsingState?.Extension ?? ""; // SharpEditorFileInfo.CharacterSheetExtension;
		}
		public string? GetCurrentFileFilter() {
			return parsingState?.FileFilter;
		}
		public string GetCurrentDefaultFilename() {
			//return parsingState?.DefaultFilename ?? "Untitled";
			return "Untitled";
		}

		public bool ColorOwners { get { return parsingState?.ColorOwners ?? false; } }
		public bool HasGeneratedContent { get { return parsingState?.HasGeneratedContent ?? false; } }
		#endregion

		#region Parsing Manager

		private BackgroundWorker backgroundWorker;
		private DispatcherTimer parseDispatcher;
		private DispatcherTimer loadResultDispatcher;

		private EventHandler changeHandler;
		private EventHandler Run;

		//private DateTime lastParseRun;
		private readonly object textChangedLock = new object();
		private volatile bool textChanged;
		private DateTime textChangedTime;
		public double TextChangedDelay { get; set; } = 0.75;

		private DateTime lastParseTime;

		private Queue<ResultEntry> resultQueue;
		private Queue<SharpDrawingException[]?> drawingErrorQueue;

		/// <summary>
		/// Maximum wait time (in seconds) between error collections.
		/// </summary>
		public TimeSpan MaximumWait { get; set; } = TimeSpan.FromSeconds(10);
		public TimeSpan Interval => parseDispatcher.Interval;

		public event ParsingEventHandler? ParseStarted;
		public event ParsingEventHandler? ParseEnded;
		public event Action? ResultLoaded;

		[MemberNotNull(nameof(resultQueue), nameof(drawingErrorQueue), nameof(changeHandler), nameof(backgroundWorker), nameof(Run), nameof(parseDispatcher), nameof(loadResultDispatcher))]
		private void InitializeParsingManager() {
			resultQueue = new Queue<ResultEntry>();
			drawingErrorQueue = new Queue<SharpDrawingException[]?>();

			//changeHandler = new EventHandler((o, s) => { textChanged = true; });
			changeHandler = new EventHandler((o, s) => {
				lock (textChangedLock) {
					textChanged = true;
					textChangedTime = DateTime.UtcNow;
				}
			});

			backgroundWorker = new BackgroundWorker {
				WorkerSupportsCancellation = false,
				WorkerReportsProgress = false
			};
			
			Run = new EventHandler((o, s) => { if (!backgroundWorker.IsBusy) { backgroundWorker.RunWorkerAsync(); } });

			parseDispatcher = new DispatcherTimer {
				Interval = TimeSpan.FromSeconds(0.5)
			};
			parseDispatcher.Tick += Run;

			loadResultDispatcher = new DispatcherTimer {
				Interval = TimeSpan.FromSeconds(0.5)
			};
			loadResultDispatcher.Tick += CheckQueue;

			lastParseTime = DateTime.MinValue;
		}

		private void InstallParsing() {
			Document.TextChanged += changeHandler;
			backgroundWorker.DoWork += ParseDocument;
		}
		private void UninstallParsing() {
			Stop();
			Document.TextChanged -= changeHandler;
			backgroundWorker.DoWork -= ParseDocument; // Necessary?

			if (parsingStateLineTransformers != null) {
				foreach (IVisualLineTransformer transformer in parsingStateLineTransformers) {
					textArea.TextView.LineTransformers.Remove(transformer);
				}
			}
		}

		private void CheckQueue(object? sender, EventArgs args) {
			bool updatedState = false;

			//Console.WriteLine($"Check queue ({resultQueue.Count} in queue).");
			ResultEntry? nextResult = null;
			while (resultQueue.Count > 0) {
				nextResult = resultQueue.Dequeue();
			}

			if (nextResult != null) {
				parsingState?.LoadResultEntry(nextResult);
				updatedState = true;
			}
			
			SharpDrawingException[]? newErrors = null;
			bool foundNewErrors = false;
			while (drawingErrorQueue.Count > 0) {
				newErrors = drawingErrorQueue.Dequeue();
				foundNewErrors = true;
			}

			if (foundNewErrors) {
				parsingState?.LoadDrawingErrors(newErrors);
				updatedState = true;
			}

			if (updatedState) {
				ResultLoaded?.Invoke();
			}
		}

		public void Start() {
			parseDispatcher.Start();
			loadResultDispatcher.Start();
			Console.WriteLine("Parsing Manager Started");
		}
		public void Stop() {
			parseDispatcher.Stop();
			loadResultDispatcher.Stop();
			Console.WriteLine("Parsing Manager Stopped");
		}

		public void Reset() {
			resultQueue.Clear();
			drawingErrorQueue.Clear();
			parsingState?.Reset(); // TODO This good here?
			Console.WriteLine("Parsing Manager Reset");
		}

		/// <summary>
		/// This method triggers the manager to check if the document, or any of its dependencies
		/// have changed, and then runs the parser if required.
		/// </summary>
		public void OptionalTrigger() {
			if (!backgroundWorker.IsBusy) {
				backgroundWorker.RunWorkerAsync();
			}
		}

		void ParseDocument(object? sender, DoWorkEventArgs args) {
			IParser? parser = parsingState?.Parser;
			bool textAreaIsVisible = Dispatcher.UIThread.Invoke(() => { return textArea.IsVisible; });

			if (parser != null && textAreaIsVisible && (textChanged || (parsingState?.Dependencies?.Any(f => File.GetLastWriteTimeUtc(f.Path) > lastParseTime) ?? false))) {
				double textChangeTimeDelta;
				lock (textChangedLock) {
					textChangeTimeDelta = (DateTime.UtcNow - textChangedTime).TotalSeconds;
				}
				if(textChangeTimeDelta < TextChangedDelay) {
					return;
				}
				lock (textChangedLock) {
					textChanged = false;

					lastParseTime = DateTime.UtcNow;
				}

				Console.WriteLine($"Parse {Document.FileName ?? "Untitled"}");

				ParseStarted?.Invoke(ParseState.NONE);
				ParseState success;

				try {
					string fileName = Document.FileName ?? (parsingState != null ? parsingState.DefaultFilename + parsingState.Extension : "unknown");
					FilePath filePath = new FilePath(System.IO.Path.GetFullPath(fileName));
					string source = SharpEditorPathInfo.GetDirectoryPathOrFallback(fileName);
					DirectoryPath sourcePath = new DirectoryPath(!string.IsNullOrEmpty(source) ? System.IO.Path.GetFullPath(source) : ""); // TODO Can we make this null if we don't have a value? Is that a more sensible fallback? (Downstream tasks will know about the uncertainty)

					ITextSource snapshot = Document.CreateSnapshot();

					object? content = parser.Parse(filePath, sourcePath, snapshot.Text, out CompilationResult compilationResult);

					Console.WriteLine("Successfully parsed document.");
					resultQueue.Enqueue(new ResultEntry(compilationResult, snapshot, content));
					//Console.WriteLine($"New result enqueued ({resultQueue.Count} in queue).");

					// Ignore warnings at this point for the purposes of determining error state
					// TODO Would it be better to use nullability of "content" here?
					success = (compilationResult?.errors?.Where(e => e is not SharpParsingWarningException)?.Count() ?? 0) > 0 ? ParseState.ERROR : ParseState.SUCCESS;
				}
				catch (Exception e) {
					// TODO Log error somehow
					Console.WriteLine("Encountered unexpected error in parse: " + e.Message);
					Console.WriteLine(e.StackTrace);
					success = ParseState.ERROR;
				}
				//lastParseRun = DateTime.Now;

				ParseEnded?.Invoke(success);
			}
		}
		#endregion

		#region Drawing Errors
		public void LoadDrawingErrors(SharpDrawingException[] exceptions) {
			drawingErrorQueue.Enqueue(exceptions);
		}

		public void ResetDrawingErrors() {
			drawingErrorQueue.Enqueue(null);
		}
		#endregion

		#region Redraw
		/// <summary>
		/// Redraws the specified text segment.
		/// </summary>
		private void Redraw(ISegment segment, DispatcherPriority redrawPriority) {
			foreach (TextView view in textViews) {
				view.Redraw(segment); // redrawPriority
			}
			RedrawRequested?.Invoke(this, EventArgs.Empty);
		}

		public event EventHandler? RedrawRequested;
		#endregion

		#region ITextViewConnect
		readonly List<TextView> textViews = new List<TextView>();

		void ITextViewConnect.AddToTextView(TextView textView) {
			if (textView != null && !textViews.Contains(textView)) {
				Debug.Assert(textView.Document == Document);
				textViews.Add(textView);
			}
		}

		void ITextViewConnect.RemoveFromTextView(TextView textView) {
			if (textView != null) {
				Debug.Assert(textView.Document == Document);
				textViews.Remove(textView);
			}
		}
		#endregion
	}

}
