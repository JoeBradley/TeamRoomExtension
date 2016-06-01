using System;
using System.ComponentModel;
using System.Threading;

namespace TeamRoomExtension.ServiceHelpers
{
    using Microsoft.TeamFoundation.Client;
    using Microsoft.TeamFoundation.Chat.WebApi;
    using Microsoft.TeamFoundation.Framework.Client;
    using Microsoft.TeamFoundation.Framework.Common;
    using Microsoft.VisualStudio.Services.WebApi;
    using System.Collections.Generic;
    using System.Linq;
    public sealed class RoomWorker
    {
        // Singleton Instance
        private static volatile RoomWorker instance;

        // Lock objects
        private static readonly object SingletonLock = new Object();
        private static readonly object PollRoomUsersLock = new Object();
        private static readonly object LoadRoomsLock = new Object();

        // Background worker
        public BackgroundWorker LoadRoomsWorker;
        public BackgroundWorker PollRoomUsersWorker;

        //public delegate void LoadRoomsWorkerCompleteHandler(object sender, RoomWorkerCompleteResult e);
        //public delegate void PollRoomUserWorkerProgressHandler(object sender, TeamRoomUsers e);

        public int RoomId;
        public Uri ProjectionCollectionUri;

        private int waitTimeout = 0;
        private const int maxtWaitTimeout = 3 * 60 * 1000;
        private const int sleepPeriod = 3 * 1000;

        private RoomWorker()
        {
            LoadRoomsWorker = CreateWorker(LoadRoomsWorker_DoWork);
            //PollRoomUsersWorker = CreateWorker(PollRoomUsersWorker_DoWork);
        }

        public static RoomWorker Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (SingletonLock)
                    {
                        if (instance == null)
                            instance = new RoomWorker();
                    }
                }

                return instance;
            }
        }

        public bool LoadRooms(Uri projectionCollectionUri)
        {
            try
            {
                ProjectionCollectionUri = projectionCollectionUri;

                if (LoadRoomsWorker != null && LoadRoomsWorker.IsBusy) return false;

                if (LoadRoomsWorker == null) LoadRoomsWorker = CreateWorker(LoadRoomsWorker_DoWork);

                LoadRoomsWorker.RunWorkerAsync(projectionCollectionUri);

                return true;
            }
            catch (Exception ex)
            {
                //Debug.Fail(ex.Message);
            }
            return false;
        }

        public bool PollRoomUsers(Uri projectionCollectionUri, int roomId, ProgressChangedEventHandler progressHandler, RunWorkerCompletedEventHandler completedHandler)
        {
            try
            {
                ProjectionCollectionUri = projectionCollectionUri;
                RoomId = roomId;
                waitTimeout = 0;

                if (PollRoomUsersWorker != null && PollRoomUsersWorker.IsBusy) return false;

                if (PollRoomUsersWorker == null)
                {
                    //PollRoomUsersWorker = CreateWorker(PollRoomUsersWorker_DoWork, progressHandler, completedHandler);
                    PollRoomUsersWorker = new BackgroundWorker();
                    PollRoomUsersWorker.WorkerReportsProgress = true;
                    PollRoomUsersWorker.WorkerSupportsCancellation = true;

                    PollRoomUsersWorker.DoWork += new DoWorkEventHandler(PollRoomUsersWorker_DoWork);
                    PollRoomUsersWorker.ProgressChanged += progressHandler;
                    PollRoomUsersWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(completedHandler);

                }

                PollRoomUsersWorker.RunWorkerAsync();

                return true;
            }
            catch (Exception ex)
            {
                //Debug.Fail(ex.Message);
            }
            return false;
        }

        private BackgroundWorker CreateWorker(DoWorkEventHandler doWorkEventHandler, ProgressChangedEventHandler reportProgressEventHandler = null, RunWorkerCompletedEventHandler completedEventHandler = null)
        {
            var bw = new BackgroundWorker();

            bw.WorkerReportsProgress = reportProgressEventHandler != null;
            bw.WorkerSupportsCancellation = true;

            bw.DoWork += new DoWorkEventHandler(doWorkEventHandler);
            if (reportProgressEventHandler != null) bw.ProgressChanged += new ProgressChangedEventHandler(reportProgressEventHandler);
            if (completedEventHandler != null) bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(completedEventHandler);

            return bw;
        }

        #region Events

        // Load Project Collection Team Rooms
        private void LoadRoomsWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;
            var args = e.Argument as Uri;
            var hasLock = false;
            try
            {
                // Get Lock
                hasLock = Monitor.TryEnter(LoadRoomsLock, 2 * 1000);
                if (hasLock)
                {
                    var rooms = TfsServiceWrapper.GetRoomsAsync(args).Result;
                    e.Result = rooms;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Background Worker Error: {0}", ex.Message);
            }
            finally
            {
                try
                {
                    // Release CritSection Lock
                    if (hasLock) Monitor.Exit(LoadRoomsLock);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Release Locks Error: {0}", ex.Message);
                }
            }
        }

        // Get the Team Room Users.  Poll for changes.
        private void PollRoomUsersWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;
            var hasLock = false;

            try
            {
                // Get Lock
                hasLock = Monitor.TryEnter(PollRoomUsersLock, 2 * 1000);
                if (hasLock)
                {
                    while (!worker.CancellationPending && ProjectionCollectionUri != null && RoomId > 0)
                    {
                        // Get a local copy of the properties (these may change whilst porocessing)
                        var uri = ProjectionCollectionUri;
                        var rId = RoomId;
                        List<User> users = TfsServiceWrapper.GetRoomUsersAsync(uri, rId).Result;
                        worker.ReportProgress(1, new TeamRoomUsers { Users = users, RoomId = rId, ConnectionUri = uri });

                        SetWaitTimeout();
                        if (!worker.CancellationPending && RoomId > 0 && ProjectionCollectionUri != null)
                        {
                            int slept = 0;
                            while (slept < waitTimeout && !worker.CancellationPending)
                            {
                                Thread.Sleep(sleepPeriod);
                                slept += sleepPeriod;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Background Worker Error: {0}", ex.Message);
            }
            finally
            {
                try
                {
                    // Release CritSection Lock
                    if (hasLock) Monitor.Exit(PollRoomUsersLock);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Release Locks Error: {0}", ex.Message);
                }
            }


        }

        private void SetWaitTimeout()
        {
            waitTimeout = Math.Min(maxtWaitTimeout, waitTimeout + (10 * 1000));
        }
        #endregion

    }

    public class RoomWorkerCompleteResult : RunWorkerCompletedEventArgs
    {
        public Uri ConnectionUri;
        public int RoomId;
        public IEnumerable<Room> Rooms { get; set; }

        public RoomWorkerCompleteResult(object result, Exception error, bool cancelled) : base(result, error, cancelled)
        {
        }
    }

    public class TeamRoomUsers
    {
        public Uri ConnectionUri;
        public int RoomId;
        public IEnumerable<User> Users { get; set; }

    }

}
