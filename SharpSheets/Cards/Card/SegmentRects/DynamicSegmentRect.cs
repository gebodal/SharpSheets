using SharpSheets.Layouts;
using SharpSheets.Widgets;
using SharpSheets.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpSheets.Canvas;
using SharpSheets.Canvas.Text;
using SharpSheets.Cards.Layouts;
using SharpSheets.Cards.CardConfigs;

namespace SharpSheets.Cards.Card.SegmentRects {

	public class DynamicSegmentRect : AbstractSegmentRect<DynamicCardSegmentConfig> {

		public override bool Splittable => base.Splittable && entries.Length > 0;

		public readonly WidgetCardRect[] entries;
		public float Gutter => Config.gutter;

		public bool EqualSizeFeatures => Config.equalSizeFeatures;
		public bool SpaceFeatures => Config.spaceFeatures;

		public DynamicSegmentRect(DynamicCardSegmentConfig config, ArrangementCollection<IWidget> outlines, WidgetCardRect[] entries, bool splittable) : base(config, outlines, splittable) {
			this.entries = entries;
		}

		private static readonly float tempRectHeight = 10000f;
		public override float CalculateMinimumHeight(ISharpGraphicsState graphicsState, float fontSize, ParagraphSpecification paragraphSpec, float width, CardQueryCache cache) {
			Rectangle? tempRect = GetOutline().RemainingRect(graphicsState, new Rectangle(width, tempRectHeight));

			if (tempRect == null) {
				throw new CardLayoutException("Could not get segment area rect.");
			}

			//Console.WriteLine($"DynamicSegmentRect.CalculateMinimumHeight: total {width}, remaining {tempRect.Width}, gutter {gutter}");

			float entriesHeight;
			if (EqualSizeFeatures) {
				entriesHeight = Divisions.CalculateTotalLength(entries.Select(e => cache.GetMinimumHeight(e, fontSize, paragraphSpec, tempRect.Width)).Max(), entries.Length, Gutter);
			}
			else {
				entriesHeight = Divisions.CalculateTotalLength(entries.Select(e => cache.GetMinimumHeight(e, fontSize, paragraphSpec, tempRect.Width)), Gutter);
			}

			float totalHeight = tempRectHeight - tempRect.Height + entriesHeight;

			return totalHeight;
		}

		public override void Draw(ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec) {
			IWidget outline = GetOutline();

			canvas.SaveState();
			canvas.SetTextSize(fontSize);
			outline.Draw(canvas, rect, default);
			Rectangle remainingRect = outline.RemainingRect(canvas, rect) ?? rect;
			canvas.RestoreState();

			//Console.WriteLine($"DynamicSegmentRect.Draw: total {rect.Width}, remaining {remainingRect.Width}, gutter {gutter}");

			canvas.RegisterAreas(Original ?? this, rect, null, Array.Empty<Rectangle>());
			//canvas.RegisterArea(Original ?? this, remainingRect);

			if (entries.Length > 0) {
				CardQueryCache tempCache = new CardQueryCache(canvas);

				float[] entryMinimumHeights = entries.Select(e => tempCache.GetMinimumHeight(e, fontSize, paragraphSpec, remainingRect.Width)).ToArray();

				Dimension[] rowHeights;
				if (EqualSizeFeatures) {
					Dimension rowHeight = Dimension.FromPoints(entryMinimumHeights.Max());
					rowHeights = ArrayUtils.MakeArray(entries.Length, rowHeight);
					//Console.WriteLine($"Using equal size features, row height = {rowHeight}");
				}
				else {
					rowHeights = entryMinimumHeights.Select(h => Dimension.FromPoints(h)).ToArray();
					//Console.WriteLine($"Using unequal size features, row heights = " + string.Join(", ", rowHeights.Select(h => h.ToString())));
				}

				if (SpaceFeatures) {
					for (int i = 0; i < rowHeights.Length; i++) {
						rowHeights[i] += Dimension.Single;
					}
				}

				Rectangle?[] entryRects = Divisions.Rows(remainingRect, rowHeights, Gutter, false, Arrangement.FRONT, LayoutOrder.FORWARD);

				for (int i = 0; i < entries.Length; i++) {
					if (entryRects[i] != null) {
						entries[i].Draw(canvas, entryRects[i]!, fontSize, paragraphSpec);
					}

					//canvas.SaveState();
					//canvas.SetLineWidth(0.4f).SetStrokeColor(Color.Purple).Rectangle(entryRects[i]).Stroke();
					//canvas.RestoreState();
				}
			}
		}

		public override IPartialCardSegmentRects Split(int parts) {
			if (Splittable) {
				return new DynamicSegmentRectPieces(this, parts);
			}
			else {
				throw new InvalidOperationException($"Cannot split a {nameof(DynamicSegmentRect)} with Splittable==false.");
			}
		}

		public class DynamicSegmentRectPieces : IPartialCardSegmentRects {
			public IFixedCardSegmentRect Parent { get { return original; } }
			private readonly DynamicSegmentRect original;
			public int Boxes { get; set; }

			public bool PenaliseSplit { get; } = true;

			private WidgetCardRect[]? remainingEntries;
			private int boxCount;

			public DynamicSegmentRectPieces(DynamicSegmentRect original, int boxes) {
				this.original = original;
				Boxes = boxes;
			}

			public void Reset() {
				remainingEntries = original.entries;
				boxCount = 0;
			}

			private class WithHeight {
				public WidgetCardRect Rect { get; }
				public float MinimumHeight { get; }
				public WithHeight(WidgetCardRect rect, float minimumHeight) {
					Rect = rect;
					MinimumHeight = minimumHeight;
				}
			}

			public IFixedCardSegmentRect? FromAvailableHeight(ISharpGraphicsState graphicsState, float availableHeight, float width, float fontSize, ParagraphSpecification paragraphSpec, CardQueryCache cache, out float resultingHeight) {

				boxCount++;

				if (boxCount > Boxes) {
					throw new CardLayoutException($"Too many requests for boxes for {nameof(DynamicSegmentRectPieces)} ({boxCount} > {Boxes})");
				}

				WidgetCardRect[]? boxEntries;

				if (boxCount < Boxes) {

					if (remainingEntries is null || remainingEntries.Length == 0 || availableHeight <= fontSize) {
						resultingHeight = 0f;
						return null;
					}

					Rectangle remainingRect;
					try {
						Rectangle availableRect = new Rectangle(width, availableHeight);
						remainingRect = original.outlines[boxCount - 1, Boxes].RemainingRect(graphicsState, availableRect) ?? availableRect;
					}
					catch (InvalidRectangleException) {
						resultingHeight = 0f;
						return null;
					}

					List<WithHeight> boxEntryRects = remainingEntries.Select(e => new WithHeight(e, cache.GetMinimumHeight(e, fontSize, paragraphSpec, remainingRect.Width))).ToList();
					List<WithHeight> remaining = new List<WithHeight>();

					float CalculateTotalHeight(List<WithHeight> entries) {
						float entriesHeight;
						if (original.EqualSizeFeatures) {
							#pragma warning disable GJT0001 // entries will always have Count>0
							entriesHeight = Divisions.CalculateTotalLength(entries.Select(w => w.MinimumHeight).Max(), entries.Count, original.Gutter);
							#pragma warning restore GJT0001 // Unhandled thrown exception from statement
						}
						else {
							entriesHeight = Divisions.CalculateTotalLength(entries.Select(w => w.MinimumHeight), original.Gutter);
						}
						return availableHeight - remainingRect.Height + entriesHeight;
					}

					while (boxEntryRects.Count > 0 && CalculateTotalHeight(boxEntryRects) > remainingRect.Height) {
						#pragma warning disable GJT0001 // entries will always have Count>0
						remaining.Add(boxEntryRects.Last());
						#pragma warning restore GJT0001 // Unhandled thrown exception from statement
						boxEntryRects.RemoveAt(boxEntryRects.Count - 1);
					}

					boxEntries = boxEntryRects.Select(w => w.Rect).ToArray();
					remainingEntries = remaining.Select(w => w.Rect).Reverse().ToArray();
				}
				else { // boxCount == parts -> This is the last available box, so everything must be put in here.
					boxEntries = remainingEntries;
					remainingEntries = null;
				}

				if (boxEntries is not null && boxEntries.Length > 0) {
					DynamicSegmentRect fixedBox = new DynamicSegmentRect(
						original.Config,
						original.outlines,
						boxEntries,
						false
						) { Original = original, PartIndex = boxCount - 1, PartsCount = Boxes };
					resultingHeight = cache.GetMinimumHeight(fixedBox, fontSize, paragraphSpec, width);
					return fixedBox;
				}
				else {
					resultingHeight = 0f;
					return null;
				}
			}

			public IPartialCardSegmentRects Clone() {
				return new DynamicSegmentRectPieces(original, Boxes);
			}
		}
	}

	public class WidgetCardRect : IFixedCardSegmentRect {

		public AbstractCardSegmentConfig? Config { get; } = null;
		public IFixedCardSegmentRect Original { get { return this; } }

		public readonly IWidget content;
		public bool Splittable { get { return false; } }
		public bool AcceptRemaining { get { return false; } }

		public WidgetCardRect(IWidget content) {
			this.content = content;
		}

		public virtual float CalculateMinimumHeight(ISharpGraphicsState graphicsState, float fontSize, ParagraphSpecification paragraphSpec, float width, CardQueryCache cache) {
			graphicsState.SaveState();
			graphicsState.SetTextSize(fontSize);

			//Console.WriteLine($"WidgetCardRect.CalculateMinimumHeight: total {width}, gutter {content.Gutter}, child count {content.Children.Count}");

			//Rectangle remainingRect = content.RemainingRect(canvas, new Rectangle(width, 10000f));
			//float totalHeight = 10000f - (remainingRect?.Height ?? 0f);

			Size minimumSize = content.MinimumSize(graphicsState, Layout.ROWS, new Size(width, 10000f));
			float totalHeight = minimumSize.Height;

			graphicsState.RestoreState();

			return totalHeight;
		}

		public virtual void Draw(ISharpCanvas canvas, Rectangle rect, float fontSize, ParagraphSpecification paragraphSpec) {
			canvas.RegisterAreas(this, rect, null, Array.Empty<Rectangle>());

			//Console.WriteLine($"WidgetCardRect.Draw: total {rect.Width}, gutter {content.Gutter}, child count {content.Children.Count}");

			canvas.SaveState();
			canvas.SetTextSize(fontSize);
			content.Draw(canvas, rect, default);
			canvas.RestoreState();

			//canvas.SaveState();
			//canvas.SetLineWidth(0.5f).SetStrokeColor(Color.Yellow).Rectangle(rect).Stroke();
			//canvas.RestoreState();
		}

		public IPartialCardSegmentRects Split(int parts) {
			throw new NotSupportedException("Cannot split SimpleCardRect.");
		}
	}

}