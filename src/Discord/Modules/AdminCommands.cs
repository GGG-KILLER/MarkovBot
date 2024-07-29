
using Discord;
using Discord.Interactions;
using MarkovBot.Data;

namespace MarkovBot.Discord.Modules;

[Group("markov-admin", "Markov related commands")]
public sealed class AdminCommands(IServerRepository serverRepository) : InteractionModuleBase<SocketInteractionContext>
{
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    [SlashCommand("block-word", "Blocks a word from being returned by the bot.")]
    public async Task BlockWordAsync(string word)
    {
        var parts = SentenceSplitter.SplitIntoParts(word).ToArray();
        if(parts.Length > 1)
        {
            await Context.Interaction.RespondAsync($"Too many parts detected: `{string.Join("`, `", parts)}`");
        }
        await serverRepository.AddForbiddenWord(Context.Interaction.GuildId.ToString()!, parts[0]);
        await Context.Interaction.RespondAsync("Word added to server blocklist.");
    }

    [DefaultMemberPermissions(GuildPermission.Administrator)]
    [SlashCommand("allow-word", "Blocks a word from being returned by the bot.")]
    public async Task AllowWordAsync(string word)
    {
        var parts = SentenceSplitter.SplitIntoParts(word).ToArray();
        if(parts.Length > 1)
        {
            await Context.Interaction.RespondAsync($"Too many parts detected: `{string.Join("`, `", parts)}`");
        }
        await serverRepository.RemoveForbiddenWord(Context.Interaction.GuildId.ToString()!, parts[0]);
        await Context.Interaction.RespondAsync("Word removed from server blocklist.");
    }
}