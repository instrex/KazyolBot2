using KazyolBot2.Modules;
using KazyolBot2.Text.Expressions;
using KazyolBot2.Text.Runtime;
using System.Text.Json.Serialization;

namespace KazyolBot2.Text;

public class TextTemplate {
    public ulong AuthorId { get; set; }
    public HashSet<ulong> Contributors { get; set; } = [];
    public string Name { get; set; }
    public string Source { get; set; }
    public string LangVersion { get; set; }
    public int Views { get; set; }
    public int Version { get; set; }

    [JsonIgnore]
    public ITextExpression CompiledExpression { get; set; }

    public void Compile() {
        var parser = new TextTemplateParser(Source, TemplateModule.TextComponents);
        CompiledExpression = parser.GetExpression();

        if (parser.Peek().Type != Tokens.TokenType.EOF)
            throw new SyntaxException {
                Position = parser.Peek().Position,
                Message = "Ожидался конец шаблона (для соединения нескольких частей, используй '+')"
            };
    }

    public IValue.TemplateResult Execute(ServerStorage storage, Action<TemplateInterpreter> init = default, bool debugMode = false) {
        if (CompiledExpression == null) 
            Compile();

        Views++;

        var interpreter = new TemplateInterpreter(storage, debugMode);
        init?.Invoke(interpreter);

        var value = interpreter.ExecuteExpression(CompiledExpression);
        var result = new IValue.TemplateResult { 
            Value = value.ToString() 
        };

        // save frame
        if (interpreter.GraphicsEngine.HasUnfinishedFrame) {
            interpreter.GraphicsEngine.EndFrame();
        }

        var gfxEngine = interpreter.GraphicsEngine;

        if (gfxEngine.Frames.Count == 1) {
            // save png (single frame)
            result.ImageStream = new MemoryStream();
            result.ImageFormat = "png";

            // save the png frame into memory stream
            interpreter.GraphicsEngine.Frames[0].Save(result.ImageStream, System.Drawing.Imaging.ImageFormat.Png);
        } else if (gfxEngine.Frames.Count > 1) {

            // save gif (multiple frames)
            result.ImageStream = interpreter.GraphicsEngine.CreateGif(
                interpreter.Env.Get("фпс") is IValue.Num n ?
                Math.Clamp((int)n.Value, 1, 50) : 30);
            result.ImageFormat = "gif";
        }

        // dispose of gfx engine
        gfxEngine.Dispose();

        return result;
    }
}
