// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Data.Entity.Core.Common
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Data.Common;
    using System.Data.Entity.Core.EntityClient;
    using System.Data.Entity.Schema;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Contracts;
    using System.Linq;

    /// <summary>
    /// Retrieves schema information using the legacy provider model by
    /// materializing the results of familiar queries.
    /// </summary>
    internal class LegacySchemaInformation : SchemaInformation
    {
        #region Queries

        private const string TableDetailSql = @"SELECT
    t.CatalogName,
    t.SchemaName,
    t.Name,
    t.ColumnName,
    t.Ordinal,
    t.IsNullable,
    t.TypeName,
    t.MaxLength,
    t.Precision,
    t.DateTimePrecision,
    t.Scale,
    t.IsIdentity,
    t.IsStoreGenerated,
    CASE
        WHEN pk.IsPrimaryKey IS NULL THEN false
        ELSE pk.IsPrimaryKey
    END as IsPrimaryKey
FROM
    (
        SELECT
            t.CatalogName,
            t.SchemaName,
            t.Name,
            c.Id as ColumnId,
            c.Name as ColumnName,
            c.Ordinal,
            c.IsNullable,
            c.ColumnType.TypeName as TypeName,
            c.ColumnType.MaxLength as MaxLength,
            c.ColumnType.Precision as Precision,
            c.ColumnType.DateTimePrecision as DateTimePrecision,
            c.ColumnType.Scale as Scale,
            c.IsIdentity,
            c.IsStoreGenerated
        FROM SchemaInformation.Tables as t
            cross apply t.Columns as c
    ) as t 
        LEFT OUTER JOIN
            (
                SELECT
                    true as IsPrimaryKey,
                    pkc.Id
                FROM OfType(SchemaInformation.TableConstraints, Store.PrimaryKeyConstraint) as pk
                    CROSS APPLY pk.Columns as pkc
            ) as pk
            ON t.ColumnId = pk.Id";

        private const string ViewDetailSql = @"SELECT
    v.CatalogName,
    v.SchemaName,
    v.Name,
    v.ColumnName,
    v.Ordinal,
    v.IsNullable,
    v.TypeName,
    v.MaxLength,
    v.Precision,
    v.DateTimePrecision,
    v.Scale,
    v.IsIdentity,
    v.IsStoreGenerated,
    CASE
        WHEN pk.IsPrimaryKey IS NULL THEN false
        ELSE pk.IsPrimaryKey
    END as IsPrimaryKey
FROM
    (
        SELECT
            v.CatalogName,
            v.SchemaName,
            v.Name,
            c.Id as ColumnId,
            c.Name as ColumnName,
            c.Ordinal,
            c.IsNullable,
            c.ColumnType.TypeName as TypeName,
            c.ColumnType.MaxLength as MaxLength,
            c.ColumnType.Precision as Precision,
            c.ColumnType.DateTimePrecision as DateTimePrecision,
            c.ColumnType.Scale as Scale,
            c.IsIdentity,
            c.IsStoreGenerated
        FROM SchemaInformation.Views as v
            cross apply v.Columns as c
    ) as v 
        LEFT OUTER JOIN
            (
                SELECT
                    true as IsPrimaryKey,
                    pkc.Id
                FROM OfType(SchemaInformation.ViewConstraints, Store.PrimaryKeyConstraint) as pk
                    CROSS APPLY pk.Columns as pkc
            ) as pk
            ON v.ColumnId = pk.Id";

        private const string FunctionReturnTableDetailSql = @"SELECT
    tvf.CatalogName,
    tvf.SchemaName,
    tvf.Name,
    tvf.ColumnName,
    tvf.Ordinal,
    tvf.IsNullable,
    tvf.TypeName,
    tvf.MaxLength,
    tvf.Precision,
    tvf.DateTimePrecision,
    tvf.Scale,
    false as IsIdentity,
    false as IsStoreGenerated,
    false as IsPrimaryKey
FROM
    (
        SELECT
            t.CatalogName,
            t.SchemaName,
            t.Name,
            c.Id as ColumnId,
            c.Name as ColumnName,
            c.Ordinal,
            c.IsNullable,
            c.ColumnType.TypeName as TypeName,
            c.ColumnType.MaxLength as MaxLength,
            c.ColumnType.Precision as Precision,
            c.ColumnType.DateTimePrecision as DateTimePrecision,
            c.ColumnType.Scale as Scale
        FROM OfType(SchemaInformation.Functions, Store.TableValuedFunction) as t
            cross apply t.Columns as c
    ) as tvf";

        private const string FunctionDetailSqlVersion3 = @"Function IsTvf(f Store.Function) as (f is of (Store.TableValuedFunction))
SELECT
    sp.CatalogName,
    sp.SchemaName,
    sp.Name,
    sp.ReturnTypeName,
    sp.IsAggregate,
    sp.IsComposable,
    sp.IsBuiltIn,
    sp.IsNiladic,
    sp.IsTvf,
    sp.ParameterName,
    sp.ParameterType,
    sp.Mode
FROM
    (
        (
            SELECT
                r.CatalogName as CatalogName,
                r.SchemaName as SchemaName,
                r.Name as Name,
                TREAT(r as Store.ScalarFunction).ReturnType.TypeName as ReturnTypeName,
                TREAT(r as Store.ScalarFunction).IsAggregate as IsAggregate,
                true as IsComposable,
                r.IsBuiltIn as IsBuiltIn,
                r.IsNiladic as IsNiladic,
                IsTvf(r) as IsTvf,
                p.Name as ParameterName,
                p.ParameterType.TypeName as ParameterType,
                p.Mode as Mode,
                p.Ordinal as Ordinal
            FROM SchemaInformation.Functions as r
                OUTER APPLY r.Parameters as p
        )
        UNION ALL
        (
            SELECT
                r.CatalogName as CatalogName,
                r.SchemaName as SchemaName,
                r.Name as Name,
                CAST(NULL as string) as ReturnTypeName,
                false as IsAggregate,
                false as IsComposable,
                false as IsBuiltIn,
                false as IsNiladic,
                false as IsTvf,
                p.Name as ParameterName,
                p.ParameterType.TypeName as ParameterType,
                p.Mode as Mode,
                p.Ordinal as Ordinal
            FROM SchemaInformation.Procedures as r
                OUTER APPLY r.Parameters as p
        )
    ) as sp
ORDER BY
    sp.SchemaName,
    sp.Name,
    sp.Ordinal";

        private const string RelationshipDetailSql = @"SELECT
    r.ToTable.CatalogName as ToTableCatalog,
    r.ToTable.SchemaName as ToTableSchema,
    r.ToTable.Name as ToTableName,
    r.ToColumnName,
    r.FromTable.CatalogName as FromTableCatalog,
    r.FromTable.SchemaName as FromTableSchema,
    r.FromTable.Name as FromTableName,
    r.FromColumnName,
    r.Ordinal,
    r.RelationshipName,
    r.RelationshipId,
    r.IsCascadeDelete
FROM
    (
        SELECT
            fks.ToColumn.Parent as ToTable,
            fks.ToColumn.Name as ToColumnName,
            c.Parent as FromTable,
            fks.FromColumn.Name as FromColumnName,
            fks.Ordinal as Ordinal,
            c.Name as RelationshipName,
            c.Id as RelationshipId,
            c.DeleteRule = 'CASCADE' as IsCascadeDelete
        FROM OfType(SchemaInformation.TableConstraints, Store.ForeignKeyConstraint) as c
            ,
                (
                    SELECT
                        Ref(fk.Constraint) as cRef,
                        fk.ToColumn,
                        fk.FromColumn,
                        fk.Ordinal
                    FROM c.ForeignKeys as fk
                ) as fks
                WHERE fks.cRef = Ref(c)
    ) as r";

        #endregion

        private readonly EntityConnection _connection;
        private readonly ICollection<TableOrView> _tablesAndViews = new Collection<TableOrView>();
        private readonly ICollection<Routine> _routines = new Collection<Routine>();
        private readonly ICollection<Constraint> _constraints = new Collection<Constraint>();

        public LegacySchemaInformation(EntityConnection storeSchemaConnection)
        {
            Contract.Requires(storeSchemaConnection != null);

            _connection = storeSchemaConnection;

            using (storeSchemaConnection)
            {
                storeSchemaConnection.Open();

                LoadTables();
                LoadViews();
                LoadFunctions();
                LoadFunctionReturnTables();
                LoadRelationships();
            }
        }

        public override IEnumerable<TableOrView> TablesAndViews
        {
            get { return _tablesAndViews; }
        }

        public override IEnumerable<Routine> Routines
        {
            get { return _routines; }
        }

        public override IEnumerable<Constraint> Constraints
        {
            get { return _constraints; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _connection != null)
            {
                _connection.Dispose();
            }

            base.Dispose(disposing);
        }

        private void LoadTables()
        {
            LoadTabularObject(
                TableDetailSql,
                GetOrCreateTableOrView<Table>);
        }

        private void LoadViews()
        {
            LoadTabularObject(
                ViewDetailSql,
                GetOrCreateTableOrView<View>);
        }

        private void LoadFunctionReturnTables()
        {
            LoadTabularObject(
                FunctionReturnTableDetailSql,
                GetOrCreateRoutine<TableValuedFunction>);
        }

        private void LoadFunctions()
        {
            // TODO: Fallback to previous query
            ReadData(
                FunctionDetailSqlVersion3,
                reader =>
                {
                    Routine routine;
                    var catalogName = (string)reader["CatalogName"];
                    var schemaName = (string)reader["SchemaName"];
                    var name = (string)reader["Name"];
                    var returnTypeName = reader["ReturnTypeName"] as string;
                    var isAggregateValue = reader["IsAggregate"];

                    if ((bool)reader["IsComposable"])
                    {
                        SchemaFunction function;
                        var isBuiltIn = (bool?)reader["IsBuiltIn"];
                        var isNiladic = (bool?)reader["IsNiladic"];

                        if ((bool)reader["IsTvf"])
                        {
                            var tvf = GetOrCreateRoutine<TableValuedFunction>(
                                catalogName,
                                schemaName,
                                name);

                            function = tvf;
                        }
                        else
                        {
                            var scalarFunction = GetOrCreateRoutine<ScalarFunction>(
                                catalogName,
                                schemaName,
                                name);
                            scalarFunction.ReturnTypeName = returnTypeName;
                            scalarFunction.IsAggregate = Convert.IsDBNull(isAggregateValue)
                                ? (bool?)null
                                : (bool)isAggregateValue;

                            function = scalarFunction;
                        }

                        function.IsBuiltIn = isBuiltIn;
                        function.IsNiladic = isNiladic;

                        routine = function;
                    }
                    else
                    {
                        var procedure = GetOrCreateRoutine<Procedure>(
                                catalogName,
                                schemaName,
                                name);

                        routine = procedure;
                    }

                    var parameterName = reader["ParameterName"] as string;

                    if (parameterName != null)
                    {
                        routine.Parameters.Add(
                            new Parameter
                                {
                                    Name = parameterName,
                                    TypeName = (string)reader["ParameterType"],
                                    Mode = (string)reader["Mode"]
                                });
                    }
                });
        }

        private void LoadRelationships()
        {
            ReadData(
                RelationshipDetailSql,
                reader =>
                {
                    var toColumn = GetOrCreateColumn(
                        (string)reader["ToTableCatalog"],
                        (string)reader["ToTableSchema"],
                        (string)reader["ToTableName"],
                        (string)reader["ToColumnName"]);
                    var fromTableCatalog = (string)reader["FromTableCatalog"];
                    var fromTableSchema = (string)reader["FromTableSchema"];
                    var fromTable = GetOrCreateTableOrView<Table>(
                        fromTableCatalog,
                        fromTableSchema,
                        (string)reader["FromTableName"]);
                    var fromColumn = GetOrCreateColumn(
                        fromTable,
                        (string)reader["FromColumnName"]);
                    var ordinal = (int)reader["Ordinal"];

                    var constraint = GetOrCreateForeignKeyConstraint(
                        fromTableCatalog,
                        fromTableSchema,
                        (string)reader["RelationshipName"]);
                    constraint.IsCascadeDelete = (bool)reader["IsCascadeDelete"];
                    constraint.Parent = fromTable;
                    constraint.ForeignKeys.Add(
                        new ForeignKey
                            {
                                Ordinal = ordinal,
                                FromColumn = fromColumn,
                                ToColumn = toColumn
                            });
                });
        }

        private void LoadTabularObject<T>(string commandText, Func<string, string, string, T> getOrCreate)
        {
            Contract.Requires(
                typeof(Table).IsAssignableFrom(typeof(T))
                    || typeof(View).IsAssignableFrom(typeof(T))
                    || typeof(TableValuedFunction).IsAssignableFrom(typeof(T)));

            ReadData(
                commandText,
                reader =>
                {
                    var obj = getOrCreate(
                            (string)reader["CatalogName"],
                            (string)reader["SchemaName"],
                            (string)reader["Name"]);

                    var column = GetOrCreateColumn(
                        obj,
                        (string)reader["ColumnName"]);
                    column.Ordinal = (int)reader["Ordinal"];
                    column.IsNullable = (bool)reader["IsNullable"];
                    column.TypeName = (string)reader["TypeName"];
                    column.MaxLength = reader["MaxLength"] as int?;
                    column.Precision = reader["Precision"] as int?;
                    column.DateTimePrecision = reader["DateTimePrecision"] as int?;
                    column.Scale = reader["Scale"] as int?;
                    column.IsIdentity = (bool)reader["IsIdentity"];
                    column.IsStoreGenerated = (bool)reader["IsStoreGenerated"];

                    if ((bool)reader["IsPrimaryKey"])
                    {
                        // TODO: Set constraint's catalog, schema & name
                        var primaryKey = GetOrCreatePrimaryKey(obj as Table);
                        primaryKey.Columns.Add(column);
                    }
                });
        }

        [SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        private void ReadData(string commandText, Action<DbDataReader> read)
        {
            using (var command = _connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 0;
                command.CommandText = commandText;

                using (var reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    while (reader.Read())
                    {
                        read(reader);
                    }
                }
            }
        }

        private T GetOrCreateTableOrView<T>(string catalogName, string schemaName, string name)
            where T : TableOrView, new()
        {
            var tableOrView = _tablesAndViews
                .OfType<T>()
                .FirstOrDefault(
                    t => t.CatalogName == catalogName
                        && t.SchemaName == schemaName
                        && t.Name == name);

            if (tableOrView == null)
            {
                tableOrView = new T
                    {
                        CatalogName = catalogName,
                        SchemaName = schemaName,
                        Name = name
                    };
                _tablesAndViews.Add(tableOrView);
            }

            return tableOrView;
        }

        private T GetOrCreateRoutine<T>(string catalogName, string schemaName, string name)
            where T : Routine, new()
        {
            var routine = _routines
                .OfType<T>()
                .FirstOrDefault(
                    f => f.CatalogName == catalogName
                        && f.SchemaName == schemaName
                        && f.Name == name);

            if (routine == null)
            {
                routine = new T
                {
                    CatalogName = catalogName,
                    SchemaName = schemaName,
                    Name = name
                };
                _routines.Add(routine);
            }

            return routine;
        }

        private PrimaryKeyConstraint GetOrCreatePrimaryKey(Table parent)
        {
            Contract.Requires(parent != null);

            var primaryKey = _constraints
                .OfType<PrimaryKeyConstraint>()
                .FirstOrDefault(pk => pk.Parent == parent);

            if (primaryKey == null)
            {
                primaryKey = new PrimaryKeyConstraint
                    {
                        Parent = parent
                    };
                _constraints.Add(primaryKey);
            }

            return primaryKey;
        }

        private ForeignKeyConstraint GetOrCreateForeignKeyConstraint(string catalogName, string schemaName, string name)
        {
            var constraint = _constraints
                .OfType<ForeignKeyConstraint>()
                .FirstOrDefault(
                    c => c.CatalogName == catalogName
                        && c.SchemaName == schemaName
                        && c.Name == name);

            if (constraint == null)
            {
                constraint = new ForeignKeyConstraint
                {
                    CatalogName = catalogName,
                    SchemaName = schemaName,
                    Name = name
                };
                _constraints.Add(constraint);
            }

            return constraint;
        }

        private Column GetOrCreateColumn(string tableCatalog, string tableSchema, string tableName, string columnName)
        {
            return GetOrCreateColumn(
                GetOrCreateTableOrView<Table>(
                    tableCatalog,
                    tableSchema,
                    tableName),
                columnName);
        }

        private static Column GetOrCreateColumn<T>(T obj, string columnName)
        {
            Contract.Requires(obj != null);
            Contract.Requires(
                typeof(T) == typeof(Table)
                || typeof(T) == typeof(View)
                || typeof(T) == typeof(TableValuedFunction));

            ICollection<Column> columns = ((dynamic)obj).Columns;
            var column = columns.FirstOrDefault(c => c.Name == columnName);

            if (column == null)
            {
                column = new Column { Name = columnName };
                columns.Add(column);
            }

            return column;
        }
    }
}
