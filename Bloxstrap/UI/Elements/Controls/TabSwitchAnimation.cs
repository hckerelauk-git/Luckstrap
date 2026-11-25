using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Bloxstrap.UI.Elements.Controls
{
    /// <summary>
    /// 给 TabControl 切换加一段淡入。只用 Opacity，走合成层，不重新测量布局。
    /// </summary>
    public static class TabSwitchAnimation
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(TabSwitchAnimation),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TabControl tab)
                return;

            tab.SelectionChanged -= OnSelectionChanged;

            if ((bool)e.NewValue)
                tab.SelectionChanged += OnSelectionChanged;
        }

        static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not TabControl tab)
                return;

            // 模板里的内容宿主。切换时它会被换掉内容，这里让它淡入并轻轻上移
            if (tab.Template?.FindName("PART_SelectedContentHost", tab) is not ContentPresenter host)
                return;

            // 140ms 太快，肉眼基本看不出来，这里拉到 220ms 并加 8px 位移
            const int DurationMs = 220;

            CubicEase ease = new() { EasingMode = EasingMode.EaseOut };

            DoubleAnimation fade = new()
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(DurationMs),
                EasingFunction = ease
            };

            DoubleAnimation slide = new()
            {
                From = 8,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(DurationMs),
                EasingFunction = ease
            };

            if (host.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                host.RenderTransform = transform;
            }

            host.BeginAnimation(UIElement.OpacityProperty, fade);
            transform.BeginAnimation(TranslateTransform.YProperty, slide);
        }
    }
}
