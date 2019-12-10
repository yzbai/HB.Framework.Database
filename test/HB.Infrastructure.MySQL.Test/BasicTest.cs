using System;
using System.Collections.Generic;
using System.Data;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using HB.Framework.Database;
using HB.Framework.DatabaseTests.Data;

namespace HB.Framework.DatabaseTests
{
    //[TestCaseOrderer("HB.Framework.Database.Test.TestCaseOrdererByTestName", "HB.Framework.Database.Test")]
    public class BasicTest : IClassFixture<ServiceFixture>
    {
        private readonly IDatabase _mysql;
        private readonly IDatabase _sqlite;
        private readonly ITestOutputHelper _output;
        private readonly IsolationLevel _isolationLevel = IsolationLevel.Serializable;

        private IDatabase GetDatabase(string databaseType) =>
            databaseType switch
            {
                "MySQL" => _mysql,
                "SQLite" => _sqlite,
                _ => null
            };

        public BasicTest(ITestOutputHelper testOutputHelper, ServiceFixture serviceFixture)
        {
            _output = testOutputHelper;
            
            _mysql = serviceFixture.MySQL;
            _sqlite = serviceFixture.SQLite;

            _mysql.Initialize();
            _sqlite.Initialize();
        }

        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public void Test_1_Batch_Add_PublisherEntity(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType);
            IList<PublisherEntity> publishers = Mocker.GetPublishers();

            TransactionContext transactionContext = database.BeginTransaction<PublisherEntity>(_isolationLevel);
            try
            {
                IEnumerable<long> newIds = database.BatchAdd<PublisherEntity>(publishers, transactionContext);

                database.Commit(transactionContext);

                Assert.Equal(newIds.Count(), publishers.Count);
                Assert.True(newIds.All(id => id > 0));

            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                database.Rollback(transactionContext);
                throw ex;
            }
        }

        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public void Test_2_Batch_Update_PublisherEntity(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType);
            TransactionContext transContext = database.BeginTransaction<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> lst = database.RetrieveAll<PublisherEntity>(transContext).ToList();

                for (int i = 0; i < lst.Count; i += 3)
                {
                    PublisherEntity entity = lst[i];
                    //entity.Guid = Guid.NewGuid().ToString();
                    entity.Type = PublisherType.Online;
                    entity.Name = "ÖÐsfasfafÎÄÃû×Ö";
                    entity.Books = new List<string>() { "xxx", "tttt" };
                    entity.BookAuthors = new Dictionary<string, Author>()
                    {
                        { "Cat", new Author() { Mobile="111", Name="BB" } },
                        { "Dog", new Author() { Mobile="222", Name="sx" } }
                    };
                }

                database.BatchUpdate(lst, transContext);

                database.Commit(transContext);

            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                database.Rollback(transContext);
                throw ex;
            }
        }

        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public void Test_3_Batch_Delete_PublisherEntity(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType);
            TransactionContext transactionContext = database.BeginTransaction<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> lst = database.Page<PublisherEntity>(2, 100, transactionContext).ToList();

                if (lst.Count != 0)
                {
                    database.BatchDelete<PublisherEntity>(lst, transactionContext);

                }

                database.Commit(transactionContext);
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                database.Rollback(transactionContext);
                throw ex;
            }
        }

        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public void Test_4_Add_PublisherEntity(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType);
            TransactionContext tContext = database.BeginTransaction<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> lst = new List<PublisherEntity>();

                for (int i = 0; i < 10; ++i)
                {
                    PublisherEntity entity = Mocker.MockOne();

                    database.Add(entity, tContext);

                    lst.Add(entity);
                }

                database.Commit(tContext);

                Assert.True(lst.All(p => p.Id > 0));
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                database.Rollback(tContext);
                throw ex;
            }
        }

        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public void Test_5_Update_PublisherEntity(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType);
            TransactionContext tContext = database.BeginTransaction<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> testEntities = database.Page<PublisherEntity>(1, 1, tContext).ToList();

                if (testEntities.Count == 0)
                {
                    throw new Exception("No Entity to update");
                }

                PublisherEntity entity = testEntities[0];

                entity.Books.Add("New Book");
                entity.BookAuthors.Add("New Book", new Author() { Mobile = "15190208956", Name = "Yuzhaobai" });

                database.Update(entity, tContext);


                PublisherEntity stored = database.Scalar<PublisherEntity>(entity.Id, tContext);
                
                database.Commit(tContext);

                Assert.True(stored.Books.Contains("New Book"));
                Assert.True(stored.BookAuthors["New Book"].Mobile == "15190208956");

            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                database.Rollback(tContext);
                throw ex;
            }
        }

        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public void Test_6_Delete_PublisherEntity(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType);
            TransactionContext tContext = database.BeginTransaction<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> testEntities = database.RetrieveAll<PublisherEntity>(tContext).ToList();

                testEntities.ForEach(entity =>
                {
                    database.Delete(entity, tContext);

                });

                long count = database.Count<PublisherEntity>(tContext);

                database.Commit(tContext);

                Assert.True(count == 0);
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                database.Rollback(tContext);
                throw ex;
            }
        }
    }
}
