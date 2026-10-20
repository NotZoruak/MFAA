using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using MFAAvalonia.Views.UserControls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MFAAvalonia.Features.CombatStrategies.Controls;

/// <summary>在当前列表内捕获手柄拖动，并显示与任务列表一致的插入位置。</summary>
internal static class StrategyReorder
{
    public static void Attach<T>(Control handle, Control row, StackPanel container, IList<T> values, T value,
        Action changed, Orientation orientation = Orientation.Vertical)
    {
        Point start = default;
        bool pressed = false, dragging = false;
        int previewIndex = -1;
        DragAdorner? adorner = null;
        var originalTransforms = new Dictionary<Control, ITransform?>();
        var originalZIndex = row.ZIndex;

        handle.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;
            start = e.GetPosition(container);
            pressed = true;
            handle.Focus();
            e.Pointer.Capture(handle);
            e.Handled = true;
        };
        handle.PointerMoved += (_, e) =>
        {
            if (!pressed) return;
            var point = e.GetPosition(container);
            var delta = point - start;
            if (!dragging && Math.Abs(delta.X) + Math.Abs(delta.Y) < 6) return;
            if (!dragging)
            {
                dragging = true;
                row.Classes.Add("strategy-dragging");
                row.ZIndex = 10;
            }

            row.Opacity = 0.82;
            var from = values.IndexOf(value);
            var target = CalculateDropIndex(container.Children.OfType<Control>().Select(x => x.Bounds).ToArray(), from,
                orientation == Orientation.Horizontal ? point.X : point.Y, orientation);
            if (target != previewIndex)
            {
                previewIndex = target;
                UpdatePreviewTransforms(from, target, delta);
            }
            else
            {
                SetTranslation(row, orientation == Orientation.Horizontal ? delta.X : 0,
                    orientation == Orientation.Vertical ? delta.Y : 0, animate: false);
            }

            var scroll = container.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
            if (scroll != null)
            {
                var scrollPoint = e.GetPosition(scroll);
                if (orientation == Orientation.Vertical)
                {
                    if (scrollPoint.Y < 35) scroll.Offset = new Vector(scroll.Offset.X, Math.Max(0, scroll.Offset.Y - 18));
                    if (scrollPoint.Y > scroll.Bounds.Height - 35) scroll.Offset = new Vector(scroll.Offset.X, scroll.Offset.Y + 18);
                }
                else
                {
                    if (scrollPoint.X < 35) scroll.Offset = new Vector(Math.Max(0, scroll.Offset.X - 18), scroll.Offset.Y);
                    if (scrollPoint.X > scroll.Bounds.Width - 35) scroll.Offset = new Vector(scroll.Offset.X + 18, scroll.Offset.Y);
                }
            }
            UpdateAdorner(from, target);
            e.Handled = true;
        };
        handle.PointerReleased += (_, e) =>
        {
            if (!pressed) return;
            var from = values.IndexOf(value);
            var point = e.GetPosition(container);
            var moved = dragging;
            var target = moved
                ? CalculateDropIndex(container.Children.OfType<Control>().Select(x => x.Bounds).ToArray(), from,
                    orientation == Orientation.Horizontal ? point.X : point.Y, orientation)
                : from;
            pressed = false;
            ClearDragVisuals();
            e.Pointer.Capture(null);
            if (moved && values.Count > 1) Move(from, target);
            e.Handled = moved;
        };
        handle.PointerCaptureLost += (_, _) =>
        {
            pressed = false;
            ClearDragVisuals();
        };
        handle.KeyDown += (_, e) =>
        {
            var from = values.IndexOf(value);
            var step = orientation == Orientation.Horizontal
                ? e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0
                : e.Key == Key.Up ? -1 : e.Key == Key.Down ? 1 : 0;
            if (step == 0) return;
            Move(from, from + step);
            e.Handled = true;
        };

        void UpdatePreviewTransforms(int from, int target, Vector pointerDelta)
        {
            var controls = container.Children.OfType<Control>().ToList();
            var sourceSize = orientation == Orientation.Horizontal ? row.Bounds.Width : row.Bounds.Height;
            var gap = container.Spacing;
            for (var i = 0; i < controls.Count; i++)
            {
                var control = controls[i];
                if (ReferenceEquals(control, row)) continue;
                var offset = 0d;
                if (from < target && i > from && i <= target)
                    offset = -(sourceSize + gap);
                else if (from > target && i >= target && i < from)
                    offset = sourceSize + gap;

                SetTranslation(control,
                    orientation == Orientation.Horizontal ? offset : 0,
                    orientation == Orientation.Vertical ? offset : 0);
            }

            SetTranslation(row,
                orientation == Orientation.Horizontal ? pointerDelta.X : 0,
                orientation == Orientation.Vertical ? pointerDelta.Y : 0, animate: false);
        }

        void UpdateAdorner(int from, int target)
        {
            var layer = AdornerLayer.GetAdornerLayer(container);
            if (layer == null) return;
            var controls = container.Children.OfType<Control>().Where(x => !ReferenceEquals(x, row)).ToList();
            if (controls.Count == 0) return;
            var isBegin = target <= 0;
            var isEnd = target >= controls.Count;
            var anchor = isEnd ? controls[^1] : controls[Math.Clamp(target, 0, controls.Count - 1)];
            var insertAfter = isEnd;
            var origin = container.TranslatePoint(new Point(anchor.Bounds.X, anchor.Bounds.Y), layer) ?? default;
            if (insertAfter)
            {
                origin += orientation == Orientation.Horizontal
                    ? new Vector(anchor.Bounds.Width, 0)
                    : new Vector(0, anchor.Bounds.Height);
            }

            adorner ??= new DragAdorner(0, 0, Brushes.Transparent);
            if (!layer.Children.Contains(adorner)) layer.Children.Add(adorner);
            if (orientation == Orientation.Horizontal)
                adorner.UpdateVerticalPosition(origin.X, origin.Y, anchor.Bounds.Height, isBegin, isEnd);
            else
            {
                adorner.UpdateXPosition(origin.X);
                adorner.UpdateWidth(anchor.Bounds.Width);
                adorner.UpdatePosition(origin.Y, isBegin, isEnd);
            }
        }

        void SetTranslation(Control control, double x, double y, bool animate = true)
        {
            if (!originalTransforms.ContainsKey(control))
                originalTransforms[control] = control.RenderTransform;
            if (control.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                control.RenderTransform = transform;
            }
            transform.Transitions = animate ? CreateTransitions() : null;
            transform.X = x;
            transform.Y = y;
        }

        void ClearDragVisuals()
        {
            dragging = false;
            previewIndex = -1;
            row.Opacity = 1;
            row.Classes.Remove("strategy-dragging");
            row.ZIndex = originalZIndex;
            foreach (var (control, transform) in originalTransforms)
                control.RenderTransform = transform;
            originalTransforms.Clear();
            if (adorner != null)
            {
                AdornerLayer.GetAdornerLayer(container)?.Children.Remove(adorner);
                adorner = null;
            }
        }

        void Move(int from, int to)
        {
            if (from < 0 || to < 0 || to >= values.Count || from == to) return;
            var controls = container.Children.OfType<Control>().ToList();
            var controlIndex = container.Children.IndexOf(row);
            if (controlIndex < 0) return;
            var before = controls.ToDictionary(x => x, x => x.TranslatePoint(new Point(0, 0), container) ?? default);
            values.RemoveAt(from);
            values.Insert(to, value);
            container.Children.RemoveAt(controlIndex);
            container.Children.Insert(Math.Clamp(to, 0, container.Children.Count), row);
            container.UpdateLayout();
            foreach (var (control, position) in before)
            {
                var current = control.TranslatePoint(new Point(0, 0), container);
                if (current == null) continue;
                var delta = position - current.Value;
                if (Math.Abs(delta.X) < 0.5 && Math.Abs(delta.Y) < 0.5) continue;
                var transform = new TranslateTransform(delta.X, delta.Y) { Transitions = CreateTransitions() };
                control.RenderTransform = transform;
                transform.X = 0;
                transform.Y = 0;
            }
            changed();
        }
    }

    internal static int CalculateDropIndex(IReadOnlyList<Rect> rows, int from, double y) =>
        CalculateDropIndex(rows, from, y, Orientation.Vertical);

    internal static int CalculateDropIndex(IReadOnlyList<Rect> rows, int from, double position, Orientation orientation)
    {
        if (rows.Count == 0) return -1;
        var index = rows.Count;
        for (var i = 0; i < rows.Count; i++)
        {
            var center = orientation == Orientation.Horizontal ? rows[i].Center.X : rows[i].Center.Y;
            if (position < center) { index = i; break; }
        }
        if (from >= 0 && from < index) index--;
        return Math.Clamp(index, 0, rows.Count - 1);
    }

    private static Transitions CreateTransitions() =>
    [
        new DoubleTransition { Property = TranslateTransform.XProperty, Duration = TimeSpan.FromMilliseconds(180), Easing = new SineEaseOut() },
        new DoubleTransition { Property = TranslateTransform.YProperty, Duration = TimeSpan.FromMilliseconds(180), Easing = new SineEaseOut() }
    ];
}
