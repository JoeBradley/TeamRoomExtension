//------------------------------------------------------------------------------
// <copyright file="TeamRoomWindowControl.xaml.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
using System.Linq;
using Microsoft.TeamFoundation.Chat.WebApi;
using System.Diagnostics;

namespace TeamRoomExtension
{
    using Microsoft.TeamFoundation.VersionControl.Client;
    using ServiceHelpers;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Globalization;
    using System.Threading.Tasks;
    using System.Web;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;

    /// <summary>
    /// Interaction logic for TeamRoomWindowControl.
    /// </summary>
    public partial class TeamRoomWindowControl : UserControl, INotifyPropertyChanged
    {        
        #region Private properties

        WorkspaceInfo _info;
        public WorkspaceInfo info
        {
            get { return _info; }
            set
            {
                if (_info != value)
                {
                    _info = value;
                    OnPropertyChanged("Info");
                    LoadRooms();
                }
            }
        }

        User me;
        Room teamRoom;

        Uri collectionUri { get { return info == null ? null : info.ServerUri; } }
        Uri teamRoomUri
        {
            get
            {
                try
                {
                    if (info.ServerUri == null || teamRoom == null) return null;

                    return new Uri(string.Format("{0}/_rooms?name={1}&_a=today", info.ServerUri, HttpUtility.UrlEncode(teamRoom.Name)));
                }
                catch (Exception)
                {
                    return null;
                }

            }
        }

        //public BoundTask<List<Room>> rooms { get; private set; }
        ObservableCollection<WorkspaceInfo> collections = new ObservableCollection<WorkspaceInfo>();
        public ObservableCollection<WorkspaceInfo> Collections
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
            RoomWorker.Instance.Worker.RunWorkerCompleted += Rooms_Loaded;

            UserWorker.Instance.Worker.RunWorkerCompleted += ProfilePictures_Loaded;

            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                info = TfsServiceWrapper.GetWorkspaceInfo();
                var pcs = TfsServiceWrapper.GetProjectCollections();

                if (info != null)
                {
                    lblConnectionName.Text = info.ServerUri.ToString();
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

                if (teamRoom != null && info != null)
                {
                    var msg = TfsServiceWrapper.PostMessage(info.ServerUri, teamRoom.Id, txtMessage.Text);
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
                Rooms.Clear();
                if (info != null)
                {
                    RoomWorker.Instance.DoWork(info.ServerUri);
                }
            }
            catch { }
        }

        //private async Task LoadRoomMessagesAsync(Uri uri, int roomId)
        //{
        //    Messages.Clear();
        //    if (info == null) return;
        //    var msgs = await TfsServiceWrapper.GetRoomMessagesAsync(uri, roomId);
        //    foreach (var msg in msgs)
        //    {
        //        Messages.Add(msg);
        //    }
        //}

        //private void LoadRoomMessages(int roomId)
        //{
        //    lstMessages.ItemsSource = messages.Where(x => x.PostedRoomId == roomId).ToList();
        //    svMessages.ScrollToEnd();
        //}

        #region Events
        
        // Cancel Background worker threads    
        private void TeamRoomWindow_Unload(object sender, RoutedEventArgs e)
        {
            teamRoom = null;
            MessagesWatcher.Instance.Cancel();
        }

        // Set the background worker to poll for messages for the selected room.
        // TODO: Handle changing rooms
        private void cmbRoomList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                messages.Clear();
                if (string.IsNullOrEmpty(cmbRoomList.SelectedValue.ToString()) || info == null)
                    return;

                teamRoom = cmbRoomList.SelectedValue as Room;
                if (teamRoom != null)
                {
                    UserWorker.Instance.DoWork(info.ServerUri, teamRoom.Id);
                    MessagesWatcher.Instance.DoWork(info.ServerUri, teamRoom.Id);
                }
            }
            catch (Exception ex)
            {

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
            // TODO: Add new messages to Messages;
            if (e.Messages == null || !e.Messages.Any())
                return;

            foreach (var item in e.Messages)
            {
                if (!Messages.Select(x => x.Id).Contains(item.Id))
                    Messages.Add(item);
            }

            //lstMessages.ItemsSource = Messages;
            svMessages.ScrollToEnd();
            
            //foreach (var item in e.Messages)
            //{
            //    if (!Messages.Contains(item))
            //        Messages.Add(item);
            //    svMessages.ScrollToEnd();
            //}
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
                foreach (var item in rooms)
                {
                    Rooms.Add(item);
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

        #region Mock Methods

        private void LoadMockMessages()
        {
            messages.Add(new Message() { Id = 1, PostedRoomId = 1, Content = "Puffin test message", PostedTime = DateTime.Now.AddMinutes(-5) });
            messages.Add(new Message() { Id = 2, PostedRoomId = 1, Content = "Puffin test message 2", PostedTime = DateTime.Now.AddMinutes(-2) });
            messages.Add(new Message() { Id = 3, PostedRoomId = 1, Content = "Anna is really cute!!!", PostedTime = DateTime.Now });
            messages.Add(new Message() { Id = 4, PostedRoomId = 2, Content = "Squirrel test message", PostedTime = DateTime.Now.AddMinutes(-5) });
            messages.Add(new Message() { Id = 5, PostedRoomId = 2, Content = "Squirrel test message 2", PostedTime = DateTime.Now.AddMinutes(-2) });
            messages.Add(new Message() { Id = 6, PostedRoomId = 2, Content = "Anna is really really cute!!!", PostedTime = DateTime.Now });
        }

        private void LoadMockRooms()
        {
            Rooms.Add(new Room() { Name = "Puffin Team Room", Id = 1 });
            Rooms.Add(new Room() { Name = "Squirrel Team Room", Id = 2 });
        }


        #endregion
        
    }

   
}