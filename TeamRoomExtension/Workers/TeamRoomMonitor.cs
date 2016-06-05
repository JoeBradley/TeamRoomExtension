using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Chat.WebApi;
using TeamRoomExtension.ServiceHelpers;
using System.Threading;
using TeamRoomExtension.Helpers;
using TeamRoomExtension.Models;

namespace TeamRoomExtension.Workers
{
    /// <summary>
    /// The Team Room Monitor intermittently polls the Team Room API for messages and users.  Any new messages, users or change in user status is reported to the UI.
    /// </summary>
    public class TeamRoomMonitor
    {
        #region  Background Workers

        private BackgroundWorker MessageMonitor;
        private BackgroundWorker UsersMonitor;

        #endregion

        #region public Events Raised

        // Raised when new messages posted to the room
        public event EventHandler<TeamRoomEventArgs<Message>> NewMessages;
        public event EventHandler<TeamRoomEventArgs<User>> UsersChanged;

        // Raised when both polling workers are complete (i.e. cancelled)
        public event EventHandler<TeamRoomEventArgs> PollingComplete;
        
        #endregion

        #region Timer Properties (in Miliseconds)

        // TODO: Move these to user settings
        const int MaxMessagePollPeriodMs = 60 * 1000;
        const int MinMessagePollPeriodMs = 2 * 1000;

        const int MaxUserPollPeriodMs = 5 * 60 * 1000;
        const int MinUserPollPeriodMs = 10 * 1000;

        // Time to sleep between checking if cancel has been called on the background worker
        const int sleepPeriodMs = 2 * 1000;

        #endregion

        #region Private variables

        // Team Room Properties
        Uri ProjectCollectionUri;
        Room TeamRoom;

        // Timer variables
        int MessagePollWaitTimeout = 0;
        private DateTime MessagesLastChange = DateTime.MinValue;
        
        int UsersPollWaitTimeout = 0;
        private DateTime UsersLastChange = DateTime.MinValue;
        
        #endregion

        #region Constructors

        public TeamRoomMonitor(Uri projectionCollectionUri, Room teamRoom) {

            ProjectCollectionUri = projectionCollectionUri;
            TeamRoom = teamRoom;

            InitWorkers(PollMessages, PollUsers);
            StartPolling();
        }

        // Overloaded Constructor for unit testing.  Inject BackgroundWorker event handlers.
        public TeamRoomMonitor(Uri projectionCollectionUri, Room teamRoom, DoWorkEventHandler pollUsersWorker, DoWorkEventHandler pollMessagesWorker)
        {
            ProjectCollectionUri = projectionCollectionUri;
            TeamRoom = teamRoom;

            InitWorkers(pollMessagesWorker, pollUsersWorker);
            StartPolling();
        }
        
        #endregion

        /// <summary>
        /// Create 
        /// </summary>
        /// <param name="pollUsersWorker"></param>
        /// <param name="pollMessagesWorker"></param>
        private void InitWorkers(DoWorkEventHandler pollUsersWorker, DoWorkEventHandler pollMessagesWorker)
        {
            if (MessageMonitor == null)
            {
                MessageMonitor = new BackgroundWorker();
                MessageMonitor.WorkerSupportsCancellation = true;
                MessageMonitor.DoWork += pollMessagesWorker;
                MessageMonitor.RunWorkerCompleted += WorkerComplete;
            }

            if (UsersMonitor == null)
            {
                UsersMonitor = new BackgroundWorker();
                UsersMonitor.DoWork += pollUsersWorker;
                UsersMonitor.WorkerSupportsCancellation = true;
                UsersMonitor.RunWorkerCompleted += WorkerComplete;
            }
        }
        
        #region BGW Delegates

        /// <summary>
        /// Poll for new messages posted to the Team Room.  Report new merssages to the UI.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PollMessages(object sender, DoWorkEventArgs e)
        {
            try
            {
                while (!MessageMonitor.CancellationPending && ProjectCollectionUri != null && TeamRoom != null)
                {
                    // Get a local copy of the properties (these may change whilst porocessing)
                    List<Message> messages = TfsServiceWrapper.GetRoomMessagesAsync(ProjectCollectionUri, TeamRoom.Id).Result;

                    // Check for changes to the users, raise event, set the polling wait time out
                    MessagePollWaitTimeout = CheckNewMessges(messages) ? GetFibonacciBackoff(MessagePollWaitTimeout, MaxMessagePollPeriodMs) : MinMessagePollPeriodMs;
                    
                    int slept = 0;
                    while (slept < MessagePollWaitTimeout && !MessageMonitor.CancellationPending)
                    {
                        Thread.Sleep(sleepPeriodMs);
                        slept += sleepPeriodMs;
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Poll the Team room for Users and Users Status.  Report changes to the UI.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PollUsers(object sender, DoWorkEventArgs e)
        {
            try {
                while (!UsersMonitor.CancellationPending && ProjectCollectionUri != null && TeamRoom != null)
                {
                    // Get a local copy of the properties (these may change whilst porocessing)
                    List<User> users = TfsServiceWrapper.GetRoomUsersAsync(ProjectCollectionUri, TeamRoom.Id).Result;

                    // Check for changes to the users, raise event, set the polling wait time out
                    UsersPollWaitTimeout = CheckChangedUsers(users) ? GetFibonacciBackoff(UsersPollWaitTimeout, MaxUserPollPeriodMs) : MinUserPollPeriodMs;


                    int slept = 0;
                    while (slept < UsersPollWaitTimeout && !UsersMonitor.CancellationPending)
                    {
                        Thread.Sleep(sleepPeriodMs);
                        slept += sleepPeriodMs;
                    }                    
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private bool CheckNewMessges(List<Message> messages)
        {
            if (messages.Any(x => x.PostedTime > MessagesLastChange))
            {
                // Load any missing user profiles
                UserProfileManager.LoadUserProfiles(ProjectCollectionUri, messages.Select(x => x.PostedBy).ToList());
                
                // Raise Event Handler
                NewMessages(this, new TeamRoomEventArgs<Message> { ProjectCollectionUri = ProjectCollectionUri, TeamRoom = TeamRoom, Data = messages.Where(x => x.PostedTime > MessagesLastChange).ToList() });
                
                // Update date of last message posted to the Team Room
                MessagesLastChange = messages.Max(x => x.PostedTime);
                return true;
            }
            return false;
        }

        private bool CheckChangedUsers(List<User> newUsers)
        {
            var changed = false;
            
            foreach (var user in newUsers)
            {
                if (user.JoinedDate > UsersLastChange || user.LastActivity > UsersLastChange)
                {
                    // Load any missing user profiles
                    UserProfileManager.LoadUserProfiles(ProjectCollectionUri, newUsers.Select(x => x.UserRef).ToList());
                    
                    // Raise Event Handler
                    UsersChanged(this, new TeamRoomEventArgs<User> { ProjectCollectionUri = ProjectCollectionUri, TeamRoom = TeamRoom, Data = newUsers });
                    
                    UsersLastChange = DateTime.UtcNow;
                    changed = true;

                    break;
                }
            }
            return changed;

        }

        /// <summary>
        /// Event raised after each Background worker is completed.  Will Raise Polling Complete event if all background workers are finished.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WorkerComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            var messagesComplete = MessageMonitor == null || !MessageMonitor.IsBusy;
            var usersComplete = UsersMonitor == null || !UsersMonitor.IsBusy;

            if (messagesComplete && usersComplete) PollingComplete(this,null);            
        }

        #endregion

        #region Public Methods
        public void StopPolling()
        {
            MessageMonitor.CancelAsync();
            UsersMonitor.CancelAsync();
        }

        public void PollNow()
        {
            MessagePollWaitTimeout = 0;
            UsersPollWaitTimeout = 0;
        }
        #endregion

        #region Private Methods
        private void StartPolling()
        {
            MessageMonitor.RunWorkerAsync();
            UsersMonitor.RunWorkerAsync();
        }

        private int GetNewTimeout(int currentTimeout, int maxTimeout)
        {
            var attempts = Math.Sqrt( ((double)currentTimeout * 2d) + 1d);
            return Convert.ToInt32( Math.Min(maxTimeout, Math.Pow(2d,attempts)/2d));        
        }

        private int GetFibonacciBackoff(int currentTimeout, int maxTimeout)
        {
            if (currentTimeout == maxTimeout) return currentTimeout;

            var n = currentTimeout / 1000;
            var m = maxTimeout / 1000;
            
            int a = 0;
            int b = 1;

            // In N steps compute Fibonacci sequence iteratively.
            for (int x = 0; a < n && a < m; x++)
            {
                int temp = a;
                a = b;
                b = temp + b;
            }
            return a * 1000;
        }

        #endregion

    }

}