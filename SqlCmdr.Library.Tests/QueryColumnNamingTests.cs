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

    [Fact]
    public void CoerceValueForTransport_LargeBigint_BecomesExactString()
    {
        // > 2^53: would lose precision as a JSON number parsed by the browser.
        QueryExecutionService.CoerceValueForTransport(9007199254740993L).Should().Be("9007199254740993");
        QueryExecutionService.CoerceValueForTransport(1234567890123456789L).Should().Be("1234567890123456789");
    }

    [Fact]
    public void CoerceValueForTransport_Decimal_BecomesExactString()
    {
        QueryExecutionService.CoerceValueForTransport(12345678901234567890.12m).Should().Be("12345678901234567890.12");
        QueryExecutionService.CoerceValueForTransport(10.50m).Should().Be("10.50");
    }

    [Fact]
    public void CoerceValueForTransport_Binary_BecomesHexString()
    {
        // Binary must render as 0x-hex (valid T-SQL), not base64.
        QueryExecutionService.CoerceValueForTransport(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }).Should().Be("0x48656C6C6F");
        QueryExecutionService.CoerceValueForTransport(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07, 0xD1 }).Should().Be("0x00000000000007D1");
        QueryExecutionService.CoerceValueForTransport(Array.Empty<byte>()).Should().Be("0x");
    }

    [Fact]
    public void CoerceValueForTransport_OtherTypes_PassThroughUnchanged()
    {
        var timestamp = new DateTime(2024, 1, 2, 3, 4, 5);

        QueryExecutionService.CoerceValueForTransport(42).Should().Be(42);
        QueryExecutionService.CoerceValueForTransport(3.14d).Should().Be(3.14d);
        QueryExecutionService.CoerceValueForTransport(true).Should().Be(true);
        QueryExecutionService.CoerceValueForTransport("hello").Should().Be("hello");
        QueryExecutionService.CoerceValueForTransport(timestamp).Should().Be(timestamp);
        QueryExecutionService.CoerceValueForTransport(null).Should().BeNull();
    }
}
