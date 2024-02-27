using KazyolBot2.Modules;
using KazyolBot2.Text.Expressions;
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
        var parser = new TextTemplateParser(Source) { Components = TemplateModule.TextComponents };
        CompiledExpression = parser.GetExpression();

        if (parser.Peek().Type != Tokens.TokenType.EOF)
            throw new SyntaxException {
                Position = parser.Peek().Position,
                Message = "Ожидался конец шаблона (для соединения нескольких частей, используй '+')"
            };
    }

    public string Execute(ServerStorage storage) {
        if (CompiledExpression == null) 
            Compile();

        Views++;

        var interpreter = new TextTemplateInterpreter(storage);
        return interpreter.ExecuteExpression(CompiledExpression);
    }
}
