using AdwRating.Domain.Helpers;

namespace AdwRating.Tests.Helpers;

public class NameNormalizerTests
{
    [Theory]
    [InlineData("John Smith", "john smith")]
    [InlineData("  Leading Spaces  ", "leading spaces")]
    [InlineData("Multiple   Spaces   Here", "multiple spaces here")]
    [InlineData("UPPERCASE", "uppercase")]
    [InlineData("MiXeD CaSe", "mixed case")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("already lowercase", "already lowercase")]
    [InlineData("tab\there", "tab here")]
    public void Normalize_ProducesExpectedResult(string input, string expected)
    {
        var result = NameNormalizer.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Kateřina Třičová", "katerina tricova")]
    [InlineData("Müller", "muller")]
    [InlineData("François", "francois")]
    [InlineData("Łukasz Wójcik", "lukasz wojcik")]
    [InlineData("Ünsal Özdemir", "unsal ozdemir")]
    [InlineData("Ångström", "angstrom")]
    public void Normalize_StripsDiacritics(string input, string expected)
    {
        var result = NameNormalizer.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_PreservesSingleSpacesBetweenWords()
    {
        var result = NameNormalizer.Normalize("First Last");
        Assert.Equal("first last", result);
    }

    [Fact]
    public void Normalize_CollapsesVariousWhitespace()
    {
        var result = NameNormalizer.Normalize("word1  \t  word2   word3");
        Assert.Equal("word1 word2 word3", result);
    }
}
