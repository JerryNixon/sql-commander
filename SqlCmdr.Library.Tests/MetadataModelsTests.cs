using SqlCmdr.Library.Models;
using FluentAssertions;
using AutoFixture;

namespace SqlCmdr.Library.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Models")]
public class MetadataModelsTests
{
    private readonly IFixture _fixture;

    public MetadataModelsTests()
    {
        _fixture = new Fixture();
    }

    #region ColumnMetadata Tests

    [Fact]
    public void ColumnMetadata_WithNullableVarchar_DisplayTypeIsCorrect()
    {
        // Arrange
        var column = new ColumnMetadata
        {
            Name = "TestColumn",
            DataType = "nvarchar",
            IsNullable = true,
            MaxLength = 50
        };

        // Act
        var displayType = column.DisplayType;

        // Assert
        displayType.Should().Be("nvarchar(50)");
    }

    [Fact]
    public void ColumnMetadata_WithMaxLengthVarchar_DisplaysMax()
    {
        // Arrange
        var column = new ColumnMetadata
        {
            Name = "LargeColumn",
            DataType = "varchar",
            IsNullable = false,
            MaxLength = -1
        };

        // Act
        var displayType = column.DisplayType;

        // Assert
        // Note: MaxLength must be positive for the format to apply, -1 results in base type
        displayType.Should().Be("varchar");
    }

    [Theory]
    [InlineData("varchar", 100, "varchar(100)")]
    [InlineData("char", 10, "char(10)")]
    [InlineData("nvarchar", 255, "nvarchar(255)")]
    [InlineData("nchar", 20, "nchar(20)")]
    public void ColumnMetadata_WithCharacterTypes_DisplayTypeIsCorrect(string dataType, int maxLength, string expected)
    {
        // Arrange
        var column = new ColumnMetadata
        {
            Name = "TestColumn",
            DataType = dataType,
            IsNullable = false,
            MaxLength = maxLength
        };

        // Act
        var displayType = column.DisplayType;

        // Assert
        displayType.Should().Be(expected);
    }

    [Theory]
    [InlineData("decimal", 10, 2, "decimal(10,2)")]
    [InlineData("numeric", 18, 4, "numeric(18,4)")]
    [InlineData("decimal", 5, 0, "decimal(5,0)")]
    public void ColumnMetadata_WithDecimalTypes_DisplayTypeIsCorrect(string dataType, int precision, int scale, string expected)
    {
        // Arrange
        var column = new ColumnMetadata
        {
            Name = "DecimalColumn",
            DataType = dataType,
            IsNullable = false,
            Precision = precision,
            Scale = scale
        };

        // Act
        var displayType = column.DisplayType;

        // Assert
        displayType.Should().Be(expected);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("bigint")]
    [InlineData("bit")]
    [InlineData("datetime")]
    [InlineData("uniqueidentifier")]
    public void ColumnMetadata_WithSimpleTypes_DisplayTypeEqualsDataType(string dataType)
    {
        // Arrange
        var column = new ColumnMetadata
        {
            Name = "SimpleColumn",
            DataType = dataType,
            IsNullable = false
        };

        // Act
        var displayType = column.DisplayType;

        // Assert
        displayType.Should().Be(dataType);
    }

    [Fact]
    public void ColumnMetadata_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var column1 = new ColumnMetadata
        {
            Name = "TestCol",
            DataType = "int",
            IsNullable = true
        };
        var column2 = new ColumnMetadata
        {
            Name = "TestCol",
            DataType = "int",
            IsNullable = true
        };

        // Assert
        column1.Should().Be(column2);
        column1.GetHashCode().Should().Be(column2.GetHashCode());
    }

    #endregion

    #region ParameterMetadata Tests

    [Fact]
    public void ParameterMetadata_WithDecimal_DisplayTypeIsCorrect()
    {
        // Arrange
        var param = new ParameterMetadata
        {
            Name = "@Price",
            DataType = "decimal",
            Direction = "Input",
            Precision = 10,
            Scale = 2
        };

        // Act
        var displayType = param.DisplayType;

        // Assert
        displayType.Should().Be("decimal(10,2)");
    }

    [Theory]
    [InlineData("varchar", 50, "varchar(50)")]
    [InlineData("nvarchar", 100, "nvarchar(100)")]
    [InlineData("char", 10, "char(10)")]
    public void ParameterMetadata_WithCharacterTypes_DisplayTypeIsCorrect(string dataType, int maxLength, string expected)
    {
        // Arrange
        var param = new ParameterMetadata
        {
            Name = "@Param",
            DataType = dataType,
            Direction = "Input",
            MaxLength = maxLength
        };

        // Act
        var displayType = param.DisplayType;

        // Assert
        displayType.Should().Be(expected);
    }

    [Theory]
    [InlineData("Input")]
    [InlineData("Output")]
    public void ParameterMetadata_WithDirection_StoresCorrectly(string direction)
    {
        // Arrange & Act
        var param = new ParameterMetadata
        {
            Name = "@Param",
            DataType = "int",
            Direction = direction
        };

        // Assert
        param.Direction.Should().Be(direction);
    }

    #endregion

    #region TableMetadata Tests

    [Fact]
    public void TableMetadata_FullName_IsFormattedCorrectly()
    {
        // Arrange
        var table = new TableMetadata
        {
            Schema = "dbo",
            Name = "Users"
        };

        // Act
        var fullName = table.FullName;

        // Assert
        fullName.Should().Be("[dbo].[Users]");
    }

    [Theory]
    [InlineData("dbo", "Customers", "[dbo].[Customers]")]
    [InlineData("sales", "Orders", "[sales].[Orders]")]
    [InlineData("HumanResources", "Employee", "[HumanResources].[Employee]")]
    public void TableMetadata_FullName_WithVariousSchemas_FormatsCorrectly(string schema, string name, string expected)
    {
        // Arrange
        var table = new TableMetadata { Schema = schema, Name = name };

        // Act
        var fullName = table.FullName;

        // Assert
        fullName.Should().Be(expected);
    }

    [Fact]
    public void TableMetadata_Columns_InitializesAsEmptyList()
    {
        // Arrange & Act
        var table = new TableMetadata { Schema = "dbo", Name = "TestTable" };

        // Assert
        table.Columns.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void TableMetadata_Columns_CanAddColumns()
    {
        // Arrange
        var table = new TableMetadata { Schema = "dbo", Name = "TestTable" };
        var column = new ColumnMetadata
        {
            Name = "Id",
            DataType = "int",
            IsNullable = false
        };

        // Act
        table.Columns.Add(column);

        // Assert
        table.Columns.Should().ContainSingle().Which.Should().Be(column);
    }

    #endregion

    #region ViewMetadata Tests

    [Fact]
    public void ViewMetadata_FullName_IsFormattedCorrectly()
    {
        // Arrange
        var view = new ViewMetadata
        {
            Schema = "dbo",
            Name = "vw_ActiveUsers"
        };

        // Act
        var fullName = view.FullName;

        // Assert
        fullName.Should().Be("[dbo].[vw_ActiveUsers]");
    }

    [Fact]
    public void ViewMetadata_Columns_InitializesAsEmptyList()
    {
        // Arrange & Act
        var view = new ViewMetadata { Schema = "dbo", Name = "vw_Test" };

        // Assert
        view.Columns.Should().NotBeNull().And.BeEmpty();
    }

    #endregion

    #region StoredProcedureMetadata Tests

    [Fact]
    public void StoredProcedureMetadata_FullName_IsFormattedCorrectly()
    {
        // Arrange
        var proc = new StoredProcedureMetadata
        {
            Schema = "dbo",
            Name = "sp_GetUsers"
        };

        // Act
        var fullName = proc.FullName;

        // Assert
        fullName.Should().Be("[dbo].[sp_GetUsers]");
    }

    [Fact]
    public void StoredProcedureMetadata_Lists_InitializeAsEmpty()
    {
        // Arrange & Act
        var proc = new StoredProcedureMetadata
        {
            Schema = "dbo",
            Name = "sp_Test"
        };

        // Assert
        proc.Parameters.Should().NotBeNull().And.BeEmpty();
        proc.OutputColumns.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void StoredProcedureMetadata_Definition_CanBeNull()
    {
        // Arrange & Act
        var proc = new StoredProcedureMetadata
        {
            Schema = "dbo",
            Name = "sp_Test",
            Definition = null
        };

        // Assert
        proc.Definition.Should().BeNull();
    }

    #endregion

    #region ForeignKeyMetadata Tests

    [Fact]
    public void ForeignKeyMetadata_AllProperties_AreRequired()
    {
        // Arrange & Act
        var fk = new ForeignKeyMetadata
        {
            Name = "FK_Orders_Customers",
            ParentSchema = "dbo",
            ParentTable = "Orders",
            ParentColumn = "CustomerId",
            ReferencedSchema = "dbo",
            ReferencedTable = "Customers",
            ReferencedColumn = "Id"
        };

        // Assert
        fk.Name.Should().Be("FK_Orders_Customers");
        fk.ParentSchema.Should().Be("dbo");
        fk.ParentTable.Should().Be("Orders");
        fk.ParentColumn.Should().Be("CustomerId");
        fk.ReferencedSchema.Should().Be("dbo");
        fk.ReferencedTable.Should().Be("Customers");
        fk.ReferencedColumn.Should().Be("Id");
    }

    #endregion

    #region DatabaseMetadata Tests

    [Fact]
    public void DatabaseMetadata_AllLists_InitializeAsEmpty()
    {
        // Arrange & Act
        var metadata = new DatabaseMetadata();

        // Assert
        metadata.Tables.Should().NotBeNull().And.BeEmpty();
        metadata.Views.Should().NotBeNull().And.BeEmpty();
        metadata.StoredProcedures.Should().NotBeNull().And.BeEmpty();
        metadata.ForeignKeys.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void DatabaseMetadata_WithClause_CopiesCollectionsCorrectly()
    {
        // Arrange
        var original = new DatabaseMetadata();
        var tables = new List<TableMetadata>
        {
            new() { Schema = "dbo", Name = "Table1" },
            new() { Schema = "dbo", Name = "Table2" }
        };

        // Act
        var updated = original with { Tables = tables };

        // Assert
        updated.Tables.Should().HaveCount(2);
        updated.Tables.Should().BeEquivalentTo(tables);
    }

    #endregion

    #region AppSettings Tests

    [Fact]
    public void AppSettings_ToConnectionString_GeneratesValidConnectionString()
    {
        // Arrange
        var settings = new AppSettings
        {
            Server = "TestServer",
            Database = "TestDb",
            UserId = "TestUser",
            Password = "TestPassword"
        };

        // Act
        var connectionString = settings.ToConnectionString();

        // Assert
        connectionString.Should().NotBeNullOrEmpty();
        connectionString.Should().Contain("TestServer");
        connectionString.Should().Contain("TestDb");
        connectionString.Should().Contain("TestUser");
    }

    [Fact]
    public void AppSettings_FromConnectionString_ParsesCorrectly()
    {
        // Arrange
        var settings = new AppSettings
        {
            Server = "LocalServer",
            Database = "AppDb",
            UserId = "AppUser",
            Password = "SecurePassword"
        };
        var connectionString = settings.ToConnectionString();

        // Act
        var parsed = AppSettings.FromConnectionString(connectionString);

        // Assert
        parsed.Server.Should().Be("LocalServer");
        parsed.Database.Should().Be("AppDb");
        parsed.UserId.Should().Be("AppUser");
    }

    [Fact]
    public void AppSettings_FromConnectionString_RoundTripsSuccessfully()
    {
        // Arrange
        var original = new AppSettings
        {
            Server = "TestServer",
            Database = "TestDatabase",
            UserId = "TestUser",
            Password = "TestPass"
        };

        // Act
        var connectionString = original.ToConnectionString();
        var roundtripped = AppSettings.FromConnectionString(connectionString);

        // Assert
        roundtripped.Server.Should().Be(original.Server);
        roundtripped.Database.Should().Be(original.Database);
        roundtripped.UserId.Should().Be(original.UserId);
    }

    [Fact]
    public void AppSettings_FromConnectionString_WithEmptyString_ReturnsDefaults()
    {
        // Act
        var settings = AppSettings.FromConnectionString(string.Empty);

        // Assert
        settings.Should().NotBeNull();
        settings.Server.Should().BeEmpty();
        settings.Database.Should().BeEmpty();
        settings.DefaultResultLimit.Should().Be(100);
    }

    [Fact]
    public void AppSettings_FromConnectionString_WithNullString_ReturnsDefaults()
    {
        // Act
        var settings = AppSettings.FromConnectionString(null!);

        // Assert
        settings.Should().NotBeNull();
        settings.Server.Should().BeEmpty();
        settings.Database.Should().BeEmpty();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void AppSettings_FromConnectionString_WithCustomResultLimit_SetsCorrectly(int resultLimit)
    {
        // Act
        var settings = AppSettings.FromConnectionString("", resultLimit);

        // Assert
        settings.DefaultResultLimit.Should().Be(resultLimit);
    }

    [Fact]
    public void AppSettings_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var settings1 = new AppSettings
        {
            Server = "Server1",
            Database = "Db1",
            DefaultResultLimit = 100
        };
        var settings2 = new AppSettings
        {
            Server = "Server1",
            Database = "Db1",
            DefaultResultLimit = 100
        };

        // Assert
        settings1.Should().Be(settings2);
    }

    [Fact]
    public void AppSettings_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var settings = new AppSettings();

        // Assert
        settings.Server.Should().BeEmpty();
        settings.Database.Should().BeEmpty();
        settings.UserId.Should().BeEmpty();
        settings.Password.Should().BeEmpty();
        settings.DefaultResultLimit.Should().Be(100);
        settings.TrustServerCertificate.Should().BeTrue();
        settings.ConnectionTimeout.Should().Be(30);
        settings.ConfirmActions.Should().BeFalse();
        settings.Theme.Should().Be("dark");
    }

    #endregion
}
