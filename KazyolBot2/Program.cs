// See https://aka.ms/new-console-template for more information

using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using KazyolBot2;
using System.Reflection;

var client = new DiscordSocketClient(new DiscordSocketConfig {
    UseInteractionSnowflakeDate = false
});

AppDomain.CurrentDomain.ProcessExit += (_, _) => Save(true);

Console.WriteLine("Preparing Storage...");
await ServerStorage.Load();

var timer = new Timer((_) => Save(false), null, 1000, 10000);

client.Ready += OnReady;

Console.WriteLine("Starting the bot...");

await client.LoginAsync(TokenType.Bot, await File.ReadAllTextAsync(ServerStorage.TokenFileName));
await client.StartAsync();

Console.WriteLine($"Hello, [K4ZY0L]!");

await Task.Delay(-1);

void Save(bool isShuttingDown) {
    foreach (var (guildId, store) in ServerStorage.ByGuildId) {
        if (!store.ShouldSave)
            continue;

        // run save method synchronously
        store.Save(isShuttingDown).GetAwaiter().GetResult();
    }
}

// OnReady callback
async Task OnReady() {
    var interactionService = new InteractionService(client);

    await interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), null);
    await interactionService.RegisterCommandsToGuildAsync(1206273190058270740);
    await interactionService.RegisterCommandsToGuildAsync(469253457308680193);

    client.InteractionCreated += async interaction => {
        await interactionService.ExecuteCommandAsync(new SocketInteractionContext(client, interaction), null);
    };

    await client.SetCustomStatusAsync("V3.1");
}