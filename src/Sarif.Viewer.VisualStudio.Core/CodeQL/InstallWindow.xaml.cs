// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Policy;
using System.Windows;


using Microsoft.Sarif.Viewer.VisualStudio.Core.CodeQL;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.Sarif.Viewer.Views
{
    /// <summary>
    /// Interaction logic for InstallWindow.xaml.
    /// </summary>
    public partial class InstallWindow : Window
    {
        public readonly BackgroundWorker backgroundWorker;
        private readonly string _version;
        private readonly string _path;
        private readonly bool _addToPath;
        private readonly bool _prerelease;
        private readonly HashSet<string> _packs;

        public InstallWindow(string version, string path, bool addToPath, HashSet<string> packs, bool prerelease)
        {
            InitializeComponent();
            _version = version;
            _path = path;
            _addToPath = addToPath;
            _packs = packs; 
            _prerelease = prerelease;
            backgroundWorker = new System.ComponentModel.BackgroundWorker();
            this.backgroundWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.InstallCodeQLAndPacksBackground);
            this.backgroundWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.BackgroundWorkCompleted);
        }
        public InstallWindow(HashSet<string> packs)
        {
            InitializeComponent();
            _packs = packs;
            backgroundWorker = new System.ComponentModel.BackgroundWorker();
            this.backgroundWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.InstallPacksOnlyBackground);
            this.backgroundWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.BackgroundWorkCompleted);
        }
        private void InstallPacksOnlyBackground(object sender, DoWorkEventArgs e)
        {
            try
            {
                ThreadHelper.JoinableTaskFactory.Run(() => CodeQLService.Instance.CodeQLInstallPacksAsync(_packs, _prerelease));
                ThreadHelper.JoinableTaskFactory.Run(() => CodeQLCommand.Instance.CodeqlRefreshAvailableQueriesAsync());
                e.Result = true; // FIXME Probably a better way to do this
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"CodeQL install fail. Threw exception: {ex.Message}");
                e.Result = false;
            }
        }

        private void InstallCodeQLAndPacksBackground(object sender, DoWorkEventArgs e)
        {
            try
            {
                ThreadHelper.JoinableTaskFactory.Run(() => CodeQLService.Instance.CodeQLInstallAsync(_version, _path, _addToPath, _packs, _prerelease));
                ThreadHelper.JoinableTaskFactory.Run(() => CodeQLCommand.Instance.CodeqlRefreshAvailableQueriesAsync());
                e.Result = true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"CodeQL pack install fail. Threw exception: {ex.Message}");
                e.Result = false;
            }
        }
        private void BackgroundWorkCompleted(object sender, EventArgs e)
        {
            this.Close();
        }
        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            backgroundWorker.CancelAsync();
            CodeQLService.Instance.CancelIfRunning();
            this.Close();
        }
    }
}
