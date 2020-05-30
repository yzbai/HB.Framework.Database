namespace HB.Framework.Database
{

    public enum DatabaseError
    {
        Unkown = 0,

        /// <summary>
        /// 成功
        /// </summary>
        Succeeded = 1,

        /// <summary>
        /// 错误：没有找到
        /// </summary>
        NotFound = 2,

        /// <summary>
        /// 错误：不可写
        /// </summary>
        NotWriteable = 3,

        /// <summary>
        /// 错误：Scalar查询，返回多个
        /// </summary>
        FoundTooMuch = 4,

        ArgumentNotValid = 5,
        NotMatch = 6,

        TransactionError = 7,
        TableCreateError = 8,
        MigrateError = 9,
        NotATableModel = 10,
        TransactionConnectionIsNull = 11,

        //各种数据库的实现Executor出错
        ExecuterError = 12,
        DefaultDatabaseError = 13,
    }
}
