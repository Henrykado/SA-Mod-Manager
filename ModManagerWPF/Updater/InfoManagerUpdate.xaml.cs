﻿using System;

using System.IO;
using System.Windows;

namespace SAModManager.Common
{
    /// <summary>
    /// Interaction logic for InfoManagerUpdate.xaml
    /// </summary>
    public partial class InfoManagerUpdate : Window
    {
        public InfoManagerUpdate(WorkflowRunInfo workflow, GitHubArtifact artifact, string changelog)
        {
            InitializeComponent();
            UpdateInfoText.Text = changelog;
        }

        public InfoManagerUpdate(string changelog)
        {
            InitializeComponent();
            UpdateInfoText.Text = changelog;
            Title = "New Mod Loader Updates";
        }

        private void CancelUpdate_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
        }

        private void DownloadUpdate_Click(object sender, RoutedEventArgs e)
        {
            bool retry = false;

            do
            {
                try
                {
                    if (!Directory.Exists(".SATemp"))
                    {
                        Directory.CreateDirectory(".SATemp");
                    }
                }
                catch (Exception ex)
                {


                    //Lang.GetString("MessageWindow.Errors.DirectoryCreateFail.Title");
                }
            } while (retry == true);

            DialogResult = true;
        }
    }
}
