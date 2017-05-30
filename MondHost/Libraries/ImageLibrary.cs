using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using Mond;
using Mond.Binding;
using Mond.Libraries;

namespace MondHost.Libraries
{
    [MondClass("Color")]
    class MondColor
    {
        public Color Color { get; }

        [MondConstructor]
        public MondColor(int r, int g, int b) => Color = Color.FromArgb(r, g, b);

        [MondConstructor]
        public MondColor(int r, int g, int b, int a) => Color = Color.FromArgb(a, r, g, b);

        [MondFunction]
        public int Red => Color.R;

        [MondFunction]
        public int Green => Color.G;

        [MondFunction]
        public int Blue => Color.B;

        [MondFunction]
        public int Alpha => Color.A;

        [MondFunction("__serialize")]
        public MondValue Serialize(MondState state, params MondValue[] args)
        {
            return new MondValue(state)
            {
                ["$ctor"] = "Color",
                ["$args"] = new MondValue(MondValueType.Array)
                {
                    Array =
                    {
                        Red, Green, Blue, Alpha
                    }
                }
            };
        }
    }

    [MondModule("Image")]
    static class ImageModule
    {
        private static LineCap _lineCap;
        private static int _rotation;

        [MondFunction]
        public static int Width => BitmapWidth;

        [MondFunction]
        public static int Height => BitmapHeight;

        [MondFunction]
        public static string Cap
        {
            get => _lineCap.ToString();
            set => _lineCap = ParseLineCap(value);
        }

        [MondFunction]
        public static int Rotation
        {
            get => _rotation;
            set
            {
                Graphics.ResetTransform();
                Graphics.RotateTransform(value);
                _rotation = value;
            }
        }

        [MondFunction]
        public static float OriginX { get; set; }

        [MondFunction]
        public static float OriginY { get; set; }

        [MondFunction("clear")]
        public static void Clear(MondColor color) => Graphics.Clear(color.Color);

        [MondFunction("drawLine")]
        public static void DrawLine(float x1, float y1, float x2, float y2, MondColor color, float thickness = 1)
        {
            Graphics.ResetTransform(); // lines dont get rotation
            Graphics.DrawLine(CreatePen(color, thickness), x1, y1, x2, y2);
        }

        [MondFunction("drawArc")]
        public static void DrawArc(float x, float y, float width, float height, float startAngle, float sweepAngle, MondColor color, float thickness = 1) =>
            Graphics.DrawArc(CreatePen(color, thickness), RotateAround(x, y, width, height), startAngle, sweepAngle);

        [MondFunction("drawRectangle")]
        public static void DrawRectangle(float x, float y, float width, float height, MondColor color, float thickness = 1) =>
            Graphics.DrawRectangles(CreatePen(color, thickness), new [] { RotateAround(x, y, width, height) });

        [MondFunction("drawRoundedRectangle")]
        public static void DrawRoundedRectangle(float x, float y, float width, float height, float radius, MondColor color, float thickness = 1) =>
            Graphics.DrawRoundedRectangle(CreatePen(color, thickness), RotateAround(x, y, width, height), radius);

        [MondFunction("drawEllipse")]
        public static void DrawEllipse(float x, float y, float width, float height, MondColor color, float thickness = 1) =>
            Graphics.DrawEllipse(CreatePen(color, thickness), RotateAround(x, y, width, height));

        [MondFunction("fillRectangle")]
        public static void FillRectangle(float x, float y, float width, float height, MondColor color) =>
            Graphics.FillRectangle(CreateBrush(color), RotateAround(x, y, width, height));

        [MondFunction("fillRoundedRectangle")]
        public static void FillRoundedRectangle(float x, float y, float width, float height, float radius, MondColor color) =>
            Graphics.FillRoundedRectangle(CreateBrush(color), RotateAround(x, y, width, height), radius);

        [MondFunction("fillEllipse")]
        public static void FillEllipse(float x, float y, float width, float height, MondColor color) =>
            Graphics.FillEllipse(CreateBrush(color), RotateAround(x, y, width, height));

        [MondFunction("drawString")]
        public static void DrawString(string str, float x, float y, MondColor color, int size = 32)
        {
            var font = GetFont(size);
            var dimensions = Graphics.MeasureString(str, font);
            Graphics.DrawString(str, font, CreateBrush(color), RotateAroundPoint(x, y, dimensions));
        }

        [MondFunction("measureString")]
        public static MondValue MeasureString(string str, int size = 32)
        {
            var dimensions = Graphics.MeasureString(str, GetFont(size));
            return new MondValue(MondValueType.Array) { Array = { dimensions.Width, dimensions.Height } };
        }

        #region Helpers
        private static Pen _pen;
        private static Brush _brush;

        private static Pen CreatePen(MondColor color, float thickness = 1)
        {
            if (_pen != null)
            {
                _pen.Dispose();
                _pen = null;
            }

            _pen = new Pen(color.Color, thickness);
            _pen.SetLineCap(_lineCap, _lineCap, DashCap.Flat);
            return _pen;
        }

        private static Brush CreateBrush(MondColor color)
        {
            if (_brush != null)
            {
                _brush.Dispose();
                _brush = null;
            }

            _brush = new SolidBrush(color.Color);
            return _brush;
        }

        private static LineCap ParseLineCap(string value)
        {
            if (!Enum.TryParse<LineCap>(value, true, out var newCap) || newCap == LineCap.Custom)
                newCap = LineCap.Flat;

            return newCap;
        }

        private static PointF RotateAroundPoint(float x, float y, SizeF dimensions)
        {
            var width = dimensions.Width;
            var height = dimensions.Height;

            return RotateAround(x, y, width, height).Location;
        }

        private static RectangleF RotateAround(float x, float y, float width, float height)
        {
            if (_rotation != 0)
            {
                Graphics.ResetTransform();
                Graphics.TranslateTransform(x, y);
                Graphics.RotateTransform(_rotation);
                Graphics.TranslateTransform(-x, -y);
            }

            return new RectangleF(x - width * OriginX, y - height * OriginY, width, height);
        }
        #endregion

        #region Fonts
        private static FontFamily _fontFamily;
        private static Dictionary<int, Font> _fonts = new Dictionary<int, Font>();

        static ImageModule()
        {
            /*var fontCollection = new PrivateFontCollection();
            fontCollection.AddFontFile("OpenSans-Regular.ttf");*/
            _fontFamily = new FontFamily(GenericFontFamilies.SansSerif); //fontCollection.Families[0];
        }

        private static Font GetFont(int size)
        {
            if (size < 8)
                size = 8;

            if (size > 128)
                size = 128;

            if (_fonts.TryGetValue(size, out var cachedFont))
                return cachedFont;

            var newFont = new Font(_fontFamily, size, FontStyle.Regular, GraphicsUnit.Pixel);
            _fonts.Add(size, newFont);
            return newFont;
        }
        #endregion

        #region Bitmap
        private const int BitmapWidth = 400;
        private const int BitmapHeight = 400;

        private static Bitmap _bitmap;
        private static Graphics _graphics;

        private static Graphics Graphics => _graphics ?? (_graphics = CreateGraphics());

        private static Graphics CreateGraphics()
        {
            if (_bitmap != null || _graphics != null)
                throw new InvalidOperationException("Image state was not reset.");

            _bitmap = new Bitmap(BitmapWidth, BitmapHeight);
            var graphics = Graphics.FromImage(_bitmap);
            graphics.Clear(Color.White);
            return graphics;
        }

        public static byte[] GetImageData()
        {
            _lineCap = LineCap.Flat;
            _rotation = 0;
            OriginX = 0;
            OriginY = 0;

            if (_bitmap == null)
                return new byte[0];

            // dispose of the graphics instance
            if (_graphics != null)
            {
                _graphics.Dispose();
                _graphics = null;
            }

            // export the image to an in-memory PNG
            var ms = new MemoryStream(64 * 1024);
            _bitmap.Save(ms, ImageFormat.Png);

            // get rid of the image
            _bitmap.Dispose();
            _bitmap = null;

            return ms.ToArray();
        }
        #endregion
    }

    class ImageLibraries : IMondLibraryCollection
    {
        public IEnumerable<IMondLibrary> Create(MondState state)
        {
            yield return new ImageLibrary(state);
        }
    }

    class ImageLibrary : IMondLibrary
    {
        private MondState State { get; }

        public ImageLibrary(MondState state)
        {
            State = state;
        }

        public IEnumerable<KeyValuePair<string, MondValue>> GetDefinitions()
        {
            var colorClass = MondClassBinder.Bind<MondColor>(State);
            yield return new KeyValuePair<string, MondValue>("Color", colorClass);

            var imageModule = MondModuleBinder.Bind(typeof(ImageModule), State);
            yield return new KeyValuePair<string, MondValue>("Image", imageModule);
        }
    }
}
