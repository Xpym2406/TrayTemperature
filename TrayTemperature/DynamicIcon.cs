using System;
using System.Drawing;
using System.Drawing.Text;

namespace TrayTemperature
{
    class DynamicIcon
    {
        // Кэшируем шрифт для избежания постоянного создания
        private static readonly Font CachedFont = new Font("Consolas", 7);

        // Создает 16x16 иконку с 2 строками текста
        public static Icon CreateIcon(string Line1Text, Color Line1Color, string Line2Text, Color Line2Color)
        {
            Font font = CachedFont;
            Bitmap bitmap = new Bitmap(16, 16);

            Graphics graph = Graphics.FromImage(bitmap);

            //Draw the temperatures
            graph.DrawString(Line1Text, font, new SolidBrush(Line1Color), new PointF(-1, -3));
            graph.DrawString(Line2Text, font, new SolidBrush(Line2Color), new PointF(-1, 7));
            graph.Dispose();
            return Icon.FromHandle(bitmap.GetHicon());
        }

        // Метод для освобождения ресурсов
        public static void Dispose()
        {
            CachedFont?.Dispose();
        }
    }
}
