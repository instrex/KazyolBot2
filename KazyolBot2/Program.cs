// See https://aka.ms/new-console-template for more information

using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using KazyolBot2;
using System.Reflection;

var client = new DiscordSocketClient(new DiscordSocketConfig {
    UseInteractionSnowflakeDate = false
});

AppDomain.CurrentDomain.ProcessExit += (_, _) => Save();

Console.WriteLine("Preparing Storage...");
await ServerStorage.Load();

var timer = new Timer((_) => Save(), null, 1000, 10000);

client.Ready += OnReady;

Console.WriteLine("Starting the bot...");

await client.LoginAsync(Discord.TokenType.Bot, "NDY5MjUyNDE4OTUwMDA0NzM2.GiYJsU.kZEAsn2_7rMXbmepQRKDrvlCZXeEWfKYTlkALM");
await client.StartAsync();

Console.WriteLine($"Hello, [K4ZY0L]!");

await Task.Delay(-1);

void Save() {
    foreach (var (guildId, store) in ServerStorage.ByGuildId) {
        if (!store.ShouldSave)
            continue;

        // run save method synchronously
        store.Save().GetAwaiter().GetResult();
    }
}

// OnReady callback
async Task OnReady() {
    var interactionService = new InteractionService(client);

    await interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), null);
    await interactionService.RegisterCommandsToGuildAsync(469253457308680193);

    client.InteractionCreated += async interaction => {
        await interactionService.ExecuteCommandAsync(new SocketInteractionContext(client, interaction), null);
    };
}