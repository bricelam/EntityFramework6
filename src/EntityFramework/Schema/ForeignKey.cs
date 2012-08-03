// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Data.Entity.Schema
{
    public class ForeignKey
    {
        public int Ordinal { get; set; }
        public virtual Column FromColumn { get; set; }
        public virtual Column ToColumn { get; set; }
    }
}
