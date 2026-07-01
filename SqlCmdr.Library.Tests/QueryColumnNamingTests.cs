using FluentAssertions;
using SqlCmdr.Services;

namespace SqlCmdr.Tests;

[Trait("Category", "Unit")]
[Trait("Service", "QueryExecutionService")]
public class QueryColumnNamingTests
{
    [Fact]
    public void MakeColumnNamesUnique_WithUniqueNames_ReturnsThemUnchanged()
    {
        var result = QueryExecutionService.MakeColumnNamesUnique(new[] { "Id", "Name", "Total" });

        result.Should().Equal("Id", "Name", "Total");
    }

    [Fact]
    public void MakeColumnNamesUnique_WithDuplicateNames_MakesThemUnique()
    {
        // e.g. SELECT a.Id, b.Id FROM a JOIN b
        var result = QueryExecutionService.MakeColumnNamesUnique(new[] { "Id", "Id", "Id" });

        result.Should().Equal("Id", "Id (2)", "Id (3)");
    }

    [Fact]
    public void MakeColumnNamesUnique_WithEmptyNames_AssignsPositionalNames()
    {
        // e.g. SELECT 1, 2, Name
        var result = QueryExecutionService.MakeColumnNamesUnique(new[] { "", "", "Name" });

        result.Should().Equal("Column1", "Column2", "Name");
    }

    [Fact]
    public void MakeColumnNamesUnique_WhenGeneratedNameCollidesWithRealColumn_StaysUnique()
    {
        var result = QueryExecutionService.MakeColumnNamesUnique(new[] { "Id", "Id", "Id (2)" });

        result.Should().OnlyHaveUniqueItems();
        result.Should().Equal("Id", "Id (2)", "Id (2) (2)");
    }

    [Fact]
    public void MakeColumnNamesUnique_WithNull_Throws()
    {
        Action act = () => QueryExecutionService.MakeColumnNamesUnique(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
