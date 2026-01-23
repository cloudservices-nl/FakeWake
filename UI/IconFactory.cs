using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FakeWake.UI
{
    public static class IconFactory
    {
        private const int IconSize = 32;

        public static Icon CreateActiveIcon()
        {
            return CreateIcon(Color.FromArgb(50, 205, 50), DrawCoffeeOverlay);
        }

        public static Icon CreateInactiveIcon()
        {
            return CreateIcon(Color.FromArgb(128, 128, 128), DrawSleepOverlay);
        }

        private static Icon CreateIcon(Color backgroundColor, Action<Graphics> drawOverlay)
        {
            var bitmap = new Bitmap(IconSize, IconSize);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (var bgBrush = new SolidBrush(backgroundColor))
                {
                    g.FillEllipse(bgBrush, 1, 1, 30, 30);
                }

                DrawBed(g);
                drawOverlay(g);
            }

            var hIcon = bitmap.GetHicon();
            return Icon.FromHandle(hIcon);
        }

        private static void DrawBed(Graphics g)
        {
            using (var bedBrush = new SolidBrush(Color.White))
            {
                // Bed frame
                g.FillRectangle(bedBrush, 4, 18, 18, 8);

                // Headboard
                g.FillRectangle(bedBrush, 4, 14, 4, 12);

                // Pillow
                g.FillEllipse(bedBrush, 6, 15, 6, 4);
            }
        }

        private static void DrawCoffeeOverlay(Graphics g)
        {
            using (var brush = new SolidBrush(Color.White))
            using (var pen = new Pen(Color.White, 2f))
            {
                // Coffee cup
                g.FillRectangle(brush, 22, 16, 6, 8);

                // Cup handle
                g.DrawArc(pen, 26, 17, 4, 5, -90, 180);

                // Steam lines
                g.DrawLine(pen, 24, 14, 24, 11);
                g.DrawLine(pen, 26, 13, 26, 10);
            }
        }

        private static void DrawSleepOverlay(Graphics g)
        {
            using (var font = new Font("Arial", 7, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            {
                g.DrawString("z", font, brush, 20, 14);
                g.DrawString("z", font, brush, 23, 8);
                g.DrawString("z", font, brush, 25, 2);
            }
        }
    }
}
