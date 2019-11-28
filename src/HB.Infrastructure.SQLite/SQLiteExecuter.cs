﻿using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;


namespace HB.Infrastructure.SQLite
{
    /// <summary>
    /// 动态SQL和SP执行
    /// 具体执行步骤都要有异常捕捉，直接抛出给上一层
    /// </summary>
    internal static partial class SQLiteExecuter
    {
        //private static void AttachParameters(SqliteCommand command, IEnumerable<IDataParameter> commandParameters)
        //{
        //    foreach (IDataParameter p in commandParameters)
        //    {
        //        //check for derived output value with no value assigned
        //        if ((p.Direction == ParameterDirection.InputOutput) && (p.Value == null))
        //        {
        //            p.Value = DBNull.Value;
        //        }

        //        command.Parameters.Add(p);
        //    }
        //}

        #region Comand Reader

        public static IDataReader ExecuteCommandReader(SqliteTransaction sqliteTransaction, IDbCommand dbCommand)
        {
            dbCommand.Transaction = sqliteTransaction;
            return ExecuteCommandReader(sqliteTransaction.Connection, false, (SqliteCommand)dbCommand);
        }

        public static IDataReader ExecuteCommandReader(string connectString, IDbCommand dbCommand)
        {
            SqliteConnection conn = new SqliteConnection(connectString);
            return ExecuteCommandReader(conn, true, (SqliteCommand)dbCommand);
        }

        private static IDataReader ExecuteCommandReader(SqliteConnection connection, bool isOwnedConnection, SqliteCommand command)
        {
            SqliteDataReader reader = null;

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                command.Connection = connection;

                if (isOwnedConnection)
                {
                    reader = command.ExecuteReader(CommandBehavior.CloseConnection);
                }
                else
                {
                    reader = command.ExecuteReader();
                }

                return reader;
            }
            catch
            {
                if (isOwnedConnection)
                {
                    connection.Close();
                }

                if (reader != null)
                {
                    reader.Close();
                }

                throw;
            }
        }

        #endregion

        #region Command Scalar

        public static object ExecuteCommandScalar(string connectString, IDbCommand dbCommand)
        {
            SqliteConnection conn = new SqliteConnection(connectString);
            return ExecuteCommandScalar(conn, true, (SqliteCommand)dbCommand);
        }

        public static object ExecuteCommandScalar(SqliteTransaction sqliteTransaction, IDbCommand dbCommand)
        {
            dbCommand.Transaction = sqliteTransaction;
            return ExecuteCommandScalar(sqliteTransaction.Connection, false, (SqliteCommand)dbCommand);
        }

        private static object ExecuteCommandScalar(SqliteConnection connection, bool isOwnedConnection, SqliteCommand command)
        {
            object rtObj = null;

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                command.Connection = connection;

                rtObj = command.ExecuteScalar();
            }
            finally
            {
                if (isOwnedConnection)
                {
                    connection.Close();
                }
            }

            return rtObj;
        }

        #endregion

        #region Command NonQuery

        public static int ExecuteCommandNonQuery(string connectString, IDbCommand dbCommand)
        {
            SqliteConnection conn = new SqliteConnection(connectString);

            return ExecuteCommandNonQuery(conn, true, (SqliteCommand)dbCommand);
        }

        public static int ExecuteCommandNonQuery(SqliteTransaction sqliteTransaction, IDbCommand dbCommand)
        {
            dbCommand.Transaction = sqliteTransaction;
            return ExecuteCommandNonQuery(sqliteTransaction.Connection, false, (SqliteCommand)dbCommand);
        }

        private static int ExecuteCommandNonQuery(SqliteConnection conn, bool isOwnedConnection, SqliteCommand command)
        {
            int rtInt = -1;

            try
            {
                if (conn.State != ConnectionState.Open)
                {
                    //TODO: 要用Polly来确保吗?
                    conn.Open();
                }

                command.Connection = conn;

                rtInt = command.ExecuteNonQuery();
            }
            //catch (Exception ex)  
            //{
            //    throw ex;
            //}
            finally
            {
                if (isOwnedConnection)
                {
                    conn.Close();
                }
            }

            return rtInt;
        }

        #endregion

        #region SQL

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public static int ExecuteSqlNonQuery(string connectionString, string sqlString)
        {
            SqliteConnection conn = new SqliteConnection(connectionString);

            using SqliteCommand command = new SqliteCommand
            {
                CommandType = CommandType.Text,
                CommandText = SQLiteUtility.SafeDbStatement(sqlString)
            };
            return ExecuteCommandNonQuery(conn, true, command);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public static int ExecuteSqlNonQuery(SqliteTransaction sqliteTransaction, string sqlString)
        {
            using SqliteCommand command = new SqliteCommand
            {
                CommandType = CommandType.Text,
                CommandText = SQLiteUtility.SafeDbStatement(sqlString),
                Transaction = sqliteTransaction
            };
            return ExecuteCommandNonQuery(sqliteTransaction.Connection, false, command);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
        public static Tuple<IDbCommand, IDataReader> ExecuteSqlReader(string connectionString, string sqlString)
        {
            SqliteConnection conn = new SqliteConnection(connectionString);

            SqliteCommand command = new SqliteCommand
            {
                CommandType = CommandType.Text,
                CommandText = SQLiteUtility.SafeDbStatement(sqlString)
            };

            return new Tuple<IDbCommand, IDataReader>(command, ExecuteCommandReader(conn, true, command));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
        public static Tuple<IDbCommand, IDataReader> ExecuteSqlReader(SqliteTransaction sqliteTransaction, string sqlString)
        {
            SqliteCommand command = new SqliteCommand
            {
                CommandType = CommandType.Text,
                CommandText = SQLiteUtility.SafeDbStatement(sqlString),
                Transaction = sqliteTransaction
            };

            return new Tuple<IDbCommand, IDataReader>(command, ExecuteCommandReader(sqliteTransaction.Connection, false, command));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public static object ExecuteSqlScalar(string connectionString, string sqlString)
        {
            SqliteConnection conn = new SqliteConnection(connectionString);

            using SqliteCommand command = new SqliteCommand
            {
                CommandType = CommandType.Text,
                CommandText = SQLiteUtility.SafeDbStatement(sqlString)
            };
            return ExecuteCommandScalar(conn, true, command);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public static object ExecuteSqlScalar(SqliteTransaction sqliteTransaction, string sqlString)
        {
            using SqliteCommand command = new SqliteCommand
            {
                CommandType = CommandType.Text,
                CommandText = SQLiteUtility.SafeDbStatement(sqlString),
                Transaction = sqliteTransaction
            };
            return ExecuteCommandScalar(sqliteTransaction.Connection, false, command);
        }

        #endregion
    }
}
