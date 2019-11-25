using System;
using System.Collections.Generic;

namespace HB.Framework.Database
{
    /// <summary>
    /// 存储在tb_sys_info的内部表
    /// </summary>
    public class SystemInfo
    {
        private const string _key_version = "Version";
        private const string _key_databaseName = "DatabaseName";

        private readonly IDictionary<string, string> _sysDict = new Dictionary<string, string>();

        public string DatabaseName
        {
            get
            {
                return _sysDict[_key_databaseName];
            }
            set
            {
                _sysDict[_key_databaseName] = value;
            }
        }

        public int Version
        {
            get
            {
                return Convert.ToInt32(_sysDict[_key_version], GlobalSettings.Culture);
            }
            set
            {
                _sysDict[_key_version] = value.ToString(GlobalSettings.Culture);
            }
        }

        public void Add(string name, string value)
        {
            _sysDict[name] = value;
        }
    }
}
