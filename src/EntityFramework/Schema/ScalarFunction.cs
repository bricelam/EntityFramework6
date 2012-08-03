// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Data.Entity.Schema
{
    public class ScalarFunction : SchemaFunction
    {
        public string ReturnTypeName { get; set; }
        public bool? IsAggregate { get; set; }
    }
}
