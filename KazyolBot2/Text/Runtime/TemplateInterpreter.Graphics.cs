using KazyolBot2.Images;
using KazyolBot2.Text.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KazyolBot2.Text.Runtime;

public partial class TemplateInterpreter {
    public readonly ImageTemplateEngine GraphicsEngine = new();

    IValue ExecuteGraphicComponent(ITextExpression.Component comp) {
        switch (comp.Info.Codename) {
            case "canvas":
                if (GraphicsEngine.HasUnfinishedFrame) {
                    throw new InvalidOperationException("Попытка начать новый кадр до завершения предыдущего.");
                } 
                
                {
                    ToNumber(ExecuteExpression(comp.Args[0]), out var widthNumber, 100);
                    ToNumber(ExecuteExpression(comp.Args[1]), out var heightNumber, 100);
                    GraphicsEngine.BeginFrame((int)widthNumber.Value, (int)heightNumber.Value);
                }
                
                return new IValue.Null();

            case "canvas-gif":
                if (GraphicsEngine.HasUnfinishedFrame) {
                    throw new InvalidOperationException("Попытка начать новый кадр до завершения предыдущего.");
                } 
                
                {
                    ToNumber(ExecuteExpression(comp.Args[0]), out var frameNumber, 12);
                    if (frameNumber.Value > 720 || frameNumber.Value < 1) {
                        throw new InvalidOperationException("Недопустимое значение кадров.");
                    }


                    ToNumber(ExecuteExpression(comp.Args[1]), out var widthNumber, 100);
                    ToNumber(ExecuteExpression(comp.Args[2]), out var heightNumber, 100);
                    if (widthNumber.Value > 640 || heightNumber.Value > 640) {
                        throw new InvalidOperationException($"Превышен лимит размера кадра анимации 640x640.");
                    }

                    var frameLogic = comp.Args[3];

                    for (var i = 0; i < frameNumber.Value; i++) {
                        GraphicsEngine.BeginFrame((int)widthNumber.Value, (int)heightNumber.Value);

                        Env.Push();
                        Env.Set("кадр", new IValue.Num(i));

                        ExecuteExpression(frameLogic);

                        Env.Pop();
                        GraphicsEngine.EndFrame();
                    }
                }
                
                return new IValue.Null();

            case "fill":
                if (!GraphicsEngine.HasUnfinishedFrame) {
                    throw new InvalidOperationException("Попытка отрисовки до начала кадра.");
                }

                GraphicsEngine.Fill(ExecuteExpression(comp.Args[0]));

                return new IValue.Null();

            case "draw-sprite":
                if (!GraphicsEngine.HasUnfinishedFrame) {
                    throw new InvalidOperationException("Попытка отрисовки до начала кадра.");
                }

                var imageName = ExecuteExpression(comp.Args[0]).ToString();
                var savedImage = Storage.Images.Find(i => i.Name == imageName);
                
                {
                    
                    // defer drawing logic
                    GraphicsEngine.DrawSprite(savedImage, comp.Args.Count > 1 && ExecuteExpression(comp.Args[1]) is IValue.Table spriteProps ?
                        spriteProps.Values : []);
                }
                
                return new IValue.Null();

            case "draw-text":
                if (!GraphicsEngine.HasUnfinishedFrame) {
                    throw new InvalidOperationException("Попытка отрисовки до начала кадра.");
                }

                GraphicsEngine.DrawText(ExecuteExpression(comp.Args[0]).ToString(),
                    comp.Args.Count > 1 && ExecuteExpression(comp.Args[1]) is IValue.Table textProps ?
                        textProps.Values : []);

                return new IValue.Null();
        }


        return new IValue.Null();
    }
}
