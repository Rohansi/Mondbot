extern alias CoreCompatSystemDrawing;

using System;
using System.Drawing;

using Brush = CoreCompatSystemDrawing::System.Drawing.Brush;
using Graphics = CoreCompatSystemDrawing::System.Drawing.Graphics;
using Pen = CoreCompatSystemDrawing::System.Drawing.Pen;

using CoreCompatSystemDrawing::System.Drawing.Drawing2D;

namespace MondBot.Slave
{
    internal static class GraphicsExtensions
    {
        public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, RectangleF bounds, float cornerRadius)
        {
            if (graphics == null)
                throw new ArgumentNullException(nameof(graphics));
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));

            using (var path = RoundedRect(bounds, cornerRadius))
            {
                graphics.DrawPath(pen, path);
            }
        }

        public static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float cornerRadius)
        {
            if (graphics == null)
                throw new ArgumentNullException(nameof(graphics));
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));

            using (GraphicsPath path = RoundedRect(bounds, cornerRadius))
            {
                graphics.FillPath(brush, path);
            }
        }

        private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            var diameter = radius * 2;
            var size = new SizeF(diameter, diameter);
            var arc = new RectangleF(bounds.Location, size);
            var path = new GraphicsPath();

            if (radius <= 0.01)
            {
                path.AddRectangle(bounds);
                return path;
            }

            // top left arc  
            path.AddArc(arc, 180, 90);

            // top right arc  
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // bottom right arc  
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // bottom left arc 
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
