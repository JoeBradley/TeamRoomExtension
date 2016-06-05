using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeamRoomExtension.Models
{
    public class UserProfile
    {
        public Microsoft.VisualStudio.Services.WebApi.IdentityRef Identity { get; set; }
        public Uri ProjectCollectionUri { get; set; }
        public byte[] ProfileImage { get; set; }
        public Color ProfileColor { get; set; }

    }
}
