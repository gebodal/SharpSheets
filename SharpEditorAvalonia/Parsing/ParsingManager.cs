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
using SharpEditorAvalonia.DataManagers;
using AvaloniaEdit.Utils;
using Avalonia.Threading;

namespace SharpEditorAvalonia {

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

		private ParsingManager(TextArea textArea) {
			this.textArea = textArea ?? throw new ArgumentNullException(nameof(textArea));
			this.Document = textArea.Document;

			colorizingLineTransformer = new ParsingStateColorizingTransformer(this, textArea);
			backgroundRenderer = new ParsingStateBackgroundRenderer(this);
			errorRenderer = new ParsingErrorBackgroundRenderer(this);

			InitializeParsingManager();
		}

		#region Installation
		public static ParsingManager Install(TextArea textArea) {
			ParsingManager manager = new ParsingManager(textArea);
			manager.textArea.TextView.BackgroundRenderers.Add(manager.backgroundRenderer);
			manager.textArea.TextView.BackgroundRenderers.Add(manager.errorRenderer);
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

		#region DocumentColorizingTransformer
		/*
		readonly SolidColorBrush ownerBrush = new SolidColorBrush(Colors.White) { Opacity = 0.15 };
		readonly SolidColorBrush childBrush = new SolidColorBrush(Colors.Orange) { Opacity = 0.15 };
		readonly SolidColorBrush unusedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#808080"));

		private bool ColorOwners { get { return parsingState?.ColorOwners ?? false; } }

		protected override void ColorizeLine(DocumentLine line) {
			if (parsingState == null) { return; }
			int lineStart = line.Offset;
			int lineEnd = lineStart + line.Length;

			HashSet<EditorParseSpan> caretChildren = null, caretOwners = null;
			if (ColorOwners) {
				caretChildren = new HashSet<EditorParseSpan>();
				caretOwners = new HashSet<EditorParseSpan>();
				foreach (EditorParseSpan caretSpan in parsingState.FindSpansContaining(textArea.Caret.Offset)) {
					caretChildren.UnionWith(caretSpan.Children);
					caretOwners.UnionWith(caretSpan.Owners);
				}
			}

			foreach (EditorParseSpan span in parsingState.FindOverlappingSpans(lineStart, line.Length)) {
				Brush foregroundBrush = null;
				Brush backgroundBrush = null;
				FontStyle? fontStyle = span.FontStyle;
				float? fontSizeFactor = span.FontSizeFactor;

				if (span.IsUnused) {
					foregroundBrush = unusedBrush;
					fontStyle = FontStyles.Italic;
					fontSizeFactor = null;
				}
				else if (span.ForegroundColor != null) {
					foregroundBrush = new SolidColorBrush(span.ForegroundColor.Value);
					foregroundBrush.Freeze();
				}

				if (span.BackgroundColor != null) {
					backgroundBrush = new SolidColorBrush(span.BackgroundColor.Value);
				}
				else if (ColorOwners && line.LineNumber != textArea.Caret.Line && !textArea.Selection.IsMultiline) {
					if (caretChildren.Contains(span)) {
						// Caret span owns this span
						backgroundBrush = childBrush;
					}
					else if (caretOwners.Contains(span)) {
						// This span owns caret span
						backgroundBrush = ownerBrush;
					}
				}

				ChangeLinePart(
					Math.Max(span.StartOffset, lineStart),
					Math.Min(span.EndOffset, lineEnd),
					element => {
						if (foregroundBrush != null) {
							element.TextRunProperties.SetForegroundBrush(foregroundBrush);
						}
						if(backgroundBrush != null) {
							element.TextRunProperties.SetBackgroundBrush(backgroundBrush);
						}
						if (fontSizeFactor != null) {
							element.TextRunProperties.SetFontRenderingEmSize(element.TextRunProperties.FontRenderingEmSize * fontSizeFactor.Value);
							element.TextRunProperties.SetFontHintingEmSize(element.TextRunProperties.FontHintingEmSize * fontSizeFactor.Value);
						}
						//element.TextRunProperties.SetTextDecorations(new TextDecorationCollection() { TextDecorations.Strikethrough });
						Typeface tf = element.TextRunProperties.Typeface;
						element.TextRunProperties.SetTypeface(new Typeface(
							tf.FontFamily,
							fontStyle ?? tf.Style,
							span.FontWeight ?? tf.Weight,
							tf.Stretch
						));
					}
				);
			}
		}
		*/
		#endregion

		#region IBackgroundRenderer
		/*
		public KnownLayer Layer {
			get {
				// draw behind selection
				return KnownLayer.Selection;
			}
		}

		static readonly TextMarkerTypes underlineMarkerTypes = TextMarkerTypes.SquigglyUnderline | TextMarkerTypes.NormalUnderline | TextMarkerTypes.DottedUnderline;

		public void Draw(TextView textView, DrawingContext drawingContext) {
			if (textView == null)
				throw new ArgumentNullException("textView");
			if (drawingContext == null)
				throw new ArgumentNullException("drawingContext");
			if (parsingState == null || !textView.VisualLinesValid)
				return;
			ReadOnlyCollection<VisualLine> visualLines = textView.VisualLines;
			if (visualLines.Count == 0)
				return;
			int viewStart = visualLines.First().FirstDocumentLine.Offset;
			int viewEnd = visualLines.Last().LastDocumentLine.EndOffset;
			foreach (EditorParseSpan span in parsingState.FindOverlappingSpans(viewStart, viewEnd - viewStart)) {
				if (span.BackgroundColor != null) {
					BackgroundGeometryBuilder geoBuilder = new BackgroundGeometryBuilder {
						AlignToWholePixels = true,
						CornerRadius = 3
					};
					geoBuilder.AddSegment(textView, span);
					Geometry geometry = geoBuilder.CreateGeometry();
					if (geometry != null) {
						Color color = span.BackgroundColor.Value;
						SolidColorBrush brush = new SolidColorBrush(color);
						brush.Freeze();
						drawingContext.DrawGeometry(brush, null, geometry);
					}
				}
				
				if ((span.MarkerTypes & underlineMarkerTypes) != 0) {
					foreach (Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, span)) {
						Point startPoint = r.BottomLeft;
						Point endPoint = r.BottomRight;

						Brush usedBrush = new SolidColorBrush(span.MarkerColor);
						usedBrush.Freeze();
						if ((span.MarkerTypes & TextMarkerTypes.SquigglyUnderline) != 0) {

							StreamGeometry geometry = new StreamGeometry();
							using (StreamGeometryContext ctx = geometry.Open()) {
								ctx.BeginFigure(startPoint, false, false);
								ctx.PolyLineTo(CreatePoints(startPoint, endPoint, 2.5), true, false);
							}
							geometry.Freeze();

							Pen usedPen = new Pen(usedBrush, 1);
							usedPen.Freeze();
							drawingContext.DrawGeometry(Brushes.Transparent, usedPen, geometry);
						}
						else if ((span.MarkerTypes & TextMarkerTypes.NormalUnderline) != 0) {
							Pen usedPen = new Pen(usedBrush, 1);
							usedPen.Freeze();
							drawingContext.DrawLine(usedPen, startPoint, endPoint);
						}
						else if ((span.MarkerTypes & TextMarkerTypes.DottedUnderline) != 0) {
							Pen usedPen = new Pen(usedBrush, 1) {
								DashStyle = DashStyles.Dot
							};
							usedPen.Freeze();
							drawingContext.DrawLine(usedPen, startPoint, endPoint);
						}
					}
				}
			}
		}

		Point[] CreatePoints(Point start, Point end, double offset) {
			int count = Math.Max((int)((end.X - start.X) / offset) + 1, 4);
			Point[] points = new Point[count];
			for(int i=0; i<count; i++) {
				points[i] = new Point(start.X + i * offset, start.Y - ((i + 1) % 2 == 0 ? offset : 0));
			}
			return points;
		}
		*/
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
