// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows;

using Sarif.Viewer.VisualStudio.Core.CodeQL;

namespace Microsoft.Sarif.Viewer.Views
{
    /// <summary>
    /// Interaction logic for InstallWindow.xaml.
    /// </summary>
    public partial class InstallWindow : Window
    {
        public InstallWindow()
        {
            InitializeComponent();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            CodeQLService.Instance.CancelIfRunning();
        }
    }
}
