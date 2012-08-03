// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Data.Entity.Core
{
    using System.Data.Common;
    using System.Data.Entity.Core.Common;
    using System.Data.Entity.Migrations.Utilities;
    using System.Data.Entity.Schema;
    using System.Data.SqlClient;
    using System.Linq;
    using Xunit;

    public class DbProviderManifestScenarios : FunctionalTestBase, IDisposable
    {
        private readonly string _connectionString;
        private readonly DbConnection _connection;

        public DbProviderManifestScenarios()
        {
            _connectionString = SimpleConnectionString(GetType().Name);
            _connection = new SqlConnection(_connectionString);

            Database.Delete(_connection);
            new DatabaseCreator().Create(_connection);
        }

        [Fact]
        public void GetStoreSchema_can_read_tables_and_constraints()
        {
            _connection.Open();
            ExecuteNonQuery(
                @"
CREATE TABLE [Products] (
    [Id] [int] NOT NULL IDENTITY,
    [Name] [nvarchar](max),
    [Weight] [decimal](8, 2) NULL,
    [ModifiedDate] [datetime] NOT NULL,
    [CategoryId] [nvarchar](128),
    CONSTRAINT [PK_Products] PRIMARY KEY ([Id])
)
CREATE TABLE [Categories] (
    [Id] [nvarchar](128) NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
)
ALTER TABLE [Products] ADD CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id])
");
            _connection.Close();

            var schemaInformation = GetStoreSchema();

            Assert.Equal(2, schemaInformation.TablesAndViews.Count());

            var tables = schemaInformation.TablesAndViews.OfType<Table>();
            Assert.Equal(2, tables.Count());

            var products = tables.Single(t => t.Name == "Products");
            Assert.Equal(5, products.Columns.Count());

            var productId = products.Columns.Single(c => c.Name == "Id");
            Assert.False(productId.IsNullable);
            Assert.Equal("int", productId.TypeName);
            Assert.Null(productId.MaxLength);
            Assert.Equal(10, productId.Precision);
            Assert.Null(productId.DateTimePrecision);
            Assert.Equal(0, productId.Scale);
            Assert.True(productId.IsIdentity);
            Assert.False(productId.IsStoreGenerated);

            var productName = products.Columns.Single(c => c.Name == "Name");
            Assert.True(productName.IsNullable);
            Assert.Equal("nvarchar(max)", productName.TypeName);
            Assert.Equal(-1, productName.MaxLength);
            Assert.Null(productName.Precision);
            Assert.Null(productName.DateTimePrecision);
            Assert.Null(productName.Scale);
            Assert.False(productName.IsIdentity);
            Assert.False(productName.IsStoreGenerated);

            var productWeight = products.Columns.Single(c => c.Name == "Weight");
            Assert.True(productWeight.IsNullable);
            Assert.Equal("decimal", productWeight.TypeName);
            Assert.Null(productWeight.MaxLength);
            Assert.Equal(8, productWeight.Precision);
            Assert.Null(productWeight.DateTimePrecision);
            Assert.Equal(2, productWeight.Scale);
            Assert.False(productWeight.IsIdentity);
            Assert.False(productWeight.IsStoreGenerated);

            var productModifiedDate = products.Columns.Single(c => c.Name == "ModifiedDate");
            Assert.False(productModifiedDate.IsNullable);
            Assert.Equal("datetime", productModifiedDate.TypeName);
            Assert.Null(productModifiedDate.MaxLength);
            Assert.Null(productModifiedDate.Precision);
            Assert.Equal(3, productModifiedDate.DateTimePrecision);
            Assert.Null(productModifiedDate.Scale);
            Assert.False(productModifiedDate.IsIdentity);
            Assert.False(productModifiedDate.IsStoreGenerated);

            var productCategoryId = products.Columns.Single(c => c.Name == "CategoryId");
            Assert.True(productCategoryId.IsNullable);
            Assert.Equal("nvarchar", productCategoryId.TypeName);
            Assert.Equal(128, productCategoryId.MaxLength);
            Assert.Null(productCategoryId.Precision);
            Assert.Null(productCategoryId.DateTimePrecision);
            Assert.Null(productCategoryId.Scale);
            Assert.False(productCategoryId.IsIdentity);
            Assert.False(productCategoryId.IsStoreGenerated);

            Assert.Equal(3, schemaInformation.Constraints.Count());

            var primaryKeys = schemaInformation.Constraints.OfType<PrimaryKeyConstraint>();
            Assert.Equal(2, primaryKeys.Count());

            var productPk = primaryKeys.Single(pk => pk.Parent == products);
            Assert.Same(products, productPk.Parent);

            var productPkColumn = productPk.Columns.Single();
            Assert.Same(productId, productPkColumn);

            var foreignKeys = schemaInformation.Constraints.OfType<ForeignKeyConstraint>();

            var productCategoryFkc = foreignKeys.Single();
            Assert.Equal("FK_Products_Categories_CategoryId", productCategoryFkc.Name);
            Assert.False(productCategoryFkc.IsCascadeDelete);

            var productCategoryFk = productCategoryFkc.ForeignKeys.Single();
            Assert.Same(productCategoryId, productCategoryFk.FromColumn);

            var categoryId = productCategoryFk.ToColumn;
            Assert.Equal("Id", categoryId.Name);
            Assert.Equal("nvarchar", categoryId.TypeName);
            Assert.Equal(128, categoryId.MaxLength);
            Assert.False(categoryId.IsNullable);
        }

        [Fact]
        public void GetStoreSchema_can_read_views()
        {
            _connection.Open();
            ExecuteNonQuery(
                 @"
CREATE VIEW [vCategoryTotals]
AS
SELECT
    0 AS [CategoryId],
    0 AS [ProductCount]
");
            _connection.Close();

            var schemaInformation = GetStoreSchema();

            Assert.Equal(1, schemaInformation.TablesAndViews.Count());

            var categoryTotals = schemaInformation.TablesAndViews.OfType<View>().Single();
            Assert.Equal("vCategoryTotals", categoryTotals.Name);
            Assert.Equal(2, categoryTotals.Columns.Count());
        }

        [Fact]
        public void GetStoreSchema_can_read_procedures()
        {
            _connection.Open();
            ExecuteNonQuery(
                @"
CREATE PROCEDURE MyProc
    @InputParameter int,
    @OutputParameter int OUTPUT
AS
SET @OutputParameter = @InputParameter
");
            _connection.Close();

            var schemaInformation = GetStoreSchema();

            Assert.Equal(1, schemaInformation.Routines.Count());

            var myProc = schemaInformation.Routines.OfType<Procedure>().Single();
            Assert.Equal("MyProc", myProc.Name);
            Assert.Equal(2, myProc.Parameters.Count());

            var inputParameter = myProc.Parameters.Single(p => p.Name == "InputParameter");
            Assert.Equal("int", inputParameter.TypeName);
            Assert.Equal("IN", inputParameter.Mode);

            var outputParameter = myProc.Parameters.Single(p => p.Name == "OutputParameter");
            Assert.Equal("int", outputParameter.TypeName);
            Assert.Equal("INOUT", outputParameter.Mode);
        }

        [Fact]
        public void GetStoreSchema_can_read_scalar_functions()
        {
            _connection.Open();
            ExecuteNonQuery(
                @"
CREATE FUNCTION MyScalarFunction()
RETURNS int
BEGIN
    RETURN 42
END
");
            _connection.Close();

            var schemaInformation = GetStoreSchema();

            Assert.Equal(1, schemaInformation.Routines.Count());

            var myScalarFunction = schemaInformation.Routines.OfType<ScalarFunction>().Single();
            Assert.Equal("MyScalarFunction", myScalarFunction.Name);
            Assert.Equal(false, myScalarFunction.IsAggregate);
            Assert.Equal(false, myScalarFunction.IsBuiltIn);
            Assert.Equal(false, myScalarFunction.IsNiladic);
            Assert.Equal("int", myScalarFunction.ReturnTypeName);
            Assert.Equal(0, myScalarFunction.Parameters.Count());
        }

        [Fact]
        public void GetStoreSchema_can_read_table_valued_functions()
        {
            _connection.Open();
            ExecuteNonQuery(
                @"
CREATE FUNCTION MyTableValuedFunction()
RETURNS TABLE
RETURN
    SELECT
        0 AS ProductId,
        42 AS MagicNumber
");
            _connection.Close();

            var schemaInformation = GetStoreSchema();

            Assert.Equal(1, schemaInformation.Routines.Count());

            var myTableValuedFunction = schemaInformation.Routines.OfType<TableValuedFunction>().Single();
            Assert.Equal("MyTableValuedFunction", myTableValuedFunction.Name);
            Assert.Equal(false, myTableValuedFunction.IsBuiltIn);
            Assert.Equal(false, myTableValuedFunction.IsNiladic);
            Assert.Equal(0, myTableValuedFunction.Parameters.Count());
            Assert.Equal(2, myTableValuedFunction.Columns.Count());
        }

        public void Dispose()
        {
            if (_connection != null)
            {
                _connection.Dispose();
            }
        }

        private void ExecuteNonQuery(string commandText)
        {
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = commandText;

                command.ExecuteNonQuery();
            }
        }

        private SchemaInformation GetStoreSchema()
        {
            var providerServices = DbProviderServices.GetProviderServices(_connection);
            var providerManifestToken = providerServices.GetProviderManifestToken(_connection);
            var providerManifest = providerServices.GetProviderManifest(providerManifestToken);

            return providerManifest.GetStoreSchema(_connection);
        }
    }
}
