using System.Windows;
using System.Windows.Forms;

namespace Bloxstrap.UI.Utility
{
    public static class WindowScaling
    {
        // Screen.PrimaryScreen 在远程会话、切显示器的时候会是 null，
        // 取不到就按 100% 算，别让缩放系数直接抛空引用
        public static double ScaleFactor
        {
            get
            {
                var screen = Screen.PrimaryScreen;

                if (screen is null)
                    return 1.0;

                double width = SystemParameters.PrimaryScreenWidth;

                if (width <= 0)
                    return 1.0;

                return screen.Bounds.Width / width;
            }
        }

        public static int GetScaledNumber(int number)
        {
            return (int)Math.Ceiling(number * ScaleFactor);
        }

        public static System.Drawing.Size GetScaledSize(System.Drawing.Size size)
        {
            return new System.Drawing.Size(GetScaledNumber(size.Width), GetScaledNumber(size.Height));
        }

        public static System.Drawing.Point GetScaledPoint(System.Drawing.Point point)
        {
            return new System.Drawing.Point(GetScaledNumber(point.X), GetScaledNumber(point.Y));
        }

        public static Padding GetScaledPadding(Padding padding)
        {
            return new Padding(GetScaledNumber(padding.Left), GetScaledNumber(padding.Top), GetScaledNumber(padding.Right), GetScaledNumber(padding.Bottom));
        }
    }
}
