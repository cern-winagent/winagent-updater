using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winagent.Updater.Models
{
    interface IAsset
    {
        string Filename { get; set; }

        string Url { get; set; }
    }
}
