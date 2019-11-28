namespace HB.Framework.Database
{

    public enum DatabaseError
    {
        /// <summary>
        /// 其他错误
        /// </summary>
        Failed = 0,

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
        ScalarReturnMore = 4,

        ArgumentNotValid = 5,
    }
}
