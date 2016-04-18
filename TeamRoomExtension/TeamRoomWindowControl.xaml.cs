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
    
    /// <summary>
    /// Interaction logic for TeamRoomWindowControl.
    /// </summary>
    public partial class TeamRoomWindowControl : UserControl, INotifyPropertyChanged
    {        
        #region Private properties
        
        Room teamRoom;
        int teamRoomId = 0;
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

        Uri projectCollectionUri;

        ObservableCollection<RegisteredProjectCollection> collections = new ObservableCollection<RegisteredProjectCollection>();
        public ObservableCollection<RegisteredProjectCollection> Collections
        {
            get { return collections; }
            set { collections = value; OnPropertyChanged("Collections"); }
        }

        ObservableCollection<Room> rooms = new ObservableCollection<Room>();
        public ObservableCollection<Room> Rooms
        {
            get { return rooms; }
            set { rooms = value; OnPropertyChanged("Rooms"); }
        }

        ObservableCollection<User> roomUsers = new ObservableCollection<User>();
        public ObservableCollection<User> RoomUsers
        {
            get { return roomUsers; }
            set { roomUsers = value; OnPropertyChanged("RoomUsers"); }
        }

        ObservableCollection<Message> messages = new ObservableCollection<Message>();
        public ObservableCollection<Message> Messages
        {
            get { return messages; }
            set {
                messages = value;
                OnPropertyChanged("Messages");
            }
        }

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

            // Set the bubble up delegate
            MessagesWatcher.Instance.ReportProgress += MessagesWatcher_NewMessages;
            MessagesWatcher.Instance.ReportComplete += MessagesWatcher_Complete;

            // Set the event handler directly on the background worker
            RoomWorker.Instance.LoadRoomsWorker.RunWorkerCompleted += Rooms_Loaded;
            RoomWorker.Instance.LoadRoomUsersWorker.RunWorkerCompleted += RoomUsers_Loaded;
            
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
                    teamRoomId = settings.TeamRoomId;

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
                    MessagesWatcher.Instance.PollNow();
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
                RoomUsers.Clear();
                teamRoom = null;
                Rooms.Clear();
                cmbRoomList.Text = "Loading Team Rooms...";
                if (projectCollectionUri != null)
                {
                    RoomWorker.Instance.LoadRooms(projectCollectionUri);
                }
            }
            catch { }
        }
        
        #region Events
        
        // Cancel Background worker threads    
        private void TeamRoomWindow_Unload(object sender, RoutedEventArgs e)
        {
            teamRoom = null;
            MessagesWatcher.Instance.Cancel();
        }

        private void cmbCollectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
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
        // TODO: Handle changing rooms
        private void cmbRoomList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                messages.Clear();
                if (cmbRoomList.SelectedValue == null || projectCollectionUri == null)
                    return;

                teamRoom = cmbRoomList.SelectedValue as Room;
                if (teamRoom != null)
                {
                    MessagesWatcher.Instance.DoWork(projectCollectionUri, teamRoom.Id);
                    RoomWorker.Instance.LoadRoomUsers(projectCollectionUri, teamRoom.Id);

                    var settings = new ExtensionSettings { ProjectCollectionUri = projectCollectionUri, TeamRoomId = teamRoom.Id };

                    TeamRoomWindowCommand.Instance.SaveUserSettings(settings);
                }
            }
            catch (Exception ex)
            {
                // Log errors
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
        
        private void MessagesWatcher_NewMessages(object sender, MessagesProgress e)
        {
            if (e.Messages == null || !e.Messages.Any())
                return;

            foreach (var item in e.Messages)
            {
                if (!Messages.Select(x => x.Id).Contains(item.Id))
                    Messages.Add(item);
            }

            svMessages.ScrollToEnd();            
        }

        private void MessagesWatcher_Complete(object sender, MessageWorkerCompleteResult e)
        {
            // TODO: add logic to re-start watcher thread if ;
            // work complete           
            //if (e.Cancelled == true)
            //{
            //    Console.WriteLine("Worker canceled");
            //}
            //else if (e.Error != null)
            //{
            //    Console.WriteLine("Worker error", e.Error.Message);
            //}
            //else
            //{
            //    var rooms = e.Result as IEnumerable<Room>;
            //    Rooms.Clear();
            //    foreach (var item in rooms)
            //    {
            //        Rooms.Add(item);
            //    }
            //}
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
                var rooms = e.Result as IEnumerable<Room>;
                Rooms.Clear();

                if (rooms != null)
                {
                    foreach (var item in rooms)
                    {
                        Rooms.Add(item);
                    }
                    if (teamRoomId != 0 && Rooms.Select(x => x.Id).Contains(teamRoomId)) {
                        foreach (Room item in cmbRoomList.Items)
                        {
                            if (item.Id == teamRoomId)
                            {
                                cmbRoomList.SelectedIndex = cmbRoomList.Items.IndexOf(item);
                                break;
                            }
                        }
                    }
                }
            } 
        }

        private void RoomUsers_Loaded(object sender, RunWorkerCompletedEventArgs e)
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
                var users = e.Result as IEnumerable<User>;
                RoomUsers.Clear();

                if (users != null)
                {
                    foreach (var item in users)
                    {
                        if (!UserWorker.Instance.ProfileImages.ContainsKey(item.UserRef.Id))
                        {
                            var profileImages = TfsServiceWrapper.GetUserProfileImages(new List<IdentityRef> { item.UserRef});
                            UserWorker.Instance.GetProfiles(profileImages);
                        }
                        RoomUsers.Add(item);
                    }
                }
            }
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
                RefreshUserProfilePictures();
            }
        }
        
        #endregion

        private void RefreshUserProfilePictures()
        {
            // HACK: Force refresh by updating a property on each object.
            lstMessages.ItemsSource = Messages;
        }       
    }    
}