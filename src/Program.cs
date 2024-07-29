// See https://aka.ms/new-console-template for more information
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Cocona;
using Discord.WebSocket;
using MarkovBot.Data;
using MarkovBot.Discord;
using MarkovBot.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Neo4j.Driver;

var builder = CoconaApp.CreateBuilder();

builder.Services.AddOptions<DatabaseOptions>()
                .Bind(builder.Configuration.GetSection("Database"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

builder.Services.AddOptions<BotOptions>()
                .Bind(builder.Configuration.GetSection("Bot"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

builder.Services.AddSingleton<IDriver>(sp =>
{
    var db = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;

    return GraphDatabase.Driver(
        builder.Configuration.GetConnectionString("Neo4j"),
        AuthTokens.Basic(db.User, db.Password));
});
builder.Services.AddTransient<IWordRepository, WordRepository>();
builder.Services.AddTransient<IServerRepository, ServerRepository>();
builder.Services.AddSingleton(new DiscordSocketConfig());
builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton<DiscordBot>();

var app = builder.Build();

app.AddCommand("import", async (
    [FromService] IWordRepository wordRepository,
    [Argument] string server,
    [Argument] string file,
    CoconaAppContext ctx) =>
{
    var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = true,
    });

    await Task.WhenAll(
        ReadLines(file, channel.Writer, ctx.CancellationToken),
        WriteLines(wordRepository, server, channel.Reader, ctx.CancellationToken)
    );

    static async Task ReadLines(string file, ChannelWriter<string> lineWriter, CancellationToken cancellationToken)
    {
        if (file.EndsWith(".json"))
        {
            using var stream = File.OpenRead(file);

            await foreach (var sentence in JsonSerializer.DeserializeAsyncEnumerable<string>(stream, cancellationToken: cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(sentence))
                    await lineWriter.WriteAsync(sentence, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            using var streamReader = new StreamReader(file);

            string? line = null;
            do
            {
                line = await streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(line))
                    await lineWriter.WriteAsync(line, cancellationToken).ConfigureAwait(false);
            }
            while (line is not null);
        }

        lineWriter.Complete();
    }

    static async Task WriteLines(IWordRepository wordRepository, string server, ChannelReader<string> lineReader, CancellationToken cancellationToken)
    {
        while (await lineReader.WaitToReadAsync(cancellationToken))
        {
            var sentence = await lineReader.ReadAsync(cancellationToken);
            await Console.Out.WriteLineAsync($"Importing sentence: {sentence}");
            var parts = SentenceSplitter.SplitIntoParts(sentence).ToArray();
            if (parts.Length < 1)
                continue;

            await wordRepository.ImportSentenceParts(server, parts, cancellationToken);
        }
    }
});

app.AddCommand("gen", async (
    [FromService] IWordRepository wordRepository,
    [Argument] string server,
    CoconaAppContext ctx,
    [Argument] int maxLength = -1
) =>
{
    var candidates = await wordRepository.SelectStarterWordCandidatesAsync(server);
    if (candidates.Length < 1)
    {
        await Console.Error.WriteLineAsync("No starter word candidates found.");
        return;
    }

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
        candidates = await wordRepository.SelectNextWordCandidatesAsync(server, current);
        if (candidates.Length < 1)
            throw new InvalidOperationException("No next word candidates found.");
    }
    await Console.Out.WriteLineAsync(builder.ToString());
});

app.AddCommand("bot", async ([FromService] DiscordBot bot, CoconaAppContext ctx) =>
{
    await bot.StartAsync();
    // Bot runs on background so we have to explicitly wait.
    try
    {
        await Task.Delay(Timeout.Infinite, ctx.CancellationToken);
    }
    catch (OperationCanceledException) { /* Do nothing because we're expecting this to happen. */ }
    await bot.StopAsync();
});

await app.RunAsync();