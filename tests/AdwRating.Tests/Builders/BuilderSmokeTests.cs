namespace AdwRating.Tests.Builders;

[TestFixture]
public class BuilderSmokeTests
{
    [Test]
    public void Builders_CreateValidEntities()
    {
        var handler = new HandlerBuilder().Build();
        Assert.That(handler.Name, Is.Not.Empty);
        Assert.That(handler.Slug, Is.Not.Empty);
        Assert.That(handler.Country, Is.Not.Empty);

        var dog = new DogBuilder().Build();
        Assert.That(dog.CallName, Is.Not.Empty);

        var team = new TeamBuilder().Build();
        Assert.That(team.Mu, Is.EqualTo(25.0f));

        var comp = new CompetitionBuilder().Build();
        Assert.That(comp.Slug, Is.Not.Empty);

        var run = new RunBuilder().Build();
        Assert.That(run.RoundKey, Is.Not.Empty);

        var result = new RunResultBuilder().Build();
        Assert.That(result.Eliminated, Is.False);
    }

    [Test]
    public void HandlerBuilder_WithName_SetsNormalizedNameAndSlug()
    {
        var handler = new HandlerBuilder().WithName("Kateřina Třičová").Build();
        Assert.That(handler.Name, Is.EqualTo("Kateřina Třičová"));
        Assert.That(handler.NormalizedName, Is.EqualTo("katerina tricova"));
        Assert.That(handler.Slug, Is.EqualTo("katerina-tricova"));
    }

    [Test]
    public void DogBuilder_WithCallName_SetsNormalizedCallName()
    {
        var dog = new DogBuilder().WithCallName("Ášja").Build();
        Assert.That(dog.CallName, Is.EqualTo("Ášja"));
        Assert.That(dog.NormalizedCallName, Is.EqualTo("asja"));
    }
}
