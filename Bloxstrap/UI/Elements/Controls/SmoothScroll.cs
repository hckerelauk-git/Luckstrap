using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace Bloxstrap.UI.Elements.Controls
{
    /// <summary>
    /// 滚轮平滑滚动。WPF 默认一次滚三行，设置页的行高一百多像素，一下就跳几百像素，看着发飘。
    /// 这里换成固定步长加 110ms 插值，滚动距离稳定，也不会一次翻过去半页。
    /// </summary>
    public static class SmoothScroll
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(SmoothScroll),
                new PropertyMetadata(false, OnIsEnabledChanged));

        const double Step = 120;
        const int DurationMs = 110;

        static readonly Dictionary<ScrollViewer, DispatcherTimer> _timers = new();

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ScrollViewer viewer)
                return;

            viewer.PreviewMouseWheel -= OnPreviewMouseWheel;
            viewer.Unloaded -= OnUnloaded;

            if (!(bool)e.NewValue)
                return;

            viewer.PreviewMouseWheel += OnPreviewMouseWheel;
            viewer.Unloaded += OnUnloaded;
        }

        static void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ScrollViewer viewer)
                return;

            if (_timers.TryGetValue(viewer, out DispatcherTimer? timer))
            {
                timer.Stop();
                _timers.Remove(viewer);
            }
        }

        static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer viewer)
                return;

            if (viewer.ScrollableHeight <= 0)
                return;

            // 鼠标停在下拉列表、日志框这类内部可滚动控件上时，交给它自己处理
            if (HasInnerScrollable(e.OriginalSource as DependencyObject, viewer))
                return;

            e.Handled = true;

            double target = viewer.VerticalOffset + (e.Delta > 0 ? -Step : Step);
            target = Math.Max(0, Math.Min(viewer.ScrollableHeight, target));

            AnimateTo(viewer, target);
        }

        static bool HasInnerScrollable(DependencyObject? source, ScrollViewer outer)
        {
            // 鼠标落在文字上时 OriginalSource 是 Run 这类 FrameworkContentElement，
            // 不是 Visual，直接丢给 VisualTreeHelper 会抛 InvalidOperationException。
            // 先沿逻辑树走到最近的 Visual，再往上爬
            while (source is not null && source is not Visual && source is not Visual3D)
                source = source is FrameworkContentElement element ? element.Parent : LogicalTreeHelper.GetParent(source);

            while (source is not null && !ReferenceEquals(source, outer))
            {
                if (source is ScrollViewer inner && inner.ScrollableHeight > 0)
                    return true;

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        static void AnimateTo(ScrollViewer viewer, double target)
        {
            if (_timers.TryGetValue(viewer, out DispatcherTimer? running))
                running.Stop();

            double start = viewer.VerticalOffset;
            double distance = target - start;

            if (Math.Abs(distance) < 0.5)
                return;

            Stopwatch watch = Stopwatch.StartNew();

            DispatcherTimer timer = new(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };

            timer.Tick += (_, _) =>
            {
                double progress = Math.Min(1, watch.ElapsedMilliseconds / (double)DurationMs);
                double eased = 1 - Math.Pow(1 - progress, 3);

                viewer.ScrollToVerticalOffset(start + distance * eased);

                if (progress < 1)
                    return;

                timer.Stop();
                _timers.Remove(viewer);
            };

            _timers[viewer] = timer;
            timer.Start();
        }
    }
}
