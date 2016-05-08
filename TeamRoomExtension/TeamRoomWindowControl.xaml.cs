//------------------------------------------------------------------------------
// <copyright file="TeamRoomWindowControl.xaml.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace TeamRoomExtension
{
    using System.Linq;
    using System.Diagnostics;
    using ServiceHelpers;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Web;
    using System.Windows;
    using System.Windows.Controls;
    //using Microsoft.TeamFoundation.Chat.WebApi;
    //using Microsoft.TeamFoundation.Client;

    using Microsoft.VisualStudio.Services.WebApi;

    // https://www.nuget.org/packages/Microsoft.TeamFoundationServer.Client/
    using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
    using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

    // https://www.nuget.org/packages/Microsoft.VisualStudio.Services.InteractiveClient/
    using Microsoft.VisualStudio.Services.Client;

    // https://www.nuget.org/packages/Microsoft.VisualStudio.Services.Client/
    using Microsoft.VisualStudio.Services.Common;

    // https://www.nuget.org/packages/Microsoft.TeamFoundationServer.ExtendedClient/
    using Microsoft.TeamFoundation.Client;
    using Microsoft.TeamFoundation.Chat.WebApi;
    using Microsoft.TeamFoundation.VersionControl.Client;

    // https://www.nuget.org/packages/Microsoft.TeamFoundationServer.Client/
    using Microsoft.TeamFoundation.SourceControl.WebApi;
    using Microsoft.TeamFoundation.Framework.Client;
    using Microsoft.TeamFoundation.Framework.Common;
    using System.Windows.Media.Imaging;
    using Microsoft.VisualStudio.Shell.Interop;
    /// <summary>
    /// Interaction logic for TeamRoomWindowControl.
    /// </summary>
    public partial class TeamRoomWindowControl : UserControl, INotifyPropertyChanged
    {
        #region Private properties

        // Team Room Id loaded from saved User profile.  Should be used on first load to automatically select the last viewed team room.
        int savedteamRoomId = 0;

        // Currently Selected Team Room
        Room teamRoom;

        // Get the URl for the currently selected Team Room
        Uri teamRoomUri
        {
            get
            {
                try
                {
                    if (projectCollectionUri == null || teamRoom == null) return null;

                    return new Uri(string.Format("{0}/_rooms?name={1}&_a=today", projectCollectionUri, HttpUtility.UrlEncode(teamRoom.Name)));
                }
                catch (Exception)
                {
                    return null;
                }

            }
        }

        // Currently selected Project Collection Uri
        Uri projectCollectionUri;

        // Does the window have focus?
        bool StatusSet = false;

        // User Project Collections
        ObservableCollection<RegisteredProjectCollection> collections = new ObservableCollection<RegisteredProjectCollection>();
        public ObservableCollection<RegisteredProjectCollection> Collections
        {
            get { return collections; }
            set { collections = value; OnPropertyChanged("Collections"); }
        }

        // Current Project Team Rooms.  This is cleared and re-loaded whenever a different Team Project is selected.
        ObservableCollection<Room> rooms = new ObservableCollection<Room>();
        public ObservableCollection<Room> Rooms
        {
            get { return rooms; }
            set { rooms = value; OnPropertyChanged("Rooms"); }
        }

        // Current Team Rooms Users.  This is cleared and re-loaded whenever a different Team Room is selected.
        ObservableCollection<User> roomUsers = new ObservableCollection<User>();
        public ObservableCollection<User> RoomUsers
        {
            get { return roomUsers; }
            set { roomUsers = value; OnPropertyChanged("RoomUsers"); }
        }

        // Current Team Room messages.  This is cleaered and re-loaded whenever a different Team Room is selected.
        ObservableCollection<Message> messages = new ObservableCollection<Message>();
        public ObservableCollection<Message> Messages
        {
            get { return messages; }
            set
            {
                messages = value;
                OnPropertyChanged("Messages");
            }
        }

        // Property CHanged event handler.  Needed for ObservableCollections (??? - still not sure how they work)
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="TeamRoomWindowControl"/> class.
        /// </summary>
        public TeamRoomWindowControl()
        {
            this.InitializeComponent();

            // Add event delegates to background workers
            TfsMonitor.Instance.ReportProgress += MessagesWatcher_NewMessages;
            TfsMonitor.Instance.ReportComplete += MessagesWatcher_Complete;

            // Set the event handler directly on the background worker
            RoomWorker.Instance.LoadRoomsWorker.RunWorkerCompleted += Rooms_Loaded;
            
            UserWorker.Instance.Worker.RunWorkerCompleted += ProfilePictures_Loaded;

            LoadProjectCollections();
        }

        private void LoadProjectCollections()
        {
            try
            {
                foreach (var pc in TfsServiceWrapper.GetProjectCollections())
                {
                    Collections.Add(pc);
                }
                var settings = TeamRoomWindowCommand.Instance.LoadUserSettings();

                if (settings.ProjectCollectionUri != null &&
                    Collections.Select(x => x.Uri).Contains(settings.ProjectCollectionUri))
                {
                    projectCollectionUri = settings.ProjectCollectionUri;
                    savedteamRoomId = settings.TeamRoomId;

                    if (projectCollectionUri != null && Collections.Select(x => x.Uri).Contains(projectCollectionUri))
                    {
                        foreach (RegisteredProjectCollection item in cmbCollectionList.Items)
                        {
                            if (item.Uri == projectCollectionUri)
                            {
                                cmbCollectionList.SelectedIndex = cmbCollectionList.Items.IndexOf(item);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        // Post a new message.  Set the MessagesWacther timeout periof to 0 so its starts polling at minimal intervals again.
        private void PostMessage()
        {
            try
            {
                if (txtMessage.Text.Trim(' ') == "") return;

                if (teamRoom != null && projectCollectionUri != null)
                {
                    var msg = TfsServiceWrapper.PostMessage(projectCollectionUri, teamRoom.Id, txtMessage.Text);
                    //var msg = new Message() { Content = txtMessage.Text, PostedTime= DateTime.UtcNow };
                    txtMessage.Text = "";
                    Messages.Add(msg);
                    TfsMonitor.Instance.PollNow();
                }
            }
            catch (Exception ex)
            {
            }
        }

        // Call the Worker thread to load the list of rooms for the connected project service uri
        private void LoadRooms()
        {
            try
            {
                TeamRoomWindowCommand.Instance.LogMessage("Loading Rooms");

                RoomUsers.Clear();
                teamRoom = null;
                Rooms.Clear();
                cmbRoomList.Text = "Loading Team Rooms...";
                if (projectCollectionUri != null)
                {
                    RoomWorker.Instance.LoadRooms(projectCollectionUri);
                }
            }
            catch (Exception ex){

                throw;
            }
        }

        #region Events

        // Cancel Background worker threads    
        private void TeamRoomWindow_Unload(object sender, RoutedEventArgs e)
        {
            teamRoom = null;
            TfsMonitor.Instance.Cancel();
        }

        private void cmbCollectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {           
                Messages.Clear();
                RefreshMessages();
                RoomUsers.Clear();
                RefreshRoomUsers();
                
                var projectCollection = cmbCollectionList.SelectedValue as RegisteredProjectCollection;
                if (projectCollection != null) projectCollectionUri = projectCollection.Uri;
                LoadRooms();
                TeamRoomWindowCommand.Instance.SaveUserSettings(new ExtensionSettings { TeamRoomId = teamRoom != null ? teamRoom.Id : 0, ProjectCollectionUri = projectCollection.Uri });
            }
            catch (Exception)
            {
                throw;
            }
        }

        // Set the background worker to poll for messages for the selected room.
        private void cmbRoomList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Clear messages and Room Users
                messages.Clear();
                RefreshMessages();

                RoomUsers.Clear();
                RefreshRoomUsers();

                int oldRoomId = teamRoom == null ? 0 : teamRoom.Id;

                if (cmbRoomList.SelectedValue == null || projectCollectionUri == null)
                    return;

                teamRoom = cmbRoomList.SelectedValue as Room;
                if (teamRoom != null)
                {
                    SignIntoRoom(oldRoomId, teamRoom.Id);

                    TfsMonitor.Instance.DoWork(projectCollectionUri, teamRoom.Id);
                    RoomWorker.Instance.PollRoomUsers(projectCollectionUri, teamRoom.Id, PollRoomUsers_NewUsers, PollRoomUsers_Complete);

                    var settings = new ExtensionSettings { ProjectCollectionUri = projectCollectionUri, TeamRoomId = teamRoom.Id };

                    TeamRoomWindowCommand.Instance.SaveUserSettings(settings);
                }
            }
            catch (Exception ex)
            {
                // Log errors
                TeamRoomWindowCommand.Instance.LogError(ex);
            }
        }

        // Post the message text
        private void btnPostMessage_Click(object sender, RoutedEventArgs e)
        {
            PostMessage();
        }

        // Post the message text
        private void txtMessage_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;

            e.Handled = true;
            PostMessage();
        }

        // Open the Team Room in a web browser.
        private void imgRoomIcon_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (teamRoomUri == null) return;

            Process.Start(new ProcessStartInfo(teamRoomUri.AbsoluteUri));
            e.Handled = true;
        }

        #endregion

        #region External Events

        private void MessagesWatcher_NewMessages(object sender, TeamRoomMessages e)
        {
            if (e.Messages == null || !e.Messages.Any() ||
                e.RoomId != teamRoom.Id || e.ConnectionUri != projectCollectionUri)
            {
                return;
            }

            int messagesAdded = 0;

            foreach (var item in e.Messages)
            {
                if (!Messages.Select(x => x.Id).Contains(item.Id))
                {
                    Messages.Add(item);
                    messagesAdded++;
                }
            }

            svMessages.ScrollToEnd();
            if (messagesAdded > 0)
            {
                SetStatusMessage(messagesAdded);
            }
        }

        private void MessagesWatcher_Complete(object sender, MessageWorkerCompleteResult e)
        {
            if (projectCollectionUri != null && teamRoom != null)
                TfsMonitor.Instance.DoWork(projectCollectionUri, teamRoom.Id);
        }

        private void Rooms_Loaded(object sender, RunWorkerCompletedEventArgs e)
        {
            // work complete           
            if (e.Cancelled == true)
            {
                Console.WriteLine("Worker canceled");
            }
            else if (e.Error != null)
            {
                Console.WriteLine("Worker error", e.Error.Message);
            }
            else
            {
                if (e.Result is IEnumerable<Room>)
                {
                    LoadRooms(e.Result as IEnumerable<Room>);
                }
                else
                {
                    //Log error
                }
            }
        }

        private void PollRoomUsers_NewUsers(object sender, ProgressChangedEventArgs e)
        {
            if (!(e.UserState is TeamRoomUsers)) return;

            TeamRoomUsers users = e.UserState as TeamRoomUsers;
            LoadRoomUsers(users);
        }

        private void PollRoomUsers_Complete(object sender, RunWorkerCompletedEventArgs e)
        {
            if (projectCollectionUri != null && teamRoom != null)
                RoomWorker.Instance.PollRoomUsers(projectCollectionUri, teamRoom.Id, PollRoomUsers_NewUsers, PollRoomUsers_Complete);
        }

        private void ProfilePictures_Loaded(object sender, RunWorkerCompletedEventArgs e)
        {
            // work complete           
            if (e.Cancelled == true)
            {
                Console.WriteLine("Worker canceled");
            }
            else if (e.Error != null)
            {
                Console.WriteLine("Worker error", e.Error.Message);
            }
            else
            {
                RefreshMessages();
            }
        }

        #endregion

        private void SignIntoRoom(int oldRoomId, int newRoomId)
        {
            if (oldRoomId != 0)
                TfsServiceWrapper.SignIntoRoom(projectCollectionUri, oldRoomId, false);
            TfsServiceWrapper.SignIntoRoom(projectCollectionUri, newRoomId, true);
        }

        private void LoadRooms(IEnumerable<Room> rooms)
        {
            Rooms.Clear();

            if (rooms != null)
            {
                foreach (var item in rooms)
                {
                    Rooms.Add(item);
                }
                if (savedteamRoomId != 0 && Rooms.Select(x => x.Id).Contains(savedteamRoomId))
                {
                    foreach (Room item in cmbRoomList.Items)
                    {
                        if (item.Id == savedteamRoomId)
                        {
                            cmbRoomList.SelectedIndex = cmbRoomList.Items.IndexOf(item);
                            break;
                        }
                    }
                }
            }
        }

        private void LoadRoomUsers(TeamRoomUsers roomUsers)
        {
            if (roomUsers.RoomId != teamRoom.Id || roomUsers.ConnectionUri != projectCollectionUri)
                return;

            RoomUsers.Clear();

            if (roomUsers.Users != null)
            {
                foreach (var item in roomUsers.Users.Where(x => x.IsOnline))
                {
                    if (!UserWorker.Instance.ProfileImages.ContainsKey(item.UserRef.Id))
                    {
                        var profileImages = TfsServiceWrapper.GetUserProfileImages(new List<IdentityRef> { item.UserRef });
                        UserWorker.Instance.GetProfiles(profileImages);
                    }
                    RoomUsers.Add(item);
                }
            }
        }

        private void SetStatusMessage(int messages)
        {
            if (!this.HasEffectiveKeyboardFocus && teamRoom != null)
            {
                TeamRoomWindowCommand.Instance.SetStatusMessage(string.Format("{0} new message{1} from {2} team room", messages, messages == 1? "":"s", teamRoom.Name));
                StatusSet = true;
            }
        }

        private void RefreshMessages()
        {
            // HACK: Force refresh by updating a property on each object.
            lstMessages.ItemsSource = Messages;
        }

        private void RefreshRoomUsers()
        {
            // HACK: Force refresh by updating a property on each object.
            lstRoomUsers.ItemsSource = RoomUsers;
        }

        private void ClearStatusMessage(object sender, RoutedEventArgs e)
        {
            if (!StatusSet) return;
            StatusSet = false;
            TeamRoomWindowCommand.Instance.ClearStatusMessage();
        }
    }
}