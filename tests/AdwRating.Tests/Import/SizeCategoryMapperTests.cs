using AdwRating.Domain.Enums;
using AdwRating.Service.Import;

namespace AdwRating.Tests.Import;

[TestFixture]
public class SizeCategoryMapperTests
{
    // FCI / null organization
    [TestCase(null, "S", SizeCategory.S)]
    [TestCase(null, "Small", SizeCategory.S)]
    [TestCase(null, "XS", SizeCategory.S)]
    [TestCase(null, "M", SizeCategory.M)]
    [TestCase(null, "Medium", SizeCategory.M)]
    [TestCase(null, "I", SizeCategory.I)]
    [TestCase(null, "Intermediate", SizeCategory.I)]
    [TestCase(null, "L", SizeCategory.L)]
    [TestCase(null, "Large", SizeCategory.L)]
    [TestCase("FCI", "S", SizeCategory.S)]
    [TestCase("FCI", "small", SizeCategory.S)]
    [TestCase("", "M", SizeCategory.M)]
    public void Map_FciOrNullOrg_MapsCorrectly(string? org, string cat, SizeCategory expected)
    {
        var (mapped, excluded) = SizeCategoryMapper.Map(org, cat);
        Assert.That(mapped, Is.EqualTo(expected));
        Assert.That(excluded, Is.False);
    }

    [Test]
    public void Map_FciUnknownCategory_ReturnsNull()
    {
        var (mapped, excluded) = SizeCategoryMapper.Map("FCI", "Unknown");
        Assert.That(mapped, Is.Null);
        Assert.That(excluded, Is.False);
    }

    // AKC
    [TestCase("8\"", SizeCategory.S)]
    [TestCase("8", SizeCategory.S)]
    [TestCase("12\"", SizeCategory.S)]
    [TestCase("12", SizeCategory.S)]
    [TestCase("16\"", SizeCategory.M)]
    [TestCase("16", SizeCategory.M)]
    [TestCase("20\"", SizeCategory.I)]
    [TestCase("20", SizeCategory.I)]
    [TestCase("24\"", SizeCategory.L)]
    [TestCase("24", SizeCategory.L)]
    public void Map_Akc_MapsHeightsCorrectly(string cat, SizeCategory expected)
    {
        var (mapped, excluded) = SizeCategoryMapper.Map("AKC", cat);
        Assert.That(mapped, Is.EqualTo(expected));
        Assert.That(excluded, Is.False);
    }

    [TestCase("Preferred")]
    [TestCase("20\" Preferred")]
    [TestCase("preferred 16\"")]
    public void Map_Akc_PreferredIsExcluded(string cat)
    {
        var (mapped, excluded) = SizeCategoryMapper.Map("AKC", cat);
        Assert.That(excluded, Is.True);
        Assert.That(mapped, Is.Null);
    }

    // USDAA
    [TestCase("12\"", SizeCategory.S)]
    [TestCase("12", SizeCategory.S)]
    [TestCase("16\"", SizeCategory.M)]
    [TestCase("16", SizeCategory.M)]
    [TestCase("22\"", SizeCategory.L)]
    [TestCase("22", SizeCategory.L)]
    [TestCase("26\"", SizeCategory.L)]
    [TestCase("26", SizeCategory.L)]
    public void Map_Usdaa_MapsHeightsCorrectly(string cat, SizeCategory expected)
    {
        var (mapped, excluded) = SizeCategoryMapper.Map("USDAA", cat);
        Assert.That(mapped, Is.EqualTo(expected));
        Assert.That(excluded, Is.False);
    }

    // WAO
    [TestCase("250", SizeCategory.S)]
    [TestCase("300", SizeCategory.M)]
    [TestCase("400", SizeCategory.I)]
    [TestCase("500", SizeCategory.L)]
    [TestCase("600", SizeCategory.L)]
    public void Map_Wao_MapsCorrectly(string cat, SizeCategory expected)
    {
        var (mapped, excluded) = SizeCategoryMapper.Map("WAO", cat);
        Assert.That(mapped, Is.EqualTo(expected));
        Assert.That(excluded, Is.False);
    }

    // UKI
    [TestCase("S", SizeCategory.S)]
    [TestCase("M", SizeCategory.M)]
    [TestCase("I", SizeCategory.I)]
    [TestCase("L", SizeCategory.L)]
    public void Map_Uki_PassthroughCorrectly(string cat, SizeCategory expected)
    {
        var (mapped, excluded) = SizeCategoryMapper.Map("UKI", cat);
        Assert.That(mapped, Is.EqualTo(expected));
        Assert.That(excluded, Is.False);
    }

    // IFCS
    [TestCase("S", SizeCategory.S)]
    [TestCase("M", SizeCategory.M)]
    [TestCase("L", SizeCategory.L)]
    public void Map_Ifcs_PassthroughCorrectly(string cat, SizeCategory expected)
    {
        var (mapped, excluded) = SizeCategoryMapper.Map("IFCS", cat);
        Assert.That(mapped, Is.EqualTo(expected));
        Assert.That(excluded, Is.False);
    }

    [Test]
    public void Map_Ifcs_IntermediateNotSupported()
    {
        var (mapped, excluded) = SizeCategoryMapper.Map("IFCS", "I");
        Assert.That(mapped, Is.Null);
        Assert.That(excluded, Is.False);
    }

    // Unknown organization
    [Test]
    public void Map_UnknownOrg_ReturnsNull()
    {
        var (mapped, excluded) = SizeCategoryMapper.Map("UNKNOWN_ORG", "S");
        Assert.That(mapped, Is.Null);
        Assert.That(excluded, Is.False);
    }

    // Case insensitivity
    [Test]
    public void Map_FciCaseInsensitive()
    {
        var (mapped, _) = SizeCategoryMapper.Map(null, "small");
        Assert.That(mapped, Is.EqualTo(SizeCategory.S));
    }

    [Test]
    public void Map_WhitespaceHandling()
    {
        var (mapped, _) = SizeCategoryMapper.Map("  FCI  ", "  S  ");
        Assert.That(mapped, Is.EqualTo(SizeCategory.S));
    }
}
