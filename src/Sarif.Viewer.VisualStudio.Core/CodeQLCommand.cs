// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Microsoft.Sarif.Viewer.Views;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;


namespace Microsoft.Sarif.Viewer.VisualStudio.Core.CodeQL
{
    internal class CodeQLCommand
    {
        /// <summary>
        /// Command ID for CodeQL analyze.
        /// </summary>
        public const int CodeQLAnalyzeCommandId = 0x0100;

        /// <summary>
        /// Command ID for CodeQL kill process.
        /// </summary>
        public const int CodeQLStopCommandId = 0x0110;

        /// <summary>
        /// Command ID for CodeQL load queries.
        /// </summary>
        public const int CodeQLLoadQueriesCommandId = 0x0111;

        /// <summary>
        /// Command ID for CodeQL database create.
        /// </summary>
        public const int CodeQLDatabaseCommandId = 0x133;

        /// <summary>
        /// Command ID for CodeQL combo box.
        /// </summary>
        public const int CodeQLComboId = 0x155;

        /// <summary>
        /// Command ID for CodeQL combo box get list.
        /// </summary>
        public const int ComboGetListId = 0x156;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("4fc96981-3ec2-43c7-acf0-96d73b1203e8");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        private static string _currentDropDownComboChoice;
        private static string[] _discoveredComboChoices;

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeQLCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file).
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private CodeQLCommand(Package package)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));

            var commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var oleCommand = new OleMenuCommand(
                      this.MenuItemCallback,
                      new CommandID(CommandSet, CodeQLAnalyzeCommandId));
                commandService.AddCommand(oleCommand);

                oleCommand = new OleMenuCommand(
                    this.MenuItemCallback,
                    new CommandID(CommandSet, CodeQLStopCommandId));
                commandService.AddCommand(oleCommand);

                oleCommand = new OleMenuCommand(
                    this.MenuItemCallback,
                    new CommandID(CommandSet, CodeQLDatabaseCommandId));
                commandService.AddCommand(oleCommand);

                oleCommand = new OleMenuCommand(
                    this.MenuItemCallback,
                    new CommandID(CommandSet, CodeQLLoadQueriesCommandId));
                commandService.AddCommand(oleCommand);

                // Combo box
                oleCommand = new OleMenuCommand(
                    new EventHandler(this.OnMenuMyDropDownCombo),
                    new CommandID(CommandSet, CodeQLComboId));
                commandService.AddCommand(oleCommand);

                oleCommand = new OleMenuCommand(
                    new EventHandler(this.OnMenuMyDropDownComboGetList),
                    new CommandID(CommandSet, ComboGetListId));
                commandService.AddCommand(oleCommand);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static CodeQLCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider => this.package;

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new CodeQLCommand(package);
        }

        private void MenuItemCallback(object sender, EventArgs e)
        {
            this.MenuItemCallbackAsync(sender, e).FileAndForget("Microsoft/SARIF/Viewer/CodeQL/Failed"); // FIXME
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private async System.Threading.Tasks.Task MenuItemCallbackAsync(object sender, EventArgs e)
        {
            if (!CodeQLService.Instance.CodeQLIsInstalled())
            {
                await ((AsyncPackage)this.package).JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    CodeQLInstallHelper codeQLInstallHelper = new CodeQLInstallHelper();
                    codeQLInstallHelper.ShowDialog();
                });
            }
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var menuCommand = (OleMenuCommand)sender;
            switch (menuCommand.CommandID.ID)
            {
                case CodeQLAnalyzeCommandId:
                    try
                    {
                        if (CodeQLService.Instance.IsCodeQLTaskRunning())
                        {
                            throw new Exception("CodeQL already running"); // FIXME
                        }
                        if (string.IsNullOrEmpty(_currentDropDownComboChoice))
                        {
                            throw new Exception("No query selected");
                        }
                        CodeQLService.Instance.InitTask();

                        Trace.WriteLine("Creating CodeQL database");
                        bool dbSuccessful = await CodeQLService.Instance.CodeQLGenerateDatabaseAsync();

                        if (dbSuccessful
                            && CodeQLService.Instance.IsCodeQLTaskCompleted())
                        {
                            CodeQLService.Instance.InitTask(); // init again since starting a new CodeQL process
                            Trace.WriteLine($"Starting CodeQL Analysis using {_currentDropDownComboChoice}");
                            await CodeQLService.Instance.CodeQLRunQuerySetAsync(_currentDropDownComboChoice.Trim().ToLower());
                        }

                        CodeQLService.Instance.ClearTask();
                    }
                    catch (Exception ex)
                    {
                        CodeQLService.Instance.ClearTask();
                        throw new Exception(ex.ToString()); // FIXME
                    }

                    break;
                case CodeQLStopCommandId:
                    try
                    {
                        CodeQLService.Instance.CancelIfRunning();
                    }
                    catch (Exception ex)
                    {
                        if (ex.GetType() == typeof(TaskCanceledException))
                        {
                            // MessageBox.Show("CodeQL process cancelled"); // FIXME
                        }
                        else
                        {
                            CodeQLService.Instance.ClearTask();
                            throw new Exception(ex.ToString()); // FIXME
                        }
                    }

                    break;
                case CodeQLDatabaseCommandId:
                    try
                    {
                        if (!CodeQLService.Instance.IsCodeQLTaskCompleted())
                        {
                            throw new Exception("CodeQL already running"); // FIXME
                        }

                        CodeQLService.Instance.InitTask();
                        _ = await CodeQLService.Instance.CodeQLGenerateDatabaseAsync();
                        CodeQLService.Instance.ClearTask();
                    }
                    catch (Exception ex)
                    {
                        CodeQLService.Instance.ClearTask();
                        throw new Exception(ex.ToString()); // FIXME
                    }

                    break;
                case CodeQLLoadQueriesCommandId:
                    try
                    {
                        await CodeqlRefreshAvailableQueriesAsync();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.ToString()); // FIXME
                    }
                    break;
                case CodeQLComboId:
                    break;
                case ComboGetListId:
                    break;
                default:
                    break;
            }
        }

        private void OnMenuMyDropDownCombo(object sender, EventArgs e)
        {
            if (e is OleMenuCmdEventArgs eventArgs)
            {
                IntPtr vOut = eventArgs.OutValue;

                if (vOut != IntPtr.Zero)
                {
                    // when vOut is non-NULL, the IDE is requesting the current value for the combo
                    Marshal.GetNativeVariantForObject(_currentDropDownComboChoice, vOut);
                }
                else
                {
                    _currentDropDownComboChoice = eventArgs.InValue is string newChoice ? newChoice : throw new ArgumentException("Invalid Selection");
                }
            }
            else
            {
                // We should never get here; EventArgs are required.
                throw new ArgumentException("EventArgs are required"); // force an exception to be thrown
            }
        }


        private void OnMenuMyDropDownComboGetList(object sender, EventArgs e)
        {
            if (e is OleMenuCmdEventArgs eventArgs)
            {
                object inParam = eventArgs.InValue;
                IntPtr vOut = eventArgs.OutValue;

                if (inParam != null)
                {
                    throw new ArgumentException("InParamIllegal"); // force an exception to be thrown
                }
                else if (vOut != IntPtr.Zero)
                {
                    if (_discoveredComboChoices == null)
                    {
                        throw new Exception("No queries found");
                    }
                    Marshal.GetNativeVariantForObject(_discoveredComboChoices, vOut);

                }
                else
                {
                    throw new ArgumentException("OutParamRequired"); // force an exception to be thrown
                }
            }
            else
            {
                throw new ArgumentException("InParamIllegal"); // force an exception to be thrown
            }
        }

        public async System.Threading.Tasks.Task CodeqlRefreshAvailableQueriesAsync()
        {
            if (_discoveredComboChoices == null && !CodeQLService.Instance.IsCodeQLTaskRunning())
            {
                _discoveredComboChoices = await CodeQLService.Instance.CodeQLFindAvailableQueriesAsync();
            }

            if (_currentDropDownComboChoice == null && _discoveredComboChoices != null && _discoveredComboChoices.Length != 0)
            {
                _currentDropDownComboChoice = _discoveredComboChoices[0];
            }
        }
    }
}
