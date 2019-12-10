using HB.Framework.Database;
using HB.Framework.Database.Entity;
using HB.Infrastructure.MySQL;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace HB.Framework.DatabaseTests
{
    //[TestCaseOrderer("HB.Framework.Database.Test.TestCaseOrdererByTestName", "HB.Framework.Database.Test")]
    public class MutipleTableTest : IClassFixture<ServiceFixture>
    {
        private readonly IDatabase _mysql;
        private readonly IDatabase _sqlite;
        private readonly ITestOutputHelper _output;

        private IDatabase GetDatabase(string databaseType) =>
            databaseType switch
            {
                "MySQL" => _mysql,
                "SQLite" => _sqlite,
                _ => null
            };

        public MutipleTableTest(ITestOutputHelper testOutputHelper, ServiceFixture serviceFixture)
        {
            _output = testOutputHelper;

            _mysql = serviceFixture.MySQL;
            _sqlite = serviceFixture.SQLite;

            _mysql.Initialize();
            _sqlite.Initialize();

            AddSomeData();

        }

        private void AddSomeData()
        {
            A a1 = new A { Name = "a1" };
            A a2 = new A { Name = "a2" };
            A a3 = new A { Name = "a3" };

            B b1 = new B { Name = "b1" };
            B b2 = new B { Name = "b2" };

            AB a1b1 = new AB { AId = a1.Guid, BId = b1.Guid };
            AB a1b2 = new AB { AId = a1.Guid, BId = b2.Guid };

            AB a2b1 = new AB { AId = a2.Guid, BId = b1.Guid };
            AB a3b2 = new AB { AId = a3.Guid, BId = b2.Guid };

            C c1 = new C { AId = a1.Guid };
            C c2 = new C { AId = a2.Guid };
            C c3 = new C { AId = a3.Guid };
            C c4 = new C { AId = a1.Guid };
            C c5 = new C { AId = a2.Guid };
            C c6 = new C { AId = a3.Guid };

            _mysql.Add(a2, null);
            _mysql.Add(a1, null);
            _mysql.Add(a3, null);

            _mysql.Add(b1, null);
            _mysql.Add(b2, null);

            _mysql.Add(a1b1, null);
            _mysql.Add(a1b2, null);
            _mysql.Add(a2b1, null);
            _mysql.Add(a3b2, null);

            _mysql.Add(c1, null);
            _mysql.Add(c2, null);
            _mysql.Add(c3, null);
            _mysql.Add(c4, null);
            _mysql.Add(c5, null);
            _mysql.Add(c6, null);


            _sqlite.Add(a2, null);
            _sqlite.Add(a1, null);
            _sqlite.Add(a3, null);

            _sqlite.Add(b1, null);
            _sqlite.Add(b2, null);

            _sqlite.Add(a1b1, null);
            _sqlite.Add(a1b2, null);
            _sqlite.Add(a2b1, null);
            _sqlite.Add(a3b2, null);

            _sqlite.Add(c1, null);
            _sqlite.Add(c2, null);
            _sqlite.Add(c3, null);
            _sqlite.Add(c4, null);
            _sqlite.Add(c5, null);
            _sqlite.Add(c6, null);
        }

        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public void Test_1_ThreeTable_JoinTest(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType);

            var from = database
                .From<A>()
                .LeftJoin<AB>((a, ab) => ab.AId == a.Guid)
                .LeftJoin<AB, B>((ab, b) => ab.BId == b.Guid);


            try
            {
                var result = database.Retrieve<A, AB, B>(from, database.Where<A>(), null);
                Assert.True(result.Count() > 0);
            }
            catch(Exception ex)
            {
                _output.WriteLine(ex.Message);

                throw ex;
            }

            
        }

        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public void Test_2_TwoTable_JoinTest(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType);
            var from = database
                .From<C>()
                .LeftJoin<A>((c, a) => c.AId == a.Guid);


            try
            {
                var result = database.Retrieve<C, A>(from, database.Where<C>(), null);
                Assert.True(result.Count() > 0);
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);

                throw ex;
            }
        }
    }

    public class A : DatabaseEntity
    {
        [UniqueGuidEntityProperty]
        public string Guid { get; set; } = SecurityUtil.CreateUniqueToken();

        [EntityProperty]
        public string Name { get; set; }
    }

    public class B : DatabaseEntity
    {
        [UniqueGuidEntityProperty]
        public string Guid { get; set; } = SecurityUtil.CreateUniqueToken();

        [EntityProperty]
        public string Name { get; set; }
    }

    public class AB : DatabaseEntity
    {
        [UniqueGuidEntityProperty]
        public string Guid { get; set; } = SecurityUtil.CreateUniqueToken();

        [EntityProperty]
        public string AId { get; set; }

        [EntityProperty]
        public string BId { get; set; }
    }

    public class C : DatabaseEntity
    {
        [UniqueGuidEntityProperty]
        public string Guid { get; set; } = SecurityUtil.CreateUniqueToken();

        [EntityProperty]
        public string Name { get; set; }

        [EntityProperty]
        public string AId { get; set; }
    }

}
