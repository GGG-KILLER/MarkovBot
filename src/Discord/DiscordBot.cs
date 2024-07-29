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
        discordClient.Ready += ClientReady;
        discordClient.JoinedGuild += GuildInitHandler;
        discordClient.GuildAvailable += GuildInitHandler;
        discordClient.Log += MessageLogged;
        discordClient.InteractionCreated += InteractionCreated;

        _interactionService.Log += MessageLogged;

        await _interactionService.AddModuleAsync<MarkovCommands>(serviceProvider);
        await _interactionService.AddModuleAsync<AdminCommands>(serviceProvider);

        await discordClient.LoginAsync(TokenType.Bot, botOptions.CurrentValue.Token).ConfigureAwait(false);
        await discordClient.StartAsync().ConfigureAwait(false);
    }

    private async Task InteractionCreated(SocketInteraction interaction)
    {
        var ctx = new SocketInteractionContext(discordClient, interaction);
        await _interactionService.ExecuteCommandAsync(ctx, serviceProvider);
    }

    private async Task MessageLogged(LogMessage message)
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

    private async Task ClientReady()
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