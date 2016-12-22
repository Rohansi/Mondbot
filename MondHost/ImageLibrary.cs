using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using Mond;
using Mond.Binding;
using Mond.Libraries;

namespace MondHost
{
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

    [MondModule("Http")]
    static class ImageModule
    {
        #region Fonts
        private static FontFamily _fontFamily;
        private static Dictionary<int, Font> _fonts = new Dictionary<int, Font>();

        static ImageModule()
        {
            var fontCollection = new PrivateFontCollection();
            fontCollection.AddFontFile("OpenSans-Regular.ttf");
            _fontFamily = fontCollection.Families[0];
        }

        private static Font GetFont(int size)
        {
            Font cachedFont;
            if (_fonts.TryGetValue(size, out cachedFont))
                return cachedFont;

            var newFont = new Font(_fontFamily, size, FontStyle.Regular, GraphicsUnit.Pixel);
            _fonts.Add(size, newFont);
            return newFont;
        }
        #endregion

        #region Bitmap
        private const int BitmapWidth = 480;
        private const int BitmapHeight = 640;

        private static Bitmap _bitmap;
        private static Graphics _graphics;

        private static Bitmap Bitmap => _bitmap ?? (_bitmap = new Bitmap(BitmapWidth, BitmapHeight));
        private static Graphics Graphics => _graphics ?? (_graphics = Graphics.FromImage(Bitmap));

        public static byte[] GetImageData()
        {
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

        [MondFunction]
        public static int Width => BitmapWidth;

        [MondFunction]
        public static int Height => BitmapHeight;

        [MondFunction("clear")]
        public static void Clear(MondColor color)
        {
            Graphics.Clear(color.Color);
        }

        [MondFunction("drawLine")]
        public static void DrawLine(float x1, float y1, float x2, float y2, MondColor color, float thickness = 1)
        {
            Graphics.DrawLine(new Pen(color.Color, thickness), x1, y1, x2, y2);
        }

        [MondFunction("drawString")]
        public static void DrawString(string str, float x, float y, MondColor color, int size = 32)
        {
            Graphics.DrawString(str, GetFont(size), new SolidBrush(color.Color), x, y);
        }

        [MondFunction("drawRectangle")]
        public static void DrawRectangle(float x, float y, float width, float height, MondColor color, float thickness = 1)
        {
            Graphics.DrawRectangle(new Pen(color.Color, thickness), x, y, width, height);
        }

        [MondFunction("drawEllipse")]
        public static void DrawEllipse(float x, float y, float width, float height, MondColor color, float thickness = 1)
        {
            Graphics.DrawEllipse(new Pen(color.Color, thickness), x, y, width, height);
        }

        [MondFunction("fillRectangle")]
        public static void FillRectangle(float x, float y, float width, float height, MondColor color)
        {
            Graphics.FillRectangle(new SolidBrush(color.Color), x, y, width, height);
        }

        [MondFunction("fillEllipse")]
        public static void FillEllipse(float x, float y, float width, float height, MondColor color)
        {
            Graphics.FillEllipse(new SolidBrush(color.Color), x, y, width, height);
        }
    }

    [MondClass("Color")]
    class MondColor
    {
        public Color Color { get; }

        [MondConstructor]
        public MondColor(int r, int g, int b)
        {
            Color = Color.FromArgb(r, g, b);
        }

        [MondConstructor]
        public MondColor(int r, int g, int b, int a)
        {
            Color = Color.FromArgb(r, g, b, a);
        }

        [MondFunction]
        public int Red => Color.R;

        [MondFunction]
        public int Green => Color.G;

        [MondFunction]
        public int Blue => Color.B;

        [MondFunction]
        public int Alpha => Color.A;

        [MondFunction("__serialize")]
        public MondValue Serialize(MondState state)
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
}
