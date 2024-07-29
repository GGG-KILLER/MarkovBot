using System.Collections.Immutable;
using Neo4j.Driver;

namespace MarkovBot.Data;

internal sealed class WordRepository(IDriver driver) : IWordRepository, IAsyncDisposable
{
    private readonly IAsyncSession _session = driver.AsyncSession();

    public async Task ImportSentenceParts(string server, string[] parts, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var first = parts[0];
        await _session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                """
                    MERGE (start:Start)
                    MERGE (first:Word {text: $text})
                    MERGE (start)-[conn:FOLLOWED_BY {server: $server}]->(first)
                    ON CREATE
                        SET conn.uses = 1
                    ON MATCH
                        SET conn.uses = conn.uses + 1
                    """, new { text = first, server });
        });

        for (var idx = 1; idx < parts.Length; idx++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var prev = parts[idx - 1];
            var curr = parts[idx];

            await _session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    """
                        MERGE (prev:Word {text: $prevText})
                        MERGE (curr:Word {text: $currText})
                        MERGE (prev)-[conn:FOLLOWED_BY {server: $server}]->(curr)
                        ON CREATE
                            SET conn.uses = 1
                        ON MATCH
                            SET conn.uses = conn.uses + 1
                        """, new { prevText = prev, currText = curr, server });
            });
        }

        cancellationToken.ThrowIfCancellationRequested();

        var last = parts[^1];
        await _session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                """
                    MERGE (last:Word {text: $text})
                    MERGE (end:End)
                    MERGE (last)-[conn:FOLLOWED_BY {server: $server}]->(end)
                    ON CREATE
                        SET conn.uses = 1
                    ON MATCH
                        SET conn.uses = conn.uses + 1
                    """, new { text = last, server });
        });
    }

    public async Task<ImmutableArray<WordCandidate>> SelectStarterWordCandidatesAsync(string server)
    {
        var temp = await _session.ExecuteReadAsync(async tx =>
        {
            var result = await tx.RunAsync(
                """
                MATCH (start:Start)-[conn:FOLLOWED_BY {server: $server}]->(starter:Word)
                WITH distinct starter, conn
                OPTIONAL MATCH (server:Server {id: $server})-[:FORBIDS_WORD]->(forbidden:Word)
                    WHERE forbidden = starter
                WITH starter, conn, count(forbidden) AS excl
                    WHERE excl = 0
                RETURN starter.text, conn.uses
                """, new { server });

            return await result.Select(r => new WordCandidate(r[0].As<string?>(), r[1].As<int>())).ToArrayAsync();
        });

        return ImmutableArray.Create(temp);
    }

    public async Task<ImmutableArray<WordCandidate>> SelectNextWordCandidatesAsync(string server, string currentWord)
    {
        var temp = await _session.ExecuteReadAsync(async tx =>
        {
            var result = await tx.RunAsync(
                """
                MATCH (curr:Word {text: $text})-[conn:FOLLOWED_BY {server: $server}]->(next)
                WITH distinct next, conn
                OPTIONAL MATCH (server:Server {id: $server})-[:FORBIDS_WORD]->(forbidden:Word)
                    WHERE forbidden = next
                WITH next, conn, count(forbidden) AS excl
                    WHERE excl = 0
                RETURN next.text, conn.uses
                """, new { text = currentWord, server });

            return await result.Select(r => new WordCandidate(r[0].As<string?>(), r[1].As<int>())).ToArrayAsync();
        });

        return ImmutableArray.Create(temp);
    }

    public ValueTask DisposeAsync() => _session.DisposeAsync();
}
