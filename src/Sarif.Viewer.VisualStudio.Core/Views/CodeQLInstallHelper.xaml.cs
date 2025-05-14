// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Microsoft.SdvMenuCommand
{
    /// <summary>
    /// Interaction logic for CodeQLInstallHelper.xaml.
    /// </summary>
    public partial class CodeQLInstallHelper : Window
    {
        private int _installCLickCount = 0;

        private async Task InstallCodeQLAsync(string version = "", string installPath = "")
        {
            if (string.IsNullOrEmpty(version))
            {
                version = "2.21.0"; // TODO get latest version instead
            }

            if (string.IsNullOrEmpty(installPath))
            {
                installPath = "C:\\codeql-home\\";
            }

            // TODO verify if the version is valid and if the path is valid
            try
            {
                if (!Directory.Exists(installPath))
                {
                    Directory.CreateDirectory(installPath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Path Error", ex);
            }

            try
            {
                new Version(version);
            }
            catch (FormatException)
            {
                throw new Exception("Incorrect Version Format");
            }
            catch
            {
                throw new Exception("Version Error");
            }

            try
            {
                await Task.Run(async () =>
                {
                    using (var client = new HttpClient())
                    {
                        string url = "https://github.com/github/codeql-cli-binaries/releases/download/v" + version + "/codeql-win64.zip";
                        HttpResponseMessage response = await client.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                        using (FileStream fs = new FileStream(System.IO.Path.Combine(installPath, "codeql-win64.zip"), FileMode.CreateNew))
                        {
                            await response.Content.CopyToAsync(fs);
                        }
                    }
                });
                await Task.Run(() => { System.IO.Compression.ZipFile.ExtractToDirectory(System.IO.Path.Combine(installPath, "codeql-win64.zip"), installPath); });
            }
            catch (Exception ex)
            {
                throw new Exception("Install Error", ex);
            }
        }

        public CodeQLInstallHelper()
        {
            try
            {
                _installCLickCount = 0;
                Owner = Application.Current.MainWindow;
                InitializeComponent();
                DataContext = this;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
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
                _installCLickCount++;
                try
                {
                    // TODO
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
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
    }
}
