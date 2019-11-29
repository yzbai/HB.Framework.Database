using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using System.Data;
using HB.Framework.Database;
using System.Linq;

namespace HB.Infrastructure.MySQL
{
    /// <summary>
    /// 动态SQL和SP执行
    /// 具体执行步骤都要有异常捕捉，直接抛出给上一层
    /// </summary>
    internal static partial class MySQLExecuter
    {
        #region Comand Reader

        public static IDataReader ExecuteCommandReader(MySqlTransaction mySqlTransaction, IDbCommand dbCommand)
        {
            dbCommand.Transaction = mySqlTransaction;
            return ExecuteCommandReader(mySqlTransaction.Connection, false, (MySqlCommand)dbCommand);
        }

        public static IDataReader ExecuteCommandReader(string connectString, IDbCommand dbCommand)
        {
            MySqlConnection conn = new MySqlConnection(connectString);
            return ExecuteCommandReader(conn, true, (MySqlCommand)dbCommand);
        }

        private static IDataReader ExecuteCommandReader(MySqlConnection connection, bool isOwnedConnection, MySqlCommand command)
        {
            MySqlDataReader reader = null;

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
            catch(Exception ex)
            {
                if (isOwnedConnection)
                {
                    connection.Close();
                }
                if (reader != null)
                {
                    reader.Close();
                }

                if (ex is MySqlException mySqlException)
                {
                    throw new DatabaseException(mySqlException.Number, mySqlException.SqlState, mySqlException.Message, mySqlException);
                }
                else
                {
                    throw new DatabaseException(DatabaseError.InnerError, "ExecuteCommandReader", connection.Database, $"CommandText:{command.CommandText}");
                }
            }
        }

        #endregion

        #region Command Scalar

        public static object ExecuteCommandScalar(string connectString, IDbCommand dbCommand)
        {
            MySqlConnection conn = new MySqlConnection(connectString);
            return ExecuteCommandScalar(conn, true, (MySqlCommand)dbCommand);
        }

        public static object ExecuteCommandScalar(MySqlTransaction mySqlTransaction, IDbCommand dbCommand)
        {
            dbCommand.Transaction = mySqlTransaction;
            return ExecuteCommandScalar(mySqlTransaction.Connection, false, (MySqlCommand)dbCommand);
        }

        private static object ExecuteCommandScalar(MySqlConnection connection, bool isOwnedConnection, MySqlCommand command)
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
            catch(Exception ex)
            {
                if (ex is MySqlException mySqlException)
                {
                    throw new DatabaseException(mySqlException.Number, mySqlException.SqlState, mySqlException.Message, mySqlException);
                }
                else
                {
                    throw new DatabaseException(DatabaseError.InnerError, "ExecuteCommandScalar", connection.Database, $"CommandText:{command.CommandText}");
                }
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
            MySqlConnection conn = new MySqlConnection(connectString);

            return ExecuteCommandNonQuery(conn, true, (MySqlCommand)dbCommand);
        }

        public static int ExecuteCommandNonQuery(MySqlTransaction mySqlTransaction, IDbCommand dbCommand)
        {
            dbCommand.Transaction = mySqlTransaction;
            return ExecuteCommandNonQuery(mySqlTransaction.Connection, false, (MySqlCommand)dbCommand);
        }

        private static int ExecuteCommandNonQuery(MySqlConnection conn, bool isOwnedConnection, MySqlCommand command)
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
            catch(Exception ex)
            {
                if (ex is MySqlException mySqlException)
                {
                    throw new DatabaseException(mySqlException.Number, mySqlException.SqlState, mySqlException.Message, mySqlException);
                }
                else
                {
                    throw new DatabaseException(DatabaseError.InnerError, "ExecuteCommandNonQuery", conn.Database, $"CommandText:{command.CommandText}");
                }
            }
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

        #region SP NonQuery

        #region private utility methods & constructors

        private static void AttachParameters(MySqlCommand command, IEnumerable<IDataParameter> commandParameters)
        {
            foreach (IDataParameter p in commandParameters)
            {
                //check for derived output value with no value assigned
                if ((p.Direction == ParameterDirection.InputOutput) && (p.Value == null))
                {
                    p.Value = DBNull.Value;
                }

                command.Parameters.Add(p);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        private static void PrepareCommand(MySqlCommand command, MySqlConnection connection, MySqlTransaction transaction,
            CommandType commandType, string commandText, IEnumerable<IDataParameter> commandParameters)
        {
            //if the provided connection is not open, we will open it
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            //associate the connection with the command
            command.Connection = connection;

            //if we were provided a transaction, assign it.
            if (transaction != null)
            {
                command.Transaction = transaction;
            }

            //set the command type
            command.CommandType = commandType;

            //attach the command parameters if they are provided
            if (commandParameters != null)
            {
                AttachParameters(command, commandParameters);
            }

            //set the command text (stored procedure name or SQL statement)
            command.CommandText = commandText;

            return;
        }

        #endregion

        public static int ExecuteSPNonQuery(string connectString, string spName, IList<IDataParameter> parameters)
        {
            MySqlConnection conn = new MySqlConnection(connectString);
            return ExecuteSPNonQuery(conn, null, true, spName, parameters);
        }

        public static int ExecuteSPNonQuery(MySqlTransaction mySqlTransaction, string spName, IList<IDataParameter> parameters)
        {
            return ExecuteSPNonQuery(mySqlTransaction.Connection, mySqlTransaction, false, spName, parameters);
        }

        private static int ExecuteSPNonQuery(MySqlConnection conn, MySqlTransaction trans, bool isOwnedConnection, string spName, IList<IDataParameter> parameters)
        {
            int rtInt = -1;
            MySqlCommand command = new MySqlCommand();

            PrepareCommand(command, conn, trans, CommandType.StoredProcedure, spName, parameters);

            try
            {
                rtInt = command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                if (ex is MySqlException mySqlException)
                {
                    throw new DatabaseException(mySqlException.Number, mySqlException.SqlState, mySqlException.Message, mySqlException);
                }
                else
                {
                    throw new DatabaseException(DatabaseError.InnerError, "ExecuteSPNonQuery", conn.Database, $"CommandText:{command.CommandText}");
                }
            }
            finally
            {
                if (isOwnedConnection)
                {
                    conn.Close();
                }

                command.Parameters.Clear();
                command.Dispose();
            }


            return rtInt;
        }

        #endregion

        #region SP Scalar

        public static object ExecuteSPScalar(string connectString, string spName, IList<IDataParameter> parameters)
        {
            MySqlConnection conn = new MySqlConnection(connectString);
            return ExecuteSPScalar(conn, null, true, spName, parameters);
        }

        public static object ExecuteSPScalar(MySqlTransaction mySqlTransaction, string spName, IList<IDataParameter> parameters)
        {
            return ExecuteSPScalar(mySqlTransaction.Connection, mySqlTransaction, false, spName, parameters);
        }

        private static object ExecuteSPScalar(MySqlConnection conn, MySqlTransaction trans, bool isOwnedConnection, string spName, IList<IDataParameter> parameters)
        {
            object rtObj = null;
            MySqlCommand command = new MySqlCommand();

            PrepareCommand(command, conn, trans, CommandType.StoredProcedure, spName, parameters);

            try
            {
                rtObj = command.ExecuteScalar();
            }
            catch (Exception ex)
            {
                if (ex is MySqlException mySqlException)
                {
                    throw new DatabaseException(mySqlException.Number, mySqlException.SqlState, mySqlException.Message, mySqlException);
                }
                else
                {
                    throw new DatabaseException(DatabaseError.InnerError, "ExecuteSPScalar", conn.Database, $"CommandText:{command.CommandText}");
                }
            }
            finally
            {
                if (isOwnedConnection)
                {
                    conn.Close();
                }

                command.Parameters.Clear();
                command.Dispose();
            }

            return rtObj;
        }

        #endregion

        #region SP Reader

        public static Tuple<IDbCommand,IDataReader> ExecuteSPReader(string connectString, string spName, IList<IDataParameter> dbParameters)
        {
            MySqlConnection conn = new MySqlConnection(connectString);
            conn.Open();

            return ExecuteSPReader(conn, null, true, spName, dbParameters);
        }

        public static Tuple<IDbCommand, IDataReader> ExecuteSPReader(MySqlTransaction mySqlTransaction, string spName, IList<IDataParameter> dbParameters)
        {
            return ExecuteSPReader(mySqlTransaction.Connection, mySqlTransaction, false, spName, dbParameters);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
        private static Tuple<IDbCommand, IDataReader> ExecuteSPReader(MySqlConnection connection, MySqlTransaction mySqlTransaction, bool isOwedConnection, string spName, IList<IDataParameter> dbParameters)
        {
            MySqlCommand command = new MySqlCommand();

            PrepareCommand(command, connection, mySqlTransaction, CommandType.StoredProcedure, spName, dbParameters);
            MySqlDataReader reader = null;

            try
            {
                if (isOwedConnection)
                {
                    reader = command.ExecuteReader(CommandBehavior.CloseConnection);
                }
                else
                {
                    reader = command.ExecuteReader();
                }
            }
            catch(Exception ex)
            {
                if (isOwedConnection)
                {
                    connection.Close();
                }

                if (reader != null)
                {
                    reader.Close();
                }

                if (ex is MySqlException mySqlException)
                {
                    throw new DatabaseException(mySqlException.Number, mySqlException.SqlState, mySqlException.Message, mySqlException);
                }
                else
                {
                    throw new DatabaseException(DatabaseError.InnerError, "ExecuteSPReader", connection.Database, $"CommandText:{command.CommandText}");
                }
            }

            command.Parameters.Clear();

            return new Tuple<IDbCommand, IDataReader>(command,reader);
        }

        #endregion

        #region SQL

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public static int ExecuteSqlNonQuery(string connectionString, string sqlString)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);

            using MySqlCommand command = new MySqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = MySQLLocalism.SafeDbStatement(sqlString)
            };
            return ExecuteCommandNonQuery(conn, true, command);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public static int ExecuteSqlNonQuery(MySqlTransaction mySqlTransaction, string sqlString)
        {
            using MySqlCommand command = new MySqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = MySQLLocalism.SafeDbStatement(sqlString),
                Transaction = mySqlTransaction
            };
            return ExecuteCommandNonQuery(mySqlTransaction.Connection, false, command);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
        public static Tuple<IDbCommand, IDataReader> ExecuteSqlReader(string connectionString, string sqlString)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);

            MySqlCommand command = new MySqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = MySQLLocalism.SafeDbStatement(sqlString)
            };

            return new Tuple<IDbCommand, IDataReader>(command, ExecuteCommandReader(conn, true, command));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
        public static Tuple<IDbCommand,IDataReader> ExecuteSqlReader(MySqlTransaction mySqlTransaction, string sqlString)
        {
            MySqlCommand command = new MySqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = MySQLLocalism.SafeDbStatement(sqlString),
                Transaction = mySqlTransaction
            };
            return new Tuple<IDbCommand, IDataReader>(command, ExecuteCommandReader(mySqlTransaction.Connection, false, command));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public static object ExecuteSqlScalar(string connectionString, string sqlString)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);

            using MySqlCommand command = new MySqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = MySQLLocalism.SafeDbStatement(sqlString)
            };
            return ExecuteCommandScalar(conn, true, command);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public static object ExecuteSqlScalar(MySqlTransaction mySqlTransaction, string sqlString)
        {
            using MySqlCommand command = new MySqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = MySQLLocalism.SafeDbStatement(sqlString),
                Transaction = mySqlTransaction
            };
            return ExecuteCommandScalar(mySqlTransaction.Connection, false, command);
        }

        #endregion

        //#region SqlDataTable

        //public static DataTable ExecuteSqlDataTable(string connectString, string sqlString)
        //{
        //    MySqlConnection conn = new MySqlConnection(connectString);
        //    return ExecuteSqlDataTable(conn, sqlString, true);
        //}

        //public static DataTable ExecuteSqlDataTable(MySqlTransaction mySqlTransaction, string sqlString)
        //{
        //    if (mySqlTransaction == null)
        //    {
        //        throw new ArgumentNullException(nameof(mySqlTransaction), "ExecuteSqlReader方法不接收NULL参数");
        //    }

        //    return ExecuteSqlDataTable(mySqlTransaction.Connection, sqlString, false);
        //}

        //private static DataTable ExecuteSqlDataTable(MySqlConnection connection, string sqlString, bool isOwndConnection)
        //{

        //    throw new NotImplementedException();

        //    //DataTable table = new DataTable();

        //    //try
        //    //{
        //    //    if (connection.State != ConnectionState.Open)
        //    //    {
        //    //        connection.Open();
        //    //    }

        //    //    using (MySqlCommand command = connection.CreateCommand())
        //    //    {
        //    //        command.CommandText = sqlString;
        //    //        command.CommandType = CommandType.Text;

        //    //        using (MySqlDataAdapter adapter = new MySqlDataAdapter(command))
        //    //        {
        //    //            adapter.Fill(table);
        //    //        }
        //    //    }

        //    //    return table;
        //    //}
        //    //catch (Exception ex)
        //    //{
        //    //    throw ex;
        //    //}
        //    //finally
        //    //{
        //    //    if (isOwndConnection)
        //    //    {
        //    //        connection.Close();
        //    //    }
        //    //}
        //}

        //#endregion

    }
}
