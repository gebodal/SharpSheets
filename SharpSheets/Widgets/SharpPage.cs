using System.Collections.Generic;
using System.Threading;
using SharpSheets.Utilities;
using SharpSheets.Layouts;
using SharpSheets.Canvas;
using SharpSheets.Exceptions;
using SharpSheets.Parsing;

namespace SharpSheets.Widgets {

	public class SharpPageList : IDocumentContent {

		public bool HasContent { get { return PageCount > 0; } }

		protected readonly List<Page> pages;

		public SharpPageList() {
			this.pages = new List<Page>();
		}

		public int PageCount { get { return pages.Count; } }

		public void AddPage(Page page) {
			this.pages.Add(page);
		}

		public void DrawTo(ISharpDocument document, out SharpDrawingException[] errors, CancellationToken cancellationToken) {
			//Console.WriteLine("Draw sheets");

			List<SharpDrawingException> errorList = new List<SharpDrawingException>();
			foreach (Page page in pages) {
				if (cancellationToken.IsCancellationRequested) {
					errors = errorList.ToArray();
					return;
				}

				ISharpCanvas canvas = document.AddNewPage(page.pageSize);

				try {
					page.Draw(canvas, canvas.CanvasRect, cancellationToken);
				}
				catch (SharpDrawingException e) {
					errorList.Add(e);
				}
				catch (Exception e) {
					errorList.Add(new SharpDrawingException(page, "Drawing error: " + e.Message, e));
				}

				if (cancellationToken.IsCancellationRequested) {
					errors = errorList.ToArray();
					return;
				}

				errorList.AddRange(canvas.GetDrawingErrors());
			}

			errors = errorList.ToArray();
		}
	}

	/// <summary>
	/// Indicates the layout to use for a page background image.
	/// </summary>
	public enum ImageLayout {
		/// <summary>
		/// The image is to be stretched to fit the aspect ratio of the page.
		/// </summary>
		STRETCH,
		/// <summary>
		/// The image will be resized to fit inside the available page space, preserving its aspect ratio.
		/// </summary>
		CONTAIN,
		/// <summary>
		/// The image will be resized to cover the entire page space, preserving its aspect ratio and potentially
		/// clipping the image to fit the page.
		/// </summary>
		COVER
	}

	/// <summary>
	/// This widget represents an entire page of a SharpSheets document, and hence should only appear
	/// at the base level of the configuration file. The current background color will be used
	/// as the background color for the whole page, or optionally an image can be provided as a background,
	/// with the option to stretch that image to fit the entire page area. Additionally, page size and page
	/// margins can be specified, along with an option to convert standard page sizes to landscape.
	/// </summary>
	public sealed class Page : SharpWidget {

		public override bool ProvidesRemaining { get; } = true;

		public readonly PageSize pageSize;
		public readonly Margins pageMargins;

		public readonly CanvasImageData? backgroundimage;
		public readonly ImageLayout? backgroundLayout;

		private readonly Div? header;
		private readonly Div? footer;

		/// <summary>
		/// Constructor for SharpPage.
		/// </summary>
		/// <param name="setup">Widget setup for SharpPage.</param>
		/// <param name="paper">Paper size to use for the page. A variety of common paper size options are available,
		/// such as "A4" or "letter", or alternatively a size may be specified explicitly (as in "20 x 20 cm").</param>
		/// <param name="pageMargins" default="(28,28,28,28)">Margins to use for the page area, separating the document
		/// content from the edge of the paper.</param>
		/// <param name="landscape">Flag to indicate that the page should be landscape (i.e. rotated 90 degree).</param>
		/// <param name="backgroundimage">An image path to use for a page background (relative to the current file).
		/// If no image path is provided, then the current background color will be used as the page background.</param>
		/// <param name="backgroundLayout">Layout for background image, allowing you to specify how the image should be sized
		/// on the page (stretched, contained, or covering).</param>
		/// <param name="header">If provided, this child will be drawn in the header area of the page -- i.e. the
		/// area at the top of the page that lies within the <paramref name="pageMargins"/>.</param>
		/// <param name="footer">If provided, this child will be drawn in the footer area of the page -- i.e. the
		/// area at the bottom of the page that lies within the <paramref name="pageMargins"/>.</param>
		/// <size>0 0</size>
		public Page(
				WidgetSetup setup,
				PageSize? paper = null,
				Margins? pageMargins = null,
				bool landscape = false,
				CanvasImageData? backgroundimage = null,
				ImageLayout? backgroundLayout = null,
				ChildHolder? header = null,
				ChildHolder? footer = null
			) : base(setup) {

			pageSize = paper ?? PageSize.LETTER;
			this.pageMargins = pageMargins ?? new Margins(28f);
			if (landscape) {
				pageSize = pageSize.Rotate();
			}

			this.backgroundimage = backgroundimage;
			this.backgroundLayout = backgroundLayout;

			this.header = header?.Child;
			this.footer = footer?.Child;
		}

		/*
		public override Rectangle ApplyMargin(Rectangle rect) {
			return base.ApplyMargin(rect.Margins(pageMargins, false));
		}
		*/

		protected override void RegisterAreas(ISharpCanvas canvas, Rectangle fullRect, Rectangle availableRect) { }

		public override void Draw(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {

			canvas.SaveState();
			canvas.SetFillColor(setup.backgroundColor);
			canvas.Rectangle(canvas.CanvasRect).Fill();
			canvas.RestoreState();

			Rectangle? headerRect = pageMargins.Top > 0 ? Rectangle.RectangleFromBounding(rect.Left, rect.Top - pageMargins.Top, rect.Right, rect.Top) : null; ;
			Rectangle? footerRect = pageMargins.Bottom > 0 ? Rectangle.RectangleFromBounding(rect.Left, rect.Bottom, rect.Right, rect.Bottom + pageMargins.Bottom) : null;
			Rectangle pageContentsRect = rect.Margins(pageMargins, false);

			canvas.RegisterAreas(this, rect, pageContentsRect, new Rectangle?[] { headerRect, footerRect }.WhereNotNull().ToArray());

			if (backgroundimage != null) {
				float? backgroundAspect = backgroundLayout == ImageLayout.STRETCH ? pageSize.AspectRatio : (float?)null;
				Rectangle imageRect = rect;
				if(backgroundLayout == ImageLayout.COVER) {
					imageRect = rect.ContainAspect(backgroundimage.Width / backgroundimage.Height);
				}
				canvas.AddImage(backgroundimage, imageRect, backgroundAspect);
			}

			if(header is not null) {
				if (headerRect is not null) {
					header.Draw(canvas, headerRect, cancellationToken);
				}
				else {
					//canvas.LogError(header, "No space to draw page header.");
					canvas.LogError(this, "No space to draw page header.");
				}
			}
			
			base.Draw(canvas, pageContentsRect, cancellationToken);

			if (footer is not null) {
				if (footerRect is not null) {
					footer.Draw(canvas, footerRect, cancellationToken);
				}
				else {
					canvas.LogError(this, "No space to draw page footer.");
				}
			}
		}

		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) { }

		protected override Rectangle GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			return rect;
		}
	}

}