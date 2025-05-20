// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Sarif.Viewer.VisualStudio.Core.CodeQL;
using Microsoft.VisualStudio.CodeAnalysis.CodeQL.Exceptions;
using Microsoft.VisualStudio.CodeAnalysis.CodeQL.Runner;

using Newtonsoft.Json.Linq;

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
                await CodeQLService.Instance.CodeQLInstallAsync(___TextBoxVersion_.Text, ___TextBoxPath_.Text, AddToPathCheckBox.IsChecked ?? false, _languagePacks);
            }
            catch
            {
                iw.Close();
                Close();
                throw new Exception("Error installing codeql");
            }
            iw.Close();
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
        public async Task<string> GetLatestVersionAsync()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                client.DefaultRequestHeaders.Add("User-Agent", "codeql-action");

                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/github/codeql-action/releases/latest");

                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(content);
                    return ((string)json["tag_name"]).Replace("codeql-bundle-v", "");
                }
                else
                {
                    return string.Empty;
                }
            }
        }
        private async Task SetVersionCheckBoxTextAsync()
        {
            ___TextBoxVersion_.Text = await GetLatestVersionAsync();
        }

        private void UseLatest_Checked(object sender, RoutedEventArgs e)
        {
            _ = SetVersionCheckBoxTextAsync();
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
