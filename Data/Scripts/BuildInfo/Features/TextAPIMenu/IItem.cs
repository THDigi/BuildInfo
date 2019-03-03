using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Digi.BuildInfo.Features.TextAPIMenu
{
    public interface IItem
    {
        bool Interactable { get; set; }

        void UpdateTitle();
    }
}
