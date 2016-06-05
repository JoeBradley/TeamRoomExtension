using Microsoft.TeamFoundation.Chat.WebApi;
using Microsoft.TeamFoundation.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeamRoomExtension.Models;
using TeamRoomExtension.ServiceHelpers;
using TeamRoomExtension.Workers;

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

        // Team Room Monitors.  Save so we can cancel them later if needed.
        List<TeamRoomMonitor> TeamRoomMonitors = new List<TeamRoomMonitor>();
        
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
            new Thread(() =>
            {
                // Get Team Project Collections
                LoadProjectCollections();
            }).Start();
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
            PollingComplete += new PollingCompleteEventHandler(control.TeamRoom_Changed);

            // Call Event Handlers with any existing data
            if (ProjectCollections.Any())
                control.RegisteredProjectCollection_Loaded(this, ProjectCollections);
            if (TeamRooms.Any())
                control.TeamRooms_Loaded(this, TeamRooms.Select(x => new TeamRoomEventArgs { ProjectCollectionUri = x.ProjectCollectionUri, TeamRoom = x.TeamRoom }).ToList());
            foreach (var tr in TeamRooms)
            {
                if (tr.Users.Any())
                    control.TeamRoomUsers_Changed(this, new TeamRoomEventArgs<User> { TeamRoom = tr.TeamRoom, ProjectCollectionUri = tr.ProjectCollectionUri, Data = tr.Users });
                if (tr.Messages.Any())
                    control.TeamRoomMessages_NewMessages(this, new TeamRoomEventArgs<Message> { TeamRoom = tr.TeamRoom, ProjectCollectionUri = tr.ProjectCollectionUri, Data = tr.Messages });
            }
        }

        public Message PostMessage(Uri projectCollectionUri, int teamRoomId, string message)
        {
            if (teamRoom == null || projectCollectionUri == null) return null;
            
                var msg = TfsServiceWrapper.PostMessage(projectCollectionUri, teamRoomId, message);
                //var msg = new Message() { Content = txtMessage.Text, PostedTime= DateTime.UtcNow };
                txtMessage.Text = "";
                Messages.Add(msg);

            TeamRoomMonitors.Single(x => x.ProjectCollectionUri == projectCollectionUri && x.TeamRoom.Id == teamRoomId).PollNow();
            return msg;
        }
        #endregion

        #region Private Methods

        private void LoadProjectCollections()
        {
            try
            {
                // Get Team Project Collections
                ProjectCollections.AddRange(TfsServiceWrapper.GetProjectCollections());
                NewTeamProjectCollections?.Invoke(this, ProjectCollections);
                
                // Get Project Collction Team Rooms, Create Monitor, Start Polling
                List<Task> tasks = ProjectCollections.Select(x => LoadTeamRoomAsync(x.Uri)).ToList();
                Task.WaitAll(tasks.ToArray());                
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private async Task LoadTeamRoomAsync(Uri projectCollectionUri)
        {
            var rooms = await TfsServiceWrapper.GetRoomsAsync(projectCollectionUri);

            foreach (var room in rooms)
            {
                // Crate new Team Room Monitor
                var trm = new TeamRoomMonitor(projectCollectionUri, room);

                // Add event handlers
                trm.NewMessages += new NewMessagesEventHandler(TeamRoomMessages_New);
                trm.UsersChanged += new UsersChangedEventHandler(TeamRoomUsers_Changed);
                trm.PollingComplete += new PollingCompleteEventHandler(TeamRoomPolling_Complete);

                // Start Polling
                trm.StartPolling();

                // Save reference
                TeamRooms.Add(new ProjectCollectionTeamRoom
                {
                    ProjectCollectionUri = projectCollectionUri,
                    TeamRoom = room,
                });
            }
        }

        private void TeamRoomMessages_New(object sender, TeamRoomEventArgs<Message> e)
        {
            var tr = TeamRooms.Single(x => x.ProjectCollectionUri == e.ProjectCollectionUri && x.TeamRoom == e.TeamRoom);
            tr.Messages.AddRange(e.Data);

            NewMessages?.Invoke(this, e);
        }

        private void TeamRoomUsers_Changed(object sender, TeamRoomEventArgs<User> e)
        {
            var tr = TeamRooms.Single(x => x.ProjectCollectionUri == e.ProjectCollectionUri && x.TeamRoom == e.TeamRoom);
            tr.Users.AddRange(e.Data);

            UsersChanged?.Invoke(this, e);
        }

        private void TeamRoomPolling_Complete(object sender, TeamRoomEventArgs e)
        {
            TeamRooms.Remove(TeamRooms.Single(x => x.ProjectCollectionUri == e.ProjectCollectionUri && x.TeamRoom == e.TeamRoom));
            PollingComplete?.Invoke(this, e);
        }

        #endregion
    }
}
