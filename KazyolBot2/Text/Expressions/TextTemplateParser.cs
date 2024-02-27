using KazyolBot2.Text.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KazyolBot2.Text.Expressions;

public class TextTemplateParser {
    public List<TextComponentInfo> Components { get; set; }

    readonly List<Token> _source;
    int _pos;

    public TextTemplateParser(string input) {
        var tokenizer = new Tokenizer(input);
        _source = tokenizer.GetTokens();
    }

    public Token Peek(int offset = 0) {
        var actualPos = _pos + offset;

        // clamp the peek position
        if (actualPos >= _source.Count)
            actualPos = _source.Count - 1;

        return _source[actualPos];
    }

    public bool Consume(TokenType type, out Token token) {
        if (Peek().Type != type) {
            token = default;
            return false;
        }

        token = Peek();
        _pos++;

        return true;
    }

    public ITextExpression GetExpression() {
        var expr = Primary();
        while (Consume(TokenType.Plus, out var plusToken)) {
            expr = new ITextExpression.Component(Components.FirstOrDefault(c => c.Codename == "concat"), [ expr, Primary() ]);
        }

        return expr;
    }

    ITextExpression Primary() {
        var token = Peek();

        // constants
        if (token.Type == TokenType.String || token.Type == TokenType.Identifier || token.Type == TokenType.Number) {
            var expr = new ITextExpression.Const(token);
            _pos++;

            return expr;
        }

        // components
        if (token.Type == TokenType.LeftParen) {
            Consume(TokenType.LeftParen, out _);

            if (!Consume(TokenType.Identifier, out var identToken)) 
                throw new SyntaxException {
                    Message = "Ожидалось название текстового компонента",
                    Position = token.Position + 1
                };

            var info = Components.FirstOrDefault(c => c.Id.Contains(identToken.Content)) ?? 
                throw new SyntaxException {
                    Message = "Неизвестный текстовый компонент",
                    Position = identToken.Position
                };

            var args = new List<ITextExpression>();

            while (true) {
                var current = Peek();
                if (current.Type == TokenType.RightParen) {
                    Consume(TokenType.RightParen, out _);
                    break;
                }

                var expr = GetExpression();
                if (Peek().Type != TokenType.RightParen && !Consume(TokenType.Separator, out _))
                    throw new SyntaxException {
                        Message = "Ожидалась ',' или ')'",
                        Position = Peek().Position
                    };

                args.Add(expr);
            }

            // verify args
            for (var i = 0; i < info.Params.Count; i++) {
                var expected = info.Params[i];

                if (i >= args.Count && !expected.CanBeNull)
                    throw new SyntaxException {
                        Message = $"Отсуствует обязательный параметр '{expected.Id}' ({expected.Desc})",
                        Position = Peek(-1).Position
                    };
            }

            var comp = new ITextExpression.Component(info, args) {
                Position = identToken.Position
            };

            return comp;
        }

        throw new SyntaxException {
            Message = "Неизвестное выражение, ожидалась строка или текстовый компонент",
            Position = Peek().Position
        };
    }
}
