using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using MarkovBot.Data;
using MarkovBot.Discord.Modules;
using MarkovBot.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarkovBot.Discord;

internal sealed class DiscordBot(
    IServiceProvider serviceProvider,
    IOptionsMonitor<BotOptions> botOptions,
    DiscordSocketClient discordClient,
    IWordRepository wordRepository,
    IServerRepository serverRepository,
    ILogger<DiscordBot> logger)
{
    private readonly InteractionService _interactionService = new(discordClient.Rest, new InteractionServiceConfig
    {
        AutoServiceScopes = true,
        DefaultRunMode = RunMode.Async,
        UseCompiledLambda = true,
    });

    public async Task StartAsync()
    {
        discordClient.Ready += ClientReadyHandler;
        discordClient.JoinedGuild += GuildInitHandler;
        discordClient.GuildAvailable += GuildInitHandler;
        discordClient.Log += MessageLoggedHandler;
        discordClient.InteractionCreated += InteractionCreatedHandler;
        discordClient.MessageReceived += MessageReceivedHandler;

        _interactionService.Log += MessageLoggedHandler;

        await _interactionService.AddModuleAsync<MarkovCommands>(serviceProvider);
        await _interactionService.AddModuleAsync<AdminCommands>(serviceProvider);

        await discordClient.LoginAsync(TokenType.Bot, botOptions.CurrentValue.Token).ConfigureAwait(false);
        await discordClient.StartAsync().ConfigureAwait(false);
    }

    private async Task MessageReceivedHandler(SocketMessage message)
    {
        if (message.Type is not (MessageType.Default or MessageType.Reply)
            || message.Source is not MessageSource.User)
            return;

        var channel = (SocketGuildChannel)message.Channel;
        var guild = channel.Guild.Id.ToString();
        var parts = SentenceSplitter.SplitIntoParts(message.Content).ToArray();
        await wordRepository.ImportSentenceParts(guild, parts);
    }

    private async Task InteractionCreatedHandler(SocketInteraction interaction)
    {
        var ctx = new SocketInteractionContext(discordClient, interaction);
        await _interactionService.ExecuteCommandAsync(ctx, serviceProvider);
    }

    private async Task MessageLoggedHandler(LogMessage message)
    {
        await Task.Yield();

        var severity = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Information
        };

        logger.Log(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
    }

    private async Task ClientReadyHandler()
    {
#if DEBUG
        await _interactionService.RegisterCommandsToGuildAsync(botOptions.CurrentValue.TestGuild, true)
                                 .ConfigureAwait(false);
#else
        await _interactionService.RegisterCommandsGloballyAsync()
                                 .ConfigureAwait(false);
#endif
    }

    private async Task GuildInitHandler(SocketGuild guild)
    {
        // This is indepotent in that it won't do anything if info is already there.
        await serverRepository.InitializeServer(guild.Id.ToString(), botOptions.CurrentValue.DefaultForbiddenWords);
    }

    public async Task StopAsync()
    {
        await discordClient.StopAsync().ConfigureAwait(false);
        await discordClient.LogoutAsync().ConfigureAwait(false);
    }
}