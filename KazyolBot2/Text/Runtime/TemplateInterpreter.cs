using Cyriller;
using Cyriller.Model;
using KazyolBot2.Text.Expressions;
using System.Text;

using static KazyolBot2.Text.Runtime.IValue;

namespace KazyolBot2.Text.Runtime;

public class TemplateInterpreter {
    public const string Version = "V2";

    public readonly ServerStorage Storage;
    public readonly EnvironmentTable Env;

    public TemplateInterpreter(ServerStorage storage) {
        Storage = storage;

        // init env
        Env = new EnvironmentTable();
        Env.Push();

        // set initial variables
        Env.Set("версия", new Str(Version));
    }

    readonly Random _random = new();

    static readonly CyrNounCollection _cyrNounCollection = new();
    static readonly CyrAdjectiveCollection _cyrAdjCollection = new();
    static readonly CyrPhrase _cyrPhrase = new(_cyrNounCollection, _cyrAdjCollection);
    static readonly CyrName _cyrName = new();

    public IValue ExecuteExpression(ITextExpression expr) {
        if (expr is ITextExpression.Const constExpr) {
            return new Str(constExpr.Text.Type switch {
                Tokens.TokenType.String => constExpr.Text.Content.Trim('"'),
                _ => constExpr.Text.Content
            });
        }

        if (expr is ITextExpression.Component comp) {
            switch (comp.Info.Codename) {
                // META
                case "ignore":
                    foreach (var i in comp.Args) {
                        _ = ExecuteExpression(i);
                    }

                    return new Null();

                case "meta-get":
                    var getIdent = ExecuteExpression(comp.Args[0]).ToString();
                    return Env.Get(getIdent);

                case "meta-set":
                    var setIdent = ExecuteExpression(comp.Args[0]).ToString();
                    var setValue = ExecuteExpression(comp.Args[1]);
                    Env.Set(setIdent, setValue);

                    return setValue;

                case "template":
                    var templateName = ExecuteExpression(comp.Args[0]).ToString();
                    var template = Storage.TextTemplates.FirstOrDefault(t => t.Name == templateName);

                    if (template is null)
                        return Null();

                    TemplateResult result = default;

                    try {
                        result = template.Execute(Storage);
                    } catch (Exception ex) {
                        result = new TemplateResult { Value = $"`[ОШИБКА: {ex.Message}]`" };
                    }

                    return result;

                // RANDOMNESS
                case "choose-cat":
                    var categoryToChooseFrom = ExecuteExpression(comp.Args.FirstOrDefault()).ToString();
                    var fragments = Storage.TextFragments.GroupBy(t => t.Category)
                        .FirstOrDefault(c => c.Key == categoryToChooseFrom);

                    if (fragments == null)
                        return Null();

                    return new Str(fragments.ToArray()[_random.Next(0, fragments.Count())].Text);

                case "choose-variant":
                    return ExecuteExpression(comp.Args[_random.Next(0, comp.Args.Count)]);

                case "choose-chance":
                    var chance = ToNumber(ExecuteExpression(comp.Args[0]), out var chanceValue) ? chanceValue.Value * 0.01 : 0.5;
                    if (_random.NextDouble() < chance) {
                        return ExecuteExpression(comp.Args[1]);
                    } else if (comp.Args.Count >= 3) {
                        return ExecuteExpression(comp.Args[2]);
                    }

                    return Null();

                // TEXT MANIPULATION
                case "linebreak":
                    return new Str("\n");

                case "concat":
                    return new Str(string.Concat(comp.Args.Select(ExecuteExpression)));

                case "letter-case": 
                    var caseFormat = ExecuteExpression(comp.Args[0]).ToString();
                    var textToBeCased = ExecuteExpression(comp.Args[1]).ToString();
                    return new Str(caseFormat switch {
                        "А" => textToBeCased.ToUpper(),
                        "а" => textToBeCased.ToLower(),
                        "Аа" when textToBeCased.Length >= 2 => string.Concat(char.ToUpper(textToBeCased[0]).ToString(), textToBeCased[1..]),
                        "Аа" when textToBeCased.Length < 2 => textToBeCased.ToUpper(),
                        _ => textToBeCased
                    });

                case "repeat":
                    ToNumber(ExecuteExpression(comp.Args[0]), out var repeatCount, 1);
                    if (repeatCount.Value < 0)
                        return Null();

                    var builder = new StringBuilder();
                    var separator = comp.Args.Count >= 3 ? ExecuteExpression(comp.Args[2]).ToString() : string.Empty;
                    for (var i = 0; i < repeatCount.Value; i++) {
                        if (i != 0) builder.Append(separator);
                        builder.Append(ExecuteExpression(comp.Args[1]));
                    }

                    return new Str(builder.ToString());

                // DECLENSION
                case "declension-name":
                    var name = ExecuteExpression(comp.Args[1]).ToString();

                    try {
                        ParseCase(ExecuteExpression(comp.Args[0]).ToString(), out var c, out var _, out var _);

                        var entry = _cyrName.Decline(name);
                        return new Str(entry.Get(c));

                    } catch (Exception) {
                        return new Str(name);
                    }

                case "declension-noun":
                    var noun = ExecuteExpression(comp.Args[1]).ToString();

                    try {
                        ParseCase(ExecuteExpression(comp.Args[0]).ToString(), out var c, out var n, out var g);

                        var entry = _cyrNounCollection.Get(noun.ToLower(), out var found, out var @case, out var @number);
                        return new Str(((n ?? @number) == NumbersEnum.Plural ? entry.DeclinePlural() : entry.Decline()).Get(c));

                    } catch (Exception) {
                        return new Str(noun);
                    }

                case "declension-adj":
                    var adj = ExecuteExpression(comp.Args[1]).ToString();

                    try {
                        ParseCase(ExecuteExpression(comp.Args[0]).ToString(), out var c, out var n, out var g);

                        var entry = _cyrAdjCollection.Get(adj.ToLower(), out var found, out var ag, out var ac, out var an, out var aa);
                        return new Str(((n ?? an) == NumbersEnum.Plural ? entry.DeclinePlural(aa) : entry.Decline(g ?? ag, aa)).Get(c));

                    } catch (Exception) {
                        return new Str(adj);
                    }

                case "declension-phrase":
                    var phrase = ExecuteExpression(comp.Args[1]).ToString();

                    var words = phrase.Split(' ');
                    var leftoverString = "";
                    if (words.Length > 2) {
                        phrase = string.Join(' ', words.Take(2));
                        leftoverString = " " + string.Join(' ', words.Skip(2));
                    }

                    try {
                        ParseCase(ExecuteExpression(comp.Args[0]).ToString(), out var c, out var n, out var g);

                        var entry = n == NumbersEnum.Plural ? _cyrPhrase.DeclinePlural(phrase.ToLower(), GetConditionsEnum.Similar) :
                            _cyrPhrase.Decline(phrase.ToLower(), GetConditionsEnum.Similar);
                        return new Str(entry.Get(c) + leftoverString);

                    } catch (Exception) {
                        return new Str(phrase);
                    }
            }
        }

        return Null();
    }

    static Null Null() => new();

    static bool ToNumber(IValue value, out Num number, int defaultValue = 0) {
        switch (value) {
            default:
                number = new Num(defaultValue);
                return false;

            case Num existingNumber:
                number = existingNumber;
                return true;

            case Str stringNumber when int.TryParse(stringNumber.Value, out var parsedNumber):
                number = new Num(parsedNumber);
                return true;
        }
    }

    static void ParseCase(string @case, out CasesEnum caseEnum, out NumbersEnum? number, out GendersEnum? gender) {
        var pair = @case.Split('-');
        caseEnum = pair[0] switch {
            "и" => CasesEnum.Nominative,
            "р" => CasesEnum.Genitive,
            "д" => CasesEnum.Dative,
            "в" => CasesEnum.Accusative,
            "т" => CasesEnum.Instrumental,
            "п" => CasesEnum.Prepositional,
            _ => CasesEnum.Nominative
        };

        number = null;
        gender = null;

        if (pair.Length > 1) {
            number = pair[1] switch {
                "ед" => NumbersEnum.Singular,
                "мн" => NumbersEnum.Plural,
                _ => null
            };

            if (pair.Length > 2 && number == NumbersEnum.Singular) {
                gender = pair[2] switch {
                    "м" => GendersEnum.Masculine,
                    "ж" => GendersEnum.Feminine,
                    "с" => GendersEnum.Neuter,
                    "н" => GendersEnum.Undefined,
                    _ => null
                };
            }
        }
    }

    public List<SyntaxException> CheckArgumentHints(ITextExpression expr) {
        var warnings = new List<SyntaxException>();
        if (expr is not ITextExpression.Component comp)
            return warnings;

        var issuedTooManyArgumentsWarning = false;
        for (var i = 0; i < comp.Args.Count; i++) {
            var arg = comp.Args[i];

            var isOverParamLimit = i >= comp.Info.Params.Count;
            TextComponentInfo.Param param = isOverParamLimit ? comp.Info.Params.LastOrDefault() : comp.Info.Params[i];

            if (!issuedTooManyArgumentsWarning && isOverParamLimit && !param.CanBeMultiple) {
                issuedTooManyArgumentsWarning = true;
                warnings.Add(new SyntaxException {
                    Message = $"Обнаружены неиспользованные параметры (компонент '{comp.Info.Id.FirstOrDefault()}' требует {comp.Info.Params.Count} аргумент)",
                    Position = arg.Position
                });
            }

            if (param.Hints == null || param.Hints.Length == 0)
                continue;

            foreach (var hint in param.Hints) {
                switch (hint) {
                    case "check-text-category" when arg is ITextExpression.Const categoryName:
                        if (!Storage.TextFragments.Any(f => f.Category == categoryName.Text.Content)) {
                            warnings.Add(new SyntaxException {
                                Message = $"Категории '{categoryName.Text.Content}' на данный момент не существует - результат будет пустым до её добавления",
                                Position = arg.Position
                            });
                        }

                        break;

                        //case "number":
                        //    if (arg is not ITextExpression.Const { Text: { Type: Tokens.TokenType.Number } } ||
                        //        (arg is ITextExpression.Component numComp && numComp.Info.ResultHints?.Contains("number") == false)) {
                        //        warnings.Add(new SyntaxException {
                        //            Message = $"Параметр '{param.Id.FirstOrDefault()}' требует число, но '{arg}' может им не являться."
                        //        });
                        //    }

                        //    break;
                }
            }
        }

        return warnings;
    }
}
