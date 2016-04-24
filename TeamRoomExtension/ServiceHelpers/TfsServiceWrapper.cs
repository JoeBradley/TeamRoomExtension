using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                {
                    _tpc.Dispose();
                    _tpc = null;
                }
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
                if (!_tpc.HasAuthenticated)
                    _tpc.Authenticate();
                return _tpc;
            }
        }

        //public static WorkspaceInfo GetWorkspaceInfo()
        //{
        //    //// Get Project Collections
        //    //var pc = RegisteredTfsConnections.GetProjectCollections();
        //    //result.AppendLine("Registered Projects");
        //    //foreach (var item in pc)
        //    //{
        //    //    result.AppendLine(String.Format("Name: {0}, Uri: {1}, Offline: {2}",
        //    //        item.DisplayName, item.Uri.ToString(), item.Offline));
        //    //}
        //    try
        //    {
        //        var info = Workstation.Current.GetAllLocalWorkspaceInfo();
        //        if (info.Any())
        //            return info.FirstOrDefault();
        //    }
        //    catch (Exception)
        //    {

        //    }
        //    return null;
        //}

        public static IEnumerable<RegisteredProjectCollection> GetProjectCollections()
        {
            try {
                // Get Project Collections
                var pc = RegisteredTfsConnections.GetProjectCollections();
                return pc.ToList();
            }
            catch (Exception ex)
            {
            }

            return null;
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

        public static async Task<List<User>> GetRoomUsersAsync(Uri uri, int roomId)
        {
            try
            {
                connectionUri = uri;

                var chatClient = tpc.GetClient<ChatHttpClient>();
                var users = await chatClient.GetChatRoomUsersAsync(roomId);
                return users;

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
                userImages = GetUserProfileImages(users.Select(x => x.UserRef).ToList());
            }
            catch (Exception ex)
            {
            }

            return userImages;
        }
        
        public static Dictionary<string, byte[]> GetUserProfileImages(List<IdentityRef> users)
        {
            var userImages = new Dictionary<string, byte[]>();
            try
            {
                var client = tpc.GetService<IIdentityManagementService2>();

                foreach (var user in users)
                {
                    if (!userImages.Keys.Contains(user.Id))
                    {
                        var ci = client.ReadIdentity(IdentitySearchFactor.DisplayName, user.DisplayName, MembershipQuery.Expanded, ReadIdentityOptions.ExtendedProperties);

                        object attr;
                        if (ci.TryGetProperty("Microsoft.TeamFoundation.Identity.Image.Data", out attr))
                        {
                            userImages.Add(user.Id, attr as byte[]);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
            }

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
