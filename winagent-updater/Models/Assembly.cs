using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace winagent_updater.Models
{
    class Assembly
    {
        #region Nested enum to identify the assembly type
        public enum AssemblyType
        {
            //
            // Summary:
            //     .exe file located in the root folder
            Executable = 0,
            //
            // Summary:
            //     .dll file located in the root folder
            Dependency = 1,
            //
            // Summary:
            //     .dll file located in the plugins folder
            Plugin = 2
        }
        #endregion

        public string Name { set; get; }

        public AssemblyType Type { set; get; } = AssemblyType.Plugin;

        public string Path
        {
            get
            {
                switch (Type)
                {
                    case AssemblyType.Executable:
                        return String.Format(@"{0}.{1}", Name, "exe");

                    case AssemblyType.Dependency:
                        return String.Format(@"{0}.{1}", Name, "dll");

                    case AssemblyType.Plugin:
                    default:
                        return String.Format(@"plugins\{0}.{1}", Name, "dll");
                }
            }
        }
    }
}
