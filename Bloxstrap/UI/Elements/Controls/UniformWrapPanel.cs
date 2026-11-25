using System;
using System.Windows;
using System.Windows.Controls;

namespace Bloxstrap.UI.Elements.Controls
{
    /// <summary>
    /// 按可用宽度均分每行的卡片，宽度不再写死。
    /// 之前用 WrapPanel + 固定 196 宽，容器宽度不是 196 的整数倍时右边会空出一大块，
    /// 差一点放不下第 4 个就整行只剩 3 个，看起来空得厉害。
    /// </summary>
    public class UniformWrapPanel : Panel
    {
        public static readonly DependencyProperty MinItemWidthProperty =
            DependencyProperty.Register(nameof(MinItemWidth), typeof(double), typeof(UniformWrapPanel),
                new FrameworkPropertyMetadata(180d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty ItemSpacingProperty =
            DependencyProperty.Register(nameof(ItemSpacing), typeof(double), typeof(UniformWrapPanel),
                new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double MinItemWidth
        {
            get => (double)GetValue(MinItemWidthProperty);
            set => SetValue(MinItemWidthProperty, value);
        }

        public double ItemSpacing
        {
            get => (double)GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            int count = InternalChildren.Count;

            if (count == 0)
                return new Size(0, 0);

            double width = availableSize.Width;

            if (double.IsInfinity(width) || double.IsNaN(width))
                width = MinItemWidth;

            int columns = GetColumns(width, count);
            double itemWidth = GetItemWidth(width, columns);

            double totalHeight = 0;
            double rowHeight = 0;

            for (int i = 0; i < count; i++)
            {
                UIElement child = InternalChildren[i];
                child.Measure(new Size(itemWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);

                if (IsRowEnd(i, columns, count))
                {
                    totalHeight += rowHeight;

                    if (i != count - 1)
                        totalHeight += ItemSpacing;

                    rowHeight = 0;
                }
            }

            return new Size(width, totalHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int count = InternalChildren.Count;

            if (count == 0)
                return finalSize;

            int columns = GetColumns(finalSize.Width, count);
            double itemWidth = GetItemWidth(finalSize.Width, columns);

            double y = 0;
            double rowHeight = 0;
            int rowStart = 0;

            for (int i = 0; i < count; i++)
            {
                rowHeight = Math.Max(rowHeight, InternalChildren[i].DesiredSize.Height);

                if (!IsRowEnd(i, columns, count))
                    continue;

                for (int j = rowStart; j <= i; j++)
                    InternalChildren[j].Arrange(new Rect((j - rowStart) * (itemWidth + ItemSpacing), y, itemWidth, rowHeight));

                y += rowHeight + ItemSpacing;
                rowHeight = 0;
                rowStart = i + 1;
            }

            return finalSize;
        }

        static bool IsRowEnd(int index, int columns, int count) => (index + 1) % columns == 0 || index == count - 1;

        int GetColumns(double width, int count)
        {
            double step = MinItemWidth + ItemSpacing;
            int columns = (int)Math.Floor((width + ItemSpacing) / step);
            return Math.Max(1, Math.Min(columns, count));
        }

        double GetItemWidth(double width, int columns) => (width - (columns - 1) * ItemSpacing) / columns;
    }
}
