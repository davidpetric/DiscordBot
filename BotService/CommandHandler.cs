using System.Reflection;
using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;

namespace BotService;
public class CommandHandler : DiscordClientService
{
    private readonly ILogger<CommandHandler> _logger;
    private readonly IServiceProvider _provider;
    private readonly CommandService _commandService;
    private readonly IConfiguration _config;

    public CommandHandler(DiscordSocketClient client, ILogger<CommandHandler> logger, IServiceProvider provider, CommandService commandService, IConfiguration config) : base(client, logger)
    {
        _logger = logger;
        _provider = provider;
        _commandService = commandService;
        _config = config;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Client.MessageReceived += HandleMessage;
        _commandService.CommandExecuted += CommandExecutedAsync;
        await _commandService.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
    }

    private async Task HandleMessage(SocketMessage incomingMessage)
    {
        if (incomingMessage is not SocketUserMessage message)
        {
            return;
        }

        if (message.Source != MessageSource.User)
        {
            return;
        }

        int argPos = 0;
        if (!message.HasStringPrefix(_config["Prefix"], ref argPos) && !message.HasMentionPrefix(Client.CurrentUser, ref argPos))
        {
            return;
        }

        SocketCommandContext? context = new SocketCommandContext(Client, message);
        await _commandService.ExecuteAsync(context, argPos, _provider);
    }

    public Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
    {
        try
        {
            Logger.LogInformation("User {user} attempted to use command {command}", context.User, command.Value.Name);

            if (!command.IsSpecified || result.IsSuccess)
            {
                return Task.CompletedTask;
            }

            _logger.LogError(result.ErrorReason, result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
        return Task.CompletedTask;
    }
}