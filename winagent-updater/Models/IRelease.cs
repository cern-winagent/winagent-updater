using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winagent.Updater.Models
{
    interface IRelease
    {
        string Version { get; set; }

        List<IAsset> Files { get; }
    }
}
