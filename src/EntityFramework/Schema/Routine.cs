// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Data.Entity.Schema
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public abstract class Routine
    {
        protected Routine()
        {
            Parameters = new Collection<Parameter>();
        }

        public string CatalogName { get; set; }
        public string SchemaName { get; set; }
        public string Name { get; set; }
        public virtual ICollection<Parameter> Parameters { get; private set; }
    }
}
