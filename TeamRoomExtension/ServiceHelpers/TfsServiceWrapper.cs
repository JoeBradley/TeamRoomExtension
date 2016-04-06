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
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.Framework.Common;
using System.Windows.Media.Imaging;
using System.IO;

namespace TeamRoomExtension.ServiceHelpers
{
    public static class TfsServiceWrapper
    {
        private static Uri _connectionUri;
        private static Uri connectionUri
        {
            get { return _connectionUri; }
            set
            {
                if (_connectionUri != null && value.AbsoluteUri == _connectionUri.AbsoluteUri)
                    return;
                if (_tpc != null)
                    _tpc.Dispose();
                _connectionUri = value;
            }
        }

        private static TfsTeamProjectCollection _tpc;
        public static TfsTeamProjectCollection tpc
        {
            get
            {
                if (_tpc == null)
                    _tpc = new TfsTeamProjectCollection(connectionUri, new TfsClientCredentials());
                _tpc.EnsureAuthenticated();
                return _tpc;
            }
        }

        public static WorkspaceInfo GetWorkspaceInfo()
        {
            //// Get Project Collections
            //var pc = RegisteredTfsConnections.GetProjectCollections();
            //result.AppendLine("Registered Projects");
            //foreach (var item in pc)
            //{
            //    result.AppendLine(String.Format("Name: {0}, Uri: {1}, Offline: {2}",
            //        item.DisplayName, item.Uri.ToString(), item.Offline));
            //}
            try
            {
                var info = Workstation.Current.GetAllLocalWorkspaceInfo();
                if (info.Any())
                    return info.FirstOrDefault();
            }
            catch (Exception)
            {

            }
            return null;
        }

        public static IEnumerable<RegisteredProjectCollection> GetProjectCollections()
        {
            // Get Project Collections
            var pc = RegisteredTfsConnections.GetProjectCollections();
            return pc.ToList();
        }

        public static async Task<List<Room>> GetRoomsAsync(Uri uri)
        {
            try
            {
                connectionUri = uri;

                var chatClient = tpc.GetClient<ChatHttpClient>();
                var rooms = await chatClient.GetRoomsAsync();
                return rooms;

            }
            catch (Exception ex)
            {
            }
            return null;
        }

        public static async Task<List<Message>> GetRoomMessagesAsync(Uri uri, int roomId)
        {
            try
            {
                connectionUri = uri;


                var chatClient = tpc.GetClient<ChatHttpClient>();
                var messages = await chatClient.GetChatRoomMessagesAsync(roomId);

                return messages;

            }
            catch (Exception ex)
            {
            }
            return null;
        }

        public static async Task<Dictionary<string, byte[]>> GetUserProfileImages(Uri uri, int roomId)
        {
            var userImages = new Dictionary<string, byte[]>();
            try
            {
                connectionUri = uri;

                var chatClient = tpc.GetClient<ChatHttpClient>();
                var users = await chatClient.GetChatRoomUsersAsync(roomId);
                foreach (var user in users)
                {
                    var client = tpc.GetService<IIdentityManagementService2>();
                    var i = client.ReadIdentity(IdentitySearchFactor.DisplayName, user.UserRef.DisplayName, MembershipQuery.Expanded, ReadIdentityOptions.ExtendedProperties);

                    object attr;
                    if (i.TryGetProperty("Microsoft.TeamFoundation.Identity.Image.Data", out attr))
                    {
                        userImages.Add(user.UserRef.Id, attr as byte[]);
                    }
                }
            }
            catch { }

            return userImages;
        }

        public static Message PostMessage(Uri uri, int roomId, string message)
        {
            try
            {
                connectionUri = uri;

                var chatClient = tpc.GetClient<ChatHttpClient>();
                var msg = new MessageData { Content = message };
                return chatClient.SendMessageToRoomAsync(msg, roomId).Result;

            }
            catch (Exception ex)
            {
            }
            return null;
        }

        public static byte[] GetProfileImage(Uri uri, string userId)
        {
            try
            {
                connectionUri = uri;

                var client = tpc.GetService<IIdentityManagementService2>();
                var i = client.ReadIdentity(IdentitySearchFactor.DisplayName, userId, MembershipQuery.Expanded, ReadIdentityOptions.ExtendedProperties);

                object attr;
                if (i.TryGetProperty("Microsoft.TeamFoundation.Identity.Image.Data", out attr))
                {
                    return attr as byte[];
                }

            }
            catch (Exception ex)
            {
            }
            return null;
        }
    }
}
