//------------------------------------------------------------------------------
// <copyright file="TeamRoomWindowControl.xaml.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
using System.Threading;

namespace TeamRoomExtension
{
    using ServiceHelpers;
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for TeamRoomWindowControl.
    /// </summary>
    public partial class TeamRoomWindowControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TeamRoomWindowControl"/> class.
        /// </summary>
        public TeamRoomWindowControl()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Handles click on the button by displaying a message box.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Justification = "Sample code")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Default event handler naming pattern")]
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                string.Format(System.Globalization.CultureInfo.CurrentUICulture, "Invoked '{0}'", this.ToString()),
                "TeamRoomWindow");
        }

        private void btnClickMe_Click(object sender, RoutedEventArgs e)
        {
            txtBugs.Text = "";
            Thread.Sleep(500);
            txtBugs.Text = TfsServiceWrapper.GetChatRooms();            
        }
    }
}