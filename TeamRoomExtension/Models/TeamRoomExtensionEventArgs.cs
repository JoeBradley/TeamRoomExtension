using Microsoft.TeamFoundation.Chat.WebApi;
using Microsoft.TeamFoundation.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeamRoomExtension.Models
{
    public class TeamRoomEventArgs : EventArgs
    {
        public Uri ProjectCollectionUri { get; set; }
        public Room TeamRoom { get; set; }
    }

    public class TeamRoomEventArgs<T> : TeamRoomEventArgs
    {
        public List<T> Data { get; set; }
    }

    public class TeamRoomChangedEventArgs : TeamRoomEventArgs {
        public bool IsRemoved = false;
    }

    public class ProjectCollectionEventArgs : EventArgs
    {
        public Uri ProjectCollectionUri { get; set; }
        public List<Room> TeamRooms { get; set; }
    }

    public class ProjectCollectionChangedEventArgs : EventArgs
    {
        public RegisteredProjectCollection ProjectCollectionUri { get; set; }
        public bool IsRemoved = false;
    }

}
