using KazyolBot2.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

    public static ConcurrentDictionary<ulong, ServerStorage> ByGuildId { get; } = [];
    public static ServerStorage GetOrCreate(ulong guildId) {
        if (ByGuildId.TryGetValue(guildId, out var storage))
            return storage;

        return ByGuildId[guildId] = new() { GuildId = guildId };
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

            var textsJsonPath = Path.Combine(dir, "texts.json");
            if (File.Exists(textsJsonPath)) {
                using var stream = File.OpenRead(textsJsonPath);
                store.TextFragments = await JsonSerializer.DeserializeAsync<List<TextFragment>>(stream, options: new(JsonSerializerDefaults.Web));
            }

            // get last fragment id
            store.LastTextFragmentId = store.TextFragments.Max(t => t.Id) + 1;

            var templatesJsonPath = Path.Combine(dir, "templates.json");
            if (File.Exists(templatesJsonPath)) {
                using var stream = File.OpenRead(templatesJsonPath);
                store.TextTemplates = await JsonSerializer.DeserializeAsync<List<TextTemplate>>(stream, options: new(JsonSerializerDefaults.Web));
            }

            Console.WriteLine($"- Loaded {guildId}");
        }
    }

    public string DataPath => Path.Combine(StorageDirectoryName, GuildId.ToString());

    public bool ShouldSave => ShouldSaveFragments || ShouldSaveTemplates;

    public ulong GuildId { get; private set; }

    public List<TextTemplate> TextTemplates { get; private set; } = [];
    public bool ShouldSaveTemplates { get; set; }

    public ulong LastTextFragmentId;
    public List<TextFragment> TextFragments { get; private set; } = [];
    public bool ShouldSaveFragments { get; set; }

    public async Task Save() {
        Directory.CreateDirectory(DataPath);

        if (ShouldSaveFragments) {
            // save word fragments
            using var fileStream = File.Open(Path.Combine(DataPath, "texts.json"), FileMode.Create);
            await JsonSerializer.SerializeAsync(fileStream, TextFragments, options: new(JsonSerializerDefaults.Web) {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic),
                WriteIndented = true
            });

            // flip the flag back
            ShouldSaveFragments = false;
        }

        if (ShouldSaveTemplates) {
            // save text templates
            using var fileStream = File.Open(Path.Combine(DataPath, "templates.json"), FileMode.Create);
            await JsonSerializer.SerializeAsync(fileStream, TextTemplates, options: new(JsonSerializerDefaults.Web) {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic),
                WriteIndented = true
            });

            // flip the flag back
            ShouldSaveTemplates = false;
        }
    }
}
