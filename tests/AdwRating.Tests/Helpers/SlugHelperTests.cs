using AdwRating.Domain.Helpers;

namespace AdwRating.Tests.Helpers;

public class SlugHelperTests
{
    [Theory]
    [InlineData("John Smith", "john-smith")]
    [InlineData("Katerina Tercova", "katerina-tercova")]
    [InlineData("  Leading Trailing  ", "leading-trailing")]
    [InlineData("Special Ch@racters!", "special-chracters")]
    [InlineData("Multiple   Spaces", "multiple-spaces")]
    [InlineData("already-a-slug", "already-a-slug")]
    [InlineData("UPPERCASE NAME", "uppercase-name")]
    [InlineData("name--with--hyphens", "name-with-hyphens")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void GenerateSlug_ProducesExpectedResult(string input, string expected)
    {
        var result = SlugHelper.GenerateSlug(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GenerateSlug_RemovesNonAlphanumericExceptHyphens()
    {
        var result = SlugHelper.GenerateSlug("hello.world@2024!");
        Assert.Equal("helloworld2024", result);
    }

    [Fact]
    public void GenerateSlug_CollapsesMultipleHyphens()
    {
        var result = SlugHelper.GenerateSlug("a - - b");
        Assert.Equal("a-b", result);
    }

    [Fact]
    public void GenerateSlug_TrimsLeadingAndTrailingHyphens()
    {
        var result = SlugHelper.GenerateSlug("-hello-world-");
        Assert.Equal("hello-world", result);
    }

    [Theory]
    [InlineData("base-slug", 2, "base-slug-2")]
    [InlineData("base-slug", 3, "base-slug-3")]
    [InlineData("john-smith", 10, "john-smith-10")]
    public void AppendSuffix_ProducesExpectedResult(string slug, int suffix, string expected)
    {
        var result = SlugHelper.AppendSuffix(slug, suffix);
        Assert.Equal(expected, result);
    }
}
