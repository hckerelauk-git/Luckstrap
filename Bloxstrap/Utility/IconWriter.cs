using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Bloxstrap.Utility
{
    /// <summary>
    /// 把下载下来的封面图写成 .ico。
    /// 源图不是正方形时先按等比裁中间一块，直接拉成正方形图标会变形。
    /// </summary>
    internal static class IconWriter
    {
        // 小尺寸用 BMP 条目，256 用 PNG。Windows 对小的 PNG 条目支持不稳，BMP 最保险
        private static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };

        public static void WriteSquare(byte[] imageData, string icoPath)
        {
            BitmapSource source = Decode(imageData);

            var frames = new List<(int Size, byte[] Data)>();

            foreach (int size in Sizes)
            {
                byte[] pixels = RenderBgra(source, size);
                frames.Add((size, size >= 256 ? EncodePng(pixels, size) : EncodeDib(pixels, size)));
            }

            using var stream = File.Create(icoPath);
            using var writer = new BinaryWriter(stream);

            writer.Write((short)0);              // reserved
            writer.Write((short)1);              // 1 = icon
            writer.Write((short)frames.Count);

            int offset = 6 + 16 * frames.Count;

            foreach (var frame in frames)
            {
                // 256 在这里写 0，是 ICO 格式的规定
                writer.Write((byte)(frame.Size >= 256 ? 0 : frame.Size));
                writer.Write((byte)(frame.Size >= 256 ? 0 : frame.Size));
                writer.Write((byte)0);           // 调色板颜色数
                writer.Write((byte)0);           // reserved
                writer.Write((short)1);          // planes
                writer.Write((short)32);         // bpp
                writer.Write(frame.Data.Length);
                writer.Write(offset);

                offset += frame.Data.Length;
            }

            foreach (var frame in frames)
                writer.Write(frame.Data);
        }

        private static BitmapSource Decode(byte[] data)
        {
            using var stream = new MemoryStream(data);

            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
                throw new InvalidDataException("图标数据里没有可用的图像");

            return decoder.Frames[0];
        }

        /// <summary>
        /// 缩放到 size×size，超出部分裁掉，保持原始比例
        /// </summary>
        private static byte[] RenderBgra(BitmapSource source, int size)
        {
            var visual = new DrawingVisual();

            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);

            using (DrawingContext context = visual.RenderOpen())
            {
                var brush = new ImageBrush(source)
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                context.DrawRectangle(brush, null, new Rect(0, 0, size, size));
            }

            var target = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);

            // Pbgra32 是预乘 alpha，ICO 要的是直通 alpha，转一次再取像素
            var converted = new FormatConvertedBitmap(target, PixelFormats.Bgra32, null, 0);

            byte[] pixels = new byte[size * size * 4];
            converted.CopyPixels(pixels, size * 4, 0);

            return pixels;
        }

        private static byte[] EncodePng(byte[] bgra, int size)
        {
            var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, bgra, size * 4);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = new MemoryStream();
            encoder.Save(stream);

            return stream.ToArray();
        }

        /// <summary>
        /// 32 位 BMP 条目：头 + 自下而上的 BGRA 像素 + 全零的 AND 掩码
        /// </summary>
        private static byte[] EncodeDib(byte[] bgra, int size)
        {
            int maskStride = ((size + 31) / 32) * 4;
            int maskSize = maskStride * size;

            using var stream = new MemoryStream(40 + size * size * 4 + maskSize);
            using var writer = new BinaryWriter(stream);

            writer.Write(40);                    // biSize
            writer.Write(size);                  // biWidth
            writer.Write(size * 2);              // biHeight，XOR 和 AND 各占一份
            writer.Write((short)1);              // biPlanes
            writer.Write((short)32);             // biBitCount
            writer.Write(0);                     // biCompression = BI_RGB
            writer.Write(size * size * 4);       // biSizeImage
            writer.Write(0);                     // biXPelsPerMeter
            writer.Write(0);                     // biYPelsPerMeter
            writer.Write(0);                     // biClrUsed
            writer.Write(0);                     // biClrImportant

            for (int y = size - 1; y >= 0; y--)
                writer.Write(bgra, y * size * 4, size * 4);

            writer.Write(new byte[maskSize]);

            return stream.ToArray();
        }
    }
}
