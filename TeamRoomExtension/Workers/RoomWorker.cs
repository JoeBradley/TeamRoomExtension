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

    public sealed class RoomWorker
    {
        // Singleton Instance
        private static volatile RoomWorker instance;

        // Lock objects
        private static readonly object SingletonLock = new Object();
        private static readonly object CritSectionLock = new Object();

        // Background worker
        public BackgroundWorker LoadRoomsWorker;
        public BackgroundWorker LoadRoomUsersWorker;
        public Uri connectionUri;

        private RoomWorker()
        {
            LoadRoomsWorker = CreateWorker(LoadRoomsWorker_DoWork);
            LoadRoomUsersWorker = CreateWorker(LoadRoomUsersWorker_DoWork);
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

        public bool LoadRooms(Uri connectionUri)
        {
            try
            {
                if (LoadRoomsWorker != null && LoadRoomsWorker.IsBusy) return false;

                if (LoadRoomsWorker == null) CreateWorker(LoadRoomsWorker_DoWork);

                LoadRoomsWorker.RunWorkerAsync(connectionUri);

                return true;
            }
            catch (Exception ex)
            {
                //Debug.Fail(ex.Message);
            }
            return false;
        }

        public bool LoadRoomUsers(Uri connection, int roomId)
        {
            try
            {
                if (LoadRoomUsersWorker != null && LoadRoomUsersWorker.IsBusy) return false;

                if (LoadRoomUsersWorker == null) CreateWorker(LoadRoomUsersWorker_DoWork);

                LoadRoomUsersWorker.RunWorkerAsync(new { Uri = connection, RoomId = roomId } );

                return true;
            }
            catch (Exception ex)
            {
                //Debug.Fail(ex.Message);
            }
            return false;
        }

        private BackgroundWorker CreateWorker(DoWorkEventHandler doWork)
        {
            var bw = new BackgroundWorker();

            bw.WorkerReportsProgress = false;
            bw.WorkerSupportsCancellation = false;

            bw.DoWork += new DoWorkEventHandler(doWork);

            return bw;
        }

        #region Events

        /// <summary>
        /// Background Worker entry point.  Critical Code Sections are wrapped in an Application, and then Database Level lock.  
        /// The Application lock ensures that the the critical section is atomic within the current application scope.  
        /// The Database Level lock ensures that the critical section is atomic across mutiple runnning applications (possibly on seperate servers).
        /// </summary>
        /// <param name="sender">Background Worker</param>
        /// <param name="e">Worker State information</param>
        private void LoadRoomsWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;
            var args = e.Argument as Uri;

            try
            {
                // Get Lock
                if (Monitor.TryEnter(CritSectionLock, 2 * 1000))
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
                    Monitor.Exit(CritSectionLock);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Release Locks Error: {0}", ex.Message);
                }
            }
        }

        private void LoadRoomUsersWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;
            dynamic args = e.Argument as dynamic;

            try
            {
                // Get Lock
                if (Monitor.TryEnter(CritSectionLock, 2 * 1000))
                {
                    var users = TfsServiceWrapper.GetRoomUsersAsync(args.Uri, args.RoomId).Result;
                    e.Result = users;
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
                    Monitor.Exit(CritSectionLock);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Release Locks Error: {0}", ex.Message);
                }
            }
        }

        #endregion
    }
}
