// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.CodeAnalysis.CodeQL.Exceptions
{

    public class CodeQLAlreadyRunningException : Exception
    {
        public CodeQLAlreadyRunningException()
        {
        }
        public CodeQLAlreadyRunningException(string message)
            : base(message)
        {
        }
        public CodeQLAlreadyRunningException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
    public class DatabaseNotFinalizedException : Exception
    {
        public DatabaseNotFinalizedException()
        {
        }

        public DatabaseNotFinalizedException(string message)
            : base(message)
        {
        }

        public DatabaseNotFinalizedException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
    public class CodeQLPacksNotFoundException : Exception
    {
        public CodeQLPacksNotFoundException()
        {
        }

        public CodeQLPacksNotFoundException(string message)
            : base(message)
        {
        }

        public CodeQLPacksNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
    public class CodeQLExeNotFoundException : Exception
    {
        public CodeQLExeNotFoundException()
        {
        }

        public CodeQLExeNotFoundException(string message)
            : base(message)
        {
        }

        public CodeQLExeNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
    public class CodeQLException : Exception
    {
        public CodeQLException()
        {
        }

        public CodeQLException(string message)
            : base(message)
        {
        }

        public CodeQLException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class CodeQLExceptionHandler
    {

        private async Task ExceptionHandlerAsync(Exception exception)
        {
            int result;
            string resourceString;


          
            if (exception.GetType() == typeof(CodeQLPacksNotFoundException))
            {
                await JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    CodeQLPackInstallHelper codeQLpackInstallHelper = new CodeQLPackInstallHelper();
                    await codeQLpackInstallHelper.UpdateMissingPackBoxesAsync();
                    codeQLpackInstallHelper.ShowDialog();
                });
                return;
            }
            else if (exception.GetType() == typeof(CodeQLExeNotFoundException))
            {
                await JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    CodeQLInstallHelper codeQLInstallHelper = new CodeQLInstallHelper();
                    codeQLInstallHelper.ShowDialog();
                });
                return;
            }
            else if (exception.GetType() == typeof(DatabaseNotFinalizedException))
            {
                resourceString = getStringFromResources("codeqlNoDatabase");
            }
            else if (exception.GetType() == typeof(MissingSarifViewerException))
            {
                resourceString = getStringFromResources("missingSarifViewer");
            }
            else if (exception.GetType() == typeof(CodeQLAlreadyRunningException))
            {
                resourceString = getStringFromResources("codeqlAlreadyRunning");
            }
            else
            {
                resourceString = getStringFromResources("codeqlException");
            }

            await JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // End Debug
                IVsUIShell uiShell = (IVsUIShell)await GetServiceAsync(typeof(SVsUIShell));
                Guid clsid = Guid.Empty;
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
                        0,
                        ref clsid,
                        resourceString,
                        string.Format(CultureInfo.CurrentCulture, "{0}.", exception.Message),
                        string.Empty,
                        0,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                        OLEMSGICON.OLEMSGICON_INFO,
                        0,        // false
                        out result));
            });
        }
    }
}
