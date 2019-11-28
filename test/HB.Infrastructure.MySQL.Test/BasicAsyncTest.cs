using HB.Framework.Database.Test.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace HB.Framework.Database.Test
{
    //[TestCaseOrderer("HB.Framework.Database.Test.TestCaseOrdererByTestName", "HB.Framework.Database.Test")]
    public class BasicAsyncTest : IClassFixture<ServiceFixture>
    {
        private readonly IDatabase _database;
        private readonly ITestOutputHelper _output;
        private readonly IsolationLevel _isolationLevel = IsolationLevel.Serializable;

        public BasicAsyncTest(ITestOutputHelper testOutputHelper, ServiceFixture serviceFixture)
        {
            _output = testOutputHelper;
            _database = serviceFixture.Database;
            _database.Initialize();
        }

        [Fact]
        public async Task Test_1_Batch_Add_PublisherEntityAsync()
        {
            IList<PublisherEntity> publishers = Mocker.GetPublishers();

            TransactionContext transactionContext = await _database.BeginTransactionAsync<PublisherEntity>(_isolationLevel).ConfigureAwait(false);

            DatabaseResult result;

            try
            {
                result = await _database.BatchAddAsync<PublisherEntity>(publishers, "tester", transactionContext);

                if (!result.IsSucceeded())
                {
                    _output.WriteLine(result.Exception?.Message);
                    throw new Exception();
                }

                await _database.CommitAsync(transactionContext);

            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                await _database.RollbackAsync(transactionContext);
                throw ex;
            }

            Assert.True(result.IsSucceeded());
        }

        [Fact]
        public async Task Test_2_Batch_Update_PublisherEntityAsync()
        {
            TransactionContext transContext = await _database.BeginTransactionAsync<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> lst = await _database.RetrieveAllAsync<PublisherEntity>(transContext);

                for (int i = 0; i < lst.Count; i += 2)
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

                DatabaseResult result = await _database.BatchUpdateAsync<PublisherEntity>(lst, "tester", transContext);

                Assert.True(result.IsSucceeded());

                if (!result.IsSucceeded())
                {
                    _output.WriteLine(result.Exception?.Message);
                    await _database.RollbackAsync(transContext);
                    throw new Exception();
                }

                await _database.CommitAsync(transContext);

            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                await _database.RollbackAsync(transContext);
                throw ex;
            }
        }

        [Fact]
        public async Task Test_3_Batch_Delete_PublisherEntityAsync()
        {
            TransactionContext transactionContext = await _database.BeginTransactionAsync<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> lst = await _database.PageAsync<PublisherEntity>(2, 100, transactionContext);

                if (lst.Count != 0)
                {
                    DatabaseResult result = await _database.BatchDeleteAsync<PublisherEntity>(lst, "deleter", transactionContext);

                    if (!result.IsSucceeded())
                    {
                        _output.WriteLine(result.Exception?.Message);
                        throw new Exception();
                    }

                    Assert.True(result.IsSucceeded());
                }

                await _database.CommitAsync(transactionContext);
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                await _database.RollbackAsync(transactionContext);
                throw ex;
            }
        }

        [Fact]
        public async Task Test_4_Add_PublisherEntityAsync()
        {
            TransactionContext tContext = await _database.BeginTransactionAsync<PublisherEntity>(_isolationLevel);

            try
            {
                for (int i = 0; i < 10; ++i)
                {
                    PublisherEntity entity = Mocker.MockOne();

                    DatabaseResult result = await _database.AddAsync(entity, tContext);

                    if (!result.IsSucceeded())
                    {
                        _output.WriteLine(result.Exception?.Message);
                        throw new Exception();
                    }

                    Assert.True(result.IsSucceeded());
                }

                await _database.CommitAsync(tContext);
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                await _database.RollbackAsync(tContext);
                throw ex;
            }
        }

        [Fact]
        public async Task Test_5_Update_PublisherEntityAsync()
        {
            TransactionContext tContext = await _database.BeginTransactionAsync<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> testEntities = await _database.PageAsync<PublisherEntity>(1, 1, tContext);

                if (testEntities.Count == 0)
                {
                    _database.Rollback(tContext);
                    return;
                }

                PublisherEntity entity = testEntities[0];

                entity.Books.Add("New Book2");
                entity.BookAuthors.Add("New Book2", new Author() { Mobile = "15190208956", Name = "Yuzhaobai" });

                DatabaseResult result = await _database.UpdateAsync(entity, tContext);

                if (!result.IsSucceeded())
                {
                    _output.WriteLine(result.Exception?.Message);
                    throw new Exception();
                }

                Assert.True(result.IsSucceeded());

                PublisherEntity stored = await _database.ScalarAsync<PublisherEntity>(entity.Id, tContext);

                Assert.True(stored.Books.Contains("New Book2"));
                Assert.True(stored.BookAuthors["New Book2"].Mobile == "15190208956");

                await _database.CommitAsync(tContext);
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                await _database.RollbackAsync(tContext);
                throw ex;
            }
        }

        [Fact]
        public async Task Test_6_Delete_PublisherEntityAsync()
        {
            TransactionContext tContext = await _database.BeginTransactionAsync<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> testEntities = await _database.RetrieveAllAsync<PublisherEntity>(tContext);

                await testEntities.ForEachAsync(async entity =>
                {
                    DatabaseResult result = await _database.DeleteAsync(entity, tContext);

                    if (!result.IsSucceeded())
                    {
                        _output.WriteLine(result.Exception?.Message);
                        throw new Exception();
                    }

                    Assert.True(result.IsSucceeded());
                });

                long count = await _database.CountAsync<PublisherEntity>(tContext);

                Assert.True(count == 0);

                await _database.CommitAsync(tContext);

                _output.WriteLine($"count: {count}");
            }
            catch (Exception ex)
            {
                await _database.RollbackAsync(tContext);
                _output.WriteLine(ex.Message);
                throw ex;
            }
        }
    }
}
