using Microsoft.TeamFoundation.Chat.WebApi;
using Microsoft.TeamFoundation.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeamRoomExtension.Models;
using TeamRoomExtension.ServiceHelpers;

namespace TeamRoomExtension.Helpers
{
    // Event Hanlder Delegates
    public delegate void NewTeamProjectCollectionsEventHandler(object sender, List<RegisteredProjectCollection> e);
    public delegate void NewRoomsEventHandler(object sender, List<TeamRoomEventArgs> e);
    public delegate void NewMessagesEventHandler(object sender, TeamRoomEventArgs<Message> e);
    public delegate void UsersChangedEventHandler(object sender, TeamRoomEventArgs<User> e);
    public delegate void PollingCompleteEventHandler(object sender, TeamRoomEventArgs e);

    /// <summary>
    /// Lazy Loaded Singleton
    /// </summary>
    public class TeamProjectCollectionManager
    {
        #region Private Variables

        // Registered TeamRoomWindow controls
        List<TeamRoomWindowControl> Controls = new List<TeamRoomWindowControl>();

        // Maintained list of all Project Collections, Team Rooms and Messages
        List<RegisteredProjectCollection> ProjectCollections = new List<RegisteredProjectCollection>();
        List<ProjectCollectionTeamRoom> TeamRooms = new List<ProjectCollectionTeamRoom>();

        #endregion

        #region Events

        // Raised when new Team Project Colelctions found
        public event NewTeamProjectCollectionsEventHandler NewTeamProjectCollections;

        // Raised when new TeamnRoom found
        public event NewRoomsEventHandler NewRooms;

        // New Messages or changes to Room Users
        public event NewMessagesEventHandler NewMessages;
        public event UsersChangedEventHandler UsersChanged;

        // Raised when both polling workers are complete (i.e. cancelled)
        public event PollingCompleteEventHandler PollingComplete;

        #endregion

        #region constructors

        private static readonly Lazy<TeamProjectCollectionManager> lazy = new Lazy<TeamProjectCollectionManager>(() => new TeamProjectCollectionManager());

        public static TeamProjectCollectionManager Instance { get { return lazy.Value; } }

        private TeamProjectCollectionManager()
        {
            // Get Team Project Collections
            // Get Team Rooms
        }

        #endregion

        #region Public Methods

        public void RegisterTeamRoomWindow(TeamRoomWindowControl control)
        {
            // Register Control Event Handlers
            NewTeamProjectCollections += new NewTeamProjectCollectionsEventHandler(control.RegisteredProjectCollection_Loaded);
            NewRooms += new NewRoomsEventHandler(control.TeamRooms_Loaded);
            UsersChanged += new UsersChangedEventHandler(control.TeamRoomUsers_Changed);
            NewMessages += new NewMessagesEventHandler(control.TeamRoomMessages_NewMessages);
            PollingComplete += new PollingCompleteEventHandler( control.TeamRoom_Changed);

            // Call Event Handlers with any existing data
            if (ProjectCollections.Any())
                control.RegisteredProjectCollection_Loaded(this, ProjectCollections);
            if (TeamRooms.Any())
                control.TeamRooms_Loaded(this, TeamRooms.Select(x => new TeamRoomEventArgs { ProjectCollectionUri = x.ProjectCollectionUri, TeamRoom = x.TeamRoom } ).ToList());
            foreach(var tr in TeamRooms)
            {
                if (tr.Users.Any())
                    control.TeamRoomUsers_Changed(this, new TeamRoomEventArgs<User> { TeamRoom = tr.TeamRoom, ProjectCollectionUri = tr.ProjectCollectionUri, Data = tr.Users });
                if (tr.Messages.Any())
                    control.TeamRoomMessages_NewMessages(this, new TeamRoomEventArgs<Message> { TeamRoom = tr.TeamRoom, ProjectCollectionUri = tr.ProjectCollectionUri, Data = tr.Messages });                
            }
        }

        #endregion

        #region Private Methods

        private void LoadProjectCollections()
        {
            try
            {
                foreach (var pc in TfsServiceWrapper.GetProjectCollections())
                {
                    ProjectCollections.Add(pc);
                }
                if (NewTeamProjectCollections != null)
                    NewTeamProjectCollections(this, ProjectCollections);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        #endregion
    }
}
