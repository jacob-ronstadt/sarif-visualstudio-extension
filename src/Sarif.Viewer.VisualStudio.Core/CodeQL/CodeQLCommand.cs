// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using System.Management.Instrumentation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.Sarif.Viewer;
using Microsoft.Sarif.Viewer.Controls;
using Microsoft.Sarif.Viewer.ErrorList;
using Microsoft.Sarif.Viewer.Models;
using Microsoft.Sarif.Viewer.ResultSources.Domain.Models;
using Microsoft.Sarif.Viewer.Sarif;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.CodeAnalysis.CodeQL.Runner;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Sarif.Viewer.VisualStudio.Core.CodeQL
{
    internal class CodeQLCommand
    {
        /// <summary>
        /// Command ID for CodeQL analyze.
        /// </summary>
        public const int CodeQLAnalyzeCommandId = 0x100;

        /// <summary>
        /// Command ID for CodeQL kill process.
        /// </summary>
        public const int CodeQLStopCommandId = 0x110;

        /// <summary>
        /// Command ID for CodeQL load queries.
        /// </summary>
        public const int CodeQLLoadQueriesCommandId = 0x111;

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

        /// <summary>
        /// Service for accessing menu commands.
        /// </summary>
        private readonly IMenuCommandService menuCommandService;

        private static string _currentDropDownComboChoice;
        private static HashSet<string> _dropDownComboChoicesDiscoveredSet;

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeQLCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file).
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private CodeQLCommand(Package package)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));

            this.menuCommandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            Assumes.Present(this.menuCommandService);

            if (menuCommandService != null)
            {
                var oleCommand = new OleMenuCommand(
                      this.MenuItemCallback,
                      new CommandID(CommandSet, CodeQLAnalyzeCommandId));
                oleCommand.ParametersDescription = "$";
                menuCommandService.AddCommand(oleCommand);

                oleCommand = new OleMenuCommand(
                    this.MenuItemCallback,
                    new CommandID(CommandSet, CodeQLStopCommandId));
                oleCommand.ParametersDescription = "$";
                menuCommandService.AddCommand(oleCommand);

                oleCommand = new OleMenuCommand(
                    this.MenuItemCallback,
                    new CommandID(CommandSet, CodeQLDatabaseCommandId));
                oleCommand.ParametersDescription = "$";
                menuCommandService.AddCommand(oleCommand);

                oleCommand = new OleMenuCommand(
                    this.MenuItemCallback,
                    new CommandID(CommandSet, CodeQLLoadQueriesCommandId));
                oleCommand.ParametersDescription = "$";
                menuCommandService.AddCommand(oleCommand);

                // Combo box
                CommandID menuMyDropDownComboCommandID = new CommandID(CommandSet, CodeQLComboId);
                OleMenuCommand menuMyDropDownComboCommand = new OleMenuCommand(new EventHandler(OnMenuMyDropDownCombo), menuMyDropDownComboCommandID);
                menuCommandService.AddCommand(menuMyDropDownComboCommand);

                CommandID menuMyDropDownComboGetListCommandID = new CommandID(CommandSet, ComboGetListId);
                MenuCommand menuMyDropDownComboGetListCommand = new OleMenuCommand(new EventHandler(OnMenuMyDropDownComboGetList), menuMyDropDownComboGetListCommandID);
                menuCommandService.AddCommand(menuMyDropDownComboGetListCommand);
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
           _ = ((AsyncPackage)package).JoinableTaskFactory.RunAsync(async () => { await this.MenuItemCallbackAsync(sender, e); });
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
            var menuCmdEventArgs = (OleMenuCmdEventArgs)e;

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
                        await CodeQLService.CodeQLGenerateDatabaseAsync();
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
            OleMenuCmdEventArgs eventArgs = e as OleMenuCmdEventArgs;
            if (eventArgs != null)
            {
                string newChoice = eventArgs.InValue as string;
                IntPtr vOut = eventArgs.OutValue;

                if (vOut != IntPtr.Zero)
                {
                    // when vOut is non-NULL, the IDE is requesting the current value for the combo
                    Marshal.GetNativeVariantForObject(_currentDropDownComboChoice, vOut);
                }
                else if (newChoice != null)
                {
                    _currentDropDownComboChoice = newChoice;
                }
                else
                {
                    throw new ArgumentException("Invalid Selection");
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
            OleMenuCmdEventArgs eventArgs = e as OleMenuCmdEventArgs;

            if (eventArgs != null)
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


        private async void CodeQLLoadAvailableQueries(object sender, EventArgs e)
        {
            try
            {
                await JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    //InstallWindow iw = new InstallWindow();
                    //iw.DataContext = this;
                    //iw.Show();
                    var packList = await CodeQLRunner.FindPacksAsync();
                    var queryList = await CodeQLRunner.FindQueriesAsync(packList, queriesNSuites: false);
                    _dropDownComboChoicesDiscoveredSet.UnionWith(queryList);
                    _dropDownComboChoices = _dropDownComboChoicesDefaultSet.Concat(_dropDownComboChoicesDiscoveredSet).ToArray();
                    iw.Close();
                    MessageBox.Show(_dropDownComboChoices.Length.ToString() + " available queries found.");

                });
            }
            catch (Exception ex)
            {
                await ExceptionHandlerAsync(ex);
            }
        }



    }
}
