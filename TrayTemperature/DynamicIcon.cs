using System;
using System.Drawing;
using System.Drawing.Text;

namespace TrayTemperature
{
    class DynamicIcon
    {
        private static readonly Font CachedFont = new Font("Consolas", 7);

        public static Icon CreateIcon(string Line1Text, Color Line1Color, string Line2Text, Color Line2Color)
        {
            using (var bitmap = new Bitmap(16, 16))
            using (var graph = Graphics.FromImage(bitmap))
            {
                graph.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

                using (var brush1 = new SolidBrush(Line1Color))
                using (var brush2 = new SolidBrush(Line2Color))
                {
                    graph.DrawString(Line1Text, CachedFont, brush1, new PointF(-1, -3));
                    graph.DrawString(Line2Text, CachedFont, brush2, new PointF(-1, 7));
                }

                var hIcon = bitmap.GetHicon();
                var icon = Icon.FromHandle(hIcon);
                return (Icon)icon.Clone();
            }
        }

        public static void Dispose()
        {
            CachedFont?.Dispose();
        }
    }
}