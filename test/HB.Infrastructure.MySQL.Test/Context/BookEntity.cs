using HB.Framework.Database.Entity;
using System;
using System.Collections.Generic;
using System.Text;

namespace HB.Framework.DatabaseTests.Data
{
    public class BookEntity : DatabaseEntity
    {
        [UniqueGuidEntityProperty]
        public string Guid { get; set; } = default!;

        [EntityProperty]
        public string Name { get; set; } = default!;

        [EntityProperty]
        public double Price { get; set; } = default!;
    }
}
