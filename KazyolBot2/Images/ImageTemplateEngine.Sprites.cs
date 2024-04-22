using KazyolBot2.Text.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KazyolBot2.Images;

public partial class ImageTemplateEngine {
    static void ParseRectProps(Dictionary<string, IValue> props, out int? width, out int? height, out float? originX, out float? originY) {
        width = default;
        height = default; 
        originX = default;
        originY = default;

        foreach (var (key, val) in props) {
            switch (key) {
                case "ш":
                    TemplateInterpreter.ToNumber(val, out var widthVal);
                    width = (int)widthVal.Value;
                    break;

                case "в":
                    TemplateInterpreter.ToNumber(val, out var heightVal);
                    height = (int)heightVal.Value;
                    break;

                case "р":
                    TemplateInterpreter.ToNumber(val, out var sizeVal);
                    width = (int)sizeVal.Value;
                    height = (int)sizeVal.Value;
                    break;

                case "ц":
                    TemplateInterpreter.ToNumber(val, out var originVal);
                    originX = (float)originVal.Value;
                    originY = (float)originVal.Value;
                    break;

                case "цх":
                    TemplateInterpreter.ToNumber(val, out var originXVal);
                    originX = (float)originXVal.Value;
                    break;

                case "цу":
                    TemplateInterpreter.ToNumber(val, out var originYVal);
                    originY = (float)originYVal.Value;
                    break;
            }
        }
    }

    public void DrawSprite(SavedImageData image, Dictionary<string, IValue> props) {
        Image img = default;

        if (image != null && !_imageCache.TryGetValue(image.Name, out img))
            _imageCache[image.Name] = img = Image.FromFile(image.Path);

        if (image == null)
            img = _missingImage;

        ParseRectProps(props, out var parsedWidth, out var parsedHeight, out var originX, out var originY);

        var transformApplied = ApplyTransform(props);

        var width = parsedWidth ?? img.Width;
        var height = parsedHeight ?? img.Height;

        var attr = new ImageAttributes();

        var color = Color.White;
        var opacity = 255;

        var sourceX = 0;
        var sourceY = 0;
        var sourceW = img.Width;
        var sourceH = img.Height;

        foreach (var (key, val) in props) {
            switch (key) {
                case "цв":
                    color = ToColor(val);
                    break;

                case "пр" when TemplateInterpreter.ToNumber(val, out var opacityNum):
                    opacity = (int)opacityNum.Value;
                    break;

                case "обрезх" when TemplateInterpreter.ToNumber(val, out var sourceXNum):
                    sourceX = (int)(sourceXNum.Value * img.Width);
                    break;

                case "обрезу" when TemplateInterpreter.ToNumber(val, out var sourceYNum):
                    sourceY = (int)(sourceYNum.Value * img.Height);
                    break;

                case "обрезш" when TemplateInterpreter.ToNumber(val, out var sourceWNum):
                    sourceW = (int)(sourceWNum.Value * img.Width);
                    break;

                case "обрезв" when TemplateInterpreter.ToNumber(val, out var sourceHNum):
                    sourceH = (int)(sourceHNum.Value * img.Height);
                    break;
            }
        }

        // adjust source rectangle
        //if (sourceX != 0 || sourceY != 0 || sourceW != img.Width || sourceH != img.Height) {
        //    var wFactor = width / (float)img.Width;
        //    var hFactor = height / (float)img.Height;
        //    (sourceX, sourceY) = ((int)(sourceX * wFactor), (int)(sourceY * hFactor));
        //    (sourceW, sourceH) = ((int)(sourceW / wFactor), (int)(sourceH / hFactor));
        //}

        if (color != Color.White || opacity != 255) {
            attr.SetColorMatrix(new ColorMatrix([
                [color.R / 255f, 0, 0, 0, 0],
                [0, color.G / 255f, 0, 0, 0],
                [0, 0, color.B / 255f, 0, 0],
                [0, 0, 0, (color.A / 255f) * (opacity / 255f), 0],
                [0, 0, 0, 0, 1],
            ]));
        }

        _currentGraphics.DrawImage(img, new Rectangle((int)(-width * (originX ?? 0)), (int)(-height * (originY ?? 0)), width, height),
            sourceX, sourceY, sourceW, sourceH, GraphicsUnit.Pixel, attr);

        if (transformApplied) _currentGraphics.ResetTransform();
    }
}