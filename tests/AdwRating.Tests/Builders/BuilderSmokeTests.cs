namespace AdwRating.Tests.Builders;

public class BuilderSmokeTests
{
    [Fact]
    public void Builders_CreateValidEntities()
    {
        var handler = new HandlerBuilder().Build();
        Assert.NotEmpty(handler.Name);
        Assert.NotEmpty(handler.Slug);
        Assert.NotEmpty(handler.Country);

        var dog = new DogBuilder().Build();
        Assert.NotEmpty(dog.CallName);

        var team = new TeamBuilder().Build();
        Assert.Equal(25.0f, team.Mu);

        var comp = new CompetitionBuilder().Build();
        Assert.NotEmpty(comp.Slug);

        var run = new RunBuilder().Build();
        Assert.NotEmpty(run.RoundKey);

        var result = new RunResultBuilder().Build();
        Assert.False(result.Eliminated);
    }

    [Fact]
    public void HandlerBuilder_WithName_SetsNormalizedNameAndSlug()
    {
        var handler = new HandlerBuilder().WithName("Kateřina Třičová").Build();
        Assert.Equal("Kateřina Třičová", handler.Name);
        Assert.Equal("katerina tricova", handler.NormalizedName);
        Assert.Equal("katerina-tricova", handler.Slug);
    }

    [Fact]
    public void DogBuilder_WithCallName_SetsNormalizedCallName()
    {
        var dog = new DogBuilder().WithCallName("Ášja").Build();
        Assert.Equal("Ášja", dog.CallName);
        Assert.Equal("asja", dog.NormalizedCallName);
    }
}
