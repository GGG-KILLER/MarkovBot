using System.Diagnostics;
using System.Text;
using Discord;
using Discord.Interactions;
using MarkovBot.Data;
using MarkovBot.Options;
using Microsoft.Extensions.Options;

namespace MarkovBot.Discord.Modules;

public sealed class MarkovCommands(IOptionsMonitor<BotOptions> botOptions, IWordRepository wordRepository) : InteractionModuleBase<SocketInteractionContext>
{
    [DefaultMemberPermissions(GuildPermission.SendMessages)]
    [SlashCommand("markov", "Generates a message using Markov")]
    public async Task GenerateAsync(
        [Summary("max-length", "The maximum number of words the response can have.")] int maxLength = -1)
    {
        if (maxLength == -1) maxLength = botOptions.CurrentValue.HardWordLimit;
        maxLength = Math.Min(maxLength, botOptions.CurrentValue.HardWordLimit);

        var guild = Context.Guild.Id.ToString();

        var candidates = await wordRepository.SelectStarterWordCandidatesAsync(guild);
        if (candidates.Length < 1)
        {
            await Context.Interaction.RespondAsync("No messages have been indexed for your server yet.");
            return;
        }

        await Context.Interaction.RespondAsync("Generating...", allowedMentions: new AllowedMentions(AllowedMentionTypes.None));

        var sw = Stopwatch.StartNew();
        var builder = new StringBuilder();
        var len = 0;
        string? current;
        while ((current = WordCandidate.Select(candidates, Random.Shared).Text) is not null)
        {
            // Add spacing if we aren't a punctuation.
            if (len > 0 && !Rune.IsPunctuation(current.EnumerateRunes().First()))
                builder.Append(' ');
            builder.Append(current);
            len += 1;

            if (maxLength != -1 && len >= maxLength)
                break;

            // Select the next candidates
            candidates = await wordRepository.SelectNextWordCandidatesAsync(guild, current);
            if (candidates.Length < 1)
                break;
        }
        sw.Stop();

        await Context.Interaction.ModifyOriginalResponseAsync(prop =>
        {
            prop.Content = builder.ToString();
        }, new RequestOptions { RetryMode = RetryMode.AlwaysRetry });
    }
}