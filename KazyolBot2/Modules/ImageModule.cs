using Discord;
using Discord.Interactions;
using KazyolBot2.Images;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace KazyolBot2.Modules;

[Group("картинки", "Модуль для взаимодействия с картинками.")]
public class ImageModule : InteractionModuleBase<SocketInteractionContext> {
    public static readonly HashSet<string> AllowedImageTypes = [
        "image/gif", "image/png", "image/jpeg"
    ];

    [SlashCommand("добавить", "Добавляет приложенную картинку. Для дальнейшего доступа к ней можно указать название или категории.")]
    public async Task Add(IAttachment attachment, 
        [Summary(description: "Одна или несколько категорий, разделенные запятой")]  string categories = default, 
        [Summary(description: "Название файла для точного доступа")]                 string name = default) {

        if (!AllowedImageTypes.Contains(attachment.ContentType)) {
            await RespondAsync("Можно добавлять только изображения в формате png, jpeg и gif.");
            return;
        }

        if (categories == null && name == null) {
            await RespondAsync("Для добавления картинки следует указать одно из следующих (либо оба) свойства: категории, название файла.");
            return;
        }

        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);
        var cats = new HashSet<string>((categories ?? "").Split(',').Select(c => c.Trim()));

        // poop
        var storedPath = await storage.SaveImage(attachment.Url, attachment.ContentType.Split('/')[1], name);

        if (storedPath is null) {
            await RespondAsync("Ошибка загрузки: `файл уже существует`.");
            return;
        }

        var savedImage = new SavedImageData {
            AuthorId = Context.User.Id,
            Name = Path.GetFileName(storedPath),
            Id = storage.LastImageId++,
            ImageUrl = attachment.Url,
            Path = storedPath,
            Categories = cats,
        };

        storage.Images.Add(savedImage);
        storage.ShouldSaveImages = true;

        await RespondAsync(embed: new EmbedBuilder()
            .WithTitle(savedImage.Name)
            .WithAuthor(Context.User)
            .WithDescription(string.Join(", ", cats))
            .WithImageUrl(savedImage.ImageUrl)
            .WithColor(Color.Green)
            .Build());
    }

    [SlashCommand("убрать", "Удаляет картинку по названию.")]
    public async Task Remove([Summary(description: "Название картинки с расширением, его козёл пишет при добавлении")] string name) {
        var storage = ServerStorage.GetOrCreate(Context.Guild.Id);

        var savedImage = storage.Images.Find(i => i.Name == name);

        if (savedImage is null) {
            await RespondAsync($"Картинки с названием '`{name}`' не существует.");
            return;
        }

        storage.Images.Remove(savedImage);
        storage.ShouldSaveImages = true;

        // delete the file
        File.Delete(savedImage.Path);

        await RespondAsync(embed: new EmbedBuilder()
            .WithTitle(savedImage.Name + " была удалена.")
            .WithAuthor(Context.User)
            .WithImageUrl(savedImage.ImageUrl)
            .WithColor(Color.Red)
            .Build());
    }
}
