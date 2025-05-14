// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Microsoft.SdvMenuCommand
{
    /// <summary>
    /// Interaction logic for CodeQLPackInstallHelper.xaml.
    /// </summary>
    public partial class CodeQLPackInstallHelper : Window
    {
        private int _installCLickCount = 0;

        public CodeQLPackInstallHelper()
        {
            try
            {
                _installCLickCount = 0;
                Owner = Application.Current.MainWindow;
                InitializeComponent();
                DataContext = this;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        // public async Task UpdateMissingPackBoxesAsync()
        // {
        //     try
        //     {
        //       // TODO
        //     }
        //     catch (Exception ex)
        //     {
        //         throw new Exception("Error updating missing pack boxes", ex);
        //     }
        // }

        /// <summary>
        /// Handles the event when the create DVL button is clicked.
        /// </summary>
        /// <param name="sender">
        /// The sender of this event.
        /// </param>
        /// <param name="e">
        /// The Event Arguments.
        /// </param>
        private void ButtonInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_installCLickCount == 0)
            {
                _installCLickCount++;
                try
                {
                   // TODO
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.ToString(), ex);
                }

                Close();
            }
        }

        /// <summary>
        /// Handles the event when the cancel button is clicked.
        /// </summary>
        /// <param name="sender">
        /// The sender of this event.
        /// </param>
        /// <param name="e">
        /// The Event Arguments.
        /// </param>
        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TODO
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // TODO
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // TODO
        }
    }
}
