// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using EnvDTE;

using Microsoft.Sarif.Viewer;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.CodeAnalysis.CodeQL.Runner;

namespace Sarif.Viewer.VisualStudio.Core.CodeQL
{
    internal class CodeQLService
    {
        /// <summary>
        /// Current selection for the drop down combo box.
        /// </summary>
        private static CancellationTokenSource _cancelToken;
        private static TaskCompletionSource<bool> _taskCompleted;

        public static bool IsCodeQLTaskCompleted()
        {
            return _taskCompleted != null && _taskCompleted.Task.IsCompleted;
        }

        public static bool IsCodeQLTaskCanceled()
        {
            return _taskCompleted != null && _taskCompleted.Task.IsCanceled;
        }

        public static void Init()
        {
            _taskCompleted = new TaskCompletionSource<bool>();
            _cancelToken = new CancellationTokenSource();
        }

        public static void Clear()
        {
            _taskCompleted = null;
            _cancelToken = null;
        }

        public static void CancelIfRunning()
        {
            try
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
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }

        public static async System.Threading.Tasks.Task CodeQLRunQuerySetAsync(string querySet)
        {
            // TODO
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                string visualStudioShellPath = ProjectHelper.GetVisualStudioFolder();
                Project project = ProjectHelper.GetActiveProject();
                string projectArch = ProjectHelper.GetProjectPropertyValue(project, "Platform");
                string projectDirectory = ProjectHelper.GetProjectDirectory(project);

                string startCommand = "\"" + Path.Combine(visualStudioShellPath, @"VC\Auxiliary\Build\vcvarsall.bat") + "\" " + projectArch + " && cd /d \"" + projectDirectory + "\" &&";

                CodeQLRunner runner = new CodeQLRunner("arch", "dir", buildEnv: startCommand, dbDir: "dir"); // FIXME
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

                // await SarifViewerUtils.OpenSarifLogAsync(sarifResults);

                _ = _taskCompleted.TrySetResult(true);
            }
            catch (Exception ex)
            {
                _ = _taskCompleted.TrySetResult(false);
                throw new Exception(ex.ToString());
            }

            await System.Threading.Tasks.Task.Delay(1000);
        }

        public static async System.Threading.Tasks.Task<bool> CodeQLGenerateDatabaseAsync()
        {
            // TODO
            await System.Threading.Tasks.Task.Delay(1000);
            return true;
        }
    }
}
