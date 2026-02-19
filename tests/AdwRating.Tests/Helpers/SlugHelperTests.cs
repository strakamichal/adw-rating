using AdwRating.Domain.Helpers;

namespace AdwRating.Tests.Helpers;

[TestFixture]
public class SlugHelperTests
{
    [TestCase("John Smith", "john-smith")]
    [TestCase("Katerina Tercova", "katerina-tercova")]
    [TestCase("  Leading Trailing  ", "leading-trailing")]
    [TestCase("Special Ch@racters!", "special-chracters")]
    [TestCase("Multiple   Spaces", "multiple-spaces")]
    [TestCase("already-a-slug", "already-a-slug")]
    [TestCase("UPPERCASE NAME", "uppercase-name")]
    [TestCase("name--with--hyphens", "name-with-hyphens")]
    [TestCase("", "")]
    [TestCase("   ", "")]
    public void GenerateSlug_ProducesExpectedResult(string input, string expected)
    {
        var result = SlugHelper.GenerateSlug(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("Kateřina Třičová", "katerina-tricova")]
    [TestCase("Müller Schmidt", "muller-schmidt")]
    [TestCase("François Dupont", "francois-dupont")]
    [TestCase("Łukasz Wójcik", "lukasz-wojcik")]
    public void GenerateSlug_StripsDiacriticsBeforeSlugifying(string input, string expected)
    {
        var result = SlugHelper.GenerateSlug(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void GenerateSlug_RemovesNonAlphanumericExceptHyphens()
    {
        var result = SlugHelper.GenerateSlug("hello.world@2024!");
        Assert.That(result, Is.EqualTo("helloworld2024"));
    }

    [Test]
    public void GenerateSlug_CollapsesMultipleHyphens()
    {
        var result = SlugHelper.GenerateSlug("a - - b");
        Assert.That(result, Is.EqualTo("a-b"));
    }

    [Test]
    public void GenerateSlug_TrimsLeadingAndTrailingHyphens()
    {
        var result = SlugHelper.GenerateSlug("-hello-world-");
        Assert.That(result, Is.EqualTo("hello-world"));
    }

    [TestCase("base-slug", 2, "base-slug-2")]
    [TestCase("base-slug", 3, "base-slug-3")]
    [TestCase("john-smith", 10, "john-smith-10")]
    public void AppendSuffix_ProducesExpectedResult(string slug, int suffix, string expected)
    {
        var result = SlugHelper.AppendSuffix(slug, suffix);
        Assert.That(result, Is.EqualTo(expected));
    }
}
