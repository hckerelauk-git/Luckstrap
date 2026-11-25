using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Bloxstrap.UI.Elements.Controls
{
    /// <summary>
    /// Interaction logic for OptionControl.xaml
    /// </summary
    [ContentProperty(nameof(InnerContent))]
    public partial class OptionControl : UserControl
    {
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(OptionControl),
                new PropertyMetadata(null, OnHeaderChanged));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(OptionControl),
                new PropertyMetadata(null, OnDescriptionChanged));

        // 这里原来是在 XAML 里写 {Binding Header, ElementName=Control}。
        // 但 CardControl.Header 的内容在模板里，ElementName 解析不到根元素，
        // 结果是 Header 明明有值，卡片上却一个字都不显示。
        // 同一个文件里 Content 的绑定是好的，所以开关能出来，只有文字是空的。
        static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((OptionControl)d).ApplyContent();

        static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((OptionControl)d).ApplyContent();

        public static readonly DependencyProperty HelpLinkProperty =
            DependencyProperty.Register(nameof(HelpLink), typeof(string), typeof(OptionControl));

        public static readonly DependencyProperty InnerContentProperty =
            DependencyProperty.Register(nameof(InnerContent), typeof(object), typeof(OptionControl));

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        public string HelpLink
        {
            get { return (string)GetValue(HelpLinkProperty); }
            set { SetValue(HelpLinkProperty, value); }
        }

        public object InnerContent
        {
            get { return GetValue(InnerContentProperty); }
            set { SetValue(InnerContentProperty, value); }
        }

        public OptionControl()
        {
            InitializeComponent();

            // XAML 里写 Header="..." 时，CardControl.Header 里的元素可能还没连上字段，
            // 回调里拿到的是 null。这里在加载完成后再补一次，保证文字一定出来
            Loaded += (_, _) =>
            {
                ApplyContent();
                RefreshGroup();
            };
        }

        /// <summary>
        /// 同一段纵向排列的项接成一张卡。并排两列的项（父级是 Grid）保持独立。
        /// 每次都把整段重算一遍，谁先加载都不会算错。
        /// </summary>
        void RefreshGroup()
        {
            if (Parent is not StackPanel panel)
                return;

            int index = panel.Children.IndexOf(this);

            if (index < 0)
                return;

            int start = index;

            while (start > 0 && panel.Children[start - 1] is OptionControl)
                start--;

            int end = index;

            while (end < panel.Children.Count - 1 && panel.Children[end + 1] is OptionControl)
                end++;

            for (int i = start; i <= end; i++)
            {
                if (panel.Children[i] is not OptionControl item)
                    continue;

                bool isFirst = i == start;
                bool isLast = i == end;

                item.RootGrid.Margin = new Thickness(0, isFirst ? 8 : 0, 0, 0);
                item.CardBorder.CornerRadius = new CornerRadius(
                    isFirst ? 8 : 0, isFirst ? 8 : 0,
                    isLast ? 8 : 0, isLast ? 8 : 0);
                item.Divider.Visibility = isLast ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        void ApplyContent()
        {
            if (HeaderText is not null)
                HeaderText.Text = Header ?? String.Empty;

            if (DescriptionText is not null)
            {
                string text = Description ?? String.Empty;
                DescriptionText.MarkdownText = text;
                DescriptionText.Visibility = String.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }
}
