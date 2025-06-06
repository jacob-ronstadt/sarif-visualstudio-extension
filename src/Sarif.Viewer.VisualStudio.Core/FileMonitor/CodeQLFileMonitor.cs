// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

using EnvDTE;

using EnvDTE80;

using Microsoft.CodeAnalysis.Sarif;
using Microsoft.Sarif.Viewer.Options;
using Microsoft.Sarif.Viewer.Services;
using Microsoft.Sarif.Viewer.Shell;
using Microsoft.Sarif.Viewer.VisualStudio.Core.CodeQL;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.Sarif.Viewer.FileMonitor
{
    /// <summary>
    /// Handles loading & monitoring CodeQL files under solution directory .codeql folder.
    /// </summary>
    internal class CodeQLFileMonitor : IDisposable
    {
        public const string CodeQLFolderName = ".codeql";
        public const string CodeQLFileExtensionName = ".ql";

        private readonly IFileSystem fileSystem;
        private IFileWatcher fileWatcher;
        private bool filesLoaded;
        private string solutionFolder = null;

        internal CodeQLFileMonitor(
            IFileSystem fs = null,
            IFileWatcher watcher = null
            )
        {
            this.fileSystem = fs ?? new FileSystem();
            this.fileWatcher = watcher ?? new FileWatcher();
        }

        public void Dispose()
        {
            this.fileWatcher?.Dispose();
        }

        /// <summary>
        /// Starts watching the .codeql folder under a particular path for files being created or destroyed. If the path does not exist it will create a new directory of that path.
        /// </summary>
        /// <param name="solutionPath">(Optional). The solution path to watch. Of no path is provided it will use the default solution directory if exists.</param>
        internal void StartWatching(string solutionPath = null)
        {
            this.solutionFolder = solutionPath ?? this.GetSolutionDirectory();
            if (string.IsNullOrEmpty(this.solutionFolder) || !fileSystem.DirectoryExists(this.solutionFolder))
            {
                return;
            }

            string codeQLLogFolder = Path.Combine(this.solutionFolder, CodeQLFolderName);
            if (!fileSystem.DirectoryExists(codeQLLogFolder))
            {
                try
                {
                    fileSystem.DirectoryCreateDirectory(codeQLLogFolder);
                }
                catch
                {
                    // failed to create .codeql folder, exit method
                    return;
                }
            }

            // load existing CodeQL files
            this.LoadExistingCodeQLFile(codeQLLogFolder);

            try
            {
                this.fileWatcher ??= new Shell.FileWatcher();
                this.fileWatcher.FilePath = codeQLLogFolder;
                this.fileWatcher.Filter = Constants.CodeQLFileSearchPattern;

                // no need to watch for the CodeQL file log updates
                // because when we load the CodeQL log file in the viewer, it's already monitored by the viewer's file watcher
                this.fileWatcher.FileCreated += this.Watcher_CodeQLFileCreated;
                this.fileWatcher.FileDeleted += this.Watcher_CodeQLFileDeleted;
                this.fileWatcher.Start();
            }
            catch
            {
            }
        }

        internal void StopWatching()
        {
            if (this.fileWatcher != null)
            {
                this.fileWatcher.Stop();
                this.fileWatcher.FileCreated -= this.Watcher_CodeQLFileCreated;
                this.fileWatcher.FileDeleted -= this.Watcher_CodeQLFileDeleted;
                this.fileWatcher = null;
            }

            if (!string.IsNullOrEmpty(this.solutionFolder))
            {
                this.CloseExistingCodeQLFile(Path.Combine(this.solutionFolder, CodeQLFolderName));
                this.solutionFolder = null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetFolderPath">The folder to load the files from.</param>
        internal void LoadExistingCodeQLFile(string targetFolderPath)
        {
            if (!string.IsNullOrEmpty(targetFolderPath) && this.fileSystem.DirectoryExists(targetFolderPath) && !this.filesLoaded)
            {
                IEnumerable<string> codeQLFiles = this.fileSystem.DirectoryGetFiles(targetFolderPath, Constants.CodeQLFileSearchPattern);
                CodeQLService.Instance.AddAdditionalQueries(new List<string>(codeQLFiles));
                this.filesLoaded = true;
            }
        }

        internal void CloseExistingCodeQLFile(string targetFolderPath)
        {
            if (!string.IsNullOrEmpty(targetFolderPath) && this.fileSystem.DirectoryExists(targetFolderPath))
            {
                IEnumerable<string> codeQLFileFiles = this.fileSystem.DirectoryGetFiles(targetFolderPath, Constants.CodeQLFileSearchPattern);
                this.filesLoaded = false;
            }
        }

        private void Watcher_CodeQLFileCreated(object sender, FileSystemEventArgs e)
        {
            if (SarifViewerPackage.IsUnitTesting)
            {
            }
            else
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    CodeQLService.Instance.AddAdditionalQueries(new List<string>(new[] { e.FullPath }));
                });
            }
        }

        private void Watcher_CodeQLFileDeleted(object sender, FileSystemEventArgs e)
        {
            if (SarifViewerPackage.IsUnitTesting)
            {
            }
            else
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    CodeQLService.Instance.RemoveQuery(e.FullPath);
                });
            }
        }

        /// <summary>
        /// Returns the solution directory, or null if no solution is open.
        /// </summary>
        private string GetSolutionDirectory()
        {
            var dte = (DTE2)Package.GetGlobalService(typeof(DTE));
            string solutionFilePath = dte.Solution?.FullName;
            return string.IsNullOrEmpty(solutionFilePath) ? null : Path.GetDirectoryName(solutionFilePath);
        }
    }
}
