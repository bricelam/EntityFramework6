// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Data.Entity.Schema
{
    public abstract class Constraint
    {
        public string CatalogName { get; set; }
        public string SchemaName { get; set; }
        public string Name { get; set; }
        public virtual TableOrView Parent { get; set; }
    }
}
