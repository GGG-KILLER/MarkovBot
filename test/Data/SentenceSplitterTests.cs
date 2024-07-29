using System.Reflection;
using MarkovBot.Data;
using Xunit.Sdk;

namespace MarkovBot.Test.Data;

public class SentenceSplitterTests
{
    [Theory]
    [InlineSentenceData("Hello world.", ["Hello", "world", "."])]
    [InlineSentenceData("Hello there https://example.com?query=string,param:for#you", ["Hello", "there", "https://example.com?query=string,param:for#you"])]
    [InlineSentenceData("This, yes this, is quite a long sentence isn't it?!?!?!?", ["This", ",", "yes", "this", ",", "is", "quite", "a", "long", "sentence", "isn't", "it", "?", "!", "?", "!", "?", "!", "?"])]
    public void SentenceSplitter_ProperlySplitsSimpleSentences(string sentence, string[] expectedParts)
    {
        // Given

        // When
        var actualParts = SentenceSplitter.SplitIntoParts(sentence).ToArray();

        // Then
        Assert.Equal(expectedParts, actualParts);
    }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
sealed class InlineSentenceDataAttribute(string sentence, string[] parts) : DataAttribute
{
    public override IEnumerable<object[]> GetData(MethodInfo testMethod) => [[sentence, parts]];
}