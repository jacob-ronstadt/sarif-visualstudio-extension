// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using EnvDTE;

using EnvDTE80;

using Microsoft.CodeAnalysis.Sarif.Converters;
using Microsoft.Sarif.Viewer;
using Microsoft.Sarif.Viewer.ErrorList;
using Microsoft.Sarif.Viewer.Services;
using Microsoft.VisualStudio.CodeAnalysis.CodeQL.Runner;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;


namespace Sarif.Viewer.VisualStudio.Core.CodeQL
{
    internal class CodeQLService
    {
        /// <summary>
        /// Current selection for the drop down combo box.
        /// </summary>
        private static CancellationTokenSource _cancelToken;
        private static TaskCompletionSource<bool> _taskCompleted;
        private static string[] _availableQueries;
        
        private static CodeQLService _instance = null;

        /// <summary>
        /// Gets the instance of the service.
        /// </summary>
        public static CodeQLService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CodeQLService();
                }
                return _instance;
            }
            private set { }
        }

        private CodeQLService()
        {
            _taskCompleted = null;
            _cancelToken = null;
            _availableQueries = null;
        }

        public bool IsCodeQLTaskRunning()
        {
            return _taskCompleted != null && !_taskCompleted.Task.IsCompleted;
        }

        public bool IsCodeQLTaskCompleted()
        {
            return _taskCompleted != null && _taskCompleted.Task.IsCompleted && !_taskCompleted.Task.IsCanceled;
        }

        public void ClearTask()
        {
            _taskCompleted = null;
            _cancelToken = null;
        }

        public void InitTask()
        {
            _taskCompleted = new TaskCompletionSource<bool>(false);
            _cancelToken = new CancellationTokenSource();
        }

        public void CancelIfRunning()
        {
            if (_taskCompleted != null && _cancelToken != null)
            {
                if (!_cancelToken.IsCancellationRequested)
                {
                    _cancelToken.Cancel();
                }

                if (!_taskCompleted.Task.IsCompleted)
                {
                    _ = _taskCompleted.TrySetCanceled();
                }
            }
        }

        public async Task<string[]> AvailableQueriesAsync()
        {
            if (_availableQueries == null)
            {
                _availableQueries = await CodeQLLoadAvailableQueriesAsync();
            }
            return _availableQueries;
        }

        private async System.Threading.Tasks.Task<string[]> CodeQLLoadAvailableQueriesAsync()
        {
            List<string> packList = await CodeQLRunner.Instance.FindPacksAsync();
            List<string> queryList = await CodeQLRunner.Instance.FindQueriesAsync(packList, queriesNSuites: false);
            return queryList.ToArray();
        }

        public async System.Threading.Tasks.Task CodeQLRunQuerySetAsync(string querySet)
        {
            
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            string visualStudioShellPath = ProjectHelper.GetVisualStudioFolder();
            Project project = ProjectHelper.GetActiveProject();
            string projectArch = ProjectHelper.GetProjectPropertyValue(project, "Platform");
            string projectDirectory = ProjectHelper.GetProjectDirectory(project);

            string startCommand = "\"" + Path.Combine(visualStudioShellPath, @"VC\Auxiliary\Build\vcvarsall.bat") + "\" " + projectArch + " && cd /d \"" + projectDirectory + "\" &&";

            // await runner.CheckCodeQLPacksInstalledAsync();

            List<string> queriesList;
            if (querySet.EndsWith(".qls") || querySet.EndsWith("ql"))
            {
                queriesList = File.Exists(querySet)
                    ? new List<string>() { querySet }
                    : throw new ArgumentException("Query file does not exist: " + querySet);
            }
            else
            {
                // queriesList = await runner.GetQueriesFromSuiteAsync(querySet);
            }
            string sarifResults = await CodeQLRunner.Instance.RunCodeQLQuerySetAsync(querySet, _cancelToken.Token);
            try
            {
                await ErrorListService.ProcessLogFileWithTracesAsync(sarifResults, ToolFormat.None, promptOnLogConversions: true, cleanErrors: true, openInEditor: false).ConfigureAwait(continueOnCapturedContext: false);
                new DataService().CloseEnhancedResultData(cookie: 0);
            }
            catch (InvalidOperationException)
            {
                VsShellUtilities.ShowMessageBox(Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider,
                                                string.Format(Resources.LogOpenFail_InvalidFormat_DialogMessage, Path.GetFileName(sarifResults)),
                                                null, // title
                                                OLEMSGICON.OLEMSGICON_CRITICAL,
                                                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                                                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }            // await SarifViewerUtils.OpenSarifLogAsync(sarifResults);

            _ = _taskCompleted.TrySetResult(true);
        }

        public async System.Threading.Tasks.Task<bool> CodeQLGenerateDatabaseAsync()
        {
            string arch = "";
            string configName="";

            await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                await ProjectHelper.BuildProjectAsync();
                // TODO check if build failed 
                Project activeProject = ProjectHelper.GetActiveProject();
                Configuration config = ProjectHelper.GetProjectActiveConfiguration(activeProject);

                arch = config.PlatformName;
                configName = config.ConfigurationName;

                string projectDir = ProjectHelper.GetProjectDirectory(activeProject);
                string startCommand = "\"" + Path.Combine(ProjectHelper.GetVisualStudioFolder(), @"VC\Auxiliary\Build\vcvarsall.bat") + "\" " + arch + " && cd /d \"" + projectDir + "\" &&";
                
                CodeQLRunner.Instance.Initialize(projectDir, startCommand);
            });
          
            string buildCmd = "msbuild /t:rebuild /p:Configuration=" + configName + " /p:Platform=" + arch;
            await CodeQLRunner.Instance.GenerateDatabaseAsync(buildCmd, _cancelToken.Token);
            _taskCompleted.TrySetResult(true);
            return true;
        }
    }
}
