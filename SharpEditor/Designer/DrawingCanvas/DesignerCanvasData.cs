using SharpSheets.Canvas;
using SharpSheets.Layouts;
using System.Collections.Generic;
using System.Linq;

namespace SharpEditor.Designer.DrawingCanvas {

	public interface ISharpDesignerCanvas : ISharpCanvas {
		Dictionary<object, RegisteredAreas> GetAreas(Avalonia.Point point);
		IEnumerable<RegisteredAreas> GetAreas(object owner);
		bool ContainsAreaOwner(object owner);
	}

	public enum FieldType { TEXT, CHECK, IMAGE }

	public struct CanvasField {
		public string Name { get; private set; }
		public string? Tooltip { get; private set; }
		public Rectangle Rect { get; private set; }
		public FieldType Type { get; private set; }
		public CanvasField(string name, string? tooltip, Rectangle rect, FieldType type) {
			Name = name;
			Tooltip = tooltip;
			Rect = rect;
			Type = type;
		}
	}

	public class RegisteredAreas {
		public Rectangle Original { get; }
		public Rectangle? Adjusted { get; }
		public Rectangle[] Inner { get; }
		public PathHandleData[]? Handles { get; }

		public Rectangle Total { get; }

		public RegisteredAreas(Rectangle original, Rectangle? adjusted, Rectangle[] inner, PathHandleData[]? handles) {
			Original = original;
			Adjusted = adjusted;
			Inner = inner;
			Handles = handles;

			Total = Adjusted is not null ? Rectangle.Union(Original, Adjusted) : Original;
		}

		public bool Contains(float x, float y) {
			return Original.Contains(x, y) || (Adjusted?.Contains(x, y) ?? false) || Inner.Any(i => i.Contains(x, y));
		}
	}

}
