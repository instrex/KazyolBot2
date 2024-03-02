using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DotNext.Threading;
using KazyolBot2.Images;
using KazyolBot2.Text;
using KazyolBot2.Text.Expressions;
using KazyolBot2.Text.Runtime;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace KazyolBot2.Modules;

[Group("шаблон", "Команды для генерации смешного (или не очень) контента!")]
public class TemplateModule : InteractionModuleBase<SocketInteractionContext> {
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

    [AutocompleteCommand("name", "инфо")]
    public async Task AutocompleteInfo() => await AutocompleteTemplateList();

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

    async Task HandleTemplateErrors(string input, Func<Task> task) {
        try { await task(); } catch (SyntaxException syntaxError) {
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

    void PrepareTemplateVariables(TemplateInterpreter interpreter, Dictionary<string, IValue> argTable = default, bool provideTestArgs = false) {
        interpreter.Env.Set("юзер", new IValue.Str(Context.User.Username));
        interpreter.Env.Set("сервер", new IValue.Str(Context.Guild.Name));

        if (argTable != null) {
            foreach (var (key, value) in argTable)
                interpreter.Env.Set(key, value);
        }

        if (provideTestArgs) {
            for (var i = 0; i < 10; i++) {
                interpreter.Env.Set($"арг{i}", new IValue.Str($"Аргумент #{i}"));
            }
        }
    }

    async Task<Dictionary<string, IValue>> PrepareArguments(string inputArgs, IAttachment attachmentArg) {
        Dictionary<string, IValue> argTable = [];

        if (inputArgs !=  null) {
            // parse arguments
            var args = TextModule.TokenizeString(inputArgs);
            for (var i = 0; i < args.Count; i++) {
                IValue value = new IValue.Str(args[i]);
                if (TemplateInterpreter.ToNumber(value, out var numValue))
                    value = numValue;

                argTable[$"арг{i}"] = value;
            }
        }

        // ephemerally save argument
        if (attachmentArg != null && ImageModule.AllowedImageTypes.Contains(attachmentArg.ContentType)) {
            var storage = ServerStorage.GetOrCreate(Context.Guild.Id);

            var storedPath = await storage.SaveImage(attachmentArg.Url, attachmentArg.ContentType.Split('/')[1], null);

            if (storedPath is null) 
                return argTable;
            
            var savedImage = new SavedImageData {
                AuthorId = Context.User.Id,
                Name = Path.GetFileName(storedPath),
                Id = storage.LastImageId++,
                ImageUrl = attachmentArg.Url,
                Ephemeral = true,
                Path = storedPath,
            };

            storage.Images.Add(savedImage);

            // write file name
            argTable["арг-файл"] = new IValue.Str(savedImage.Name);
        }

        return argTable;
    }

    [SlashCommand("тест", "Проверить шаблон на ошибки")]
    public async Task Test(string input, 
        [Summary(description: "Список аргументов для передачи в шаблон")] string args = default,
        [Summary(description: "Картинка для передачи в шаблон")] IAttachment attachmentArg = default,
        bool debugMode = false) {

        await DeferAsync();

        // prepare args
        var argTable = await PrepareArguments(args, attachmentArg);

        // prepare storage and trim input from code brackets
        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);
        input = input.Trim('`');

        var template = new TextTemplate { Source = input };

        await HandleTemplateErrors(input, async () => {
            var result = template.Execute(storage, i => PrepareTemplateVariables(i, argTable), debugMode);

            await ModifyOriginalResponseAsync(msg => {
                var embed = new EmbedBuilder()
                    .WithDescription(result.Value);

                if (result.ImageStream != null) {
                    msg.Attachments = new([new FileAttachment(result.ImageStream, $"result.{result.ImageFormat}")]);
                    embed = embed.WithImageUrl($"attachment://result.{result.ImageFormat}");
                }

                msg.Content = $"```{input}```\n";
                msg.Embed = embed.Build();
            });

            if (result.ImageStream != null) {
                // dispose of the created stream
                await result.ImageStream.DisposeAsync();
            }
        });
    }

    [SlashCommand("инфо", "Высвечивает информацию и код шаблона")]
    public async Task Info(string name) {
        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);

    }

    [SlashCommand("добавить", "Добавляет или изменяет текстовый шаблон")]
    public async Task Add(string name, string input) {
        await DeferAsync();

        // prepare storage and trim input from code brackets
        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);
        input = input.Trim('`');

        var template = new TextTemplate {
            LangVersion = TemplateInterpreter.Version,
            AuthorId = Context.User.Id,
            Source = input,
            Name = name,
            Version = 1,
        };

        // find already defined template, if it exists
        var existingTemplate = storage.TextTemplates.Find(t => t.Name == template.Name);

        await HandleTemplateErrors(input, async () => {
            var result = template.Execute(storage, i => PrepareTemplateVariables(i, provideTestArgs: true));

            if (existingTemplate != null) {
                // keep track of contributions
                if (template.AuthorId != existingTemplate.AuthorId) 
                    existingTemplate.Contributors.Add(template.AuthorId);
                
                // move new values to existing template
                existingTemplate.LangVersion = template.LangVersion;
                existingTemplate.CompiledExpression = template.CompiledExpression;
                existingTemplate.Source = template.Source;
                existingTemplate.Version++;

                // update the reference to existing template
                template = existingTemplate;

                // else, add template to storage as new
            } else storage.TextTemplates.Add(template);

            // mark templates as dirty
            storage.ShouldSaveTemplates = true;

            await ModifyOriginalResponseAsync(msg => {
                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithAuthor(Context.User)
                    .WithTitle(existingTemplate != null ? $"Шаблон '{template.Name}' обновлен до версии {template.Version}!" :
                        $"Шаблон '{template.Name}' успешно добавлен!");

                if (!string.IsNullOrEmpty(result.Value)) {
                    embed = embed.AddField("Пример:", result.Value);
                }

                if (result.ImageStream != null) {
                    msg.Attachments = new([new FileAttachment(result.ImageStream, $"result.{result.ImageFormat}")]);
                    embed = embed.WithImageUrl($"attachment://result.{result.ImageFormat}");
                }

                msg.Embed = embed.Build();
            });

            if (result.ImageStream != null) {
                // dispose of the img stream
                await result.ImageStream.DisposeAsync();
            }
        });
    }

    [SlashCommand("выполнить", "Возвращает результат выполнения шаблона")]
    public async Task Run([Autocomplete] string name,
        [Summary(description: "Список аргументов для передачи в шаблон")] string args = default,
        [Summary(description: "Картинка для передачи в шаблон")] IAttachment attachmentArg = default,
        [MinValue(1)] [MaxValue(50)] int repeat = 1) {

        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);
        var template = storage.TextTemplates.Find(t => t.Name == name);

        if (template is null) {
            await RespondAsync($"Шаблона '{name}' не существует.");
            return;
        }

        await DeferAsync();

        // prepare args
        var argTable = await PrepareArguments(args, attachmentArg);

        await HandleTemplateErrors(template.Source, async () => {
            var results = new List<IValue.TemplateResult>();

            for (var i = 0; i < repeat; i++) {
                var result = template.Execute(storage, i => PrepareTemplateVariables(i, argTable));
                results.Add(result);

                // allow only one image per run
                if (result.ImageStream != null)
                    break;
            }

            var lastResult = results.Last();
            await ModifyOriginalResponseAsync(msg => {
                var embed = new EmbedBuilder()
                    .WithDescription(string.Join("\n", results))
                    .WithColor(Color.Blue);

                if (lastResult.ImageStream != null) {
                    msg.Attachments = new([new FileAttachment(lastResult.ImageStream, $"result.{lastResult.ImageFormat}")]);
                    embed = embed.WithImageUrl($"attachment://result.{lastResult.ImageFormat}");
                }

                msg.Embed = embed.Build();
            });

            if (lastResult.ImageStream != null) {
                // dispose of the created stream
                await lastResult.ImageStream.DisposeAsync();
            }
        });
    }
}
