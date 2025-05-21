// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Sarif.Viewer.VisualStudio.Core.CodeQL;
using Microsoft.VisualStudio.CodeAnalysis.CodeQL.Exceptions;

namespace Microsoft.Sarif.Viewer.Views
{
    /// <summary>
    /// Interaction logic for CodeQLPackInstallHelper.xaml.
    /// </summary>
    public partial class CodeQLPackInstallHelper : Window
    {
        private readonly HashSet<string> _languagePacks;

        public CodeQLPackInstallHelper()
        {
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
            InstallWindow iw = new InstallWindow(_languagePacks);
            iw.Owner = this;
            iw.DataContext = this;
            iw.backgroundWorker.RunWorkerAsync();
            iw.ShowDialog();
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

        private void LanguagePack_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            if (checkBox.IsChecked ?? false)
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
