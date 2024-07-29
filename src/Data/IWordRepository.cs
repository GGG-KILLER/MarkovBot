using System.Collections.Immutable;

namespace MarkovBot.Data;

public interface IWordRepository
{
    Task ImportSentenceParts(string server, string[] parts, CancellationToken cancellationToken = default);
    Task<ImmutableArray<WordCandidate>> SelectStarterWordCandidatesAsync(string server);
    Task<ImmutableArray<WordCandidate>> SelectNextWordCandidatesAsync(string server, string currentWord);
}