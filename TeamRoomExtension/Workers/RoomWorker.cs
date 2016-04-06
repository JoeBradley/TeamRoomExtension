using System;
using System.ComponentModel;
using System.Threading;

namespace TeamRoomExtension.ServiceHelpers
{
    public sealed class RoomWorker
    {
        // Singleton Instance
        private static volatile RoomWorker instance;

        // Lock objects
        private static readonly object SingletonLock = new Object();
        private static readonly object CritSectionLock = new Object();

        // Background worker
        public BackgroundWorker Worker;
        public Uri connectionUri;

        private RoomWorker()
        {
            Worker = CreateWorker();
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

        public Boolean DoWork(Uri connectionUri)
        {
            try
            {
                if (Worker != null && Worker.IsBusy) return false;

                if (Worker == null) CreateWorker();

                Worker.RunWorkerAsync(connectionUri);

                return true;
            }
            catch (Exception ex)
            {
                //Debug.Fail(ex.Message);
            }
            return false;
        }

        private BackgroundWorker CreateWorker()
        {
            var bw = new BackgroundWorker();

            bw.DoWork += new DoWorkEventHandler(BackgroundWorker_DoWork);

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
        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
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

        #endregion
    }
}
