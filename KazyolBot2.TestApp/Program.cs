// See https://aka.ms/new-console-template for more information



// example: (Component 1) (if (X == 3), Yes, No)
//        = Component 1 Yes

// "String" (component "String", "String")

// "прив"+(перенос-стр)+"ладно"


using KazyolBot2;
using KazyolBot2.Text;
using KazyolBot2.Text.Expressions;
using KazyolBot2.Text.Runtime;
using KazyolBot2.Text.Tokens;
using System.Text.Json;

Console.InputEncoding = System.Text.Encoding.Unicode;
Console.OutputEncoding = System.Text.Encoding.Unicode;

List<TextComponentInfo> components = default;

using (var stream = File.OpenRead(@"C:\Users\instrex\source\repos\instrex\KazyolBot2\KazyolBot2\Data\text_components.json")) {
    components = await JsonSerializer.DeserializeAsync<List<TextComponentInfo>>(stream, options: new(JsonSerializerDefaults.Web));
}

var storage = new ServerStorage();
storage.TextFragments.Add(new() { Category = "ass", Text = "жопа" });
storage.TextFragments.Add(new() { Category = "ass", Text = "попа" });
storage.TextFragments.Add(new() { Category = "ass", Text = "задница" });
storage.TextFragments.Add(new() { Category = "boobs", Text = "сиська" });
storage.TextFragments.Add(new() { Category = "boobs", Text = "титичька" });
storage.TextFragments.Add(new() { Category = "boobs", Text = "грудь" });
storage.TextFragments.Add(new() { Category = "personality", Text = "ляжки" });
storage.TextFragments.Add(new() { Category = "personality", Text = "ножки" });
storage.TextFragments.Add(new() { Category = "personality", Text = "пяточки" });

while (true) {
    var input = Console.ReadLine();

    try {
        CheckTokenizer(input);

        var parser = new TextTemplateParser(input, components);
        var expr = parser.GetExpression();
        Console.WriteLine(expr);
        Console.WriteLine();

        var interpreter = new TemplateInterpreter(storage);
        var warnings = interpreter.CheckArgumentHints(expr);

        if (warnings.Count > 0) {
            Console.WriteLine($"Найдено {warnings.Count} примечаний:");
            foreach (var warning in warnings) {
                Console.WriteLine(warning.CreatePrettyPrint(input, "примечание"));
                Console.WriteLine();
            }
        }

        var template = new TextTemplate {
            Source = input,
            CompiledExpression = expr,
            LangVersion = TemplateInterpreter.Version
        };

        Console.WriteLine($"Пример: \n{interpreter.ExecuteExpression(template.CompiledExpression)}");

    } catch (SyntaxException ex) {
        
        Console.WriteLine(ex.CreatePrettyPrint(input));
    }
    
    Console.WriteLine();
}

void CheckTokenizer(string input) {
    var tokenizer = new Tokenizer(input);
    Console.WriteLine(string.Join("\n", tokenizer.GetTokens()));
    Console.WriteLine();
}