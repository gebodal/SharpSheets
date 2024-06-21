using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using SharpSheets.Canvas;
using SharpSheets.Exceptions;

namespace SharpEditorAvalonia.Designer {

	public enum GenerationState { NONE, SUCCESS, ERROR, TERMINATED }
	public delegate void DesignerEventHandler(GenerationState state);

	public class DesignerGenerator {

		// UIElement -> Control

		private class GenerateData {
			public IParsingState? ParsingState { get; }
			public IDocumentContent? DocumentContent { get; }
			public IDrawingMapper? DrawingMapper { get; }
			public GenerateData(IParsingState? parsingState, IDocumentContent? content, IDrawingMapper? mapper) {
				ParsingState = parsingState;
				DocumentContent = content;
				DrawingMapper = mapper;
			}
		}

		private readonly Control designerArea;
		private readonly ParsingManager parsingManager;

		public SharpAvaloniaDrawingDocument? Document { get; private set; }
		public IDocumentContent? DocumentContent { get; private set; }
		private IDocumentContent? LastProcessedDocumentContent { get; set; }
		public IDrawingMapper? DrawingMapper { get; private set; }

		public event DesignerEventHandler? GenerationStarted;
		public event DesignerEventHandler? GenerationEnded;
		public event EventHandler? NewContentAvailable;

		private readonly Task task;
		private readonly EventHandler newStateHandler;
		private readonly BlockingCollection<GenerateData> dataQueue;
		private readonly CancellationTokenSource cancellationSource;
		private CancellationToken cancellationToken;

		private DesignerGenerator(Control designerArea, ParsingManager parsingManager) {
			this.designerArea = designerArea;
			this.parsingManager = parsingManager;

			this.dataQueue = new BlockingCollection<GenerateData>();
			cancellationSource = new CancellationTokenSource();
			cancellationToken = cancellationSource.Token;
			newStateHandler = new EventHandler((o, s) => EnqueueCurrentParseState());

			task = new Task(Run);
		}

		#region Installation
		public static DesignerGenerator Install(Control designerArea, ParsingManager parsingManager) {
			DesignerGenerator generator = new DesignerGenerator(designerArea, parsingManager);

			parsingManager.ParseStateChanged += generator.newStateHandler;
			generator.task.Start();

			Console.WriteLine("Designer Generator Installed");

			return generator;
		}

		public static void Uninstall(DesignerGenerator generator) {
			generator.parsingManager.ParseStateChanged -= generator.newStateHandler;
			generator.cancellationSource.Cancel();
			//generator.task.Wait(); // TODO Is not waiting here a problem?
			Console.WriteLine("Designer Generator Uninstalled");
		}
		#endregion

		private void EnqueueCurrentParseState() {
			IParsingState? parsingState = parsingManager.GetParsingState();
			if (parsingState != null) {
				IDocumentContent? newContent = parsingState?.DrawableContent;
				IDrawingMapper? newDrawingMapper = parsingState?.DrawingMapper;
				GenerateData data = new GenerateData(parsingState, newContent, newDrawingMapper);
				dataQueue.Add(data, cancellationToken);
			}
		}

		public void Redraw() {
			EnqueueCurrentParseState();
		}

		public void Reset() {
			try {
				while (dataQueue.TryTake(out _, 100, cancellationToken)) { } // Is this OK?
			}
			catch (OperationCanceledException) { }
			Document = null;
			DrawingMapper = null;
			DocumentContent = null;
			LastProcessedDocumentContent = null;
			NewContentAvailable?.Invoke(this, EventArgs.Empty);
		}

		void Run() {
			while (!cancellationToken.IsCancellationRequested && !dataQueue.IsCompleted) {

				GenerateData data;
				try {
					data = dataQueue.Take(cancellationToken);
					// If there are more recent entries, get the most recent one
					while (dataQueue.TryTake(out GenerateData? newerData, 100, cancellationToken)) {
						data = newerData; // Is this OK?
					}
				}
				catch (OperationCanceledException) {
					break;
				}

				if (!cancellationToken.IsCancellationRequested && designerArea.IsVisible && data != null && data.DocumentContent != null && !ReferenceEquals(LastProcessedDocumentContent, data.DocumentContent) && !ReferenceEquals(DocumentContent, data.DocumentContent)) {

					GenerationStarted?.Invoke(GenerationState.NONE);
					GenerationState success = GenerationState.NONE;

					IDocumentContent newContent = data.DocumentContent;
					IDrawingMapper? newDrawingMapper = data.DrawingMapper;

					LastProcessedDocumentContent = newContent;

					try {
						SharpAvaloniaDrawingDocument nextDocument = new SharpAvaloniaDrawingDocument();

						newContent.DrawTo(nextDocument, out SharpDrawingException[] drawingErrors, cancellationToken);

						if (cancellationToken.IsCancellationRequested) { break; }

						nextDocument.Freeze();

						DocumentContent = newContent;
						Document = nextDocument;
						DrawingMapper = newDrawingMapper;
						success = drawingErrors.Length > 0 ? GenerationState.ERROR : GenerationState.SUCCESS;

						if (drawingErrors.Length > 0) {
							parsingManager.LoadDrawingErrors(drawingErrors);
						}
						else { parsingManager.ResetDrawingErrors(); }
					}
					catch (Exception) {
						Document = null;
						DrawingMapper = null;
						parsingManager.LoadDrawingErrors(new SharpDrawingException[] { new SharpDrawingException(this, "Unknown drawing error.") });
						success = GenerationState.ERROR;
					}
					finally {
						if (!cancellationToken.IsCancellationRequested) {
							NewContentAvailable?.Invoke(this, EventArgs.Empty);
							GenerationEnded?.Invoke(success);
						}
					}

				}
			}
			Console.WriteLine("Ended designer generator Run.");
		}
	}

}
