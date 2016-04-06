using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Threading;
using Microsoft.TeamFoundation.Chat.WebApi;

namespace TeamRoomExtension.ServiceHelpers
{
    public sealed class MessagesWatcher
    {
        // Singleton Instance
        private static volatile MessagesWatcher instance;

        // Lock objects
        private static readonly object SingletonLock = new Object();
        private static readonly object CritSectionLock = new Object();

        // Worker Event Delegate and Handler
        public event ReportProgressEventHandler ReportProgress = delegate { };
        public delegate void ReportProgressEventHandler(object sender, MessagesProgress e);
        
        public event  ReportCompleteEventHandler ReportComplete = delegate { };
        public delegate void ReportCompleteEventHandler(object sender, MessageWorkerCompleteResult e);
        
        // Background worker
        private BackgroundWorker worker;
        private int waitTimeout = 0;
        private const int sleepPeriod = 3000;
        private DateTime lastMessage = DateTime.UtcNow;
        private DateTime minDate = new DateTime(DateTime.UtcNow.Year, 1, 1);

        private MessagesWatcher()
        {
            worker = CreateWorker();
        }

        public static MessagesWatcher Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (SingletonLock)
                    {
                        if (instance == null)
                            instance = new MessagesWatcher();
                    }
                }

                return instance;
            }
        }

        public Boolean DoWork(Uri connectionUri, int roomId)
        {
            try
            {
                if (worker != null && worker.IsBusy) return false;

                if (worker == null) worker = CreateWorker();

                worker.RunWorkerAsync(new MessageWorkerEventArgs() { ConnectionUri = connectionUri, RoomId = roomId });
                return true;
            }
            catch (Exception ex)
            {
                //Debug.Fail(ex.Message);
            }
            return false;
        }

        public void PollNow()
        {
            waitTimeout = 0;
        }

        public void Cancel()
        {
            if (worker != null && worker.IsBusy) worker.CancelAsync();
        }

        private BackgroundWorker CreateWorker()
        {
            var bw = new BackgroundWorker();

            bw.WorkerSupportsCancellation = true;
            bw.WorkerReportsProgress = true;

            bw.DoWork += new DoWorkEventHandler(BackgroundWorker_DoWork);
            bw.ProgressChanged += new ProgressChangedEventHandler(BackgroundWorker_ProgressChanged);            
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BackgroundWorker_RunWorkerCompleted);

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
            var args = e.Argument as MessageWorkerEventArgs;

            var lockKey = Guid.NewGuid();

            try
            {
                // Get Lock
                if (Monitor.TryEnter(CritSectionLock, 2 * 1000))
                {

                    while (!worker.CancellationPending)
                    {
                        var messages = TfsServiceWrapper.GetRoomMessagesAsync(args.ConnectionUri, args.RoomId).Result;
                        //var messages = new List<Message> {
                        //    new Message { Content = "New Message", Id= new Random().Next(), PostedTime= DateTime.UtcNow },
                        //    new Message { Content = "Another Message", Id= new Random().Next(), PostedTime= DateTime.UtcNow },
                        //    new Message { Content = "Heaps more Messages", Id= new Random().Next(), PostedTime= DateTime.UtcNow },
                        //};
                        worker.ReportProgress(1, new MessagesProgress() { Messages = messages });
                        if (!worker.CancellationPending)
                        {
                            if (messages.Any()) lastMessage = messages.Max(x => x.PostedTime);
                            SetWaitTimeout();
                            int slept = 0;
                            while (slept < waitTimeout)
                            {
                                Thread.Sleep(sleepPeriod);
                                slept += sleepPeriod;
                            }
                        }
                    }

                    if (worker.CancellationPending)
                    {
                        e.Cancel = true;
                    }
                    //e.Result = new WorkerCompleteObject { Identifier = args.Name, Status = "Incomplete" };
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

        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //Console.WriteLine("{0}: {1}%", e.UserState, e.ProgressPercentage);

            // Expose event
            this.ReportProgress(this, e.UserState as MessagesProgress);
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try {
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
                    var result = e.Result as MessageWorkerCompleteResult;

                    if (result != null)
                    {
                        //try
                        //{
                        //    Console.WriteLine("{0} complete: {1}", result.Identifier, result.Status);
                        //}
                        //catch
                        //{
                        //    Console.WriteLine("Worker complete");
                        //}
                    }
                }
                this.ReportComplete(this, e.Result as MessageWorkerCompleteResult);
            }
            catch { }
            
        }
        
        #endregion Events

        private void SetWaitTimeout()
        {
            var lastPost = minDate > lastMessage ? minDate : lastMessage;
            var ts = (DateTime.UtcNow - lastPost).Seconds;

            if (ts < 30)
            {
                waitTimeout = 3 * 1000;
            }
            else if (ts < (5 * 60))
            {
                waitTimeout = 10 * 1000;
            }
            else if (ts < (30 *60))
            {
                waitTimeout = 15 * 1000;
            }
            else if (ts < (4 * 60 * 60))
            {
                waitTimeout = 30 * 1000;
            }
            else {
                waitTimeout = 60 * 1000;
            }
        }

    }

    public class MessagesProgress
    {
        public Uri ConnectionUri;
        public int RoomId;
        public IEnumerable<Message> Messages { get; set; }
    }

    public class MessageWorkerEventArgs : EventArgs
    {
        public Uri ConnectionUri;
        public int RoomId;
    }

    public class MessageWorkerCompleteResult
    {
        public Uri ConnectionUri;
        public int RoomId;
        public IEnumerable<Message> Messages { get; set; }
        public string Status { get; set; }
    }
}
