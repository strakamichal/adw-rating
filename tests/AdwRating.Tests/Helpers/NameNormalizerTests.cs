using AdwRating.Domain.Helpers;

namespace AdwRating.Tests.Helpers;

[TestFixture]
public class NameNormalizerTests
{
    [TestCase("John Smith", "john smith")]
    [TestCase("  Leading Spaces  ", "leading spaces")]
    [TestCase("Multiple   Spaces   Here", "multiple spaces here")]
    [TestCase("UPPERCASE", "uppercase")]
    [TestCase("MiXeD CaSe", "mixed case")]
    [TestCase("", "")]
    [TestCase("   ", "")]
    [TestCase("already lowercase", "already lowercase")]
    [TestCase("tab\there", "tab here")]
    public void Normalize_ProducesExpectedResult(string input, string expected)
    {
        var result = NameNormalizer.Normalize(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("Kateřina Třičová", "katerina tricova")]
    [TestCase("Müller", "muller")]
    [TestCase("François", "francois")]
    [TestCase("Łukasz Wójcik", "lukasz wojcik")]
    [TestCase("Ünsal Özdemir", "unsal ozdemir")]
    [TestCase("Ångström", "angstrom")]
    public void Normalize_StripsDiacritics(string input, string expected)
    {
        var result = NameNormalizer.Normalize(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("Tercova, Katerina", "katerina tercova")]
    [TestCase("Třičová, Kateřina", "katerina tricova")]
    public void Normalize_ReordersLastFirst(string input, string expected)
    {
        var result = NameNormalizer.Normalize(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("A, B, C", "a, b, c")]
    public void Normalize_DoesNotReorderMultipleCommas(string input, string expected)
    {
        var result = NameNormalizer.Normalize(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("John Smith", "john smith")]
    public void Normalize_DoesNotReorderWithoutComma(string input, string expected)
    {
        var result = NameNormalizer.Normalize(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("\u201CQuoted\u201D", "\"quoted\"")]
    [TestCase("\u2018Quoted\u2019", "'quoted'")]
    public void Normalize_NormalizesTypographicQuotes(string input, string expected)
    {
        var result = NameNormalizer.Normalize(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Normalize_PreservesSingleSpacesBetweenWords()
    {
        var result = NameNormalizer.Normalize("First Last");
        Assert.That(result, Is.EqualTo("first last"));
    }

    [Test]
    public void Normalize_CollapsesVariousWhitespace()
    {
        var result = NameNormalizer.Normalize("word1  \t  word2   word3");
        Assert.That(result, Is.EqualTo("word1 word2 word3"));
    }
}
