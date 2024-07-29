using System.Collections.Immutable;
using Neo4j.Driver;

namespace MarkovBot.Data;

public sealed class ServerRepository(IDriver driver) : IServerRepository, IAsyncDisposable
{
    private readonly IAsyncSession _session = driver.AsyncSession();

    public async Task InitializeServer(string id, IEnumerable<string> forbiddenWords)
    {
        await _session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                """
                MERGE (s:Server {id: $id})
                MERGE (i:IndexStatus)
                MERGE (s)-[:HAS_INDEX]->(i)
                """,
                new { id });

            foreach (var word in forbiddenWords)
            {
                var res = await tx.RunAsync(
                    """
                    MATCH (s:Server {id: $id})
                    MERGE (w:Word {text: $word})
                    MERGE (s)-[:FORBIDS_WORD]->(w)
                    """,
                    new { id, word });
            }
        }).ConfigureAwait(false);
    }

    public async Task<ImmutableHashSet<string>> GetForbiddenWords(string id)
    {
        return await _session.ExecuteReadAsync(async tx =>
        {
            var res = await tx.RunAsync(
                """
                MATCH (:Server {id: $id})-[:FORBIDS_WORD]->(word:Word) RETURN word.text
                """, new { id });

            return (await res.Select(x => x[0].As<string>()).ToHashSetAsync())
                .ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);
        }).ConfigureAwait(false);
    }

    public async Task AddForbiddenWord(string id, string word)
    {
        await _session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                """
                MATCH (s:Server {id: $id})
                MERGE (w:Word {text: $word})
                MERGE (s)-[:FORBIDS_WORD]->(w)
                """,
                new { id, word });
        }).ConfigureAwait(false);
    }

    public async Task RemoveForbiddenWord(string id, string word)
    {
        await _session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                """
                OPTIONAL MATCH (:Server {id: $id})-[link:FORBIDS_WORD]->(:Word {text: $word})
                DELETE link
                """,
                new { id, word });
        });
    }

    public ValueTask DisposeAsync()
    {
        return _session.DisposeAsync();
    }
}

public interface IServerRepository
{
    Task InitializeServer(string id, IEnumerable<string> forbiddenWords);
    Task<ImmutableHashSet<string>> GetForbiddenWords(string id);
    Task AddForbiddenWord(string id, string word);
    Task RemoveForbiddenWord(string id, string word);
}