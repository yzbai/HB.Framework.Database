using System;
using System.Collections.Generic;
using System.Text;

namespace HB.Framework.Database
{
    /// <summary>
    /// 内部表tb_sys_info中的键值对
    /// </summary>
    public class SystemInfo
    {
        private readonly IDictionary<string, string> _sysDict = new Dictionary<string, string>();

        public string DatabaseName
        {
            get
            {
                return _sysDict[SystemInfoNames.DatabaseName];
            }
            set
            {
                _sysDict[SystemInfoNames.DatabaseName] = value;
            }
        }

        public int Version
        {
            get
            {
                return Convert.ToInt32(_sysDict[SystemInfoNames.Version], GlobalSettings.Culture);
            }
            set
            {
                _sysDict[SystemInfoNames.Version] = value.ToString(GlobalSettings.Culture);
            }
        }


        public SystemInfo()
        {

        }

        public void Add(string name, string value)
        {
            _sysDict[name] = value;
        }
    }
}
