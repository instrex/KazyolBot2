namespace KazyolBot2.Text.Tokens;

public ref struct Tokenizer(ReadOnlySpan<char> source) {
    readonly ReadOnlySpan<char> _source = source;
    readonly int _len = source.Length;
    int _pos = 0;

    public List<Token> GetTokens() {
        var result = new List<Token>();
        while (_pos < _len) {
            var ch = _source[_pos];
            if (char.IsWhiteSpace(ch)) {
                _pos++;
                continue;
            }

            TokenType? simpleTokenType = ch switch {
                '(' => TokenType.LeftParen,
                ')' => TokenType.RightParen,
                ',' => TokenType.Separator,
                '+' => TokenType.Plus,
                '=' => TokenType.Equals,
                '%' => TokenType.Percent,
                '!' => TokenType.Exclamation,
                _ => null
            };

            // done matching
            if (simpleTokenType is TokenType tokenType) {
                result.Add(new(tokenType, ch.ToString(), _pos++));
                continue;
            }
            
            if (char.IsLetter(ch)) {
                var token = Capture(_source[_pos..], ch => IsCharSuitableForIdentifier(ch));
                result.Add(new(TokenType.Identifier, token.ToString(), _pos));
                _pos += token.Length;
                continue;
            }

            if (char.IsDigit(ch)) {
                var token = Capture(_source[_pos..], char.IsDigit);
                if (!int.TryParse(token, out var _)) {
                    throw new SyntaxException {
                        Message = "Число вне допустимого диапазона",
                        Position = _pos
                    };
                }

                result.Add(new(TokenType.Number, token.ToString(), _pos));
                _pos += token.Length;
                continue;
            }
            
            if (ch is '"') {
                var token = Capture(_source[(_pos + 1)..], ch => ch != '"');
                result.Add(new(TokenType.String, $"\"{token.ToString()}\"", _pos));

                var startPos = _pos;
                _pos += token.Length + 1;

                // bruh
                if (_pos >= _len || _source[_pos] != '"') {
                    throw new SyntaxException {
                        Message = "Строка не закрыта (ты забыл кавычку)",
                        Position = startPos
                    };
                } 

                _pos++;

                continue;
            }

            throw new SyntaxException {
                Message = $"Непонятный символ '{ch}'",
                Position = _pos
            };
        }

        result.Add(new Token(TokenType.EOF, "", _pos));
        return result;
    }

    static bool IsCharSuitableForIdentifier(char c) => char.IsLetterOrDigit(c) || c is '-' or '_';

    static ReadOnlySpan<char> Capture(ReadOnlySpan<char> text, Predicate<char> condition) {
        var offset = 0;

        while (offset < text.Length) {
            var ch = text[offset];
            if (condition(ch)) {
                offset++;
                continue;
            }

            break;
        }

        return text[..offset];
    }
}
