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

    [TestCase("Kerek-Ful", "kerek ful")]
    [TestCase("T-REX", "t rex")]
    [TestCase("Nova`s", "nova's")]
    [TestCase("Adam-Bokenyi", "adam bokenyi")]
    public void Normalize_NormalizesHyphensAndBackticks(string input, string expected)
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

    [TestCase("Jessi Jessi", "jessi")]
    [TestCase("Lucky Lucky", "lucky")]
    [TestCase("word word word", "word")]
    [TestCase("hello world world", "hello world")]
    [TestCase("hello hello world", "hello world")]
    [TestCase("hello world", "hello world")]
    public void Normalize_DeduplicatesConsecutiveWords(string input, string expected)
    {
        var result = NameNormalizer.Normalize(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Normalize_CollapsesVariousWhitespace()
    {
        var result = NameNormalizer.Normalize("word1  \t  word2   word3");
        Assert.That(result, Is.EqualTo("word1 word2 word3"));
    }

    // ExtractCallName tests

    [Test]
    public void ExtractCallName_Parentheses_ExtractsCallName()
    {
        var (callName, registered) = NameNormalizer.ExtractCallName("Daylight Neverending Force (Day)");
        Assert.That(callName, Is.EqualTo("Day"));
        Assert.That(registered, Is.EqualTo("Daylight Neverending Force"));
    }

    [Test]
    public void ExtractCallName_DoubleQuotes_ExtractsCallName()
    {
        var (callName, registered) = NameNormalizer.ExtractCallName("Shadow of Aire Under Pressure \"Ninja\"");
        Assert.That(callName, Is.EqualTo("Ninja"));
        Assert.That(registered, Is.EqualTo("Shadow of Aire Under Pressure"));
    }

    [Test]
    public void ExtractCallName_NoParensOrQuotes_ReturnsNull()
    {
        var (callName, registered) = NameNormalizer.ExtractCallName("Day");
        Assert.That(callName, Is.Null);
        Assert.That(registered, Is.Null);
    }

    [Test]
    public void ExtractCallName_FCI_IsIgnored()
    {
        var (callName, registered) = NameNormalizer.ExtractCallName("Let's Rock Ryujin Jakka Shepter (FCI)");
        Assert.That(callName, Is.Null);
    }

    [Test]
    public void ExtractCallName_SameAsRegistered_StillExtracts()
    {
        var (callName, registered) = NameNormalizer.ExtractCallName("Plexie (Plexie)");
        Assert.That(callName, Is.EqualTo("Plexie"));
        Assert.That(registered, Is.EqualTo("Plexie"));
    }

    [Test]
    public void ExtractCallName_EmptyOrWhitespace_ReturnsNull()
    {
        var (callName, _) = NameNormalizer.ExtractCallName("");
        Assert.That(callName, Is.Null);

        var (callName2, _) = NameNormalizer.ExtractCallName("   ");
        Assert.That(callName2, Is.Null);
    }

    [Test]
    public void ExtractCallName_MultiWordCallName()
    {
        var (callName, registered) = NameNormalizer.ExtractCallName("Contact Point's Blew Bayou (Blew Bayou)");
        Assert.That(callName, Is.EqualTo("Blew Bayou"));
        Assert.That(registered, Is.EqualTo("Contact Point's Blew Bayou"));
    }

    [Test]
    public void ExtractCallName_ShortCandidate_IsIgnored()
    {
        // Single char in parens is too short to be a call name
        var (callName, _) = NameNormalizer.ExtractCallName("Some Dog (X)");
        Assert.That(callName, Is.Null);
    }

    [Test]
    public void ExtractCallName_Cp_IsIgnored()
    {
        // (cp) = Italian "conduttore proprietario", not a call name
        var (callName, _) = NameNormalizer.ExtractCallName("BECKY G (cp)");
        Assert.That(callName, Is.Null);
    }

    [Test]
    public void ExtractCallName_None_IsIgnored()
    {
        // (None) = no call name provided, not a call name
        var (callName, _) = NameNormalizer.ExtractCallName("Granda (None)");
        Assert.That(callName, Is.Null);
    }
}
