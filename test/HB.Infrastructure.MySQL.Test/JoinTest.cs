﻿using HB.Framework.Database;
using HB.Framework.Database.Entity;
using HB.Infrastructure.MySQL;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                _ => throw new ArgumentException(nameof(databaseType))
            };

        public MutipleTableTest(ITestOutputHelper testOutputHelper, ServiceFixture serviceFixture)
        {
            _output = testOutputHelper;

            _mysql = serviceFixture.MySQL;
            _sqlite = serviceFixture.SQLite;

            _mysql.InitializeAsync();
            _sqlite.InitializeAsync();

            AddSomeDataAsync().Wait();

        }

        private async Task AddSomeDataAsync()
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

            await _mysql.AddAsync(a2, null);
            await _mysql.AddAsync(a1, null);
            await _mysql.AddAsync(a3, null);

            await _mysql.AddAsync(b1, null);
            await _mysql.AddAsync(b2, null);

            await _mysql.AddAsync(a1b1, null);
            await _mysql.AddAsync(a1b2, null);
            await _mysql.AddAsync(a2b1, null);
            await _mysql.AddAsync(a3b2, null);

            await _mysql.AddAsync(c1, null);
            await _mysql.AddAsync(c2, null);
            await _mysql.AddAsync(c3, null);
            await _mysql.AddAsync(c4, null);
            await _mysql.AddAsync(c5, null);
            await _mysql.AddAsync(c6, null);


            await _sqlite.AddAsync(a2, null);
            await _sqlite.AddAsync(a1, null);
            await _sqlite.AddAsync(a3, null);

            await _sqlite.AddAsync(b1, null);
            await _sqlite.AddAsync(b2, null);

            await _sqlite.AddAsync(a1b1, null);
            await _sqlite.AddAsync(a1b2, null);
            await _sqlite.AddAsync(a2b1, null);
            await _sqlite.AddAsync(a3b2, null);

            await _sqlite.AddAsync(c1, null);
            await _sqlite.AddAsync(c2, null);
            await _sqlite.AddAsync(c3, null);
            await _sqlite.AddAsync(c4, null);
            await _sqlite.AddAsync(c5, null);
            await _sqlite.AddAsync(c6, null);
        }

        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public async Task Test_1_ThreeTable_JoinTestAsync(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType);

            var from = database
                .From<A>()
                .LeftJoin<AB>((a, ab) => ab.AId == a.Guid)
                .LeftJoin<AB, B>((ab, b) => ab.BId == b.Guid);


            try
            {
                IEnumerable<Tuple<A, AB?, B?>>? result = await database.RetrieveAsync<A, AB, B>(from, database.Where<A>(), null);
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
        public async Task Test_2_TwoTable_JoinTestAsync(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType);
            var from = database
                .From<C>()
                .LeftJoin<A>((c, a) => c.AId == a.Guid);


            try
            {
                IEnumerable<Tuple<C, A?>>? result = await database.RetrieveAsync<C, A>(from, database.Where<C>(), null).ConfigureAwait(false);
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
        public string Name { get; set; } = default!;
    }

    public class B : DatabaseEntity
    {
        [UniqueGuidEntityProperty]
        public string Guid { get; set; } = SecurityUtil.CreateUniqueToken();

        [EntityProperty]
        public string Name { get; set; } = default!;
    }

    public class AB : DatabaseEntity
    {
        [UniqueGuidEntityProperty]
        public string Guid { get; set; } = SecurityUtil.CreateUniqueToken();

        [EntityProperty]
        public string AId { get; set; } = default!;

        [EntityProperty]
        public string BId { get; set; } = default!;
    }

    public class C : DatabaseEntity
    {
        [UniqueGuidEntityProperty]
        public string Guid { get; set; } = SecurityUtil.CreateUniqueToken();

        [EntityProperty]
        public string Name { get; set; } = default!;

        [EntityProperty]
        public string AId { get; set; } = default!;
    }

}
