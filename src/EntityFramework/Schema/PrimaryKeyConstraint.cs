// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Data.Entity.Schema
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public class PrimaryKeyConstraint : Constraint
    {
        public PrimaryKeyConstraint()
        {
            Columns = new Collection<Column>();
        }

        public virtual ICollection<Column> Columns { get; private set; }
    }
}
