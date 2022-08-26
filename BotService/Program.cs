using System.Net.WebSockets;
using bot;
using BotService;
using Discord;
using Discord.Addons.Hosting;
using Discord.WebSocket;

IHost host = Host.CreateDefaultBuilder()
    .ConfigureDiscordHost((context, config) =>
    {
        context.Configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        config.SocketConfig = new DiscordSocketConfig
        {
            LogLevel = LogSeverity.Verbose,
            AlwaysDownloadUsers = true,
            MessageCacheSize = 200
        };

        config.Token = context.Configuration["DiscordToken:TokenValue"];
    })
    .UseCommandService((context, config) =>
    {
        config.DefaultRunMode = Discord.Commands.RunMode.Async;
        config.CaseSensitiveCommands = false;
    })
    .UseInteractionService((context, config) =>
    {
        config.LogLevel = LogSeverity.Info;
        config.UseCompiledLambda = true;
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton<DiscordSocketClient>();
        services.AddSingleton<ClientWebSocket>();
        services.AddHostedService<CommandHandler>();
        services.AddSingleton<PlayManager>();
    })
    .Build();

await host.RunAsync();