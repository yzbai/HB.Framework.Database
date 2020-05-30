using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using HB.Framework.Database;
using Microsoft.Data.Sqlite;


namespace HB.Infrastructure.SQLite
{
    /// <summary>
    /// 动态SQL和SP执行
    /// 具体执行步骤都要有异常捕捉，直接抛出给上一层
    /// </summary>
    internal static class SQLiteExecuter
    {
        #region Command Reader

        /// <summary>
        /// ExecuteCommandReaderAsync
        /// </summary>
        /// <param name="sqliteTransaction"></param>
        /// <param name="dbCommand"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException"></exception>
        public static Task<IDataReader> ExecuteCommandReaderAsync(SqliteTransaction sqliteTransaction, IDbCommand dbCommand)
        {
            dbCommand.Transaction = sqliteTransaction;
            return ExecuteCommandReaderAsync(sqliteTransaction.Connection, false, (SqliteCommand)dbCommand);
        }

        /// <summary>
        /// ExecuteCommandReaderAsync
        /// </summary>
        /// <param name="connectString"></param>
        /// <param name="dbCommand"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException"></exception>
        public static Task<IDataReader> ExecuteCommandReaderAsync(string connectString, IDbCommand dbCommand)
        {
            SqliteConnection conn = new SqliteConnection(connectString);
            return ExecuteCommandReaderAsync(conn, true, (SqliteCommand)dbCommand);
        }

        /// <summary>
        /// ExecuteCommandReaderAsync
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="isOwnedConnection"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException"></exception>
        private static async Task<IDataReader> ExecuteCommandReaderAsync(SqliteConnection connection, bool isOwnedConnection, SqliteCommand command)
        {
            SqliteDataReader? reader = null;

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                }

                command.Connection = connection;

                if (isOwnedConnection)
                {
                    reader = (SqliteDataReader)await command.ExecuteReaderAsync(CommandBehavior.CloseConnection).ConfigureAwait(false);
                }
                else
                {
                    reader = (SqliteDataReader)await command.ExecuteReaderAsync().ConfigureAwait(false);
                }

                return reader;
            }
            catch (Exception ex)
            {
                if (isOwnedConnection)
                {
                    connection.Close();
                }

                reader?.Close();

                if (ex is SqliteException sqliteException)
                {
                    throw new DatabaseException(DatabaseError.ExecuterError, nameof(ExecuteCommandReaderAsync), null, $"CommandText:{command.CommandText}", sqliteException);
                }
                else
                {
                    throw new DatabaseException(DatabaseError.Unkown, nameof(ExecuteCommandReaderAsync), null, $"CommandText:{command.CommandText}", ex);
                }
            }
        }

        #endregion

        #region Command Scalar

        /// <summary>
        /// ExecuteCommandScalarAsync
        /// </summary>
        /// <param name="connectString"></param>
        /// <param name="dbCommand"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException"></exception>
        public static Task<object> ExecuteCommandScalarAsync(string connectString, IDbCommand dbCommand)
        {
            using SqliteConnection conn = new SqliteConnection(connectString);
            return ExecuteCommandScalarAsync(conn, true, (SqliteCommand)dbCommand);
        }

        /// <summary>
        /// ExecuteCommandScalarAsync
        /// </summary>
        /// <param name="sqliteTransaction"></param>
        /// <param name="dbCommand"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException"></exception>
        public static Task<object> ExecuteCommandScalarAsync(SqliteTransaction sqliteTransaction, IDbCommand dbCommand)
        {
            dbCommand.Transaction = sqliteTransaction;
            return ExecuteCommandScalarAsync(sqliteTransaction.Connection, false, (SqliteCommand)dbCommand);
        }

        /// <summary>
        /// ExecuteCommandScalarAsync
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="isOwnedConnection"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException"></exception>
        private static async Task<object> ExecuteCommandScalarAsync(SqliteConnection connection, bool isOwnedConnection, SqliteCommand command)
        {
            object rtObj;

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                }

                command.Connection = connection;

                rtObj = await command.ExecuteScalarAsync().ConfigureAwait(false);
            }
            catch (SqliteException sqliteException)
            {
                throw new DatabaseException(DatabaseError.ExecuterError, nameof(ExecuteCommandScalarAsync), null, $"CommandText:{command.CommandText}", sqliteException);
            }
            catch (Exception ex)
            {
                throw new DatabaseException(DatabaseError.Unkown, nameof(ExecuteCommandScalarAsync), null, $"CommandText:{command.CommandText}", ex);
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

        #region Comand NonQuery

        /// <summary>
        /// ExecuteCommandNonQueryAsync
        /// </summary>
        /// <param name="connectString"></param>
        /// <param name="dbCommand"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException"></exception>
        public static Task<int> ExecuteCommandNonQueryAsync(string connectString, IDbCommand dbCommand)
        {
            using SqliteConnection conn = new SqliteConnection(connectString);

            return ExecuteCommandNonQueryAsync(conn, true, (SqliteCommand)dbCommand);
        }

        /// <summary>
        /// ExecuteCommandNonQueryAsync
        /// </summary>
        /// <param name="sqliteTransaction"></param>
        /// <param name="dbCommand"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException"></exception>
        public static Task<int> ExecuteCommandNonQueryAsync(SqliteTransaction sqliteTransaction, IDbCommand dbCommand)
        {
            dbCommand.Transaction = sqliteTransaction;
            return ExecuteCommandNonQueryAsync(sqliteTransaction.Connection, false, (SqliteCommand)dbCommand);
        }

        /// <summary>
        /// ExecuteCommandNonQueryAsync
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="isOwnedConnection"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException"></exception>
        private static async Task<int> ExecuteCommandNonQueryAsync(SqliteConnection conn, bool isOwnedConnection, SqliteCommand command)
        {
            int rtInt = -1;

            try
            {
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync().ConfigureAwait(false);
                }

                command.Connection = conn;

                rtInt = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            catch (SqliteException sqliteException)
            {
                throw new DatabaseException(DatabaseError.ExecuterError, nameof(ExecuteCommandNonQueryAsync), null, $"CommandText:{command.CommandText}", sqliteException);
            }
            catch (Exception ex)
            {
                throw new DatabaseException(DatabaseError.Unkown, nameof(ExecuteCommandNonQueryAsync), null, $"CommandText:{command.CommandText}", ex);
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
    }
}
