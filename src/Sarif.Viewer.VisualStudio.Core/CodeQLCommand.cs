// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Microsoft;
using Microsoft.VisualStudio.Shell;

namespace Sarif.Viewer.VisualStudio.Core.CodeQL
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

        private static readonly HashSet<string> _dropDownComboChoicesDiscoveredSet = new HashSet<string>();

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
                      new CommandID(CommandSet, CodeQLAnalyzeCommandId))
                {
                    ParametersDescription = "$"
                };
                commandService.AddCommand(oleCommand);

                oleCommand = new OleMenuCommand(
                    this.MenuItemCallback,
                    new CommandID(CommandSet, CodeQLStopCommandId))
                {
                    ParametersDescription = "$"
                };
                commandService.AddCommand(oleCommand);

                oleCommand = new OleMenuCommand(
                    this.MenuItemCallback,
                    new CommandID(CommandSet, CodeQLDatabaseCommandId))
                {
                    ParametersDescription = "$"
                };
                commandService.AddCommand(oleCommand);

                oleCommand = new OleMenuCommand(
                    this.MenuItemCallback,
                    new CommandID(CommandSet, CodeQLLoadQueriesCommandId))
                {
                    ParametersDescription = "$"
                };
                commandService.AddCommand(oleCommand);

                // Combo box
                var menuMyDropDownComboCommandID = new CommandID(CommandSet, CodeQLComboId);
                var menuMyDropDownComboCommand = new OleMenuCommand(new EventHandler(OnMenuMyDropDownCombo), menuMyDropDownComboCommandID);
                commandService.AddCommand(menuMyDropDownComboCommand);

                var menuMyDropDownComboGetListCommandID = new CommandID(CommandSet, ComboGetListId);
                MenuCommand menuMyDropDownComboGetListCommand = new OleMenuCommand(new EventHandler(OnMenuMyDropDownComboGetList), menuMyDropDownComboGetListCommandID);
                commandService.AddCommand(menuMyDropDownComboGetListCommand);
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
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var menuCommand = (OleMenuCommand)sender;
            switch (menuCommand.CommandID.ID)
            {
                case CodeQLAnalyzeCommandId:
                    try
                    {
                        if (!CodeQLService.IsCodeQLTaskCompleted())
                        {
                            throw new Exception("CodeQL already running"); // FIXME
                        }

                        CodeQLService.Init();

                        // remake database in case anything has changed.
                        bool dbSuccessful = false; // FIXME // await CodeQLGenerateDatabaseAsync();
                        if (dbSuccessful && CodeQLService.IsCodeQLTaskCompleted() && !CodeQLService.IsCodeQLTaskCanceled())
                        {
                            CodeQLService.Init(); // init again since starting a new CodeQL process
                            await CodeQLService.CodeQLRunQuerySetAsync(_currentDropDownComboChoice.Trim().ToLower());
                        }
                        else
                        {
                            // await OutputToWindowPaneAsync("CodeQL", "Database generation failed, skipping query set execution.");
                        }

                        CodeQLService.Clear();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.ToString()); // FIXME
                    }

                    break;
                case CodeQLStopCommandId:
                    try
                    {
                        CodeQLService.CancelIfRunning();
                    }
                    catch (Exception ex)
                    {
                        if (ex.GetType() == typeof(TaskCanceledException))
                        {
                            // MessageBox.Show("CodeQL process cancelled"); // FIXME
                        }
                        else
                        {
                            throw new Exception(ex.ToString()); // FIXME
                        }
                    }

                    break;
                case CodeQLDatabaseCommandId:
                    try
                    {
                        if (!CodeQLService.IsCodeQLTaskCompleted())
                        {
                            throw new Exception("CodeQL already running"); // FIXME
                        }

                        CodeQLService.Init();
                        _ = await CodeQLService.CodeQLGenerateDatabaseAsync();
                        CodeQLService.Clear();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.ToString()); // FIXME
                    }

                    break;
                case CodeQLLoadQueriesCommandId:
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
                    Marshal.GetNativeVariantForObject(_dropDownComboChoicesDiscoveredSet, vOut);
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

        /*        private async void CodeQLLoadAvailableAsync(object sender, EventArgs e)
                {
                    await System.Threading.Tasks.Task.Delay(1000);
                }*/
    }
}
