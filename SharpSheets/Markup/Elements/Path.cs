using SharpSheets.Canvas;
using SharpSheets.Evaluations;
using SharpSheets.Exceptions;
using SharpSheets.Markup.Canvas;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharpSheets.Markup.Elements {

	/// <summary>
	/// A path element draws a shape by combining multiple straight or curved lines in
	/// a series of drawing operations, that may then filled and/or stroked to create
	/// complex shapes. The path data is provided as a series of drawing instructions
	/// (represented by an upper- or lower-case letter) with zero or more associated
	/// argument values.
	/// </summary>
	public class Path : ShapeElement {

		private readonly DrawOperation[] data; // d

		/// <summary>
		/// Constructor for Path.
		/// </summary>
		/// <param name="_id" default="null">A unique name for this element.</param>
		/// <param name="styleSheet">StyleSheet for this element.</param>
		/// <param name="_d" default="null">The path data, as a series of drawing instructions
		/// and arguments.</param>
		public Path(StyleSheet styleSheet, string? _id = null, DrawOperation[]? _d = null) : base(_id, styleSheet) {
			this.data = _d ?? Array.Empty<DrawOperation>();
		}

		protected override void DoAssignGeometry(MarkupCanvas canvas) {
			foreach(DrawOperation operation in data) {
				if (operation is MoveOperation move) {
					canvas.MoveTo(move.startPoint);
				}
				else if (operation is LineOperation line) {
					canvas.LineTo(line.endPoint);
				}
				else if (operation is CubicOperation cubic) {
					canvas.CurveTo(cubic.controlPoint1, cubic.controlPoint2, cubic.endPoint);
				}
				else if (operation is QuadraticOperation quadratic) {
					canvas.CurveTo(quadratic.controlPoint, quadratic.endPoint);
				}
				else if (operation is ArcOperation arc) {
					canvas.EllipseArc(arc.endPoint, arc.radiusX, arc.radiusY, arc.angle, arc.largeArc, arc.sweep);
				}
				else if (operation is CloseOperation) {
					canvas.ClosePath();
				}
			}
		}

		public override IPathCalculator? GetPath(MarkupCanvas canvas) {
			List<IPathCalculator> completePaths = new List<IPathCalculator>();
			List<IPathCalculator>? currentPathParts = null;

			DrawPoint? start = null;
			DrawPoint? previous = null;
			for(int i=0; i<data.Length; i++) {
				if (data[i] is MoveOperation move) {
					if(currentPathParts != null && currentPathParts.Count > 0 && CompositePathCalculator.Create(currentPathParts.ToArray(), false) is IPathCalculator partCalc) {
						completePaths.Add(partCalc);
					}

					start = canvas.TransformPoint(move.startPoint);
					previous = start;
					currentPathParts = new List<IPathCalculator>();
				}
				else if (data[i] is LineOperation line) {
					if (currentPathParts is null || !previous.HasValue) { continue; }

					DrawPoint end = canvas.TransformPoint(line.endPoint);
					currentPathParts.Add(new LinePathCalculator(previous.Value, end));
					previous = end;
				}
				else if (data[i] is CubicOperation cubic) {
					if (currentPathParts is null || !previous.HasValue) { continue; }

					DrawPoint s = previous.Value;
					DrawPoint c1 = canvas.TransformPoint(cubic.controlPoint1);
					DrawPoint c2 = canvas.TransformPoint(cubic.controlPoint2);
					DrawPoint e = canvas.TransformPoint(cubic.endPoint);

					currentPathParts.Add(LUTPathCalculator.Create((float t, out Vector v) => CubicOperation.GetPoint(s, c1, c2, e, t, out v), 100, false));

					previous = e;
				}
				else if (data[i] is QuadraticOperation quadratic) {
					if (currentPathParts is null || !previous.HasValue) { continue; }

					DrawPoint s = previous.Value;
					DrawPoint c = canvas.TransformPoint(quadratic.controlPoint);
					DrawPoint e = canvas.TransformPoint(quadratic.endPoint);

					currentPathParts.Add(LUTPathCalculator.Create((float t, out Vector v) => QuadraticOperation.GetPoint(s, c, e, t, out v), 100, false));

					previous = e;
				}
				else if (data[i] is ArcOperation arc) {
					if (currentPathParts is null || !previous.HasValue) { continue; }

					DrawPoint s = previous.Value;
					DrawPoint e = canvas.TransformPoint(arc.endPoint);
					float rx = canvas.Evaluate(arc.radiusX, 0f);
					float ry = canvas.Evaluate(arc.radiusY, 0f);
					float angle = canvas.Evaluate(arc.angle, 0f);
					bool largeArc = canvas.Evaluate(arc.largeArc, false);
					bool sweep = canvas.Evaluate(arc.sweep, false);

					currentPathParts.Add(EllipseArcCalculator.Create(s, e, rx, ry, angle, largeArc, sweep));

					previous = e;
				}
				else if (data[i] is CloseOperation) {
					if (currentPathParts != null) {
						if (previous.HasValue && start.HasValue) {
							currentPathParts.Add(new LinePathCalculator(previous.Value, start.Value));
						}
						start = null;
						previous = null;

						if (currentPathParts.Count > 0 && CompositePathCalculator.Create(currentPathParts.ToArray(), true) is IPathCalculator closingPartCalc) {
							completePaths.Add(closingPartCalc);
						}
					}
					currentPathParts = null;
				}
			}

			if (currentPathParts != null && currentPathParts.Count > 0 && CompositePathCalculator.Create(currentPathParts.ToArray(), false) is IPathCalculator finalPartCalc) {
				completePaths.Add(finalPartCalc);
			}

			if (completePaths.Count == 1) {
				return completePaths[0];
			}
			else {
				return CompositePathCalculator.Create(completePaths.ToArray(), false);
			}
		}

		private static readonly RegexChunker commandRegex = new RegexChunker(
			new Regex(@"
				(?<command>[MmLlHhVvCcSsQqTtAaZz])
				(?<values>
					(?:
						(?:\s*\,\s*|\s*)
						(?:
							\{(?:[^\{\}]|\\[\{\}])+\}
							|
							(
								[\-\+]?[0-9]+(?:\.[0-9]*)?
								|
								\.[0-9]+
							)
							([eE][\-\+]?[0-9]+)? # Exponent for standard notation
						)
					)*
				)
			", RegexOptions.IgnorePatternWhitespace),
			new Regex(@"\s*"), true);
		private static readonly RegexChunker valueRegex = new RegexChunker(
			new Regex(@"
				\{(?<expression>(?:[^\{\}]|\\[\{\}])+)\}
				|
				(?<number>
					(
						\-?[0-9]+(?:\.[0-9]*)?
						|
						\.[0-9]+
					)
					(e(?<exponent>[\-\+]?[0-9]+))?
				)
			", RegexOptions.IgnorePatternWhitespace),
			new Regex(@"\s*\,\s*|\s+"), true);

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		private static FloatExpression ParseValue(Match match, IVariableBox variables) {
			try {
				if (match.Groups["expression"].Success) {
					return FloatExpression.Parse(match.Groups["expression"].Value.Trim(), variables);
				}
				else {
					return new FloatExpression(float.Parse(match.Groups["number"].Value.Trim()));
				}
			}
			catch(EvaluationException e) {
				throw new FormatException("Invalid expression: " + e.Message, e);
			}
		}

		private static FloatExpression[]? ParseValues(Group valueGroup, IVariableBox variables, List<(int index, int length, Exception ex)> parseErrors) {
			List<FloatExpression> values = new List<FloatExpression>();

			int errorCount = 0;
			foreach (Match valueMatch in valueRegex.Matches(valueGroup.Value)) {
				try {
					FloatExpression value = ParseValue(valueMatch, variables);
					values.Add(value);
				}
				catch(FormatException e) {
					parseErrors.Add((valueGroup.Index + valueMatch.Index, valueMatch.Length, e));
					errorCount++;
				}
			}

			if (errorCount > 0) {
				return null;
			}
			else {
				return values.ToArray();
			}
		}

		public static Path Parse(string? id, StyleSheet styleSheet, string? data, IVariableBox variables, out (int index, int length, Exception ex)[] errors) {
			List<DrawOperation> operations = new List<DrawOperation>();
			List<(int index, int length, Exception ex)> parseErrors = new List<(int, int, Exception)>();

			if (data != null) {
				DrawPointExpression? currentStart = null;
				DrawPointExpression? currentPoint = null;

				foreach (Match commandMatch in commandRegex.Matches(data)) {
					try {
						char command = commandMatch.Groups["command"].Value[0];
						bool relative = char.IsLower(command);

						// TODO This needs to be updated
						FloatExpression[]? values = commandMatch.Groups["values"].Length > 0 ? ParseValues(commandMatch.Groups["values"], variables, parseErrors) : Array.Empty<FloatExpression>();

						if(values == null) {
							continue;
						}

						bool IsCommand(char letter) { return string.Equals(command.ToString(), letter.ToString(), StringComparison.InvariantCultureIgnoreCase); }

						if (IsCommand('M')) {
							DrawPointExpression[] points = GetDrawPoints(values);
							currentStart = MovePoint(currentStart, currentPoint, points[0], relative);
							operations.Add(new MoveOperation(currentStart));
							currentPoint = currentStart;
							for (int i = 1; i < points.Length; i++) {
								currentPoint = MovePoint(currentStart, currentPoint, points[i], relative);
								operations.Add(new LineOperation(currentPoint));
							}
						}
						else if (IsCommand('L')) {
							DrawPointExpression[] points = GetDrawPoints(values);
							currentPoint = MovePoint(currentStart, currentPoint, points[0], relative);
							currentStart ??= currentPoint;
							operations.Add(new LineOperation(currentPoint));
							for (int i = 1; i < points.Length; i++) {
								currentPoint = MovePoint(currentStart, currentPoint, points[i], relative);
								operations.Add(new LineOperation(currentPoint));
							}
						}
						else if (IsCommand('H')) {
							DrawPointExpression? referencePoint = currentPoint ?? currentStart;
							if(referencePoint is null) { throw new FormatException("No start location defined."); }
							currentPoint = new DrawPointExpression(relative ? referencePoint.X + values[0] : values[0], referencePoint.Y);
							currentStart ??= currentPoint;
							operations.Add(new LineOperation(currentPoint));
							for (int i = 1; i < values.Length; i++) {
								currentPoint = new DrawPointExpression(relative ? currentPoint.X + values[i] : values[i], currentPoint.Y);
								operations.Add(new LineOperation(currentPoint));
							}
						}
						else if (IsCommand('V')) {
							DrawPointExpression? referencePoint = currentPoint ?? currentStart;
							if (referencePoint is null) { throw new FormatException("No start location defined."); }
							currentPoint = new DrawPointExpression(referencePoint.X, relative ? referencePoint.Y + values[0] : values[0]);
							currentStart ??= currentPoint;
							operations.Add(new LineOperation(currentPoint));
							for (int i = 1; i < values.Length; i++) {
								currentPoint = new DrawPointExpression(currentPoint.X, relative ? currentPoint.Y + values[i] : values[i]);
								operations.Add(new LineOperation(currentPoint));
							}
						}
						else if (IsCommand('C')) {
							DrawPointExpression[] points = GetDrawPoints(values);
							if (points.Length % 3 != 0) {
								throw new FormatException($"Cubic bezier curve requires points to be a multiple of 3 (got {points.Length}).");
							}
							DrawPointExpression control1 = MovePoint(currentStart, currentPoint, points[0], relative);
							DrawPointExpression control2 = MovePoint(currentStart, currentPoint, points[1], relative);
							currentPoint = MovePoint(currentStart, currentPoint, points[2], relative);
							currentStart ??= currentPoint;
							operations.Add(new CubicOperation(control1, control2, currentPoint));
							for (int i = 3; i < points.Length; i += 3) {
								control1 = MovePoint(currentStart, currentPoint, points[i], relative);
								control2 = MovePoint(currentStart, currentPoint, points[i + 1], relative);
								currentPoint = MovePoint(currentStart, currentPoint, points[i + 2], relative);
								operations.Add(new CubicOperation(control1, control2, currentPoint));
							}
						}
						else if (IsCommand('S')) {
							DrawPointExpression? referencePoint = currentPoint ?? currentStart;
							if (referencePoint is null) { throw new FormatException("No start location defined."); }

							DrawPointExpression[] points = GetDrawPoints(values);

							if (points.Length % 2 != 0) {
								throw new FormatException($"Sequential bezier curves require points to be a multiple of 2 (got {points.Length}).");
							}

							DrawPointExpression control1 = referencePoint;
							if (operations.LastOrDefault() is CubicOperation previous) {
								control1 = Reflect(previous.controlPoint2, referencePoint);
							}

							DrawPointExpression control2 = MovePoint(currentStart, currentPoint, points[0], relative);
							currentPoint = MovePoint(currentStart, currentPoint, points[1], relative);
							currentStart ??= currentPoint;
							operations.Add(new CubicOperation(control1, control2, currentPoint));
							for (int i = 2; i < points.Length; i += 2) {
								control1 = Reflect(control2, currentPoint);
								control2 = MovePoint(currentStart, currentPoint, points[i], relative);
								currentPoint = MovePoint(currentStart, currentPoint, points[i + 1], relative);
								operations.Add(new CubicOperation(control1, control2, currentPoint));
							}
						}
						else if (IsCommand('Q')) {
							DrawPointExpression[] points = GetDrawPoints(values);
							if (points.Length % 2 != 0) {
								throw new FormatException($"Quadratic bezier curve requires points to be a multiple of 2 (got {points.Length}).");
							}
							DrawPointExpression control = MovePoint(currentStart, currentPoint, points[0], relative);
							currentPoint = MovePoint(currentStart, currentPoint, points[1], relative);
							currentStart ??= currentPoint;
							operations.Add(new QuadraticOperation(control, currentPoint));
							for (int i = 2; i < points.Length; i += 2) {
								control = MovePoint(currentStart, currentPoint, points[i], relative);
								currentPoint = MovePoint(currentStart, currentPoint, points[i + 1], relative);
								operations.Add(new QuadraticOperation(control, currentPoint));
							}
						}
						else if (IsCommand('T')) {
							DrawPointExpression? referencePoint = currentPoint ?? currentStart;
							if (referencePoint is null) { throw new FormatException("No start location defined."); }

							DrawPointExpression[] points = GetDrawPoints(values);

							DrawPointExpression control = referencePoint;
							if (operations.LastOrDefault() is QuadraticOperation previous) {
								control = Reflect(previous.controlPoint, referencePoint);
							}

							currentPoint = MovePoint(currentStart, currentPoint, points[0], relative);
							currentStart ??= currentPoint;
							operations.Add(new QuadraticOperation(control, currentPoint));
							for (int i = 1; i < points.Length; i++) {
								control = Reflect(control, currentPoint);
								currentPoint = MovePoint(currentStart, currentPoint, points[i], relative);
								operations.Add(new QuadraticOperation(control, currentPoint));
							}
						}
						else if (IsCommand('A')) {
							if (values.Length % 7 != 0) {
								throw new FormatException($"Arc paths require parameters to be a multiple of 7 (got {values.Length}).");
							}
							//DrawPointExpression startPoint = currentPoint;
							FloatExpression rx = values[0];
							FloatExpression ry = values[1];
							FloatExpression angle = values[2] * ((float)Math.PI / 180f);
							BoolExpression largeArc = BoolExpression.IsNonZero(values[3]);
							BoolExpression sweep = BoolExpression.IsNonZero(values[4]);
							currentPoint = MovePoint(currentStart, currentPoint, new DrawPointExpression(values[5], values[6]), relative);
							currentStart ??= currentPoint;
							operations.Add(new ArcOperation(rx, ry, angle, largeArc, sweep, currentPoint));
							for (int i = 7; i < values.Length; i += 7) {
								//startPoint = currentPoint;
								rx = values[i];
								ry = values[i + 1];
								angle = values[i + 2];
								largeArc = BoolExpression.IsNonZero(values[i + 3]);
								sweep = BoolExpression.IsNonZero(values[i + 4]);
								currentPoint = MovePoint(currentStart, currentPoint, new DrawPointExpression(values[i + 5], values[i + 6]), relative);
								operations.Add(new ArcOperation(rx, ry, angle, largeArc, sweep, currentPoint));
							}
						}
						else if (IsCommand('Z')) {
							if (values.Length != 0) {
								throw new FormatException("Close operation takes no arguments.");
							}
							else {
								operations.Add(new CloseOperation());
								currentPoint = currentStart;
								currentStart = null;
							}
						}
						else {
							throw new FormatException($"Unrecognised path command \"{command}\".");
						}
					}
					catch (EvaluationException e) {
						parseErrors.Add((commandMatch.Index, commandMatch.Length, e));
					}
					catch (FormatException e) {
						parseErrors.Add((commandMatch.Index, commandMatch.Length, e));
					}
					catch (IndexOutOfRangeException) {
						parseErrors.Add((commandMatch.Index, commandMatch.Length, new FormatException("Invalid value count.")));
					}
				}
			}

			errors = parseErrors.ToArray();
			return new Path(styleSheet, id, operations.ToArray());
		}

		private static DrawPointExpression MovePoint(DrawPointExpression? start, DrawPointExpression? current, DrawPointExpression move, bool relative) {
			if (relative && (current ?? start) is DrawPointExpression referencePoint) {
				return referencePoint + move;
			}
			else {
				return move;
			}
		}

		private static DrawPointExpression Reflect(DrawPointExpression original, DrawPointExpression about) {
			// TODO Verify this is correct
			return about + about - original;
		}

		/// <summary></summary>
		/// <exception cref="FormatException"></exception>
		/// <exception cref="EvaluationException"></exception>
		private static DrawPointExpression[] GetDrawPoints(FloatExpression[] values) {
			if (values.Length % 2 != 0) {
				throw new FormatException("Invalid number of points.");
			}
			DrawPointExpression[] points = new DrawPointExpression[values.Length / 2];
			for (int i = 0; i < values.Length; i += 2) {
				points[i / 2] = new DrawPointExpression(values[i], values[i + 1]);
			}
			return points;
		}

		public abstract class DrawOperation { }

		protected class MoveOperation : DrawOperation {
			public readonly DrawPointExpression startPoint;
			public MoveOperation(DrawPointExpression startPoint) {
				this.startPoint = startPoint;
			}
		}

		protected class LineOperation : DrawOperation {
			public readonly DrawPointExpression endPoint;
			public LineOperation(DrawPointExpression endPoint) {
				this.endPoint = endPoint;
			}
		}

		protected class CubicOperation : DrawOperation {
			public readonly DrawPointExpression controlPoint1;
			public readonly DrawPointExpression controlPoint2;
			public readonly DrawPointExpression endPoint;
			public CubicOperation(DrawPointExpression controlPoint1, DrawPointExpression controlPoint2, DrawPointExpression endPoint) {
				this.controlPoint1 = controlPoint1;
				this.controlPoint2 = controlPoint2;
				this.endPoint = endPoint;
			}

			public static DrawPoint GetPoint(DrawPoint s, DrawPoint c1, DrawPoint c2, DrawPoint e, float t, out Vector normal) {
				float ti = 1 - t;

				float x = ti * ti * ti * s.X + 3 * ti * ti * t * c1.X + 3 * ti * t * t * c2.X + t * t * t * e.X;
				float y = ti * ti * ti * s.Y + 3 * ti * ti * t * c1.Y + 3 * ti * t * t * c2.Y + t * t * t * e.Y;

				float xp = 3 * ti * ti * (c1.X - s.X) + 6 * ti * t * (c2.X - c1.X) + 3 * t * t * (e.X - c2.X);
				float yp = 3 * ti * ti * (c1.Y - s.Y) + 6 * ti * t * (c2.Y - c1.Y) + 3 * t * t * (e.Y - c2.Y);
				normal = new Vector(-yp, xp).Normal();

				return new DrawPoint(x, y);
			}
		}

		protected class QuadraticOperation : DrawOperation {
			public readonly DrawPointExpression controlPoint;
			public readonly DrawPointExpression endPoint;
			public QuadraticOperation(DrawPointExpression controlPoint, DrawPointExpression endPoint) {
				this.controlPoint = controlPoint;
				this.endPoint = endPoint;
			}

			public static DrawPoint GetPoint(DrawPoint s, DrawPoint c, DrawPoint e, float t, out Vector normal) {
				float ti = 1 - t;

				float x = ti * (ti * s.X + t * c.X) + t * (ti * c.X + t * e.X);
				float y = ti * (ti * s.Y + t * c.Y) + t * (ti * c.Y + t * e.Y);

				float xp = 2 * ti * (c.X - s.X) + 2 * t * (e.X - c.X);
				float yp = 2 * ti * (c.Y - s.Y) + 2 * t * (e.Y - c.Y);
				normal = new Vector(-yp, xp).Normal();

				return new DrawPoint(x, y);
			}
		}

		protected class ArcOperation : DrawOperation {
			public readonly FloatExpression radiusX;
			public readonly FloatExpression radiusY;
			public readonly FloatExpression angle;
			public readonly BoolExpression largeArc;
			public readonly BoolExpression sweep;
			public readonly DrawPointExpression endPoint;
			public ArcOperation(FloatExpression radiusX, FloatExpression radiusY, FloatExpression angle, BoolExpression largeArc, BoolExpression sweep, DrawPointExpression endPoint) {
				this.radiusX = radiusX;
				this.radiusY = radiusY;
				this.angle = angle;
				this.largeArc = largeArc;
				this.sweep = sweep;
				this.endPoint = endPoint;
			}
		}

		protected class CloseOperation : DrawOperation { }
	}

}