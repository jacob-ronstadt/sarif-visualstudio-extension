// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

using Microsoft.VisualStudio.CodeAnalysis.CodeQL.Exceptions;
using Microsoft.VisualStudio.CodeAnalysis.CodeQL.Runner;
using Microsoft.VisualStudio.PlatformUI;

using Sarif.Viewer.VisualStudio.Core.CodeQL;

namespace Microsoft.Sarif.Viewer.Views
{
    /// <summary>
    /// Interaction logic for CodeQLInstallHelper.xaml.
    /// </summary>
    public partial class CodeQLInstallHelper : Window
    {
        private int _installCLickCount = 0;
        private readonly HashSet<string> _languagePacks;
        public CodeQLInstallHelper()
        {
            _languagePacks = new HashSet<string>();
            _installCLickCount = 0;
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
                _ = this.CodeQLInstallAsync();
            }
            else if (_installCLickCount > 0)
            {
                throw new CodeQLExeNotFoundException("CodeQL already installed by extension, but was not found. Please check your installation.");
            }
        }

        private async Task CodeQLInstallAsync()
        {
            _installCLickCount++;
            InstallWindow iw = new InstallWindow();
            iw.Owner = this;
            iw.DataContext = this;
            iw.Show();
            try
            {
                await CodeQLService.Instance.CodeQLInstallAsync(___TextBoxVersion_.Text, ___TextBoxPath_.Text, AddToPathCheckBox.IsChecked??false, _languagePacks);
            }
            catch
            {
                iw.Close();
                MessageBox.Show("Error Installing CodeQL");
                Close();
                return;
            }
            iw.Close();
            MessageBox.Show("CodeQL successfully installed");
            Close();
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

        private async Task SetVersionCheckBoxTextAsync()
        {
            ___TextBoxVersion_.Text = await CodeQLRunner.Instance.GetLatestVersionAsync();
        }

        private void UseLatest_Checked(object sender, RoutedEventArgs e)
        {
            _ = SetVersionCheckBoxTextAsync();
        }
        private void LanguagePack_Checked(object sender, RoutedEventArgs e)
        {
            // FIXME get content only
            _languagePacks.Add(sender.ToString());
        }
    }
}
