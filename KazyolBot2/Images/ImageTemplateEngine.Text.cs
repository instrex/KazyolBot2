using KazyolBot2.Text.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KazyolBot2.Images;

public partial class ImageTemplateEngine {

    public void DrawText(string text, Dictionary<string, IValue> props) {
        if (string.IsNullOrEmpty(text))
            return;

        ParseRectProps(props, out var parsedWidth, out var parsedHeight, out var originX, out var originY);

        var fontSize = 12;
        var fontName = "Arial";
        var fontStyle = FontStyle.Regular;
        var textAlignH = StringAlignment.Center;
        var textAlignV = StringAlignment.Center;
        var textColor = Color.White;
        var opacity = 255;

        var outlineColor = Color.Black;
        var outlineThickness = 0;

        var shadowDist = 4;
        var shadowColor = Color.Transparent;

        foreach (var (key, val) in props) {
            switch (key) {
                case "размер" when TemplateInterpreter.ToNumber(val, out var fontSizeValue):
                    fontSize = (int)fontSizeValue.Value;
                    break;

                case "шрифт":
                    fontName = val.ToString();
                    break;

                case "стиль":
                    var styleString = val.ToString();
                    for (var i = 0; i < styleString.Length; i++) {
                        fontStyle |= styleString[i] switch {
                            'ж' => FontStyle.Bold,
                            'н' => FontStyle.Italic,
                            'п' => FontStyle.Underline,
                            'з' => FontStyle.Strikeout,
                            _ => FontStyle.Regular
                        };
                    }

                    break;

                case "выравн":
                    textAlignH = val.ToString() switch { 
                        "л" => StringAlignment.Near,
                        "ц" => StringAlignment.Center,
                        "п" => StringAlignment.Far,
                        _ => StringAlignment.Near,
                    };

                    break;

                case "выравн-в":
                    textAlignV = val.ToString() switch {
                        "в" => StringAlignment.Near,
                        "ц" => StringAlignment.Center,
                        "н" => StringAlignment.Far,
                        _ => StringAlignment.Near,
                    };

                    break;

                case "цв":
                    textColor = ToColor(val);
                    break;

                case "пр" when TemplateInterpreter.ToNumber(val, out var opacityNum):
                    opacity = (int)opacityNum.Value;
                    break;

                case "обвод" when TemplateInterpreter.ToNumber(val, out var outlineSizeValue):
                    outlineThickness = (int)outlineSizeValue.Value;
                    break;

                case "цв-обвод":
                    outlineColor = ToColor(val);
                    break;

                case "цв-тень":
                    shadowColor = ToColor(val);
                    break;

                case "тень" when TemplateInterpreter.ToNumber(val, out var shadowValue):
                    shadowDist = (int)shadowValue.Value;
                    break;
            }
        }

        var format = new StringFormat {
            Trimming = StringTrimming.EllipsisWord,
            Alignment = textAlignH,
            LineAlignment = textAlignV,
        };

        var boxSize = new SizeF(parsedWidth ?? 500, parsedHeight ?? 500);

        var fontFamily = new FontFamily(fontName);
        var font = new Font(fontFamily, fontSize, fontStyle);

        var autoSizeTest = _currentGraphics.MeasureString(text, font, parsedWidth ?? int.MaxValue);
        while ((autoSizeTest.Width > boxSize.Width || autoSizeTest.Height > boxSize.Height) && fontSize > 4) {
            font = new Font(fontFamily, --fontSize, fontStyle);
            autoSizeTest = _currentGraphics.MeasureString(text, font, parsedWidth ?? int.MaxValue, format);
        }

        var offsetX = (int)(-boxSize.Width * (originX ?? 0.5f));
        var offsetY = (int)(-boxSize.Height * (originY ?? 0.5f));

        using var brush = new SolidBrush(Color.FromArgb((int)((opacity / 255f) * (textColor.A / 255f) * 255), textColor));

        var textRect = new RectangleF(offsetX, offsetY, boxSize.Width, boxSize.Height);
        ApplyTransform(props);

        if (shadowColor != Color.Transparent) {
            using var shadowBrush = new SolidBrush(Color.FromArgb((int)(100 * (opacity / 255f)), shadowColor));

            _currentGraphics.DrawString(text, font, shadowBrush, textRect with {
                X = offsetX + shadowDist,
                Y = offsetY + shadowDist,
            }, format);
        }

        if (outlineThickness > 0) {
            using var outlineBrush = new SolidBrush(Color.FromArgb(opacity, outlineColor));

            for (var x = -1; x < 2; x++) {
                for (var y = -1; y < 2; y++) {
                    if (x == 0 && y == 0)
                        continue;

                    // draw outline
                    _currentGraphics.DrawString(text, font, outlineBrush, textRect with { 
                        X = offsetX + x * outlineThickness, 
                        Y = offsetY + y * outlineThickness 
                    }, format);
                }
            }
        }

        // draw string
        _currentGraphics.DrawString(text, font, brush, textRect, format);

        if (DebugMode) {
            _currentGraphics.DrawRectangle(new Pen(Color.Red, 2f), textRect);
        }

        _currentGraphics.ResetTransform();
    }

}