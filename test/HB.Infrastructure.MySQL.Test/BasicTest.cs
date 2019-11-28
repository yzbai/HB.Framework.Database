using HB.Framework.Database.Test.Data;
using System;
using System.Collections.Generic;
using System.Data;
using Xunit;
using Xunit.Abstractions;

namespace HB.Framework.Database.Test
{
    //[TestCaseOrderer("HB.Framework.Database.Test.TestCaseOrdererByTestName", "HB.Framework.Database.Test")]
    public class BasicTest : IClassFixture<ServiceFixture>
    {
        private readonly IDatabase _database;
        private readonly ITestOutputHelper _output;
        private readonly IsolationLevel _isolationLevel = IsolationLevel.Serializable;


        public BasicTest(ITestOutputHelper testOutputHelper, ServiceFixture serviceFixture)
        {
            _output = testOutputHelper;
            _database = serviceFixture.Database;
            _database.Initialize();
        }

        [Fact]
        public void Test_1_Batch_Add_PublisherEntity()
        {
            IList<PublisherEntity> publishers = Mocker.GetPublishers();

            TransactionContext transactionContext = _database.BeginTransaction<PublisherEntity>(_isolationLevel);
            DatabaseResult result;
            try
            {
                result = _database.BatchAdd<PublisherEntity>(publishers, "tester", transactionContext);

                if (!result.IsSucceeded())
                {
                    _output.WriteLine(result.Exception?.Message);
                    throw new Exception();
                }

                _database.Commit(transactionContext);

            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                _database.Rollback(transactionContext);
                throw ex;
            }

            Assert.True(result.IsSucceeded());
        }

        [Fact]
        public void Test_2_Batch_Update_PublisherEntity()
        {
            TransactionContext transContext = _database.BeginTransaction<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> lst = _database.RetrieveAll<PublisherEntity>(transContext);

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

                DatabaseResult result = _database.BatchUpdate<PublisherEntity>(lst, "tester", transContext);

                if (!result.IsSucceeded())
                {
                    _output.WriteLine(result.Exception?.Message);
                    _database.Rollback(transContext);
                    throw new Exception();
                }

                _database.Commit(transContext);

                Assert.True(result.IsSucceeded());
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                _database.Rollback(transContext);
                throw ex;
            }
        }

        [Fact]
        public void Test_3_Batch_Delete_PublisherEntity()
        {
            TransactionContext transactionContext = _database.BeginTransaction<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> lst = _database.Page<PublisherEntity>(2, 100, transactionContext);

                if (lst.Count != 0)
                {
                    DatabaseResult result = _database.BatchDelete<PublisherEntity>(lst, "deleter", transactionContext);

                    if (!result.IsSucceeded())
                    {
                        _output.WriteLine(result.Exception?.Message);
                        throw new Exception();
                    }

                    Assert.True(result.IsSucceeded());

                }

                _database.Commit(transactionContext);
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                _database.Rollback(transactionContext);
                throw ex;
            }
        }

        [Fact]
        public void Test_4_Add_PublisherEntity()
        {
            TransactionContext tContext = _database.BeginTransaction<PublisherEntity>(_isolationLevel);

            try
            {
                DatabaseResult result = DatabaseResult.Failed(new Exception());

                for (int i = 0; i < 10; ++i)
                {
                    PublisherEntity entity = Mocker.MockOne();

                    result = _database.Add(entity, tContext);

                    if (!result.IsSucceeded())
                    {
                        _output.WriteLine(result.Exception?.Message);
                        throw new Exception();
                    }

                    
                }

                _database.Commit(tContext);

                Assert.True(result.IsSucceeded());
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                _database.Rollback(tContext);
                throw ex;
            }
        }

        [Fact]
        public void Test_5_Update_PublisherEntity()
        {
            TransactionContext tContext = _database.BeginTransaction<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> testEntities = _database.Page<PublisherEntity>(1, 1, tContext);

                if (testEntities.Count == 0)
                {
                    _database.Rollback(tContext);
                    return;
                }

                PublisherEntity entity = testEntities[0];

                entity.Books.Add("New Book");
                entity.BookAuthors.Add("New Book", new Author() { Mobile = "15190208956", Name = "Yuzhaobai" });

                DatabaseResult result = _database.Update(entity, tContext);

                if (!result.IsSucceeded())
                {
                    _output.WriteLine(result.Exception?.Message);
                    throw new Exception();
                }


                PublisherEntity stored = _database.Scalar<PublisherEntity>(entity.Id, tContext);
                
                _database.Commit(tContext);

                Assert.True(result.IsSucceeded());
                Assert.True(stored.Books.Contains("New Book"));
                Assert.True(stored.BookAuthors["New Book"].Mobile == "15190208956");

            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                _database.Rollback(tContext);
                throw ex;
            }
        }

        [Fact]
        public void Test_6_Delete_PublisherEntity()
        {
            TransactionContext tContext = _database.BeginTransaction<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> testEntities = _database.RetrieveAll<PublisherEntity>(tContext);

                testEntities.ForEach(entity =>
                {
                    DatabaseResult result = _database.Delete(entity, tContext);

                    if (!result.IsSucceeded())
                    {
                        _output.WriteLine(result.Exception?.Message);
                        throw new Exception();
                    }

                    Assert.True(result.IsSucceeded());
                });

                long count = _database.Count<PublisherEntity>(tContext);

                _database.Commit(tContext);

                _output.WriteLine($"count: {count}");

                Assert.True(count == 0);
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                _database.Rollback(tContext);
                throw ex;
            }
        }
    }
}
