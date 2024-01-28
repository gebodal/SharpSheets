using SharpSheets.Layouts;
using SharpSheets.Utilities;
using System;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;

namespace SharpSheets.Shapes {

	public interface IShape {
		/// <summary>
		/// 
		/// </summary>
		/// <param name="canvas"></param>
		/// <param name="rect"></param>
		/// <exception cref="InvalidOperationException"></exception>
		void Draw(ISharpCanvas canvas, Rectangle rect);
	}

	public interface IAreaShape : IShape {
		float Aspect { get; }
		Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect);
	}

	public interface IFramedArea {
		Rectangle RemainingRect(ISharpGraphicsState graphicsState, Rectangle rect);
	}
	public interface IFramedContainerArea : IFramedArea {
		Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect);
	}
	public interface ILabelledArea {
		Rectangle LabelRect(ISharpGraphicsState graphicsState, Rectangle rect);
	}
	public interface IEntriedArea {
		int EntryCount { get; }

		/// <summary></summary>
		/// <param name="graphicsState"></param>
		/// <param name="areaIndex"></param>
		/// <param name="rect"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		Rectangle EntryRect(ISharpGraphicsState graphicsState, int areaIndex, Rectangle rect);
	}

	public interface IContainerShape : IAreaShape, IFramedContainerArea { }

	public interface IBox : IContainerShape { }

	public interface ILabelledBox : IContainerShape, ILabelledArea { }

	public interface IAbstractTitledFrame : IContainerShape { } // TODO This needs a better name
	public interface ITitledBox : IAbstractTitledFrame, IBox { }
	public interface ITitleStyledBox : IAbstractTitledFrame { }

	public interface IBar : IAreaShape, IFramedArea, ILabelledArea { }

	public interface IUsageBar : IAreaShape, ILabelledArea, IEntriedArea { }

	public interface IDetail : IShape {
		Layout Layout { set; }
		//void Draw(ISharpCanvas canvas, Rectangle rect, Layout layout); // TODO Could layout be a { set; } Property?
	}

	public static class AbstractShapeUtils {

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static void Draw<T>(this T framedArea, ISharpCanvas canvas, Rectangle rect, out Rectangle remainingRect) where T : IAreaShape, IFramedArea {
			framedArea.Draw(canvas, rect);
			remainingRect = framedArea.RemainingRect(canvas, rect);
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static void Draw<T>(this T framedLabelledArea, ISharpCanvas canvas, Rectangle rect, out Rectangle labelRect, out Rectangle remainingRect) where T : IAreaShape, IFramedArea, ILabelledArea {
			framedLabelledArea.Draw(canvas, rect);
			labelRect = framedLabelledArea.LabelRect(canvas, rect);
			remainingRect = framedLabelledArea.RemainingRect(canvas, rect);
		}

		/// <summary></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public static void Draw(this IUsageBar bar, ISharpCanvas canvas, Rectangle rect, out Rectangle labelRect, out Rectangle firstEntryRect, out Rectangle secondEntryRect) {
			bar.Draw(canvas, rect);
			labelRect = bar.LabelRect(canvas, rect);
			firstEntryRect = bar.EntryRect(canvas, 0, rect);
			secondEntryRect = bar.EntryRect(canvas, 1, rect);
		}

		public static Size RemainingSize(this IFramedArea area, ISharpGraphicsState graphicsState, Size size) {
			return (Size)area.RemainingRect(graphicsState, (Rectangle)size);
		}

		public static Size FullSize(this IFramedContainerArea area, ISharpGraphicsState graphicsState, Size size) {
			return (Size)area.FullRect(graphicsState, (Rectangle)size);
		}

		public static Rectangle[] EntryRects(this IEntriedArea entriedArea, ISharpGraphicsState graphicsState, Rectangle rect) {
			Rectangle[] entries = new Rectangle[entriedArea.EntryCount];
			for (int i = 0; i < entriedArea.EntryCount; i++) {
				entries[i] = entriedArea.EntryRect(graphicsState, i, rect);
			}
			return entries;
		}

		public static Rectangle FirstEntryRect(this IUsageBar bar, ISharpGraphicsState graphicsState, Rectangle rect) {
			return bar.EntryRect(graphicsState, 0, rect);
		}
		public static Rectangle SecondEntryRect(this IUsageBar bar, ISharpGraphicsState graphicsState, Rectangle rect) {
			return bar.EntryRect(graphicsState, 1, rect);
		}

	}

	public abstract class AbstractAreaShape : IAreaShape {

		public float Aspect { get; } // TODO This should probably be overridable, given some of the subclass behaviour

		public AbstractAreaShape(float aspect) {
			this.Aspect = aspect;
		}

		public virtual Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return rect.Aspect(Aspect);
		}

		public void Draw(ISharpCanvas canvas, Rectangle rect) {
			DrawFrame(canvas, AspectRect(canvas, rect));
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="canvas"></param>
		/// <param name="aspectRect"></param>
		/// <exception cref="InvalidOperationException"></exception>
		protected abstract void DrawFrame(ISharpCanvas canvas, Rectangle aspectRect);

		/// <summary></summary>
		/// <exception cref="InvalidRectangleException"></exception>
		protected static Rectangle[] ValidateRects(Rectangle?[] rects, string message) {
			Rectangle[] processed = new Rectangle[rects.Length];
			for(int i=0; i<rects.Length; i++) {
				processed[i] = rects[i] ?? throw new InvalidRectangleException(message);
			}
			return processed;
		}
	}

	public abstract class AbstractFrame : AbstractAreaShape, IFramedArea {

		public AbstractFrame(float aspect) : base(aspect) { }

		public Rectangle RemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return GetRemainingRect(graphicsState, AspectRect(graphicsState, rect));
		}
		protected abstract Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, Rectangle aspectRect);
	}

	public abstract class AbstractContainerFrame : AbstractFrame, IFramedContainerArea {

		public AbstractContainerFrame(float aspect) : base(aspect) { }

		public abstract Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect);
	}

	public abstract class BoxBase : AbstractContainerFrame, IBox {
		public BoxBase(float aspect) : base(aspect) { }
	}

	public abstract class LabelledBoxBase : AbstractContainerFrame, ILabelledBox {

		public LabelledBoxBase(float aspect) : base(aspect) { }

		public Rectangle LabelRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return GetLabelRect(graphicsState, AspectRect(graphicsState, rect));
		}
		protected abstract Rectangle GetLabelRect(ISharpGraphicsState graphicsState, Rectangle rect);
	}

	// This is a generic box with a name
	public abstract class AbstractTitledBox : AbstractContainerFrame, IAbstractTitledFrame {

		protected readonly string[] parts;
		protected readonly TextFormat format;
		protected readonly float fontSize;

		public AbstractTitledBox(float aspect, string name, TextFormat format, float fontSize) : base(aspect) {
			parts = name.Replace("\\n", "\n").SplitAndTrim('\n');
			this.format = format;
			this.fontSize = fontSize;
		}
	}

	// This is specifically a box with an intrinsic name
	public abstract class TitledBoxBase : AbstractTitledBox, ITitledBox {
		public TitledBoxBase(float aspect, string name, TextFormat format, float fontSize) : base(aspect, name, format, fontSize) { }
	}

	// This is for title styles which are tacked onto existing box styles
	public abstract class TitleStyledBoxBase : AbstractTitledBox, ITitleStyledBox {

		protected IContainerShape box;
		protected Vector offset;
		protected float spacing;
		protected Colors.Color? textColor;

		public TitleStyledBoxBase(IContainerShape box, string name, TextFormat format, float fontSize, Vector offset, float spacing, Colors.Color? color) : base(-1, name, format, fontSize) {
			this.box = box;
			this.offset = offset;
			this.spacing = spacing;
			this.textColor = color;
		}

		public override Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			// This should be overriden as needed by subclasses
			return box.AspectRect(graphicsState, rect);
		}
	}

	public abstract class BarBase : AbstractFrame, IBar {

		public BarBase(float aspect) : base(aspect) { }

		public Rectangle LabelRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return GetLabelRect(graphicsState, AspectRect(graphicsState, rect));
		}
		protected abstract Rectangle GetLabelRect(ISharpGraphicsState graphicsState, Rectangle rect);
	}

	public abstract class UsageBarBase : AbstractAreaShape, IUsageBar {

		public int EntryCount { get; } = 2;

		public UsageBarBase(float aspect) : base(aspect) { }

		public Rectangle LabelRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return GetLabelRect(graphicsState, AspectRect(graphicsState, rect));
		}
		protected abstract Rectangle GetLabelRect(ISharpGraphicsState graphicsState, Rectangle rect);

		public Rectangle EntryRect(ISharpGraphicsState graphicsState, int entryIndex, Rectangle rect) {
			if(entryIndex == 0) {
				return GetFirstEntryRect(graphicsState, AspectRect(graphicsState, rect));
			}
			else if(entryIndex == 1) {
				return GetSecondEntryRect(graphicsState, AspectRect(graphicsState, rect));
			}
			else {
				throw new ArgumentOutOfRangeException("UsageBar shapes only provide two entries.");
			}
		}

		protected abstract Rectangle GetFirstEntryRect(ISharpGraphicsState graphicsState, Rectangle rect);
		protected abstract Rectangle GetSecondEntryRect(ISharpGraphicsState graphicsState, Rectangle rect);
	}

	public abstract class DetailBase : IDetail {
		public Layout Layout { protected get; set; }
		public abstract void Draw(ISharpCanvas canvas, Rectangle rect);
	}

}