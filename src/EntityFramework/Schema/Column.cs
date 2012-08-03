// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Data.Entity.Schema
{
    public class Column
    {
        public string Name { get; set; }
        public int Ordinal { get; set; }
        public bool IsNullable { get; set; }
        public string TypeName { get; set; }
        public int? MaxLength { get; set; }
        public int? Precision { get; set; }
        public int? DateTimePrecision { get; set; }
        public int? Scale { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsStoreGenerated { get; set; }
    }
}
