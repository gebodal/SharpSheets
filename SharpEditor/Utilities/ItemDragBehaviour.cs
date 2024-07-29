using System;
using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Transformation;
using Avalonia.Xaml.Interactivity;

// Based on: https://github.com/AvaloniaUI/Avalonia.Xaml.Behaviors/blob/d62d2aa68e3943b26b8f33cc120f2e8cc707e038/src/Avalonia.Xaml.Interactions.Draggable/ItemDragBehavior.cs

namespace SharpEditor.Utilities {

	/// <summary>
	/// 
	/// </summary>
	public class ItemDragBehavior : Behavior<Control> {
		private bool _enableDrag;
		private bool _dragStarted;
		private Point _start;
		private int _draggedIndex;
		private int _targetIndex;
		private ItemsControl? _itemsControl;
		private Control? _draggedContainer;
		private bool _captured;

		/// <summary>
		/// 
		/// </summary>
		public static readonly StyledProperty<Orientation> OrientationProperty =
			AvaloniaProperty.Register<ItemDragBehavior, Orientation>(nameof(Orientation));

		/// <summary>
		/// 
		/// </summary>
		public static readonly StyledProperty<double> HorizontalDragThresholdProperty =
			AvaloniaProperty.Register<ItemDragBehavior, double>(nameof(HorizontalDragThreshold), 3);

		/// <summary>
		/// 
		/// </summary>
		public static readonly StyledProperty<double> VerticalDragThresholdProperty =
			AvaloniaProperty.Register<ItemDragBehavior, double>(nameof(VerticalDragThreshold), 3);

		/// <summary>
		/// 
		/// </summary>
		public Orientation Orientation {
			get => GetValue(OrientationProperty);
			set => SetValue(OrientationProperty, value);
		}

		/// <summary>
		/// 
		/// </summary>
		public double HorizontalDragThreshold {
			get => GetValue(HorizontalDragThresholdProperty);
			set => SetValue(HorizontalDragThresholdProperty, value);
		}

		/// <summary>
		/// 
		/// </summary>
		public double VerticalDragThreshold {
			get => GetValue(VerticalDragThresholdProperty);
			set => SetValue(VerticalDragThresholdProperty, value);
		}

		/// <inheritdoc />
		protected override void OnAttachedToVisualTree() {
			if (AssociatedObject is not null) {
				AssociatedObject.AddHandler(InputElement.PointerReleasedEvent, PointerReleased, RoutingStrategies.Tunnel);
				AssociatedObject.AddHandler(InputElement.PointerPressedEvent, PointerPressed, RoutingStrategies.Tunnel);
				AssociatedObject.AddHandler(InputElement.PointerMovedEvent, PointerMoved, RoutingStrategies.Tunnel);
				AssociatedObject.AddHandler(InputElement.PointerCaptureLostEvent, PointerCaptureLost, RoutingStrategies.Tunnel);
			}
		}

		/// <inheritdoc />
		protected override void OnDetachedFromVisualTree() {
			if (AssociatedObject is not null) {
				AssociatedObject.RemoveHandler(InputElement.PointerReleasedEvent, PointerReleased);
				AssociatedObject.RemoveHandler(InputElement.PointerPressedEvent, PointerPressed);
				AssociatedObject.RemoveHandler(InputElement.PointerMovedEvent, PointerMoved);
				AssociatedObject.RemoveHandler(InputElement.PointerCaptureLostEvent, PointerCaptureLost);
			}
		}

		private void PointerPressed(object? sender, PointerPressedEventArgs e) {
			PointerPointProperties properties = e.GetCurrentPoint(AssociatedObject).Properties;
			if (properties.IsLeftButtonPressed && AssociatedObject?.Parent is ItemsControl itemsControl) {
				_enableDrag = true;
				_dragStarted = false;
				_start = e.GetPosition(itemsControl);
				_draggedIndex = -1;
				_targetIndex = -1;
				_itemsControl = itemsControl;
				_draggedContainer = AssociatedObject;

				if (_draggedContainer is not null) {
					SetDraggingPseudoClasses(_draggedContainer, true);
				}

				AddTransforms(_itemsControl);

				_captured = true;
			}
		}

		private void PointerReleased(object? sender, PointerReleasedEventArgs e) {
			if (_captured) {
				if (e.InitialPressMouseButton == MouseButton.Left) {
					Released();
				}

				_captured = false;
			}
		}

		private void PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) {
			Released();
			_captured = false;
		}

		private void Released() {
			if (!_enableDrag) {
				return;
			}

			RemoveTransforms(_itemsControl);

			if (_itemsControl is not null) {
				foreach (Control control in _itemsControl.GetRealizedContainers()) {
					SetDraggingPseudoClasses(control, true);
				}
			}

			if (_dragStarted) {
				if (_draggedIndex >= 0 && _targetIndex >= 0 && _draggedIndex != _targetIndex) {
					MoveDraggedItem(_itemsControl, _draggedIndex, _targetIndex);
				}
			}

			if (_itemsControl is not null) {
				foreach (Control control in _itemsControl.GetRealizedContainers()) {
					SetDraggingPseudoClasses(control, false);
				}
			}

			if (_draggedContainer is not null) {
				SetDraggingPseudoClasses(_draggedContainer, false);
			}

			_draggedIndex = -1;
			_targetIndex = -1;
			_enableDrag = false;
			_dragStarted = false;
			_itemsControl = null;

			_draggedContainer = null;
		}

		private void PointerMoved(object? sender, PointerEventArgs e) {
			PointerPointProperties properties = e.GetCurrentPoint(AssociatedObject).Properties;
			if (_captured && properties.IsLeftButtonPressed) {
				if (_itemsControl?.Items is null || _draggedContainer?.RenderTransform is null || !_enableDrag) {
					return;
				}

				Orientation orientation = Orientation;

				Point position = e.GetPosition(_itemsControl); // Pointer position relative to _itemsControl
				Vector delta = position - _start; // Change in position from drag start to current pos

				// If drag has not actually started yet, check if we've travelled sufficiently far to start it
				if (!_dragStarted) {
					double horizontalDragThreshold = HorizontalDragThreshold;
					double verticalDragThreshold = VerticalDragThreshold;

					_dragStarted = orientation == Orientation.Horizontal
						? Math.Abs(delta.X) > horizontalDragThreshold
						: Math.Abs(delta.Y) > verticalDragThreshold;

					if (!_dragStarted) {
						// Not travelled far enough, so return
						return;
					}
				}

				//Rect wholeAreaBounds = _itemsControl.Bounds;

				Vector draggedTransform = orientation == Orientation.Horizontal
					? new Vector(delta.X, 0)
					: new Vector(0, delta.Y);

				_draggedIndex = _itemsControl.IndexFromContainer(_draggedContainer); // Index of the item being dragged
				_targetIndex = -1; // Reset target index

				Rect draggedBounds = _draggedContainer.Bounds;

				double draggedStart = orientation == Orientation.Horizontal ? draggedBounds.X : draggedBounds.Y; // Top/left of the dragged item

				// Start (left/top) of dragged item bounds after drag move
				double draggedDeltaStart = orientation == Orientation.Horizontal
					? draggedBounds.X + delta.X
					: draggedBounds.Y + delta.Y;

				// End (right/bottom) of dragged item bounds after drag move
				double draggedDeltaEnd = orientation == Orientation.Horizontal
					? draggedBounds.X + delta.X + draggedBounds.Width
					: draggedBounds.Y + delta.Y + draggedBounds.Height;

				double draggedMidOrthog = orientation == Orientation.Horizontal
					? draggedBounds.Y + draggedBounds.Height / 2
					: draggedBounds.X + draggedBounds.Width / 2;
				double draggedDeltaMidOrthog = orientation == Orientation.Horizontal
					? draggedBounds.Y + delta.Y + draggedBounds.Height / 2
					: draggedBounds.X + delta.X + draggedBounds.Width / 2;

				double? targetRankMid = null;
				int t = 0;
				foreach (object? _ in _itemsControl.Items) {
					Control? targetContainer = _itemsControl.ContainerFromIndex(t);
					if (targetContainer?.RenderTransform is null || ReferenceEquals(targetContainer, _draggedContainer)) {
						t++;
						continue;
					}

					Rect targetBounds = targetContainer.Bounds;

					double targetMidOrthog = orientation == Orientation.Horizontal
						? targetBounds.Y + targetBounds.Height / 2
						: targetBounds.X + targetBounds.Width / 2;

					bool targetAndDraggedDeltaSameRank = orientation == Orientation.Horizontal
						? targetBounds.Top <= draggedDeltaMidOrthog && draggedDeltaMidOrthog <= targetBounds.Bottom
						: targetBounds.Left <= draggedDeltaMidOrthog && draggedDeltaMidOrthog <= targetBounds.Right;

					if (targetAndDraggedDeltaSameRank) {
						targetRankMid = targetMidOrthog;
						break;
					}
					else if(orientation == Orientation.Horizontal) {
						if(position.Y <= targetBounds.Top && (targetRankMid is null || targetRankMid.Value > targetMidOrthog)) {
							targetRankMid = targetMidOrthog;
						}
						else if(position.Y >= targetBounds.Bottom && (targetRankMid is null || targetRankMid.Value < targetMidOrthog)) {
							targetRankMid = targetMidOrthog;
						}
					}
					else {
						if (position.X <= targetBounds.Left && (targetRankMid is null || targetRankMid.Value > targetMidOrthog)) {
							targetRankMid = targetMidOrthog;
						}
						else if (position.X >= targetBounds.Right && (targetRankMid is null || targetRankMid.Value < targetMidOrthog)) {
							targetRankMid = targetMidOrthog;
						}
					}

					t++;
				}

				int i = 0;
				foreach (object? _ in _itemsControl.Items) {
					// For each container in the itemsControl
					Control? targetContainer = _itemsControl.ContainerFromIndex(i);
					if (targetContainer?.RenderTransform is null || ReferenceEquals(targetContainer, _draggedContainer)) {
						i++;
						continue;
					}
					// If container has been setup for translation, and is not the dragged container

					Rect targetBounds = targetContainer.Bounds;

					bool targetAndDraggedSameRank = orientation == Orientation.Horizontal
						? targetBounds.Top < draggedMidOrthog && draggedMidOrthog < targetBounds.Bottom
						: targetBounds.Left < draggedMidOrthog && draggedMidOrthog < targetBounds.Right;
					
					bool targetAndDraggedDeltaSameRank;
					if (targetRankMid.HasValue) {
						targetAndDraggedDeltaSameRank = orientation == Orientation.Horizontal
							? targetBounds.Top < targetRankMid.Value && targetRankMid.Value < targetBounds.Bottom
							: targetBounds.Left < targetRankMid.Value && targetRankMid.Value < targetBounds.Right;
					}
					else { // Fallback, should not actually be used...?
						targetAndDraggedDeltaSameRank = orientation == Orientation.Horizontal
							? targetBounds.Top < draggedDeltaMidOrthog && draggedDeltaMidOrthog < targetBounds.Bottom
							: targetBounds.Left < draggedDeltaMidOrthog && draggedDeltaMidOrthog < targetBounds.Right;
					}

					double targetStart = orientation == Orientation.Horizontal ? targetBounds.X : targetBounds.Y; // Top/left of the potential target item

					// Mid-point of the potential target container
					double targetMid = orientation == Orientation.Horizontal
						? targetBounds.X + targetBounds.Width / 2
						: targetBounds.Y + targetBounds.Height / 2;

					// Index of the potential target container
					int targetIndex = _itemsControl.IndexFromContainer(targetContainer);

					// If dragged is on a different rank to its original and we're on the target rank
					// and dragged current start is "before" potential target mid-point
					if (targetAndDraggedDeltaSameRank && !targetAndDraggedSameRank && draggedDeltaStart <= targetMid) {
						if (orientation == Orientation.Horizontal) {
							SetTranslateTransform(targetContainer, draggedBounds.Width, 0); // Shift right
						}
						else {
							SetTranslateTransform(targetContainer, 0, draggedBounds.Height); // Shift down
						}

						if (_targetIndex == -1 || targetIndex < _targetIndex) {
							_targetIndex = targetIndex;
						}

						draggedTransform = orientation == Orientation.Horizontal
							? new Vector(draggedTransform.X, targetBounds.Y - draggedBounds.Y)
							: new Vector(targetBounds.X - draggedBounds.X, draggedTransform.Y);
					}
					// If dragged is on a different rank to its original and we're on the target rank
					// and potential target mid-point is "before" dragged current end
					else if (targetAndDraggedDeltaSameRank && !targetAndDraggedSameRank && targetMid <= draggedDeltaEnd) {
						SetTranslateTransform(targetContainer, 0, 0); // Reset translation to zero

						if (_targetIndex == -1 || targetIndex > _targetIndex) {
							_targetIndex = targetIndex;
						}
						
						draggedTransform = orientation == Orientation.Horizontal
							? new Vector(draggedTransform.X, targetBounds.Y - draggedBounds.Y)
							: new Vector(targetBounds.X - draggedBounds.X, draggedTransform.Y);
					}
					// If dragged is still on its original rank
					// and dragged original location is "before" potential target
					// and potential target mid-point is "before" dragged current end
					else if (targetAndDraggedDeltaSameRank && targetAndDraggedSameRank && draggedStart < targetStart && targetMid <= draggedDeltaEnd) {
						if (orientation == Orientation.Horizontal) {
							SetTranslateTransform(targetContainer, -draggedBounds.Width, 0); // Shift left
						}
						else {
							SetTranslateTransform(targetContainer, 0, -draggedBounds.Height); // Shift up
						}

						// If no target selected yet
						// or if this target has a higher index than the currently registered target
						if(_targetIndex == -1 || targetIndex > _targetIndex) {
							_targetIndex = targetIndex;
						}
					}
					// If dragged is still on its original rank
					// and potential target start is "before" draggable original start
					// and dragged current start is "before" potential target mid-point
					else if (targetAndDraggedDeltaSameRank && targetAndDraggedSameRank && targetStart < draggedStart && draggedDeltaStart <= targetMid) {
						if (orientation == Orientation.Horizontal) {
							SetTranslateTransform(targetContainer, draggedBounds.Width, 0); // Shift right
						}
						else {
							SetTranslateTransform(targetContainer, 0, draggedBounds.Height); // Shift down
						}

						if(_targetIndex == -1 || targetIndex < _targetIndex) {
							_targetIndex = targetIndex;
						}
					}
					// If dragged is on a different rank to its original and we're on the original rank
					// and dragged original location is "before" potential target
					else if (!targetAndDraggedDeltaSameRank && targetAndDraggedSameRank && draggedStart < targetStart) {
						if (orientation == Orientation.Horizontal) {
							SetTranslateTransform(targetContainer, -draggedBounds.Width, 0); // Shift left
						}
						else {
							SetTranslateTransform(targetContainer, 0, -draggedBounds.Height); // Shift up
						}
					}
					else {
						// Otherwise, reset transform
						SetTranslateTransform(targetContainer, 0, 0);
					}

					i++;
				}

				SetTranslateTransform(_draggedContainer, draggedTransform);
			}
		}

		private static void AddTransforms(ItemsControl? itemsControl) {
			if (itemsControl?.Items is null) {
				return;
			}

			int i = 0;

			foreach (object? _ in itemsControl.Items) {
				Control? container = itemsControl.ContainerFromIndex(i);
				if (container is not null) {
					SetTranslateTransform(container, 0, 0);
				}

				i++;
			}
		}

		private static void RemoveTransforms(ItemsControl? itemsControl) {
			if (itemsControl?.Items is null) {
				return;
			}

			int i = 0;

			foreach (object? _ in itemsControl.Items) {
				Control? container = itemsControl.ContainerFromIndex(i);
				if (container is not null) {
					SetTranslateTransform(container, 0, 0);
				}

				i++;
			}
		}

		private static void MoveDraggedItem(ItemsControl? itemsControl, int draggedIndex, int targetIndex) {
			if (itemsControl?.ItemsSource is IList itemsSource) {
				object? draggedItem = itemsSource[draggedIndex];
				itemsSource.RemoveAt(draggedIndex);
				itemsSource.Insert(targetIndex, draggedItem);

				if (itemsControl is SelectingItemsControl selectingItemsControl) {
					selectingItemsControl.SelectedIndex = targetIndex;
				}
			}
			else if (itemsControl?.Items is { IsReadOnly: false } itemCollection) {
				object? draggedItem = itemCollection[draggedIndex];
				itemCollection.RemoveAt(draggedIndex);
				itemCollection.Insert(targetIndex, draggedItem);

				if (itemsControl is SelectingItemsControl selectingItemsControl) {
					selectingItemsControl.SelectedIndex = targetIndex;
				}
			}
		}

		private static void SetDraggingPseudoClasses(Control control, bool isDragging) {
			if (isDragging) {
				((IPseudoClasses)control.Classes).Add(":dragging");
			}
			else {
				((IPseudoClasses)control.Classes).Remove(":dragging");
			}
		}

		private static void SetTranslateTransform(Control control, double x, double y) {
			TransformOperations.Builder transformBuilder = new TransformOperations.Builder(1);
			transformBuilder.AppendTranslate(x, y);
			control.RenderTransform = transformBuilder.Build();
		}

		private static void SetTranslateTransform(Control control, Vector v) {
			SetTranslateTransform(control, v.X, v.Y);
		}
	}
}