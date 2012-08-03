// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Data.Entity.Schema
{
    using System.Collections.Generic;
    using System.Linq;

    public abstract class SchemaInformation : IDisposable
    {
        ~SchemaInformation()
        {
            Dispose(false);
        }

        public virtual IEnumerable<TableOrView> TablesAndViews
        {
            get { return Enumerable.Empty<TableOrView>(); }
        }

        public virtual IEnumerable<Routine> Routines
        {
            get { return Enumerable.Empty<Routine>(); }
        }

        public virtual IEnumerable<Constraint> Constraints
        {
            get { return Enumerable.Empty<Constraint>(); }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
