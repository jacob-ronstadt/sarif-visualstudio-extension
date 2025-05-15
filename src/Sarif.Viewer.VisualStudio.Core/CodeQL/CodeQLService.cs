// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using EnvDTE;
using EnvDTE80;
using Microsoft.Sarif.Viewer;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.CodeAnalysis.CodeQL.Runner;
using Microsoft.VisualStudio.OLE.Interop;
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
                        _taskCompleted.TrySetCanceled();
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
            string visualStudioShellPath = ProjectHelper.GetVisualStudioFolder();
            Project project = ProjectHelper.GetActiveProject();
            string projectArch = ProjectHelper.GetProjectPropertyValue(project, "Platform");
            string projectDirectory = ProjectHelper.GetProjectDirectory(project);

            string startCommand = "\"" + Path.Combine(visualStudioShellPath, @"VC\Auxiliary\Build\vcvarsall.bat") + "\" " + projectArch + " && cd /d \"" + projectDirectory + "\" &&";

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
