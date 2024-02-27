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

[Group("текст", "Команды для регистрации контента!")]
public class TextModule : InteractionModuleBase<SocketInteractionContext> {
    async Task AutocompleteCategory() {
        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);

        var autocompleteInteraction = Context.Interaction as SocketAutocompleteInteraction;
        var input = autocompleteInteraction.Data.Current.Value.ToString();

        await autocompleteInteraction.RespondAsync(
            storage.TextFragments.Select(f => f.Category)
                .Distinct()
                .OrderBy(c => LevenshteinDistance.Compute(c, input))
                .Select(c => new AutocompleteResult(c, c))
                .TakeLast(25)
        );
    }

    [AutocompleteCommand("category", "добавить")]
    public async Task AutocompleteCategoryAdd() => await AutocompleteCategory();

    [SlashCommand("поиск", "Ищет айдишники страшных слов")]
    public async Task Search(string text) {
        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);
        var foundItems = storage.TextFragments.Where(t => t.Text.StartsWith(text))
            .GroupBy(t => t.Category);

        if (string.IsNullOrWhiteSpace(text)) {
            await RespondAsync("А где текст епта.");
            return;
        }

        try {
            var count = foundItems.SelectMany(g => g).Count();
            await RespondAsync($"Найдено {count} теста.", embed: count == 0 ? null : new EmbedBuilder()
                .WithFields(foundItems.Select(g => new EmbedFieldBuilder()
                    .WithName(g.Key)
                    .WithValue(string.Join("\n", g.Select(t => $"[`#{t.Id}`] {t.Text}"))))
                    .ToArray()
                ).Build());
        } catch (Exception ex) {
            await RespondAsync("Слишком обширный поиск, попробуй что-нибудь конкретнее.");
        }
    }

    [SlashCommand("добавить", "Добавляет разделенные запятой слова в категорию")]
    public async Task AddMany([Autocomplete] string category, string text) {
        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);
        storage.ShouldSaveFragments = true;

        if (string.IsNullOrWhiteSpace(category)) {
            await RespondAsync("А где текст епта.");
            return;
        }

        var words = new List<string>();
        var addedFragments = new List<TextFragment>();
        var duplicateFragments = new List<TextFragment>();

        var textPos = 0;
        while (textPos < text.Length) {
            var ch = text[textPos++];

            if (char.IsWhiteSpace(ch) || ch == ',')
                continue;

            if (ch == '"') {
                var builder = new StringBuilder();
                while (textPos < text.Length && text[textPos] != '"') {
                    builder.Append(text[textPos]);
                    textPos++;
                }

                words.Add(builder.ToString());
                textPos++;

                continue;
            }

            var word = new StringBuilder();
            word.Append(ch);
            while (textPos < text.Length && text[textPos] != ',') {
                word.Append(text[textPos]);
                textPos++;
            }

            words.Add(word.ToString());
            textPos++;
        }

        foreach (var item in words) {
            var fragment = new TextFragment {
                Id = storage.LastTextFragmentId++,
                AuthorId = Context.User.Id,
                Category = category,
                Text = item.Trim(),
            };

            if (storage.TextFragments.Any(t => t.Category == fragment.Category && t.Text == fragment.Text)) {
                duplicateFragments.Add(fragment);
                continue;
            }

            storage.TextFragments.Add(fragment);
            addedFragments.Add(fragment);
        }

        var message = "";
        if (addedFragments.Count > 0) {
            message = $"В категорию `{category}` записано: {string.Join(", ", addedFragments.Select(fragment => $"[`#{fragment.Id}`] `\"{fragment.Text}\"`"))}";
        }

        if (duplicateFragments.Count > 0) {
            message += $"\n{string.Join(", ", duplicateFragments.Select(f => $"`\"{f.Text}\"`"))} уже находились в `{category}`.";
        }

        await RespondAsync(message);
    }

    [SlashCommand("убрать", "Удаляет текст по Id")]
    public async Task Remove(string ids) {
        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);

        var removedFragments = new List<TextFragment>();

        var textIds = ids.Split(',');
        foreach (var stringId in textIds) {
            if (!ulong.TryParse(stringId, out var id))
                continue;

            if (storage.TextFragments.FirstOrDefault(f => f.Id == id) is not TextFragment fragment)
                continue;

            storage.TextFragments.Remove(fragment);
            storage.ShouldSaveFragments = true;
            removedFragments.Add(fragment);
        }

        await RespondAsync($"{string.Join(", ", removedFragments.Select(fragment => $"[`#{fragment.Id}`] `\"{fragment.Text}\"`"))} были удалены из категорий " +
            $"`{string.Join(", ", removedFragments.Select(t => $"\"{t.Category}\"").Distinct())}`.");
    }
}
