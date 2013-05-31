// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Data.Entity.Core.Objects.ELinq
{
    using System.Data.Entity.Core.Common.CommandTrees;
    using System.Data.Entity.Core.Common.CommandTrees.Internal;
    using System.Data.Entity.Resources;
    using System.Data.Entity.TestModels.ArubaModel;
    using System.Linq;
    using Xunit;

    public class ExpressionConverterTests
    {
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

        public class ConvertTests : IDisposable
        {
            private readonly ArubaContext _db = new ArubaContext();
            private readonly Funcletizer _funcletizer;
            private readonly ExpressionPrinter _printer = new ExpressionPrinter();

            public ConvertTests()
            {
                _funcletizer = Funcletizer.CreateQueryFuncletizer(_db.InternalContext.ObjectContext);
            }

            public void Dispose()
            {
                _db.Dispose();
            }

            [Fact]
            public void Convert_throws_on_from_express_with_cast_when_non_primitive()
            {
                var query = from ArubaBaseline b in _db.Failures
                            select b;

                var ex = Assert.Throws<NotSupportedException>(() => Convert(query));
                Assert.Equal(Strings.ELinq_UnsupportedCast(typeof(ArubaFailure), typeof(ArubaBaseline)), ex.Message);
            }

            [Fact]
            public void Convert_from_expression_with_cast()
            {
                var query = from long id in _db.Runs.Select(r => r.Id)
                            select id;

                var expression = Convert(query);

                var printedExpression = _printer.Print(expression);
                Assert.Equal(
                    @"Project
|_Input : 'LQ3'
| |_Project
|   |_Input : 'LQ2'
|   | |_Project
|   |   |_Input : 'LQ1'
|   |   | |_Scan : ArubaContext.Runs
|   |   |_Projection
|   |     |_Var(LQ1).Id
|   |_Projection
|     |_Cast(Var(LQ2) As Edm.Int64)
|_Projection
  |_Var(LQ3)",
                    printedExpression);
            }

            [Fact]
            public void Convert_group_by_expression()
            {
                var query = from r in _db.Runs
                            group r by r.Purpose;

                var expression = Convert(query) as DbProjectExpression;

                var printedExpression = _printer.Print(expression);
                Assert.Equal(
                    @"Project
|_Input : 'LQ2'
| |_GroupBy
|   |_Input : 'LQ1', 'GroupLQ1'
|   | |_Scan : ArubaContext.Runs
|   |_Keys
|   | |_Key : 'Key'
|   |   |_Var(LQ1).Purpose
|   |_Aggregates
|     |_Aggregate : 'Group'
|       |_GroupAggregate
|         |_Var(GroupLQ1)
|_Projection
  |_NewInstance : Record['Key'=Edm.Int32, 'Group'=Collection{CodeFirstNamespace.ArubaRun}]
    |_Column : 'Key'
    | |_Var(LQ2).Key
    |_Column : 'Group'
      |_Var(LQ2).Group",
                    printedExpression);
            }

            [Fact]
            public void Convert_group_by_into_expression()
            {
                var query = from r in _db.Runs
                            group r by r.Purpose into g
                            select g;

                var expression = Convert(query) as DbProjectExpression;

                var printedExpression = _printer.Print(expression);
                Assert.Equal(
                    @"Project
|_Input : 'LQ3'
| |_Project
|   |_Input : 'LQ2'
|   | |_GroupBy
|   |   |_Input : 'LQ1', 'GroupLQ1'
|   |   | |_Scan : ArubaContext.Runs
|   |   |_Keys
|   |   | |_Key : 'Key'
|   |   |   |_Var(LQ1).Purpose
|   |   |_Aggregates
|   |     |_Aggregate : 'Group'
|   |       |_GroupAggregate
|   |         |_Var(GroupLQ1)
|   |_Projection
|     |_NewInstance : Record['Key'=Edm.Int32, 'Group'=Collection{CodeFirstNamespace.ArubaRun}]
|       |_Column : 'Key'
|       | |_Var(LQ2).Key
|       |_Column : 'Group'
|         |_Var(LQ2).Group
|_Projection
  |_Var(LQ3)",
                    printedExpression);
            }

            [Fact]
            public void Convert_join_in_on_equals_into_expression()
            {
                var query = from r in _db.Runs
                            join b in _db.Bugs on r.Id equals b.Number into g
                            select g;

                var expression = Convert(query);

                var printedExpression = _printer.Print(expression);
                Assert.Equal(
                    @"Project
|_Input : 'LQ3'
| |_Project
|   |_Input : 'LQ1'
|   | |_Scan : ArubaContext.Runs
|   |_Projection
|     |_NewInstance : Record['o'=CodeFirstNamespace.ArubaRun, 'i'=Collection{CodeFirstNamespace.ArubaBug}]
|       |_Column : 'o'
|       | |_Var(LQ1)
|       |_Column : 'i'
|         |_Filter
|           |_Input : 'LQ2'
|           | |_Scan : ArubaContext.Bugs
|           |_Predicate
|             |_
|               |_
|               | |_Var(LQ1).Id
|               | |_=
|               | |_Var(LQ2).Number
|               |_Or
|               |_
|                 |_IsNull
|                 | |_Var(LQ1).Id
|                 |_And
|                 |_IsNull
|                   |_Var(LQ2).Number
|_Projection
  |_Var(LQ3).i",
                    printedExpression);
            }

            [Fact]
            public void Convert_join_in_on_equals_expression()
            {
                var query = from r in _db.Runs
                            join b in _db.Bugs on r.Id equals b.Number
                            select r;

                var expression = Convert(query);

                var printedExpression = _printer.Print(expression);
                Assert.Equal(
                    @"Project
|_Input : 'LQ3'
| |_InnerJoin
|   |_Left : 'LQ1'
|   | |_Scan : ArubaContext.Runs
|   |_Right : 'LQ2'
|   | |_Scan : ArubaContext.Bugs
|   |_JoinCondition
|     |_
|       |_
|       | |_Var(LQ1).Id
|       | |_=
|       | |_Var(LQ2).Number
|       |_Or
|       |_
|         |_IsNull
|         | |_Var(LQ1).Id
|         |_And
|         |_IsNull
|           |_Var(LQ2).Number
|_Projection
  |_Var(LQ3).LQ1",
                    printedExpression);
            }

            [Fact]
            public void Convert_orderby_expression()
            {
                var query = from r in _db.Runs
                            orderby r.Name
                            select r;

                var expression = Convert(query);

                var printedExpression = _printer.Print(expression);
                Assert.Equal(
                    @"Sort
|_Input : 'LQ1'
| |_Scan : ArubaContext.Runs
|_SortOrder
  |_Asc
    |_Var(LQ1).Name",
                    printedExpression);
            }

            [Fact]
            public void Convert_orderby_descending_expression()
            {
                var query = from r in _db.Runs
                            orderby r.Name descending
                            select r;

                var expression = Convert(query);

                var printedExpression = _printer.Print(expression);
                Assert.Equal(
                    @"Sort
|_Input : 'LQ1'
| |_Scan : ArubaContext.Runs
|_SortOrder
  |_Desc
    |_Var(LQ1).Name",
                    printedExpression);
            }

            [Fact]
            public void Convert_select_expression()
            {
                var query = from r in _db.Runs
                            select r.Id;

                var expression = Convert(query);

                var printedExpression = _printer.Print(expression);
                Assert.Equal(
                    @"Project
|_Input : 'LQ1'
| |_Scan : ArubaContext.Runs
|_Projection
  |_Var(LQ1).Id",
                    printedExpression);
            }

            [Fact]
            public void Convert_multiple_from_expressions()
            {
                var query = from r in _db.Runs
                            from t in r.Tasks
                            select t;

                var expression = Convert(query);

                var printedExpression = _printer.Print(expression);
                Assert.Equal(
                    @"Project
|_Input : 'LQ3'
| |_CrossApply
|   |_Input : 'LQ1'
|   | |_Scan : ArubaContext.Runs
|   |_Apply : 'LQ2'
|     |_Var(LQ1).Tasks
|_Projection
  |_Var(LQ3).LQ2",
                    printedExpression);
            }

            [Fact]
            public void Convert_orderby_expression_with_multiple_keys()
            {
                var query = from r in _db.Runs
                            orderby r.Purpose, r.Name
                            select r;

                var expression = Convert(query);

                var printedExpression = _printer.Print(expression);
                Assert.Equal(
                    @"Sort
|_Input : 'LQ1'
| |_Scan : ArubaContext.Runs
|_SortOrder
  |_Asc
  | |_Var(LQ1).Purpose
  |_Asc
    |_Var(LQ1).Name",
                    printedExpression);
            }

            [Fact]
            public void Convert_orderby_descending_expression_with_multiple_keys()
            {
                var query = from r in _db.Runs
                            orderby r.Purpose descending, r.Name descending
                            select r;

                var expression = Convert(query);

                var printedExpression = _printer.Print(expression);
                Assert.Equal(
                    @"Sort
|_Input : 'LQ1'
| |_Scan : ArubaContext.Runs
|_SortOrder
  |_Desc
  | |_Var(LQ1).Purpose
  |_Desc
    |_Var(LQ1).Name",
                    printedExpression);
            }

            [Fact]
            public void Convert_where_expression()
            {
                var query = from r in _db.Runs
                            where r.Id == 1
                            select r;

                var expression = Convert(query);

                var printedExpression = _printer.Print(expression);
                Assert.Equal(
                    @"Filter
|_Input : 'LQ1'
| |_Scan : ArubaContext.Runs
|_Predicate
  |_
    |_
    | |_1
    | |_=
    | |_Var(LQ1).Id
    |_And
    |_Not
      |_IsNull
        |_Var(LQ1).Id",
                    printedExpression);
            }

            private DbExpression Convert(IQueryable query)
            {
                return new ExpressionConverter(_funcletizer, query.Expression).Convert();
            }
        }
    }
}
