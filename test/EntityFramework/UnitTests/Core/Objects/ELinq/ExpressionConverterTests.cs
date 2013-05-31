// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Data.Entity.Core.Objects.ELinq
{
    using System.Data.Entity.Core.Common.CommandTrees;
    using System.Data.Entity.Core.Common.CommandTrees.Internal;
    using System.Data.Entity.Core.Metadata.Edm;
    using System.Data.Entity.TestModels.ArubaModel;
    using System.Linq;
    using Xunit;

    public class ExpressionConverterTests
    {
        private readonly ArubaContext _db = new ArubaContext();
        private readonly Funcletizer _funcletizer;

        public ExpressionConverterTests()
        {
            _funcletizer = Funcletizer.CreateQueryFuncletizer(_db.InternalContext.ObjectContext);
        }

        [Fact]
        public void DescribeClrType_returns_full_name_for_normal_types()
        {
            Assert.Equal("System.Data.Entity.Core.Objects.ELinq.ExpressionConverterTests", ExpressionConverter.DescribeClrType(GetType()));

            Assert.Equal(
                "System.Data.Entity.Core.Objects.ELinq.ExpressionConverterTests+ANest",
                ExpressionConverter.DescribeClrType(typeof(ANest)));
        }

        private class ANest
        {
        }

        [Fact]
        public void Convert_from_expression_with_cast()
        {
            // NOTE: We only support casting primitive or enum types
            var query = from long id in _db.Runs.Select(r => r.Id)
                        select id;

            var expression = Convert(query) as DbProjectExpression;

            Assert.NotNull(expression);
            var project = expression.Input.Expression as DbProjectExpression;
            Assert.NotNull(project);
            var cast = project.Projection as DbCastExpression;
            Assert.NotNull(cast);
            Assert.Equal(PrimitiveType.GetEdmPrimitiveType(PrimitiveTypeKind.Int64), cast.ResultType.EdmType);
        }

        [Fact]
        public void Convert_group_by_expression()
        {
            var query = from r in _db.Runs
                        group r by r.Purpose;

            var expression = Convert(query) as DbProjectExpression;

            Assert.NotNull(expression);
            var groupBy = expression.Input.Expression as DbGroupByExpression;
            Assert.NotNull(groupBy);
            Assert.Equal(1, groupBy.Keys.Count);
            var property = groupBy.Keys[0] as DbPropertyExpression;
            Assert.NotNull(property);
            Assert.Equal("Purpose", property.Property.Name);
        }

        [Fact]
        public void Convert_group_by_into_expression()
        {
            var query = from r in _db.Runs
                        group r by r.Purpose into g
                        select g;

            var expression = Convert(query) as DbProjectExpression;

            Assert.NotNull(expression);
            var project = expression.Input.Expression as DbProjectExpression;
            Assert.NotNull(project);
            var groupBy = project.Input.Expression as DbGroupByExpression;
            Assert.NotNull(groupBy);
            Assert.Equal(1, groupBy.Keys.Count);
            var property = groupBy.Keys[0] as DbPropertyExpression;
            Assert.NotNull(property);
            Assert.Equal("Purpose", property.Property.Name);

            // DEBUG:
            var text = new ExpressionPrinter().Print(expression);
        }

        private DbExpression Convert(IQueryable query)
        {
            return new ExpressionConverter(_funcletizer, query.Expression).Convert();
        }
    }
}
