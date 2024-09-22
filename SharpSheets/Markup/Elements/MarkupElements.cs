using SharpSheets.Evaluations;
using SharpSheets.Utilities;
using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Markup.Canvas;

namespace SharpSheets.Markup.Elements {

	#region Structural Elements

	/// <summary>
	/// A container used to group other elements together. Any transformation applied
	/// to this element will apply to all descendents.
	/// </summary>
	public class Grouping : DrawableElement { // g

		readonly IDrawableElement[] elements;

		/// <summary>
		/// Constructor for Grouping.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="elements">Child graphical elements of this grouping.</param>
		public Grouping(string? _id, StyleSheet styleSheet, IEnumerable<IDrawableElement> elements) : base(_id, styleSheet) {
			this.elements = elements.ToArray();
		}

		public override void Draw(MarkupCanvas canvas) {
			if (StyleSheet.Enabled.Evaluate(canvas.Environment)) {
				canvas.SaveState();

				if (StyleSheet.DrawingCoords != null) {
					canvas.SetDrawingCoords(StyleSheet.DrawingCoords);
				}
				if (StyleSheet.Transform != null) {
					canvas.ApplyTransform(StyleSheet.Transform);
				}
				if (StyleSheet.ClipPath != null) {
					StyleSheet.ClipPath.Apply(canvas);
				}

				foreach (IDrawableElement elem in elements) {
					foreach(IEnvironment forEachEnv in elem.StyleSheet.GetForEachEnvironments(canvas.Environment)) {
						canvas.SaveEnvironment();
						canvas.ApplyEnvironment(forEachEnv);
						elem.Draw(canvas);
						canvas.RestoreEnvironment();
					}
				}

				canvas.RestoreState();
			}

			if (canvas.CollectingDiagnostics) {
				CalculateBounds(canvas, true);
			}
		}

		private (Rectangle? bounds, PathHandleData[] handles) CalculateBounds(MarkupCanvas canvas, bool register) {
			canvas.SaveState();

			if (StyleSheet.DrawingCoords != null) {
				canvas.SetDrawingCoords(StyleSheet.DrawingCoords);
			}
			if (StyleSheet.Transform != null) {
				canvas.ApplyTransform(StyleSheet.Transform);
			}

			Rectangle? bounds = null;
			List<PathHandleData> handles = new List<PathHandleData>();

			foreach (IDrawableElement elem in elements) {
				Rectangle? elemBounds = null;
				PathHandleData[]? elemHandles = null;
				if (elem is Grouping other) {
					(elemBounds, elemHandles) = other.CalculateBounds(canvas, false);
				}
				else if (elem is IShapeElement shapeElem && shapeElem.GetPath(canvas) is IPathCalculator shapeElemPath) {
					elemBounds = shapeElemPath.GetBoundingBox();
					elemHandles = shapeElemPath.GetPathHandles();
				}

				if (elemBounds is not null) {
					if (elem.StyleSheet.Transform is not null) {
						elemBounds = MarkupGeometry.TransformRectangle(elemBounds, canvas.Evaluate(elem.StyleSheet.Transform, Transform.Identity));
					}

					if (bounds == null) {
						bounds = elemBounds;
					}
					else {
						bounds = Rectangle.Union(bounds, elemBounds);
					}
				}

				if(elemHandles is not null && elemHandles.Length > 0) {
					handles.AddRange(elemHandles);
				}
			}

			if (register && bounds is not null) {
				canvas.RegisterArea(this, bounds, handles.Count > 0 ? handles.ToArray() : null);
			}

			canvas.RestoreState();

			return (bounds, handles.ToArray());
		}
	}

	/// <summary>
	/// A clipping path, defined by a series of child shape elements, that another
	/// element can use to limit the area to which content can be drawn.
	/// </summary>
	public class ClipPath : IStyledElement {
		// clipPathUnits // Worth implementing?

		public string? ID { get; }
		public StyleSheet StyleSheet { get; }

		private readonly IShapeElement[] elements;

		/// <summary>
		/// Constructor for ClipPath.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="elements">Child shape elements which make up this clipping path.</param>
		public ClipPath(string? _id, StyleSheet styleSheet, IEnumerable<IShapeElement> elements) {
			this.ID = _id;
			this.StyleSheet = styleSheet;

			this.elements = elements.ToArray();
		}

		/*
		public void Draw(SVGCanvas canvas) {
			foreach (IShapeElement elem in elements) {
				elem.Draw(canvas);
			}
		}
		*/

		/// <summary></summary>
		/// <exception cref="EvaluationException"></exception>
		/// <exception cref="MarkupCanvasStateException"></exception>
		public void Apply(MarkupCanvas canvas) {
			if (StyleSheet.Enabled.Evaluate(canvas.Environment) && elements.Length > 0) {
				foreach (IShapeElement elem in elements) {
					foreach (IEnvironment forEachEnv in elem.StyleSheet.GetForEachEnvironments(canvas.Environment)) {
						canvas.SaveEnvironment();
						canvas.ApplyEnvironment(forEachEnv);
						elem.AssignGeometry(canvas);
						canvas.RestoreEnvironment();
					}
				}

				// TODO What to do about DrawingCoords here?
				// TODO How to apply transform to this element?

				canvas.ClipUsing(StyleSheet.ClipRule, AreaRule.NonZero).EndPath();

				if (canvas.CollectingDiagnostics) {
					Rectangle? bounds = null;
					List<PathHandleData> handles = new List<PathHandleData>();

					foreach (IShapeElement elem in elements) {
						if (elem.GetPath(canvas) is IPathCalculator path) {
							Rectangle? elemBounds = path.GetBoundingBox();
							PathHandleData[]? elemHandles = path.GetPathHandles();
							if (elemBounds is not null) {
								if (bounds == null) {
									bounds = elemBounds;
								}
								else {
									bounds = Rectangle.Union(bounds, elemBounds);
								}
								canvas.RegisterArea(elem, elemBounds, elemHandles is not null ? elemHandles : null);
							}
							if (elemHandles is not null) {
								handles.AddRange(elemHandles);
							}
						}
					}

					if (bounds is not null) {
						canvas.RegisterArea(this, bounds, handles.Count > 0 ? handles.ToArray() : null);
					}
				}
			}
		}
	}

	/*
	public class Use : IDrawableElement {

		public string ID { get; }
		public StyleSheet StyleSheet { get; }

		readonly IBoxElement href;
		readonly DrawPointExpression location; // x, y
		readonly FloatExpression width;
		readonly FloatExpression height;

		public Use(string id, StyleSheet styleSheet, IBoxElement href, DrawPointExpression location, FloatExpression width, FloatExpression height) {
			this.ID = id;
			this.StyleSheet = styleSheet;

			this.href = href;
			this.location = location;
			this.width = width;
			this.height = height;
		}

		public void Draw(SVGCanvas canvas) {
			// Apply any transform we may have been given
			if (StyleSheet.Transform != null) {
				canvas.ApplyTransform(StyleSheet.Transform);
			}

			// If we have a clip path, apply it
			if (StyleSheet.ClipPath != null) {
				StyleSheet.ClipPath.Apply(canvas);
			}

			// TODO Needs finishing
			DrawPoint location = canvas.TransformPoint(this.location);
			float? width = this.width != null ? (float?)canvas.TransformLength(this.width) : null;
			float? height = this.height != null ? (float?)canvas.TransformLength(this.height) : null;
			if(href is Symbol symbol) {
				symbol.Draw(canvas, location, width, height);
			}
			else if(href is IDrawableElement drawable) {
				throw new NotImplementedException(); // TODO Implement
			}
		}
	}
	*/

	/// <summary>
	/// A graphical template that can be instantiated with a &lt;use&gt; element.
	/// This allows the same graphical elements to be easily repeated in a Markup
	/// document. This element may have other graphical elements as children.
	/// </summary>
	public class Symbol : IDrawableElement {

		public string? ID { get; }
		public StyleSheet StyleSheet { get; }

		private readonly RectangleExpression? viewBox;
		private readonly DrawPointExpression location; // Offset for location
		private readonly FloatExpression width;
		private readonly FloatExpression height;
		private readonly PreserveAspectRatioExpression preserveAspectRatio;
		// refX, refY?

		readonly IDrawableElement[] elements;

		/// <summary>
		/// Constructor for Symbol.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_viewBox" default="null">The view box for this symbol, which determines the
		/// available canvas on which child graphical elements can be drawn.</param>
		/// <param name="_x" default="0">The x coordinate of the symbol. Note that this may be
		/// overriden by a &lt;use&gt; element.</param>
		/// <param name="_y" default="0">The y coordinate of the symbol. Note that this may be
		/// overriden by a &lt;use&gt; element.</param>
		/// <param name="_width" default="0">The width of the symbol. Note that this may be
		/// overriden by a &lt;use&gt; element. The view box will be scaled to this size.</param>
		/// <param name="_height" default="0">The height of the symbol. Note that this may be
		/// overriden by a &lt;use&gt; element. The view box will be scaled to this size.</param>
		/// <param name="_preserveAspectRatio" default="null">Determines how the viewbox will
		/// be deformed if it is used in a container with a different aspect ratio.</param>
		/// <param name="elements">Child graphical elements of this symbol.</param>
		/// <exception cref="EvaluationException"></exception>
		public Symbol(string? _id, StyleSheet styleSheet, RectangleExpression? _viewBox, XLengthExpression _x, YLengthExpression _y, XLengthExpression _width, YLengthExpression _height, PreserveAspectRatioExpression _preserveAspectRatio, IEnumerable<IDrawableElement> elements) {
			this.ID = _id;
			this.StyleSheet = styleSheet;

			this.viewBox = _viewBox;
			this.location = new DrawPointExpression(_x, _y);
			this.width = _width;
			this.height = _height;
			this.preserveAspectRatio = _preserveAspectRatio;

			this.elements = elements.ToArray();
		}

		public void Draw(MarkupCanvas canvas) {
			if (!StyleSheet.Enabled.Evaluate(canvas.Environment)) {
				return;
			}

			canvas.SaveState();

			if (canvas.CollectingDiagnostics) {
				RectangleExpression area = new RectangleExpression(location.X, location.Y, this.width, this.height);
				canvas.RegisterArea(this, area);
			}

			// TODO What to do about DrawingCoords here?

			// Move to the specified location
			canvas.ApplyTransform(TransformExpression.Translate(location.X, location.Y));

			// Apply any transform we may have been given
			if (StyleSheet.Transform != null) {
				canvas.ApplyTransform(StyleSheet.Transform);
			}

			// If we have a clip path, apply it
			if (StyleSheet.ClipPath != null) {
				StyleSheet.ClipPath.Apply(canvas);
			}

			// Get appropriate transform from preserveAspectRatio
			PreserveAspectRatio preserveAspectRatio = canvas.Evaluate(this.preserveAspectRatio, PreserveAspectRatio.Default);
			float width = this.width.Evaluate(canvas.Environment); // canvas.TransformLength(this.width);
			float height = this.height.Evaluate(canvas.Environment); // canvas.TransformLength(this.height);
			Rectangle viewBox = this.viewBox?.Evaluate(canvas.Environment) ?? new Rectangle(0, 0, width, height); // Is this the right default?
			Transform viewBoxTransform = preserveAspectRatio.GetTransform(width, height, viewBox, out _);
			canvas.ApplyTransform(viewBoxTransform);

			// Clip to symbol viewBox
			canvas.Rectangle(viewBox).Clip().EndPath(); // TODO I don't think this is right. The viewBox position matters somehow? Needs testing

			// TODO Need to get size of new rect and make a new canvas with a rect that size

			foreach (IDrawableElement elem in elements) {
				foreach (IEnvironment forEachEnv in elem.StyleSheet.GetForEachEnvironments(canvas.Environment)) {
					canvas.SaveEnvironment();
					canvas.ApplyEnvironment(forEachEnv);
					elem.Draw(canvas);
					canvas.RestoreEnvironment();
				}
			}

			//canvas.SetLineWidth(0.01f).SetStrokeColor(System.Drawing.Color.Red).Rectangle(clipArea).Stroke();

			canvas.RestoreState();
		}
	}

	/*
	public enum MarkerOrientation { Auto, AutoStartReverse }
	public class Marker { // TODO Implement
		float markerHeight;
		// markerUnits // Worth implementing?
		float markerWidth;
		MarkerOrientation orient;
		bool preserveAspectRatio;
		// refX, refY?
		// viewBox?
	}
	*/

	#endregion

	#region Graphics Elements

	/// <summary>
	/// This element draws an image to the page, at the specified coordinates, with
	/// the specified size. If the image aspect ratio does not match the specified area,
	/// then the preserveAspectRatio attribute will determine how the image is scaled/sliced
	/// to fit.
	/// </summary>
	public class Image : IDrawableElement {

		public string? ID { get; }
		public StyleSheet StyleSheet { get; }

		private readonly FloatExpression x;
		private readonly FloatExpression y;
		private readonly FloatExpression width;
		private readonly FloatExpression height;
		private readonly FilePathExpression filepath; // href
		private readonly PreserveAspectRatioExpression preserveAspectRatio;

		/// <summary>
		/// Constructor for Image.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_x" default="0">The x coordinate for the image.</param>
		/// <param name="_y" default="0">The y coordinate for the image.</param>
		/// <param name="_width" default="$width">The width for the image.</param>
		/// <param name="_height" default="$height">The height for the image.</param>
		/// <param name="_file">The filepath for the image to draw.</param>
		/// <param name="_preserveAspectRatio" default="null">Determines how the image will
		/// be deformed/cropped if it is used in an area with a different aspect ratio.</param>
		public Image(string? _id, StyleSheet styleSheet, XLengthExpression _x, YLengthExpression _y, XLengthExpression _width, YLengthExpression _height, FilePathExpression _file, PreserveAspectRatioExpression _preserveAspectRatio) {
			ID = _id;
			StyleSheet = styleSheet;
			this.x = _x;
			this.y = _y;
			this.width = _width;
			this.height = _height;
			this.filepath = _file;
			this.preserveAspectRatio = _preserveAspectRatio;
		}

		public void Draw(MarkupCanvas canvas) {
			if (!StyleSheet.Enabled.Evaluate(canvas.Environment)) {
				return;
			}

			FilePath? filepath = canvas.Evaluate(this.filepath, null);
			//Console.WriteLine("Image filepath: " + (filepath?.Path ?? "null"));

			if(filepath is null) {
				canvas.LogError(this, "No image file provided.", new InvalidOperationException());
				return;
			}
			else if (!filepath.Exists) {
				canvas.LogError(this, $"The provided file does not exist: {filepath.Path}", new FileNotFoundException(null, filepath.Path));
				return;
			}

			canvas.SaveState();

			// TODO What to do about DrawingCoords here?

			// Apply any transform we may have been given
			if (StyleSheet.Transform != null) {
				canvas.ApplyTransform(StyleSheet.Transform);
			}

			// If we have a clip path, apply it
			if (StyleSheet.ClipPath != null) {
				StyleSheet.ClipPath.Apply(canvas);
			}

			try {
				CanvasImageData imageData = new CanvasImageData(filepath);

				Rectangle viewBox = new Rectangle(0f, 0f, imageData.Width, imageData.Height);
				RectangleExpression placement = new RectangleExpression(x, y, width, height);
				canvas.ApplyViewBox(placement, viewBox, preserveAspectRatio, out RectangleExpression contentRect);
				try {
					canvas.AddImage(imageData, contentRect, -1); // -1 aspect so that image conforms to contentRect aspect (allowing for "none" preserveAspectRatio values)
				}
				catch (IOException e) {
					canvas.LogError(this, $"Could not draw image {imageData.Path.Path}.", e);
				}

				if (canvas.CollectingDiagnostics) { canvas.RegisterArea(this, placement); }
			}
			catch (IOException e) {
				throw new InvalidOperationException($"Cannot access image file: {filepath.Path}", e);
			}
			catch (SystemException e) {
				throw new InvalidOperationException($"Error drawing image element: " + e.Message, e);
			}

			canvas.RestoreState();
		}
	}

	#endregion

	/// <summary>
	/// This element can be used to change the way in which the canvas of a <see cref="DivElement"/>
	/// is rescaled, to avoid distorting certain parts of the area. If x and y values are provided,
	/// these will override the border values.
	/// </summary>
	public class SlicingValuesElement : IIdentifiableMarkupElement {
		public string? ID { get; }

		public NSliceValuesExpression NSliceValues { get; }
		public BoolExpression Enabled { get; }

		/// <summary>
		/// Constructor for SlicingValuesElement.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="_xs" default="null">A series of x-coordinates for the dividing lines
		/// for the slices. These will override the x-coordinates of any margins given for
		/// <paramref name="_border"/>.</param>
		/// <param name="_ys" default="null">A series of y-coordinates for the dividing lines
		/// for the slices. These will override the y-coordinates of any margins given for
		/// <paramref name="_border"/>.</param>
		/// <param name="_border" default="null">These values will produce a 9-sliced
		/// canvas based on the margins provided. These values will be overriden by any
		/// values provided for <paramref name="_xs"/> and <paramref name="_ys"/>.</param>
		/// <param name="_enabled" default="true">A flag to indicate whether this element should be
		/// enabled and included in layout calculations.</param>
		public SlicingValuesElement(string? _id, FloatExpression[]? _xs, FloatExpression[]? _ys, MarginsExpression? _border, BoolExpression _enabled) {
			this.ID = _id;
			this.Enabled = _enabled;

			FloatExpression[] finalXs = Array.Empty<FloatExpression>();
			FloatExpression[] finalYs = Array.Empty<FloatExpression>();

			if (_border is MarginsExpression borderMargins) {
				finalXs = new FloatExpression[] { borderMargins.Left, MarkupEnvironments.WidthExpression - borderMargins.Right };
				finalYs = new FloatExpression[] { borderMargins.Bottom, MarkupEnvironments.HeightExpression - borderMargins.Top };
			}

			finalXs = _xs ?? finalXs;
			finalYs = _ys ?? finalYs;

			this.NSliceValues = new NSliceValuesExpression(finalXs, finalYs);
		}
	}

}
