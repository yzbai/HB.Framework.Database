using System;
using System.Collections.Generic;
using System.Text;

namespace HB.Framework.Database
{
    public static class SystemInfoNames
    {
        public const string Version = "Version";
        public const string DatabaseName = "DatabaseName";
    }
    public class SystemInfo
    {
        public string DatabaseName {
            get {
                return _sysDict[SystemInfoNames.DatabaseName];
            }
            set {
                _sysDict[SystemInfoNames.DatabaseName] = value;
            }
        }

        public int Version {
            get {
                return Convert.ToInt32(_sysDict[SystemInfoNames.Version], GlobalSettings.Culture);
            }
            set {
                _sysDict[SystemInfoNames.Version] = value.ToString(GlobalSettings.Culture);
            }
        }

        private IDictionary<string, string> _sysDict = new Dictionary<string, string>();

        public SystemInfo()
        {

        }

        public void Add(string name, string value)
        {
            _sysDict[name] = value;
        }
    }
}
