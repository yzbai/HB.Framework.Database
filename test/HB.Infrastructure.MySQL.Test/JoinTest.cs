using HB.Framework.Database.Entity;
using HB.Infrastructure.MySQL;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace HB.Framework.Database.Test
{
    //[TestCaseOrderer("HB.Framework.Database.Test.TestCaseOrdererByTestName", "HB.Framework.Database.Test")]
    public class MutipleTableTest : IClassFixture<ServiceFixture>
    {
        private readonly IDatabase _db;
        private readonly ITestOutputHelper _output; 

        public MutipleTableTest(ITestOutputHelper testOutputHelper, ServiceFixture serviceFixture)
        {
            _output = testOutputHelper;

            _db = serviceFixture.Database;

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

            _db.Add(a1, null);
            _db.Add(a2, null);
            _db.Add(a3, null);

            _db.Add(b1, null);
            _db.Add(b2, null);

            _db.Add(a1b1, null);
            _db.Add(a1b2, null);
            _db.Add(a2b1, null);
            _db.Add(a3b2, null);

            _db.Add(c1, null);
            _db.Add(c2, null);
            _db.Add(c3, null);
            _db.Add(c4, null);
            _db.Add(c5, null);
            _db.Add(c6, null);
        }

        [Fact]
        public void Test_1_ThreeTable_JoinTest()
        {
            var from = _db
                .From<A>()
                .LeftJoin<AB>((a, ab) => ab.AId == a.Guid)
                .LeftJoin<AB, B>((ab, b) => ab.BId == b.Guid);


            try
            {
                var result = _db.Retrieve<A, AB, B>(from, _db.Where<A>(), null);
                Assert.True(result.Count > 0);
            }
            catch(Exception ex)
            {
                _output.WriteLine(ex.Message);

                throw ex;
            }

            
        }

        [Fact]
        public void Test_2_TwoTable_JoinTest()
        {
            var from = _db
                .From<C>()
                .LeftJoin<A>((c, a) => c.AId == a.Guid);


            try
            {
                var result = _db.Retrieve<C, A>(from, _db.Where<C>(), null);
                Assert.True(result.Count > 0);
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
