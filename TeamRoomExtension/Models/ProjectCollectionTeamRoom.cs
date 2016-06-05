using Microsoft.TeamFoundation.Chat.WebApi;
using Microsoft.TeamFoundation.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeamRoomExtension.Models
{
    public class ProjectCollectionTeamRoom
    {
        public Uri ProjectCollectionUri { get; set; }
        public Room TeamRoom { get; set; }
        public List<User> Users = new List<User>();
        public List<Message> Messages = new List<Message>();
    }
}
