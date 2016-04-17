using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Threading;
using System.Windows.Media;

namespace TeamRoomExtension.ServiceHelpers
{
    public sealed class UserWorker
    {
        #region Const
        private string[] defaultColors = new[] { "#ffff6138", "#ff00a388","#fffffb8c", "#ffbeeb9f", "#ff79bd8f" };

        #endregion

        // Singleton Instance
        private static volatile UserWorker instance;

        // Lock objects
        private static readonly object SingletonLock = new Object();
        private static readonly object CritSectionLock = new Object();

        // Background worker
        public BackgroundWorker Worker;

        // Rooms already scanned
        private List<int> RoomsLoaded = new List<int>();

        // All loaded profile pictures: UserId, BitmapImage
        public Dictionary<string, byte[]> ProfileImages = new Dictionary<string, byte[]>();
        // userId, color
        public Dictionary<string, Color> ProfileColors = new Dictionary<string, Color>();
        
        private UserWorker()
        {
            Worker = CreateWorker();
        }

        public static UserWorker Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (SingletonLock)
                    {
                        if (instance == null)
                            instance = new UserWorker();
                    }
                }

                return instance;
            }
        }

        public Boolean DoWork(Uri uri, int roomId)
        {
            try
            {
                if (RoomsLoaded.Contains(roomId)) return true;

                if (Worker != null && Worker.IsBusy) return false;

                if (Worker == null) CreateWorker();

                Worker.RunWorkerAsync(new { Uri = uri, RoomId = roomId });

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
            dynamic args = e.Argument as dynamic;

            try
            {
                // Get Lock
                if (Monitor.TryEnter(CritSectionLock, 2 * 1000))
                {
                    Dictionary<string,byte[]> profiles = TfsServiceWrapper.GetUserProfileImages(args.Uri, args.RoomId).Result;
                    UserWorker.Instance.GetProfiles(profiles);
                    RoomsLoaded.Add(args.RoomId);
                    e.Result = true;
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

        public void GetProfiles(Dictionary<string, byte[]> profiles)
        {
            foreach (KeyValuePair<string, byte[]> profile in profiles)
            {
                if (!ProfileImages.Keys.Contains(profile.Key))
                    ProfileImages.Add(profile.Key, profile.Value);
                if (!ProfileColors.Keys.Contains(profile.Key))
                {
                    string hex = defaultColors[ProfileColors.Count % defaultColors.Length];
                    Color c = (Color)ColorConverter.ConvertFromString(hex);
                    ProfileColors.Add(profile.Key, c);
                }
            }
        }


    }
}
