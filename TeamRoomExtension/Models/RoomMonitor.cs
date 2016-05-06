using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeamRoomExtension.Models
{
    public class RoomMonitor
    {
        public Uri projectCollectionUri { get; set; }
        public int roomId { get; set; }

        BackgroundWorker UsersMonitor;
        BackgroundWorker MessagesMonitor;


        public RoomMonitor(Uri projectCollectionUri, int roomId)
        {
            this.projectCollectionUri = projectCollectionUri;
            this.roomId = roomId;
        }
    }
}
