using AdwRating.Domain.Helpers;

namespace AdwRating.Tests.Helpers;

[TestFixture]
public class LevenshteinDistanceTests
{
    [TestCase("abc", "abc", 0)]
    [TestCase("", "", 0)]
    [TestCase("hello", "hello", 0)]
    public void Compute_SameStrings_ReturnsZero(string a, string b, int expected)
    {
        Assert.That(LevenshteinDistance.Compute(a, b), Is.EqualTo(expected));
    }

    [TestCase("", "abc", 3)]
    [TestCase("abc", "", 3)]
    [TestCase("", "x", 1)]
    public void Compute_EmptyVsNonEmpty_ReturnsLengthOfNonEmpty(string a, string b, int expected)
    {
        Assert.That(LevenshteinDistance.Compute(a, b), Is.EqualTo(expected));
    }

    [Test]
    public void Compute_KittenSitting_Returns3()
    {
        Assert.That(LevenshteinDistance.Compute("kitten", "sitting"), Is.EqualTo(3));
    }

    [TestCase("cat", "bat", 1)]
    [TestCase("cat", "car", 1)]
    [TestCase("cat", "cats", 1)]
    public void Compute_SingleCharDifference_Returns1(string a, string b, int expected)
    {
        Assert.That(LevenshteinDistance.Compute(a, b), Is.EqualTo(expected));
    }

    [Test]
    public void Compute_NullHandledAsEmpty()
    {
        Assert.That(LevenshteinDistance.Compute(null!, "abc"), Is.EqualTo(3));
        Assert.That(LevenshteinDistance.Compute("abc", null!), Is.EqualTo(3));
        Assert.That(LevenshteinDistance.Compute(null!, null!), Is.EqualTo(0));
    }
}
