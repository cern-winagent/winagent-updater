using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace winagent_updater.Models
{
    interface IAsset
    {
        string Filename { get; set; }

        string Url { get; set; }
    }
}
