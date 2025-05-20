// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

using Microsoft.VisualStudio.CodeAnalysis.CodeQL.Exceptions;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using Sarif.Viewer.VisualStudio.Core.CodeQL;

namespace Microsoft.Sarif.Viewer.Views
{
    /// <summary>
    /// Interaction logic for CodeQLPackInstallHelper.xaml.
    /// </summary>
    public partial class CodeQLPackInstallHelper : Window
    {
        private int _installCLickCount = 0;
        private readonly HashSet<string> _languagePacks;

        public CodeQLPackInstallHelper()
        {
            _installCLickCount = 0;
            _languagePacks = new HashSet<string>();
            Owner = Application.Current.MainWindow;
            InitializeComponent();
            DataContext = this;
        }


        /// <summary>
        /// Handles the event when the create DVL button is clicked.
        /// </summary>
        /// <param name="sender">
        /// The sender of this event.
        /// </param>
        /// <param name="e">
        /// The Event Arguments.
        /// </param>
        private void ButtonInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_installCLickCount == 0)
            {
                _ = this.InstallPacksAsync();
            }
            else if (_installCLickCount > 0)
            {
                throw new CodeQLExeNotFoundException("CodeQL already installed by extension, but was not found. Please check your installation.");
            }
        }

        private async System.Threading.Tasks.Task InstallPacksAsync()
        {
            if (_installCLickCount == 0)
            {
                _installCLickCount++;
                try
                {
                    InstallWindow iw = new InstallWindow();
                    iw.Owner = this;
                    iw.DataContext = this;
                    iw.Show();
                    await CodeQLService.Instance.CodeQLInstallPacksAsync(_languagePacks);
                    await CodeQLCommand.Instance.CodeqlRefreshAvailableQueriesAsync();
                    iw.Close();                  
                    Close();

                }
                catch (Exception ex)
                {
                    Close();
                    throw new Exception(ex.ToString(), ex);
                }
            }
        }

        /// <summary>
        /// Handles the event when the cancel button is clicked.
        /// </summary>
        /// <param name="sender">
        /// The sender of this event.
        /// </param>
        /// <param name="e">
        /// The Event Arguments.
        /// </param>
        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LanguagePack_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            if(checkBox.IsChecked ?? false)
            {
                _languagePacks.Add(checkBox.Content.ToString());
            }
            else
            {
                _languagePacks.Remove(checkBox.Content.ToString());
            }
        }
    }
}
