// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Data.Entity.Schema
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public class ForeignKeyConstraint : Constraint
    {
        public ForeignKeyConstraint()
        {
            ForeignKeys = new Collection<ForeignKey>();
        }

        public bool IsCascadeDelete { get; set; }
        public virtual ICollection<ForeignKey> ForeignKeys { get; private set; }
    }
}
