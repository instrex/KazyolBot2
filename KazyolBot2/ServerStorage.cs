using KazyolBot2.Images;
using KazyolBot2.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace KazyolBot2;

public class ServerStorage {
    public const string StorageDirectoryName = "Storage";
    public const string DataDirectoryName = "Data";

    public static string TokenFileName => Path.Combine(DataDirectoryName, "token.txt");

    public static ConcurrentDictionary<ulong, ServerStorage> ByGuildId { get; } = [];
    public static ServerStorage GetOrCreate(ulong guildId) {
        if (ByGuildId.TryGetValue(guildId, out var storage))
            return storage;

        return ByGuildId[guildId] = new() { GuildId = guildId };
    }

    public string DataPath => Path.Combine(StorageDirectoryName, GuildId.ToString());

    public bool ShouldSave => ShouldSaveFragments || ShouldSaveTemplates || ShouldSaveImages;

    public ulong GuildId { get; private set; }

    // image data
    public List<SavedImageData> Images { get; private set; } = [];
    public bool ShouldSaveImages { get; set; }

    // template data
    public List<TextTemplate> TextTemplates { get; private set; } = [];
    public bool ShouldSaveTemplates { get; set; }

    public ulong LastTextFragmentId, LastImageId;

    // fragment data
    public List<TextFragment> TextFragments { get; private set; } = [];
    public bool ShouldSaveFragments { get; set; }

    public async Task<string> SaveImage(string url, string type, string filename) {
        var dir = Path.Combine(DataPath, "img");
        Directory.CreateDirectory(dir);

        if (filename != null) {
            var real = Path.GetFileNameWithoutExtension(filename);
            filename = $"{real}.{type}";

            // filename is taken
            if (File.Exists(filename)) 
                return null;

        } else do {

            // search for new random filename
            filename = $"{Path.GetRandomFileName()}.{type}";
        } while (File.Exists(filename));

        // get big path
        filename = Path.Combine(dir, filename);

        using var webClient = new HttpClient();

        try {
            // download stuff
            using var outputFileStream = new FileStream(filename, FileMode.Create);
            using var webStream = await webClient.GetStreamAsync(url);
            await webStream.CopyToAsync(outputFileStream);

            // return physical location
            return filename;
        } catch {
            // rip
            return null;
        }
    }

    public async Task Save(bool isShuttingDown) {
        Directory.CreateDirectory(DataPath);

        if (ShouldSaveImages) {
            // save images
            using var fileStream = File.Open(Path.Combine(DataPath, "images.json"), FileMode.Create);
            await JsonSerializer.SerializeAsync(fileStream, Images.Where(img => !img.Ephemeral).ToList(), options: new(JsonSerializerDefaults.Web) {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            });

            // flip the flag back
            ShouldSaveImages = false;
        }

        // delete ephemeral images when shutting down
        if (isShuttingDown) {
            foreach (var img in Images.Where(i => i.Ephemeral)) {
                File.Delete(img.Path);
            }
        }

        if (ShouldSaveFragments) {
            // save word fragments
            using var fileStream = File.Open(Path.Combine(DataPath, "texts.json"), FileMode.Create);
            await JsonSerializer.SerializeAsync(fileStream, TextFragments, options: new(JsonSerializerDefaults.Web) {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            });

            // flip the flag back
            ShouldSaveFragments = false;
        }

        if (ShouldSaveTemplates) {
            // save text templates
            using var fileStream = File.Open(Path.Combine(DataPath, "templates.json"), FileMode.Create);
            await JsonSerializer.SerializeAsync(fileStream, TextTemplates, options: new(JsonSerializerDefaults.Web) {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            });

            // flip the flag back
            ShouldSaveTemplates = false;
        }
    }

    public static async Task Load() {
        if (!Directory.Exists(StorageDirectoryName))
            return;

        foreach (var dir in Directory.GetDirectories(StorageDirectoryName)) {
            var guildIdString = Path.GetFileName(dir);
            if (!ulong.TryParse(guildIdString, out var guildId)) {
                Console.WriteLine($"- Failed to load directory. '{guildIdString}'");
                continue;
            }

            var store = GetOrCreate(guildId);

            Console.WriteLine($"- Loading {guildId}...");

            var textsJsonPath = Path.Combine(dir, "texts.json");
            if (File.Exists(textsJsonPath)) {
                using var stream = File.OpenRead(textsJsonPath);
                store.TextFragments = await JsonSerializer.DeserializeAsync<List<TextFragment>>(stream, options: new(JsonSerializerDefaults.Web));
                Console.WriteLine($"  - [{store.TextFragments.Count}] text fragments.");
            }

            if (store.TextFragments.Count > 0) {
                // get last fragment id
                store.LastTextFragmentId = store.TextFragments.Max(t => t.Id) + 1;
            }

            var templatesJsonPath = Path.Combine(dir, "templates.json");
            if (File.Exists(templatesJsonPath)) {
                using var stream = File.OpenRead(templatesJsonPath);
                store.TextTemplates = await JsonSerializer.DeserializeAsync<List<TextTemplate>>(stream, options: new(JsonSerializerDefaults.Web));
                Console.WriteLine($"  - [{store.TextTemplates.Count}] templates.");
            }

            var imagesJsonPath = Path.Combine(dir, "images.json");
            if (File.Exists(imagesJsonPath)) {
                using var stream = File.OpenRead(imagesJsonPath);
                store.Images = await JsonSerializer.DeserializeAsync<List<SavedImageData>>(stream, options: new(JsonSerializerDefaults.Web));
                Console.WriteLine($"  - [{store.Images.Count}] images.");
            }

            if (store.Images.Count > 0) {
                // get last image id
                store.LastImageId = store.Images.Max(i => i.Id) + 1;
            }
        }
    }
}
