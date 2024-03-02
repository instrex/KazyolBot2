using AnimatedGif;
using DotNext.Collections.Generic;
using KazyolBot2.Text.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KazyolBot2.Images;

public partial class ImageTemplateEngine : IDisposable {
    readonly static Image _missingImage = Image.FromFile(Path.Combine(ServerStorage.DataDirectoryName, "missing_image.png"));

    public readonly List<Bitmap> Frames = [];
    public bool DebugMode { get; set; }
    public bool HasUnfinishedFrame => _currentGraphics != null;

    readonly Dictionary<string, Image> _imageCache = [];
    Graphics _currentGraphics;
    Bitmap _currentFrame;

    public void BeginFrame(int width, int height) {
        if (_currentGraphics != null) {
            throw new InvalidOperationException("Отрисовка кадра не была завершена.");
        }

        if (width > 1000 || height > 1000) {
            throw new InvalidOperationException($"Превышен лимит размера кадра 1000х1000.");
        }

        _currentFrame = new Bitmap(width, height);
        _currentGraphics = Graphics.FromImage(_currentFrame);

        // set smoothing
        _currentGraphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        _currentGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    }

    public void EndFrame() {
        Frames.Add(_currentFrame);
        _currentGraphics.Dispose();
        _currentGraphics = null;
    }

    public MemoryStream CreateGif(int fps) {
        var stream = new MemoryStream();

        using var gif = new AnimatedGifCreator(stream, (int)(1.0f / fps * 1000));

        foreach (var frame in Frames) {
            gif.AddFrame(frame, -1, GifQuality.Bit8);
        }

        return stream;
    }

    public void Dispose() {
        _currentGraphics?.Dispose();

        // dispose of images
        foreach (var frame in _imageCache.Values.Concat(Frames))
            frame.Dispose();

        GC.SuppressFinalize(this);
    }

    public static Brush ToBrush(IValue value) {
        var str = value.ToString();
        return str switch {
            "черный" or "ч" => Brushes.Black,
            _ => Brushes.White
        };
    }

    public static Color ToColor(IValue value) {
        var str = value.ToString();
        Color? presetColor = str switch {
            "черный" or "ч" => Color.Black,
            "белый" or "б" => Color.White,
            "красный" or "к" => Color.Red,
            "зеленый" or "з" => Color.Green,
            "синий" or "с" => Color.Blue,
            "голубой" or "г" => Color.LightBlue,
            "розовый" or "р" => Color.Pink,
            "серый" => Color.Gray,
            "темносерый" => Color.DarkGray,
            _ => null
        };

        if (presetColor != null) 
            return presetColor.Value;

        var components = str.Split(' ');
        if (components.Length < 3)
            return Color.White;

        return Color.FromArgb(
            components.Length == 4 ? (byte.TryParse(components[3], out var a) ? a : 0) : 255,
            byte.TryParse(components[0], out var r) ? r : 0,
            byte.TryParse(components[1], out var g) ? g : 0,
            byte.TryParse(components[2], out var b) ? b : 0
        );
    }

    public bool ApplyTransform(Dictionary<string, IValue> props) {
        var anythingApplied = false;

        var xOffset = 0f;
        var yOffset = 0f;
        var rotation = 0f;
        var scale = 1f;
        var scaleX = 1f;
        var scaleY = 1f;

        foreach (var (key, val) in props) {
            switch (key) {
                case "х":
                    TemplateInterpreter.ToNumber(val, out var offX);
                    xOffset = (float)offX.Value;
                    break;

                case "у":
                    TemplateInterpreter.ToNumber(val, out var offY);
                    yOffset = (float)offY.Value;
                    break;

                case "вращ":
                    TemplateInterpreter.ToNumber(val, out var rotationVal);
                    rotation = (float)rotationVal.Value;
                    break;

                case "скейл":
                    TemplateInterpreter.ToNumber(val, out var scaleVal);
                    scale = (float)scaleVal.Value;
                    break;

                case "скейлх":
                    TemplateInterpreter.ToNumber(val, out var scaleXVal);
                    scaleX = (float)scaleXVal.Value;
                    break;

                case "скейлу":
                    TemplateInterpreter.ToNumber(val, out var scaleYVal);
                    scaleY = (float)scaleYVal.Value;
                    break;
            }
        }

        if (scale != 1 || scaleX != 1 || scaleY != 1) {
            _currentGraphics.ScaleTransform(scaleX * scale, scaleY * scale, MatrixOrder.Append);
            anythingApplied = true;
        }

        if (rotation != 0) {
            _currentGraphics.RotateTransform(rotation, MatrixOrder.Append);
            anythingApplied = true;
        }

        if (xOffset != 0 || yOffset != 0) {
            _currentGraphics.TranslateTransform(xOffset, yOffset, MatrixOrder.Append);
            anythingApplied = true;
        }

        return anythingApplied;
    }

    public void Fill(IValue color) {
        using var brush = new SolidBrush(ToColor(color));
        _currentGraphics.FillRectangle(brush, 0, 0, _currentFrame.Width, _currentFrame.Height);
    }
}
