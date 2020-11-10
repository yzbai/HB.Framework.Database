using HB.Framework.Database;
using HB.Framework.DatabaseTests.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace HB.Framework.DatabaseTests
{
    //[TestCaseOrderer("HB.Framework.Database.Test.TestCaseOrdererByTestName", "HB.Framework.Database.Test")]
    public class BasicAsyncTest : IClassFixture<ServiceFixture>
    {
        private readonly IDatabase _mysql;
        private readonly IDatabase _sqlite;
        private readonly ITestOutputHelper _output;
        private readonly IsolationLevel _isolationLevel = IsolationLevel.Serializable;

        private IDatabase? GetDatabase(string databaseType)
        {

            return databaseType switch
            {
                "MySQL" => _mysql,
                "SQLite" => _sqlite,
                _ => null
            };
        }


        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="testOutputHelper"></param>
        /// <param name="serviceFixture"></param>
        /// <exception cref="DatabaseException">Ignore.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "<Pending>")]
        public BasicAsyncTest(ITestOutputHelper testOutputHelper, ServiceFixture serviceFixture)
        {
            _output = testOutputHelper;

            _mysql = serviceFixture.MySQL;
            _sqlite = serviceFixture.SQLite;

            _mysql.InitializeAsync().Wait();
            _sqlite.InitializeAsync().Wait();
        }

        /// <summary>
        /// Test_1_Batch_Add_PublisherEntityAsync
        /// </summary>
        /// <param name="databaseType"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException">Ignore.</exception>
        /// <exception cref="Exception">Ignore.</exception>
        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public async Task Test_1_Batch_Add_PublisherEntityAsync(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType)!;

            IList<PublisherEntity> publishers = Mocker.GetPublishers();

            TransactionContext transactionContext = await database.BeginTransactionAsync<PublisherEntity>(_isolationLevel).ConfigureAwait(false);

            try
            {
                await database.BatchAddAsync<PublisherEntity>(publishers, "lastUsre", transactionContext);

                await database.CommitAsync(transactionContext);
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                await database.RollbackAsync(transactionContext);
                throw ex;
            }
        }

        /// <summary>
        /// Test_2_Batch_Update_PublisherEntityAsync
        /// </summary>
        /// <param name="databaseType"></param>
        /// <returns></returns>
        /// <exception cref="Exception">Ignore.</exception>
        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public async Task Test_2_Batch_Update_PublisherEntityAsync(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType)!;

            TransactionContext transContext = await database.BeginTransactionAsync<PublisherEntity>(_isolationLevel);

            try
            {
                IEnumerable<PublisherEntity> lst = await database.RetrieveAllAsync<PublisherEntity>(transContext);

                for (int i = 0; i < lst.Count(); i += 2)
                {
                    PublisherEntity entity = lst.ElementAt(i);
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

                await database.BatchUpdateAsync<PublisherEntity>(lst, "lastUsre", transContext);

                await database.CommitAsync(transContext);

            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                await database.RollbackAsync(transContext);
                throw ex;
            }
        }

        /// <summary>
        /// Test_3_Batch_Delete_PublisherEntityAsync
        /// </summary>
        /// <param name="databaseType"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException">Ignore.</exception>
        /// <exception cref="Exception">Ignore.</exception>
        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public async Task Test_3_Batch_Delete_PublisherEntityAsync(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType)!;
            TransactionContext transactionContext = await database.BeginTransactionAsync<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> lst = (await database.PageAsync<PublisherEntity>(2, 100, transactionContext)).ToList();

                if (lst.Count != 0)
                {
                    await database.BatchDeleteAsync<PublisherEntity>(lst, "lastUsre", transactionContext);

                }

                await database.CommitAsync(transactionContext);
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                await database.RollbackAsync(transactionContext);
                throw ex;
            }
        }

        /// <summary>
        /// Test_4_Add_PublisherEntityAsync
        /// </summary>
        /// <param name="databaseType"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException">Ignore.</exception>
        /// <exception cref="Exception">Ignore.</exception>
        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public async Task Test_4_Add_PublisherEntityAsync(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType)!;
            TransactionContext tContext = await database.BeginTransactionAsync<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> lst = new List<PublisherEntity>();

                for (int i = 0; i < 10; ++i)
                {
                    PublisherEntity entity = Mocker.MockOne();

                    await database.AddAsync(entity, "lastUsre", tContext);

                    lst.Add(entity);
                }

                await database.CommitAsync(tContext);

                Assert.True(lst.All(p => p.Id > 0));
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                await database.RollbackAsync(tContext);
                throw ex;
            }
        }

        /// <summary>
        /// Test_5_Update_PublisherEntityAsync
        /// </summary>
        /// <param name="databaseType"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException">Ignore.</exception>
        /// <exception cref="Exception">Ignore.</exception>
        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public async Task Test_5_Update_PublisherEntityAsync(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType)!;
            TransactionContext tContext = await database.BeginTransactionAsync<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> testEntities = (await database.PageAsync<PublisherEntity>(1, 1, tContext)).ToList();

                if (testEntities.Count == 0)
                {
                    throw new Exception("No Entity to update");
                }

                PublisherEntity entity = testEntities[0];

                entity.Books.Add("New Book2");
                entity.BookAuthors.Add("New Book2", new Author() { Mobile = "15190208956", Name = "Yuzhaobai" });

                await database.UpdateAsync(entity, "lastUsre", tContext);

                PublisherEntity? stored = await database.ScalarAsync<PublisherEntity>(entity.Id, tContext);

                await database.CommitAsync(tContext);

                Assert.True(stored?.Books.Contains("New Book2"));
                Assert.True(stored?.BookAuthors["New Book2"].Mobile == "15190208956");

            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.Message);
                await database.RollbackAsync(tContext);
                throw ex;
            }
        }

        /// <summary>
        /// Test_6_Delete_PublisherEntityAsync
        /// </summary>
        /// <param name="databaseType"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException">Ignore.</exception>
        /// <exception cref="Exception">Ignore.</exception>
        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public async Task Test_6_Delete_PublisherEntityAsync(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType)!;
            TransactionContext tContext = await database.BeginTransactionAsync<PublisherEntity>(_isolationLevel);

            try
            {
                IList<PublisherEntity> testEntities = (await database.RetrieveAllAsync<PublisherEntity>(tContext)).ToList();

                await testEntities.ForEachAsync(async entity =>
                {
                    await database.DeleteAsync(entity, "lastUsre", tContext);

                });

                long count = await database.CountAsync<PublisherEntity>(tContext);

                await database.CommitAsync(tContext);

                Assert.True(count == 0);
            }
            catch (Exception ex)
            {
                await database.RollbackAsync(tContext);
                _output.WriteLine(ex.Message);
                throw ex;
            }
        }

        [Theory]
        [InlineData("MySQL")]
        [InlineData("SQLite")]
        public async Task Test_7_AddOrUpdate_PublisherEntityAsync(string databaseType)
        {
            IDatabase database = GetDatabase(databaseType)!;
            TransactionContext tContext = await database.BeginTransactionAsync<PublisherEntity>(_isolationLevel);

            try
            {

                var publishers = Mocker.GetPublishers();

                var newIds = await database.BatchAddAsync(publishers, "xx", tContext);

                for (int i = 0; i < publishers.Count; i += 2)
                {
                    publishers[i].Name = "GGGGG" + i.ToString();

                }

                var affectedIds = await database.BatchAddOrUpdateAsync(publishers, "AddOrUpdaterrrr", tContext);


                publishers[0].Guid = SecurityUtil.CreateUniqueToken();

                await database.AddOrUpdateAsync(publishers[0], "single", tContext);


                await database.CommitAsync(tContext);
            }
            catch (Exception ex)
            {
                await database.RollbackAsync(tContext);
                _output.WriteLine(ex.Message);
                throw ex;
            }
        }
    }
}
