using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winagent.Updater.Models
{
    public class Assembly
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

        public Assembly (string name, AssemblyType type)
        {
            Name = name;
            Type = type;

            switch (Type)
            {
                case AssemblyType.Executable:
                    Path = String.Format(@".\{0}.{1}", Name, "exe");
                    break;
                case AssemblyType.Dependency:
                    Path = String.Format(@".\{0}.{1}", Name, "dll");
                    break;

                case AssemblyType.Plugin:
                default:
                    Path = String.Format(@".\plugins\{0}.{1}", Name, "dll");
                    break;
            }
        }

        public string Name { set; get; }

        public AssemblyType Type { set; get; }

        public string Path { get; }

        public override bool Equals(object obj)
        {
            return ((Assembly) obj).Name == this.Name;
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }
    }
}
