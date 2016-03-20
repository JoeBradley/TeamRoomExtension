using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

namespace TeamRoomExtension.ServiceHelpers
{
    public static class TfsServiceWrapper
    {
        private static string collectionUri = "https://christopher-cassidy.visualstudio.com/DefaultCollection";
        private static string teamProjectName = "Puffin";

        public static String GetBugs()
        {
            try {
                // Create a connection object, which we will use to get httpclient objects.  This is more robust
                // then newing up httpclient objects directly.  Be sure to send in the full collection uri.
                // For example:  http://myserver:8080/tfs/defaultcollection
                // We are using default VssCredentials which uses NTLM against a Team Foundation Server.  See additional provided
                // examples for creating credentials for other types of authentication.
                VssConnection connection = new VssConnection(new Uri(collectionUri), new VssCredentials());

                // Create instance of WorkItemTrackingHttpClient using VssConnection
                WorkItemTrackingHttpClient witClient = connection.GetClient<WorkItemTrackingHttpClient>();

                // Get 2 levels of query hierarchy items
                List<QueryHierarchyItem> queryHierarchyItems = witClient.GetQueriesAsync(teamProjectName, depth: 2).Result;

                // Search for 'My Queries' folder
                QueryHierarchyItem myQueriesFolder = queryHierarchyItems.FirstOrDefault(qhi => qhi.Name.Equals("My Queries"));
                if (myQueriesFolder != null)
                {
                    string queryName = "REST Sample";

                    // See if our 'REST Sample' query already exists under 'My Queries' folder.
                    QueryHierarchyItem newBugsQuery = null;
                    if (myQueriesFolder.Children != null)
                    {
                        newBugsQuery = myQueriesFolder.Children.FirstOrDefault(qhi => qhi.Name.Equals(queryName));
                    }
                    if (newBugsQuery == null)
                    {
                        // if the 'REST Sample' query does not exist, create it.
                        newBugsQuery = new QueryHierarchyItem()
                        {
                            Name = queryName,
                            Wiql = "SELECT [System.Id],[System.WorkItemType],[System.Title],[System.AssignedTo],[System.State],[System.Tags] FROM WorkItems WHERE [System.TeamProject] = @project AND [System.WorkItemType] = 'Bug' AND [System.State] = 'New'",
                            IsFolder = false
                        };
                        newBugsQuery = witClient.CreateQueryAsync(newBugsQuery, teamProjectName, myQueriesFolder.Name).Result;
                    }

                    // run the 'REST Sample' query
                    WorkItemQueryResult result = witClient.QueryByIdAsync(newBugsQuery.Id).Result;

                    if (result.WorkItems.Any())
                    {
                        StringBuilder bugsText = new StringBuilder();
                        int skip = 0;
                        const int batchSize = 100;
                        IEnumerable<WorkItemReference> workItemRefs;
                        do
                        {
                            workItemRefs = result.WorkItems.Skip(skip).Take(batchSize);
                            if (workItemRefs.Any())
                            {
                                // get details for each work item in the batch
                                List<WorkItem> workItems = witClient.GetWorkItemsAsync(workItemRefs.Select(wir => wir.Id)).Result;
                                foreach (WorkItem workItem in workItems)
                                {
                                    // write work item to console
                                    bugsText.AppendLine(String.Format("{0} {1}", workItem.Id, workItem.Fields["System.Title"]));
                                }
                            }
                            skip += batchSize;
                        }
                        while (workItemRefs.Count() == batchSize);
                        return bugsText.ToString();
                    }
                    else
                    {
                        return "No work items were returned from query.";
                    }
                }

                return "Could not find My Queries folder, so aborting.";
            } catch (AggregateException aex) {
                StringBuilder bugsText = new StringBuilder();
                bugsText.AppendLine("Aggregate exceptions occurred:");
                foreach (Exception ex in aex.InnerExceptions) {
                    bugsText.AppendLine(ex.Message);
                }
                return bugsText.ToString();
            } catch (Exception ex) {
                return "Exception: " + ex.Message;
            }


}

        public static String GetChatRooms()
        {
            StringBuilder result = new StringBuilder();
            try
            {
                TfsTeamService ts = new TfsTeamService();
                var info = Workstation.Current.GetAllLocalWorkspaceInfo();
                result.AppendLine("Workstation Info");
                foreach (var item in info)
                {
                    result.AppendLine("Server Uri: " + item.ServerUri.ToString());
                }
                //TeamFoundationServerExt tfsext = new
                Workstation.Current.GetLocalWorkspaceInfo

                var pc = RegisteredTfsConnections.GetProjectCollections();
                result.AppendLine("Registered Projects");
                foreach (var item in pc)
                {
                    result.AppendLine(String.Format("Name: {0}, Uri: {1}, Offline: {2}",
                        item.DisplayName, item.Uri.ToString(), item.Offline));
                }

                // Create a connection object, which we will use to get httpclient objects.  This is more robust
                // then newing up httpclient objects directly.  Be sure to send in the full collection uri.
                // For example:  http://myserver:8080/tfs/defaultcollection
                // We are using default VssCredentials which uses NTLM against a Team Foundation Server.  See additional provided
                // examples for creating credentials for other types of authentication.
                // authenticate using Visual Studio sign-in prompt
                using (TfsTeamProjectCollection tpc = new TfsTeamProjectCollection(new Uri(collectionUri), new TfsClientCredentials()))
                {
                    tpc.
                    result.AppendLine("Start");
                    tpc.Authenticate();
                    result.AppendLine("Authenticated");

                    if (!tpc.HasAuthenticated) return "Could not authenticate";

                    result.AppendLine("Get Client");
                    var chatClient = tpc.GetClient<ChatHttpClient>();
                    result.AppendLine("Client retrieved");
                    var rooms = chatClient.GetRoomsAsync().Result;
                    result.AppendLine("Results retrieved");

                    tpc.Disconnect();

                    result.AppendLine(String.Format("Rooms: {0}", String.Join(", ", rooms.Select(x => x.Name))));
                }
            }
            catch (Exception ex)
            {
                result.AppendLine("Exception: " + ex.Message);
            }
            return result.ToString();
        }

        public static void MixedSample()
        {
            // Get TfsTeamProjectCollection using standard SOAP convention
            using (TfsTeamProjectCollection tpc = new TfsTeamProjectCollection(new Uri(collectionUri)))
            {
                // Can retrieve SOAP service from TfsTeamProjectCollection instance
                VersionControlServer vcServer = tpc.GetService<VersionControlServer>();
                ItemSet itemSet = vcServer.GetItems("$/", RecursionType.OneLevel);
                foreach (Item item in itemSet.Items)
                {
                    Console.WriteLine(item.ServerItem);
                }

                // Can retrieve REST client from same TfsTeamProjectCollection instance
                TfvcHttpClient tfvcClient = tpc.GetClient<TfvcHttpClient>();
                List<TfvcItem> tfvcItems = tfvcClient.GetItemsAsync("$/", VersionControlRecursionType.OneLevel).Result;
                foreach (TfvcItem item in tfvcItems)
                {
                    Console.WriteLine(item.Path);
                }
            }
        }
        
    }
}
