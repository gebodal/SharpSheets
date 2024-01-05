using SharpSheets.Layouts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using SharpSheets.Canvas;
using SharpSheets.Exceptions;

namespace SharpSheets.Widgets.DrawingWidget {
	/*
	public abstract class DrawOperation { }

	public abstract class PositionalOperation : DrawOperation {
		public readonly bool isAbsolute;
		public readonly bool isRelative;
		public PositionalOperation(bool isAbsolute, bool isRelative) {
			this.isAbsolute = isAbsolute;
			this.isRelative = isRelative;
		}
	}

	public class MoveOperation : PositionalOperation {
		public readonly DrawPoint startPoint;
		public MoveOperation(bool isAbsolute, bool isRelative, DrawPoint startPoint) : base(isAbsolute, isRelative) {
			this.startPoint = startPoint;
		}
	}

	public class LineOperation : PositionalOperation {
		public readonly DrawPoint endPoint;
		public LineOperation(bool isAbsolute, bool isRelative, DrawPoint endPoint) : base(isAbsolute, isRelative) {
			this.endPoint = endPoint;
		}
	}

	public class HorizontalOperation : PositionalOperation {
		public readonly float x;
		public HorizontalOperation(bool isAbsolute, bool isRelative, float x) : base(isAbsolute, isRelative) {
			this.x = x;
		}
	}

	public class VerticalOperation : PositionalOperation {
		public readonly float y;
		public VerticalOperation(bool isAbsolute, bool isRelative, float y) : base(isAbsolute, isRelative) {
			this.y = y;
		}
	}

	public class CubicOperation : PositionalOperation {
		public readonly DrawPoint controlPoint1;
		public readonly DrawPoint controlPoint2;
		public readonly DrawPoint endPoint;
		public CubicOperation(bool isAbsolute, bool isRelative, DrawPoint controlPoint1, DrawPoint controlPoint2, DrawPoint endPoint) : base(isAbsolute, isRelative) {
			this.controlPoint1 = controlPoint1;
			this.controlPoint2 = controlPoint2;
			this.endPoint = endPoint;
		}
	}

	public class QuadraticOperation : PositionalOperation {
		public readonly DrawPoint controlPoint;
		public readonly DrawPoint endPoint;
		public QuadraticOperation(bool isAbsolute, bool isRelative, DrawPoint controlPoint, DrawPoint endPoint) : base(isAbsolute, isRelative) {
			this.controlPoint = controlPoint;
			this.endPoint = endPoint;
		}
	}

	public class ArcOperation : PositionalOperation {
		public readonly float radius;
		public readonly bool largeArc;
		public readonly bool sweep;
		public readonly DrawPoint endPoint;
		public ArcOperation(bool isAbsolute, bool isRelative, float radius, bool largeArc, bool sweep, DrawPoint endPoint) : base(isAbsolute, isRelative) {
			this.radius = radius;
			this.largeArc = largeArc;
			this.sweep = sweep;
			this.endPoint = endPoint;
		}
	}

	public class CloseOperation : DrawOperation { }

	public class EndOperation : DrawOperation { }

	public class FillOperation : DrawOperation { }

	public class StrokeOperation : DrawOperation { }

	public class LineWidthOperation : DrawOperation {
		public readonly float width;
		public LineWidthOperation(float width) {
			this.width = width;
		}
	}

	public static class DrawingOperations {

		class Scanner {

			readonly IEnumerator<string> stream;

			public Scanner(IEnumerable<string> input) {
				stream = input.GetEnumerator();
			}

			/// <summary></summary>
			/// <exception cref="InvalidOperationException"></exception>
			public string NextString() {
				if (stream.MoveNext()) {
					return stream.Current;
				}
				else {
					throw new InvalidOperationException();
				}
			}

			/// <summary></summary>
			/// <exception cref="FormatException"></exception>
			/// <exception cref="InvalidOperationException"></exception>
			public bool NextBool() {
				string next = NextString();
				if (next == "0") { return false; }
				else if (next == "1") { return true; }
				else { throw new FormatException($"\"{next}\" is not a valid boolean."); }
			}

			/// <summary></summary>
			/// <exception cref="FormatException"></exception>
			/// <exception cref="InvalidOperationException"></exception>
			public int NextInt() {
				string next = NextString();
				return int.Parse(next);
			}

			/// <summary></summary>
			/// <exception cref="FormatException"></exception>
			/// <exception cref="InvalidOperationException"></exception>
			public float NextFloat() {
				string next = NextString();
				return float.Parse(next);
			}

			/// <summary></summary>
			/// <exception cref="FormatException"></exception>
			/// <exception cref="InvalidOperationException"></exception>
			public DrawPoint NextDrawPoint() {
				float x = NextFloat();
				float y = NextFloat();
				return new DrawPoint(x, y);
			}
		}

		private static readonly Regex segmentRegex = new Regex(@"[MmLlHhVvCcQqAa][Rr]?|[ZzEeFfSsWw]|\-?[0-9]+(\.[0-9]*)?|\.[0-9]+");

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		public static IEnumerable<DrawOperation> Parse(string commands) {
			Scanner scanner = new Scanner(segmentRegex.Matches(commands).Select(m => m.Value));

			while (true) {
				string command;
				try {
					command = scanner.NextString();
				}
				catch (InvalidOperationException) {
					break;
				}

				//Console.WriteLine($"Command: {command}");

				bool CommandIs(string c) {
					return command[0].ToString().Equals(c.ToString(), StringComparison.InvariantCultureIgnoreCase);
				}
				bool isAbsolute = char.IsUpper(command[0]);
				bool isRelative = command.Length >= 2 && command[1].ToString().Equals("R", StringComparison.InvariantCultureIgnoreCase);

				DrawOperation? operation = null;
				try {
					if (CommandIs("M")) {
						operation = new MoveOperation(isAbsolute, isRelative, scanner.NextDrawPoint());
					}
					else if (CommandIs("L")) {
						operation = new LineOperation(isAbsolute, isRelative, scanner.NextDrawPoint());
					}
					else if (CommandIs("H")) {
						operation = new HorizontalOperation(isAbsolute, isRelative, scanner.NextFloat());
					}
					else if (CommandIs("V")) {
						operation = new VerticalOperation(isAbsolute, isRelative, scanner.NextFloat());
					}
					else if (CommandIs("C")) {
						operation = new CubicOperation(isAbsolute, isRelative, scanner.NextDrawPoint(), scanner.NextDrawPoint(), scanner.NextDrawPoint());
					}
					else if (CommandIs("Q")) {
						operation = new QuadraticOperation(isAbsolute, isRelative, scanner.NextDrawPoint(), scanner.NextDrawPoint());
					}
					else if (CommandIs("A")) {
						operation = new ArcOperation(isAbsolute, isRelative, scanner.NextFloat(), scanner.NextBool(), scanner.NextBool(), scanner.NextDrawPoint());
					}
					else if (CommandIs("Z")) {
						operation = new CloseOperation();
					}
					else if (CommandIs("E")) {
						operation = new EndOperation();
					}
					else if (CommandIs("F")) {
						operation = new FillOperation();
					}
					else if (CommandIs("S")) {
						operation = new StrokeOperation();
					}
					else if (CommandIs("W")) {
						operation = new LineWidthOperation(scanner.NextFloat());
					}
					else {
						throw new FormatException($"Unrecognised drawing operation \"{command}\"");
					}
				}
				catch (InvalidOperationException) {
					throw new FormatException("Badly formatted drawing operations.");
				}

				yield return operation;
			}
		}
	}


	/// <summary>
	/// A widget which can draw arbitrary shapes, using a series of drawing commands.
	/// </summary>
	public class DrawingWidget : SharpWidget {

		protected readonly List<DrawOperation> operations;
		protected readonly Position? remaining;
		protected readonly Margins? frame;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="setup"></param>
		/// <param name="commands"></param>
		/// <param name="_remaining"></param>
		/// <param name="_frame"></param>
		/// <size>0 0</size>
		public DrawingWidget(
				WidgetSetup setup,
				List<string>? commands = null,
				Position? _remaining = null,
				Margins? _frame = null
			) : base(setup) {

			operations = new List<DrawOperation>();

			commands ??= new List<string>();
			for (int i = 0; i < commands.Count; i++) {
				try {
					//Console.WriteLine($"Parse command: {commands[i]}");
					operations.AddRange(DrawingOperations.Parse(commands[i]));
				}
				catch (FormatException) {
					throw new SharpInitializationException($"commands<{i}>", typeof(DrawingWidget), "Badly formatted drawing commands.");
				}
			}

			//foreach(DrawOperation operation in operations) {
			//	Console.WriteLine($"Operation: {operation.GetType().Name}");
			//}

			this.remaining = _remaining;
			this.frame = _frame;
			if (frame != null && remaining != null) {
				throw new SharpInitializationException(new string[] { "_remaining", "_frame" }, typeof(DrawingWidget), "Cannot specify both remaining and frame properties."); // TODO How to deal with differing types?
			}

		}

		private static DrawPoint RelativePoint(Rectangle rect, DrawPoint point) {
			return new DrawPoint(point.X * rect.Width, point.Y * rect.Height);
		}

		/// <summary></summary>
		/// <exception cref="SharpDrawingException"></exception>
		protected override void DrawWidget(ISharpCanvas canvas, Rectangle rect, CancellationToken cancellationToken) {

			canvas.SaveState();

			DrawPoint? point = null;
			foreach (DrawOperation operation in operations) {
				if (operation is MoveOperation move) {
					DrawPoint movePoint = move.isRelative ? RelativePoint(rect, move.startPoint) : move.startPoint;
					if (move.isAbsolute) { point = movePoint; }
					else if (point != null) { point = point.Value + movePoint; }
					else { throw new SharpDrawingException(this, "No previous point for relative position."); }
					canvas.MoveTo(rect.X + point.Value.X, rect.Y + point.Value.Y);
				}
				else if (operation is LineOperation line) {
					DrawPoint linePoint = line.isRelative ? RelativePoint(rect, line.endPoint) : line.endPoint;
					if (line.isAbsolute) { point = linePoint; }
					else if (point != null) { point = point.Value + linePoint; }
					else { throw new SharpDrawingException(this, "No previous point for relative position."); }
					canvas.LineTo(rect.X + point.Value.X, rect.Y + point.Value.Y);
				}
				else if (operation is HorizontalOperation horizontal) {
					if (point != null) {
						float horiz = horizontal.isRelative ? rect.Width * horizontal.x : horizontal.x;
						if (horizontal.isAbsolute) { point = new DrawPoint(horiz, point.Value.Y); }
						else { point = new DrawPoint(point.Value.X + horiz, point.Value.Y); }
						canvas.LineTo(rect.X + point.Value.X, rect.Y + point.Value.Y);
					}
					else { throw new SharpDrawingException(this, "No previous point for relative position."); }
				}
				else if (operation is VerticalOperation vertical) {
					if (point != null) {
						float vert = vertical.isRelative ? rect.Height * vertical.y : vertical.y;
						if (vertical.isAbsolute) { point = new DrawPoint(point.Value.X, vert); }
						else { point = new DrawPoint(point.Value.X, point.Value.Y + vert); }
						canvas.LineTo(rect.X + point.Value.X, rect.Y + point.Value.Y);
					}
					else { throw new SharpDrawingException(this, "No previous point for relative position."); }
				}
				else if (operation is CubicOperation cubic) {
					DrawPoint control1 = cubic.isRelative ? RelativePoint(rect, cubic.controlPoint1) : cubic.controlPoint1;
					DrawPoint control2 = cubic.isRelative ? RelativePoint(rect, cubic.controlPoint2) : cubic.controlPoint2;
					DrawPoint cEnd = cubic.isRelative ? RelativePoint(rect, cubic.endPoint) : cubic.endPoint;
					if (cubic.isAbsolute) {
						//control1 = control1;
						//control2 = control2;
						point = cEnd;
					}
					else if (point != null) {
						control1 = point.Value + control1;
						control2 = point.Value + control2;
						point = point.Value + cEnd;
					}
					else { throw new SharpDrawingException(this, "No previous point for relative position."); }
					canvas.CurveTo(rect.X + control1.X, rect.Y + control1.Y, rect.X + control2.X, rect.Y + control2.Y, rect.X + point.Value.X, rect.Y + point.Value.Y);
				}
				else if (operation is QuadraticOperation quadratic) {
					DrawPoint control = quadratic.isRelative ? RelativePoint(rect, quadratic.controlPoint) : quadratic.controlPoint;
					DrawPoint qEnd = quadratic.isRelative ? RelativePoint(rect, quadratic.endPoint) : quadratic.endPoint;
					if (quadratic.isAbsolute) {
						//control = control;
						point = qEnd;
					}
					else if (point != null) {
						control = point.Value + control;
						point = point.Value + qEnd;
					}
					else { throw new SharpDrawingException(this, "No previous point for relative position."); }
					canvas.CurveTo(rect.X + control.X, rect.Y + control.Y, rect.X + point.Value.X, rect.Y + point.Value.Y);
				}
				else if (operation is ArcOperation arc) {
					if (point != null) {
						//DrawPoint start = point.Value;
						DrawPoint arcPoint = arc.isRelative ? RelativePoint(rect, arc.endPoint) : arc.endPoint;
						if (arc.isAbsolute) { point = arcPoint; }
						else { point = point.Value + arcPoint; }
						canvas.ArcTo(rect.X + point.Value.X, rect.Y + point.Value.Y, arc.radius, arc.largeArc, arc.sweep);
					}
					else {
						throw new SharpDrawingException(this, "No previous point for relative position.");
					}
				}
				else if (operation is CloseOperation close) {
					canvas.ClosePath();
					point = null;
				}
				else if (operation is EndOperation end) {
					canvas.EndPath();
					point = null;
				}
				else if (operation is FillOperation fill) {
					canvas.Fill();
					point = null;
				}
				else if (operation is StrokeOperation stroke) {
					canvas.Stroke();
					point = null;
				}
				else if (operation is LineWidthOperation lineWidth) {
					canvas.SetLineWidth(lineWidth.width);
				}
			}

			if (point != null) {
				canvas.EndPath();
				point = null;
			}

			canvas.RestoreState();
		}

		protected override Rectangle GetContainerArea(ISharpGraphicsState graphicsState, Rectangle rect) {
			if (frame.HasValue) {
				return rect.Margins(frame.Value, false);
			}
			else if (remaining.HasValue) {
				return remaining.Value.GetFrom(rect);
			}
			else {
				return rect;
			}
		}
	}
	*/
}