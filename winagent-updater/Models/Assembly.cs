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
        private static string path;

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
            private set
            {
                switch (Type)
                {
                    case AssemblyType.Executable:
                        path = String.Format(@"{0}.{1}", Name, "exe");
                        break;
                    case AssemblyType.Dependency:
                        path = String.Format(@"{0}.{1}", Name, "dll");
                        break;

                    case AssemblyType.Plugin:
                    default:
                        path = String.Format(@"plugins\{0}.{1}", Name, "dll");
                        break;
                }
            }

            get
            {
                return path;
            }
        }
    }
}
