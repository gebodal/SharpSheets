using SharpSheets.Layouts;
using SharpSheets.Utilities;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using System.Diagnostics.CodeAnalysis;

namespace SharpSheets.Shapes {

	public interface IShape {
		string DisplayName { get; }
	}

	public interface IDrawRectShape : IShape {
		/// <exception cref="InvalidOperationException"></exception>
		void Draw(ISharpCanvas canvas, Rectangle rect);
	}

	public interface IAspectArea {
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
		int EntryCount(ISharpGraphicsState graphicsState, Rectangle rect);

		/// <exception cref="ArgumentOutOfRangeException"></exception>
		Rectangle EntryRect(ISharpGraphicsState graphicsState, int areaIndex, Rectangle rect);
	}

	public interface IAreaShape : IDrawRectShape, IAspectArea { }

	public interface IBox : IAreaShape, IFramedContainerArea { }

	public interface ILabelledBox : IAreaShape, IFramedContainerArea, ILabelledArea { }

	public interface IEntriedShape : IAreaShape, IEntriedArea { }

	public interface IBar : IAreaShape, IFramedArea, ILabelledArea { }

	public interface IUsageBar : IEntriedShape, ILabelledArea { }

	public interface ITitledBox : IBox {
		void Draw(ISharpCanvas canvas, string name, Rectangle rect); // If IDrawRectShape.Draw is called, call this with default name
		Rectangle RemainingRect(ISharpGraphicsState graphicsState, string name, Rectangle rect); // If IFramedArea.RemainingRect is called, call this with default name
		Rectangle FullRect(ISharpGraphicsState graphicsState, string name, Rectangle rect); // If IFramedContainerArea.FullRect is called, call this with default name
	}
	public interface ITitleStyle : IShape {
		Rectangle AspectRect(ISharpGraphicsState graphicsState, IBox shape, string name, Rectangle rect);
		void Draw(ISharpCanvas canvas, IBox shape, string name, Rectangle rect);

		Rectangle RemainingRect(ISharpGraphicsState graphicsState, IBox shape, string name, Rectangle rect);
		Rectangle FullRect(ISharpGraphicsState graphicsState, IBox shape, string name, Rectangle rect);
	}

	public interface IDetail : IShape {
		void Draw(ISharpCanvas canvas, Rectangle rect, LayoutDirection layout);
	}

	public static class AbstractShapeUtils {

		/// <exception cref="InvalidOperationException"></exception>
		public static void Draw<T>(this T framedArea, ISharpCanvas canvas, Rectangle rect, out Rectangle remainingRect) where T : IAreaShape, IFramedArea {
			framedArea.Draw(canvas, rect);
			remainingRect = framedArea.RemainingRect(canvas, rect);
		}

		/// <exception cref="InvalidOperationException"></exception>
		public static void Draw<T>(this T framedLabelledArea, ISharpCanvas canvas, Rectangle rect, out Rectangle labelRect, out Rectangle remainingRect) where T : IAreaShape, IFramedArea, ILabelledArea {
			framedLabelledArea.Draw(canvas, rect);
			labelRect = framedLabelledArea.LabelRect(canvas, rect);
			remainingRect = framedLabelledArea.RemainingRect(canvas, rect);
		}

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
			Rectangle[] entries = new Rectangle[entriedArea.EntryCount(graphicsState, rect)];
			for (int i = 0; i < entries.Length; i++) {
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

		public string DisplayName => this.GetType().Name;

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
		/// <exception cref="InvalidOperationException"></exception>
		protected abstract void DrawFrame(ISharpCanvas canvas, Rectangle aspectRect);

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

	public abstract class EntriedShapeBase : AbstractAreaShape, IEntriedShape {
		public EntriedShapeBase(float aspect) : base(aspect) { }

		public abstract int EntryCount(ISharpGraphicsState graphicsState, Rectangle rect);
		public abstract Rectangle EntryRect(ISharpGraphicsState graphicsState, int entryIndex, Rectangle rect);
	}

	public abstract class BarBase : AbstractFrame, IBar {

		public BarBase(float aspect) : base(aspect) { }

		public Rectangle LabelRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return GetLabelRect(graphicsState, AspectRect(graphicsState, rect));
		}
		protected abstract Rectangle GetLabelRect(ISharpGraphicsState graphicsState, Rectangle rect);
	}

	public abstract class UsageBarBase : AbstractAreaShape, IUsageBar {

		public UsageBarBase(float aspect) : base(aspect) { }

		public Rectangle LabelRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			return GetLabelRect(graphicsState, AspectRect(graphicsState, rect));
		}
		protected abstract Rectangle GetLabelRect(ISharpGraphicsState graphicsState, Rectangle rect);

		public int EntryCount(ISharpGraphicsState graphicsState, Rectangle rect) => 2;

		public Rectangle EntryRect(ISharpGraphicsState graphicsState, int entryIndex, Rectangle rect) {
			if (entryIndex == 0) {
				return GetFirstEntryRect(graphicsState, AspectRect(graphicsState, rect));
			}
			else if (entryIndex == 1) {
				return GetSecondEntryRect(graphicsState, AspectRect(graphicsState, rect));
			}
			else {
				throw new ArgumentOutOfRangeException(nameof(entryIndex), "UsageBar shapes only provide two entries.");
			}
		}

		protected abstract Rectangle GetFirstEntryRect(ISharpGraphicsState graphicsState, Rectangle rect);
		protected abstract Rectangle GetSecondEntryRect(ISharpGraphicsState graphicsState, Rectangle rect);
	}

	// This is specifically a box with an intrinsic name
	public abstract class TitledBoxBase : ITitledBox {

		public string DisplayName => this.GetType().Name;

		public float Aspect { get; }

		protected readonly TextFormat format;
		protected readonly float fontSize;

		public TitledBoxBase(float aspect, TextFormat format, float fontSize) {
			Aspect = aspect;
			//parts = name.Replace("\\n", "\n").SplitAndTrim('\n');
			this.format = format;
			this.fontSize = fontSize;
		}

		protected static string[] GetParts(string title) {
			return title.Replace("\\n", "\n").SplitAndTrim('\n');
		}

		public virtual Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect) {
			// This should be overriden as needed by subclasses
			return rect.Aspect(Aspect);
		}

		/// <exception cref="InvalidOperationException"></exception>
		protected abstract void DrawFrame(ISharpCanvas canvas, string name, Rectangle aspectRect);
		
		public void Draw(ISharpCanvas canvas, string name, Rectangle rect) {
			DrawFrame(canvas, name, AspectRect(canvas, rect));
		}

		public Rectangle RemainingRect(ISharpGraphicsState graphicsState, string name, Rectangle rect) {
			return GetRemainingRect(graphicsState, name, AspectRect(graphicsState, rect));
		}
		protected abstract Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, string name, Rectangle aspectRect);

		public abstract Rectangle FullRect(ISharpGraphicsState graphicsState, string name, Rectangle rect);

		public void Draw(ISharpCanvas canvas, Rectangle rect) => Draw(canvas, DisplayName, rect);
		public Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) => FullRect(graphicsState, DisplayName, rect);
		public Rectangle RemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) => RemainingRect(graphicsState, DisplayName, rect);
	}

	public class ConcreteTitledBox : IBox {

		public string DisplayName => titledBox.DisplayName;
		public float Aspect => titledBox.Aspect;

		private readonly ITitledBox titledBox;
		private readonly string title;

		public ConcreteTitledBox(ITitledBox titledBox, string title) {
			this.titledBox = titledBox;
			this.title = title;
		}

		public Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect) => titledBox.AspectRect(graphicsState, rect);
		public void Draw(ISharpCanvas canvas, Rectangle rect) => titledBox.Draw(canvas, title, rect);
		public Rectangle RemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) => titledBox.RemainingRect(graphicsState, title, rect);
		public Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) => titledBox.FullRect(graphicsState, title, rect);

	}

	// This is for title styles which are tacked onto existing box styles
	public abstract class TitleStyleBase : ITitleStyle {

		public string DisplayName => this.GetType().Name;

		protected readonly TextFormat format;
		protected readonly float fontSize;
		protected readonly Vector offset;
		protected readonly float spacing;
		protected readonly Colors.Color? textColor;

		public TitleStyleBase(TextFormat format, float fontSize, Vector offset, float spacing, Colors.Color? color) {
			this.format = format;
			this.fontSize = fontSize;
			this.offset = offset;
			this.spacing = spacing;
			this.textColor = color;
		}

		protected static string[] GetParts(string title) {
			return title.Replace("\\n", "\n").SplitAndTrim('\n');
		}

		protected RichString[] GetRichParts(string title) {
			return GetParts(title).Select(p => RichString.Create(p, format)).ToArray();
		}

		public abstract Rectangle AspectRect(ISharpGraphicsState graphicsState, IBox shape, string name, Rectangle rect);

		/// <exception cref="InvalidOperationException"></exception>
		protected abstract void DrawFrame(ISharpCanvas canvas, IBox shape, string name, Rectangle aspectRect);

		public void Draw(ISharpCanvas canvas, IBox shape, string name, Rectangle rect) {
			DrawFrame(canvas, shape, name, AspectRect(canvas, shape, name, rect));
		}

		public Rectangle RemainingRect(ISharpGraphicsState graphicsState, IBox shape, string name, Rectangle rect) {
			return GetRemainingRect(graphicsState, shape, name, AspectRect(graphicsState, shape, name, rect));
		}
		protected abstract Rectangle GetRemainingRect(ISharpGraphicsState graphicsState, IBox shape, string name, Rectangle aspectRect);

		public abstract Rectangle FullRect(ISharpGraphicsState graphicsState, IBox shape, string name, Rectangle rect);

	}

	public class TitleStyledBox : IBox {

		public string DisplayName => this.GetType().Name;
		public float Aspect => -1f;

		private readonly ITitleStyle titleStyle;
		private readonly IBox shape;
		private readonly string title;

		public TitleStyledBox(ITitleStyle titleStyle, IBox shape, string title) {
			this.titleStyle = titleStyle;
			this.shape = shape;
			this.title = title;
		}

		public Rectangle AspectRect(ISharpGraphicsState graphicsState, Rectangle rect) => titleStyle.AspectRect(graphicsState, shape, title, rect);
		public void Draw(ISharpCanvas canvas, Rectangle rect) => titleStyle.Draw(canvas, shape, title, rect);
		public Rectangle RemainingRect(ISharpGraphicsState graphicsState, Rectangle rect) => titleStyle.RemainingRect(graphicsState, shape, title, rect);
		public Rectangle FullRect(ISharpGraphicsState graphicsState, Rectangle rect) => titleStyle.FullRect(graphicsState, shape, title, rect);

	}

	public static class TitledBoxes {

		[return: NotNullIfNotNull(nameof(box))]
		[return: NotNullIfNotNull(nameof(titleStyle))]
		public static IBox? ResolveBox(IBox? box, ITitleStyle? titleStyle, string title) {
			if (box is ITitledBox titled) {
				return new ConcreteTitledBox(titled, title);
			}
			else if (titleStyle is not null) {
				return new TitleStyledBox(titleStyle, box ?? new NoOutline(-1f), title);
			}
			else {
				return box;
			}
		}

	}

	public abstract class DetailBase : IDetail {
		public string DisplayName => this.GetType().Name;
		public abstract void Draw(ISharpCanvas canvas, Rectangle rect, LayoutDirection layout);
	}

}