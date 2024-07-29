namespace MarkovBot.Data;

public readonly record struct WordCandidate(string? Text, int Uses)
{
    public static WordCandidate Select(IReadOnlyList<WordCandidate> candidates, Random random)
    {
        var acc = 0;
        var cdf = new List<int>(candidates.Count);
        foreach ((_, var weight) in candidates)
        {
            acc += weight;
            cdf.Add(acc);
        }

        var value = random.Next(0, acc);
        var index = cdf.BinarySearch(value);

        if (index >= 0)
            return candidates[index];
        else
            return candidates[Math.Clamp(~index, 0, candidates.Count - 1)];
    }
}