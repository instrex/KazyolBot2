using KazyolBot2.Text.Tokens;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KazyolBot2.Text.Expressions;

public class TextTemplateParser {
    readonly Dictionary<string, TextComponentInfo> _components;

    readonly List<Token> _source;
    int _pos;

    public TextTemplateParser(string input, List<TextComponentInfo> components = default) {
        var tokenizer = new Tokenizer(input);
        _source = tokenizer.GetTokens();

        if (components != null) {
            _components = components.ToDictionary(c => c.Codename, c => c);
        }
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
        List<ITextExpression> concatList = default;

        var expr = Primary();
        while (Consume(TokenType.Plus, out var plusToken)) {
            (concatList ??= [expr]).Add(Primary());
        }

        if (concatList != null) {
            expr = new ITextExpression.Component(_components["concat"], concatList);
        }

        return expr;
    }

    ITextExpression Component(Token openingToken) {
        Consume(TokenType.LeftParen, out _);

        var wrapIgnore = Consume(TokenType.Exclamation, out var exclamationToken);

        if (!Consume(TokenType.Identifier, out var identToken))
            throw new SyntaxException {
                Message = "Ожидалось название текстового компонента или переменной",
                Position = openingToken.Position + 1
            };

        ITextExpression.Component comp = default;

        // parse assignment
        if (Consume(TokenType.Equals, out var equalsToken)) {
            var value = GetExpression();
            if (!Consume(TokenType.RightParen, out var _))
                throw new SyntaxException {
                    Message = "Ожидалась ')'",
                    Position = Peek().Position
                };

            comp = new ITextExpression.Component(_components["meta-set"], [new ITextExpression.Const(identToken), value]) {
                Position = equalsToken.Position
            };
        }

        // parse default component body
        else {
            var component = _components.FirstOrDefault(p => p.Value.Id.Contains(identToken.Content));
            var info = component.Value ??
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

            // save resulting expr
            comp = new ITextExpression.Component(info, args) {
                Position = identToken.Position
            };
        }

        return wrapIgnore ?
            new ITextExpression.Component(_components["ignore"], [comp]) { Position = exclamationToken.Position } :
            comp;
    }

    ITextExpression Primary() {
        var token = Peek();

        // meta
        if (token.Type == TokenType.Percent) {
            _pos++;

            if (!Consume(TokenType.Identifier, out var identifierToken))
                throw new SyntaxException {
                    Message = "Ожидался идентификатор переменной",
                    Position = token.Position + 1
                };

            return new ITextExpression.Component(_components["meta-get"], [new ITextExpression.Const(identifierToken)]) { 
                Position = token.Position
            };
        }

        // constants
        if (token.Type == TokenType.String || token.Type == TokenType.Identifier || token.Type == TokenType.Number) {
            var expr = new ITextExpression.Const(token);
            _pos++;

            return expr;
        }

        // components
        if (token.Type == TokenType.LeftParen) {
            return Component(token);
        }

        throw new SyntaxException {
            Message = "Неизвестное выражение, ожидалась строка или текстовый компонент",
            Position = Peek().Position
        };
    }
}
