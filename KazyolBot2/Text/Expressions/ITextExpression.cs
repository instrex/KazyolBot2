using KazyolBot2.Text.Tokens;

namespace KazyolBot2.Text.Expressions;

public interface ITextExpression {
    int Position { get; }

    public record Const(Token Text) : ITextExpression {
        public int Position => Text.Position;
        public override string ToString() => $"[{Text.Content}]";
    }

    public record Component(TextComponentInfo Info, List<ITextExpression> Args) : ITextExpression {
        public int Position { get; init; }
        public override string ToString() => $"({Info.Id.FirstOrDefault()}" + (Args.Count == 0 ? ")" : $" {string.Join(", ", Args)})");
    }
}
