using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DotNext.Threading;
using KazyolBot2.Text;
using KazyolBot2.Text.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KazyolBot2.Modules;

[Group("шаблон", "Команды для генерации смешного (или не очень) контента!")]
public class TemplateModule: InteractionModuleBase<SocketInteractionContext> {
    public static readonly List<TextComponentInfo> TextComponents;

    static TemplateModule() {
        using var stream = File.OpenRead(Path.Combine(ServerStorage.DataDirectoryName, "text_components.json"));
        TextComponents = JsonSerializer.Deserialize<List<TextComponentInfo>>(stream, options: new(JsonSerializerDefaults.Web));
    }

    [AutocompleteCommand("component", "помогите")]
    public async Task AutocompleteTextComponent() {
        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);
        var autocompleteInteraction = Context.Interaction as SocketAutocompleteInteraction;
        await autocompleteInteraction.RespondAsync(
            TextComponents.Select(c => new AutocompleteResult(c.Id.FirstOrDefault(), c.Id.FirstOrDefault()))
                .Take(25)
        );
    }

    async Task AutocompleteTemplateList() {
        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);

        var autocompleteInteraction = Context.Interaction as SocketAutocompleteInteraction;
        var input = autocompleteInteraction.Data.Current.Value.ToString();

        await autocompleteInteraction.RespondAsync(
            storage.TextTemplates.Select(f => f.Name)
                .OrderBy(c => LevenshteinDistance.Compute(c, input))
                .Select(c => new AutocompleteResult(c, c))
        );
    }

    [AutocompleteCommand("name", "выполнить")]
    public async Task AutocompleteAdd() => await AutocompleteTemplateList();

    [SlashCommand("помогите", "Справка о различных текстовых компонентах")]
    public async Task Help([Autocomplete] string component) {
        if (TextComponents.FirstOrDefault(c => c.Id.FirstOrDefault() == component) is not TextComponentInfo info) {
            await RespondAsync("Такой хуйни не существует.");
            return;
        }

        await RespondAsync(embeds: [
            new EmbedBuilder()
                .WithTitle(info.Id.FirstOrDefault() + (info.Params.Count == 0 ? "" : $" ({string.Join(", ", info.Params.Select(p => p.Id))})"))
                .WithDescription(char.ToUpper(info.Desc[0]) + info.Desc[1..] + ".")
                .WithFields(info.Params.Select(p => new EmbedFieldBuilder()
                    .WithName(p.Id)
                    .WithValue(p.Desc)
                    .WithIsInline(false)))
                .Build()
        ]);
    }

    [SlashCommand("тест", "Проверить шаблон на ошибки")]
    public async Task Test(string input) {
        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);
        input = input.Trim('`');

        await DeferAsync();

        var template = new TextTemplate {
            Source = input
        };

        try {
            var result = template.Execute(storage);
            await ModifyOriginalResponseAsync(msg => {
                msg.Content = $"```{input}```\n" + result;
            });

        } catch (SyntaxException syntaxError) {
            await ModifyOriginalResponseAsync(msg => {
                msg.Content = $"```{input}```";
                msg.Embed = new EmbedBuilder()
                    .WithColor(Color.Red)
                    .WithTitle("Что-то пошло не так...")
                    .WithDescription($"```{syntaxError.CreatePrettyPrint(input)}```")
                    .Build();
            });
        } catch (Exception ex) {
            await ModifyOriginalResponseAsync(msg => {
                msg.Content = $"```{input}```";
                msg.Embed = new EmbedBuilder()
                    .WithColor(Color.DarkOrange)
                    .WithTitle(ex.GetType().Name + ": " + ex.Message)
                    .WithDescription($"```{ex.StackTrace}```")
                    .Build();
            });
        }
    }

    [SlashCommand("добавить", "Добавляет или изменяет текстовый шаблон")]
    public async Task Add(string name, string input) {
        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);
        input = input.Trim('`');

        var template = new TextTemplate {
            LangVersion = TextTemplateInterpreter.Version,
            AuthorId = Context.User.Id,
            Source = input,
            Name = name,
            Version = 1,
        };

        await DeferAsync();

        try {
            var results = new List<string>();
            for (var i = 0; i < 10; i++) {
                results.Add(template.Execute(storage));
            }

            storage.ShouldSaveTemplates = true;
            if (storage.TextTemplates.FirstOrDefault(t => t.Name == name) is TextTemplate existingTemplate) {
                existingTemplate.Source = input;
                existingTemplate.CompiledExpression = template.CompiledExpression;
                existingTemplate.Contributors.Add(Context.User.Id);
                existingTemplate.LangVersion = template.LangVersion;
                existingTemplate.Version++;
            } else {
                storage.TextTemplates.Add(template);
            }

            var author = Context.User;
            await ModifyOriginalResponseAsync(msg => {
                msg.Embed = new EmbedBuilder()
                    .WithTitle($"Шаблон '{name}' записан!")
                    .WithAuthor(author)
                    .AddField("Примеры", string.Join('\n', results))
                    .Build();
            });

        } catch (SyntaxException syntaxError) {
            await ModifyOriginalResponseAsync(msg => {
                msg.Content = $"```{input}```";
                msg.Embed = new EmbedBuilder()
                    .WithColor(Color.Red)
                    .WithTitle("Что-то пошло не так...")
                    .WithDescription($"```{syntaxError.CreatePrettyPrint(input)}```")
                    .Build();
            });
        } catch (Exception ex) {
            await ModifyOriginalResponseAsync(msg => {
                msg.Content = $"```{input}```";
                msg.Embed = new EmbedBuilder()
                    .WithColor(Color.DarkOrange)
                    .WithTitle(ex.GetType().Name + ": " + ex.Message)
                    .WithDescription($"```{ex.StackTrace}```")
                    .Build();
            });
        }
    }

    [SlashCommand("выполнить", "Возвращает результат выполнения шаблона")]
    public async Task Run([Autocomplete] string name, int repeat = 1) {
        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);
        if (storage.TextTemplates.FirstOrDefault(t => t.Name == name) is not TextTemplate template) {
            await RespondAsync($"Шаблона '{name}' не существует.");
            return;
        }

        await DeferAsync();

        try {
            var result = "";
            for (var i = 0; i < repeat; i++) {
                result += template.Execute(storage) + "\n";
            }

            await ModifyOriginalResponseAsync(msg => {
                msg.Embed = new EmbedBuilder()
                    .WithDescription(result)
                    .WithColor(Color.Blue)
                    .Build();
            });
        } catch (SyntaxException syntaxError) {
            await ModifyOriginalResponseAsync(msg => {
                msg.Content = $"```{template.Source}```";
                msg.Embed = new EmbedBuilder()
                    .WithColor(Color.Red)
                    .WithTitle("Что-то пошло не так...")
                    .WithDescription($"```{syntaxError.CreatePrettyPrint(template.Source)}```")
                    .Build();
            });
        } catch (Exception ex) {
            await ModifyOriginalResponseAsync(msg => {
                msg.Content = $"```{template.Source}```";
                msg.Embed = new EmbedBuilder()
                    .WithColor(Color.DarkOrange)
                    .WithTitle(ex.GetType().Name + ": " + ex.Message)
                    .WithDescription($"```{ex.StackTrace}```")
                    .Build();
            });
        }
    }
}
