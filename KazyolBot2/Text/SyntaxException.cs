// See https://aka.ms/new-console-template for more information



// example: (Component 1) (if (X == 3), Yes, No)
//        = Component 1 Yes

// "String" (component "String", "String")

// "прив"+(перенос-стр)+"ладно"





public class SyntaxException : Exception {
    public new string Message { get; set; }
    public int Position { get; set; }

    public string CreatePrettyPrint(string source, string prefix = "ошибка") {
        const int MaxCharactersLeft = 16;
        var startPos = Math.Clamp(Position - MaxCharactersLeft, 0, source.Length);
        var endPos = Math.Clamp(Position + 32, 0, source.Length);

        var summary = source[startPos..endPos];
        if (startPos != 0) summary = "..." + summary;
        if (endPos != source.Length) summary += "...";

        return summary + "\n" + "^".PadLeft(Position < MaxCharactersLeft ? Position + 1 : MaxCharactersLeft + 3 + 1) + "\n" + $"{prefix}: {Message}";
    }
}
