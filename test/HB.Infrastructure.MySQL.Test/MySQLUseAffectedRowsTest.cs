﻿using HB.Framework.Database;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace HB.Framework.DatabaseTests
{
    public class MySQLUseAffectedRowsTest : IClassFixture<ServiceFixture>
    {
        [Fact]
        public void TestUseAffectedRow_When_True_Test()
        {
            string connectString = "server=127.0.0.1;port=3306;user=admin;password=_admin;database=test_db;SslMode=None;UseAffectedRows=true";

            using MySqlConnection mySqlConnection = new MySqlConnection(connectString);
            mySqlConnection.Open();

            string commandText = $"update `tb_publisherentity` set  `Name`='{new Random().NextDouble()}', `Version`=2 WHERE `Id`=1 ;";

            using MySqlCommand mySqlCommand1 = new MySqlCommand(commandText, mySqlConnection);

            int rt1 =  mySqlCommand1.ExecuteNonQuery();

            using MySqlCommand mySqlCommand2 = new MySqlCommand(commandText, mySqlConnection);

            int rt2 = mySqlCommand1.ExecuteNonQuery();

            Assert.NotEqual(rt1, rt2);
        }

        [Fact]
        public void TestUseAffectedRow_When_False_Test()
        {
            string connectString = "server=127.0.0.1;port=3306;user=admin;password=_admin;database=test_db;SslMode=None;UseAffectedRows=false";

            using MySqlConnection mySqlConnection = new MySqlConnection(connectString);
            mySqlConnection.Open();

            string commandText = $"update `tb_publisherentity` set  `Name`='{new Random().NextDouble()}', `Version`=2 WHERE `Id`=1 ;";

            using MySqlCommand mySqlCommand1 = new MySqlCommand(commandText, mySqlConnection);

            int rt1 = mySqlCommand1.ExecuteNonQuery();

            using MySqlCommand mySqlCommand2 = new MySqlCommand(commandText, mySqlConnection);

            int rt2 = mySqlCommand1.ExecuteNonQuery();

            Assert.Equal(rt1, rt2);
        }
    }
}