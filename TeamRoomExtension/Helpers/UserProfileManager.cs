using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using TeamRoomExtension.Models;
using TeamRoomExtension.ServiceHelpers;

namespace TeamRoomExtension.Helpers
{
    // Static class for manageing User Profiles.  Keeps single static list of User Profiles.  Load Profiles (set color and Profile Uri
    public class UserProfileManager
    {
        private static string[] defaultColors = new[] { "#ffff6138", "#ff00a388", "#fffffb8c", "#ffbeeb9f", "#ff79bd8f" };

        public static List<UserProfile> Users = new List<UserProfile>();

        public static bool HasProfile(Uri projectCollectionUri, IdentityRef identity)
        {
            return Users.Any(x => x.Identity.Id == identity.Id && x.ProjectCollectionUri == projectCollectionUri);
        }

        public static UserProfile GetProfile(Uri projectCollectionUri, IdentityRef identity)
        {
            return Users.Single(x => x.Identity.Id == identity.Id && x.ProjectCollectionUri == projectCollectionUri);
        }

        // TODO: Make this ASYNC
        public static void LoadUserProfile(Uri projectCollectionUri, IdentityRef identity)
        {
            if (Users.Any(x => x.Identity.Id == identity.Id && x.ProjectCollectionUri == projectCollectionUri)) return;

            var profileImage = TfsServiceWrapper.GetProfileImage(projectCollectionUri, identity.Id);

            Users.Add(new UserProfile() { Identity = identity, ProjectCollectionUri = projectCollectionUri, ProfileImage = profileImage, ProfileColor = GetNextProfileColor(projectCollectionUri) });
        }

        // TODO: Make this ASYNC
        public static void LoadUserProfiles(Uri projectCollectionUri, List<IdentityRef> identities)
        {
            foreach (var identity in identities)
            {
                LoadUserProfile(projectCollectionUri, identity);
            }            
        }

        private static Color GetNextProfileColor(Uri projectCollectionUri)
        {
            string hex = defaultColors[Users.Count(x => x.ProjectCollectionUri == projectCollectionUri) % defaultColors.Length];
            return (Color)ColorConverter.ConvertFromString(hex);
        }
    }
}
