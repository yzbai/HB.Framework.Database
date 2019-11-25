using System;
using System.Collections.Generic;
using System.Text;

namespace HB.Framework.Database
{
    /// <summary>
    /// 数据库参数设置
    /// </summary>
    public class DatabaseSettings
    {
        /// <summary>
        /// 数据库数据结构版本，用来检测升级
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// 默认varchar长度
        /// </summary>
        public int DefaultVarcharLength { get; set; } = 200;

        /// <summary>
        /// 包含哪些实体，即哪些表
        /// </summary>
        public IList<EntityInfo> EntityInfos { get; } = new List<EntityInfo>();

        /// <summary>
        /// 是否自动创建表
        /// </summary>
        public bool AutomaticCreateTable { get; set; } = true;

        /// <summary>
        /// 实体放在哪些程序集里
        /// </summary>
        public IList<string> AssembliesIncludeEntity { get; } = new List<string>();
    }
}
