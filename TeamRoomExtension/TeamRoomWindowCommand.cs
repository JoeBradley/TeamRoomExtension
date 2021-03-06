﻿//------------------------------------------------------------------------------
// <copyright file="TeamRoomWindowCommand.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;
using System.Drawing;

namespace TeamRoomExtension
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class TeamRoomWindowCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("53324120-9cf0-4ac0-88f0-1e6fdd2a9013");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        /// <summary>
        /// Initializes a new instance of the <see cref="TeamRoomWindowCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private TeamRoomWindowCommand(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new MenuCommand(this.ShowToolWindow, menuCommandID);
                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static TeamRoomWindowCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new TeamRoomWindowCommand(package);
        }

        /// <summary>
        /// Shows the tool window when the menu item is clicked.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        private void ShowToolWindow(object sender, EventArgs e)
        {
            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            ToolWindowPane window = this.package.FindToolWindow(typeof(TeamRoomWindow), 0, true);
            if ((null == window) || (null == window.Frame))
            {
                throw new NotSupportedException("Cannot create tool window");
            }

            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }

        // https://msdn.microsoft.com/en-us/library/dn949254.aspx
        public ExtensionSettings LoadUserSettings()
        {
            try
            {
                SettingsManager settingsManager = new ShellSettingsManager(ServiceProvider);
                WritableSettingsStore userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

                Uri selectedTeamProjectUri;
                int selectedRoomId = userSettingsStore.GetInt32("Team Room Extension", "RoomId", 0);
                Uri.TryCreate(userSettingsStore.GetString("Team Room Extension", "TeamProjectUri", ""), UriKind.Absolute, out selectedTeamProjectUri);

                return new ExtensionSettings { ProjectCollectionUri = selectedTeamProjectUri, TeamRoomId = selectedRoomId };
            }
            catch (Exception ex)
            {
                return new ExtensionSettings();
            }
        }

        public void SaveUserSettings(ExtensionSettings settings)
        {
            try
            {
                SettingsManager settingsManager = new ShellSettingsManager(ServiceProvider);
                WritableSettingsStore userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

                if (!userSettingsStore.CollectionExists("Team Room Extension"))
                    userSettingsStore.CreateCollection("Team Room Extension");

                userSettingsStore.SetInt32("Team Room Extension", "RoomId", settings.TeamRoomId);
                userSettingsStore.SetString("Team Room Extension", "TeamProjectUri", settings.ProjectCollectionUri != null ? settings.ProjectCollectionUri.ToString() : "");
            }
            catch (Exception ex)
            {

            }
        }

        private IVsStatusbar _statusBar = null;
        private IVsStatusbar StatusBar
        {
            get
            {
                if (_statusBar == null)
                {
                    _statusBar = (IVsStatusbar)ServiceProvider.GetService(typeof(SVsStatusbar));
                }

                return _statusBar;
            }
        }
        private object _animationObject;
        private void SetAnimationObject()
        {            
            if (_animationObject == null)
            {
                Bitmap img = new Bitmap(@"Resources/icon.png");
                IntPtr hdc = IntPtr.Zero;
                hdc = img.GetHbitmap();
                _animationObject = (object)hdc;                
            }            
        }

        public void SetStatusMessage(String message)
        {
            // Make sure the status bar is not frozen
            int frozen;

            StatusBar.IsFrozen(out frozen);

            if (frozen != 0)
            {
                StatusBar.FreezeOutput(0);
            }

            // Set the status bar text and make its display static.
            StatusBar.SetText(message);

            //SetAnimationObject();
            //StatusBar.Animation(1, ref _animationObject);          
        }

        public void ClearStatusMessage()
        {
            // Make sure the status bar is not frozen
            int frozen;

            StatusBar.IsFrozen(out frozen);

            if (frozen != 0)
            {
                StatusBar.FreezeOutput(0);
            }

            StatusBar.SetText("");
            StatusBar.Animation(0, _animationObject);
            //StatusBar.Clear();
        }

        private IVsActivityLog _log;
        private IVsActivityLog Log {
            get {
                //if (_log == null)
                    _log = (IVsActivityLog)ServiceProvider.GetService(typeof(SVsActivityLog));
                return _log;
            }
        }

        public void LogMessage(string message)
        {
            int hr = Log.LogEntry((UInt32)__ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION,
                this.ToString(), message);
        }

        public void LogError(Exception ex)
        {
            int hr = Log.LogEntry((UInt32)__ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION,
                this.ToString(),
                string.Format(CultureInfo.CurrentCulture, "Exception: {0}, Stack Trace: {1}", ex.Message, ex.StackTrace));
        }
    }
}
