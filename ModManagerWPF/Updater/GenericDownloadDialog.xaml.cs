﻿using SAModManager.Common;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SAModManager.Updater
{

	/// <summary>
	/// Interaction logic for GenericUpdateDialog.xaml
	/// </summary>
	public partial class GenericDownloadDialog : Window
	{
		private readonly Uri uri;
		private readonly string fileName;
		private string dest = ".SATemp";
		private bool defaultFolder = false;
		private readonly CancellationTokenSource tokenSource = new();
		private bool install = false;
		public bool done = false;
		private bool silent = false;

		public GenericDownloadDialog(Uri uri, string title, string fileName, bool defaultFolder = false, string dest = null, bool silent = false, bool install = false)
		{
			InitializeComponent();

			if (silent)
				Hide();

			Title = (!install ? "Download - " : "Install - ") + title;
			DLInfo.Text += " " + title + "...";
			this.fileName = fileName;
			this.uri = uri;
			this.defaultFolder = defaultFolder;
			this.install = install;
			this.silent = silent;

			if (!string.IsNullOrEmpty(dest))
			{
				this.dest = dest;
			}
			else if (!defaultFolder)
			{
				Directory.CreateDirectory(dest);
			}

			if (!defaultFolder)
			{
				try
				{
					if (!Directory.Exists(dest))
					{
						Directory.CreateDirectory(dest);
					}
				}
				catch { }
			}
		}

		private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				DLProgress.Value = e.ProgressPercentage;
			});
		}

		private async void WebClient_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				DLInfo.Text = install ? "Install Completed" : "Download completed." +"\n Copying files...";

			});


			await Task.Delay(200);

			await Application.Current.Dispatcher.Invoke(async () =>
			{
				if (File.Exists(fileName) && !defaultFolder && dest is not null)
				{
					await Util.MoveFile(fileName, Path.Combine(dest, fileName), true);
				}

				done = true;
			});


			Application.Current.Dispatcher.Invoke(() =>
			{
				if (dest is not null && File.Exists(fileName) && !defaultFolder)
					File.Delete(fileName);

				if (!silent)
					this.Close();
			});
		}

		public async Task StartDL()
		{

			using (var client = new UpdaterWebClient())
			{
				CancellationToken token = tokenSource.Token;
				client.DownloadProgressChanged += WebClient_DownloadProgressChanged;
				client.DownloadFileCompleted += WebClient_DownloadFileCompleted;

				bool retry = false;

				if (silent)
					Hide();

				do
				{
					try
					{
						await Task.Run(() => client.DownloadFileTaskAsync(this.uri, fileName));
					}
					catch (AggregateException ae)
					{
						// Handle the exception
						ae.Handle(ex =>
						{
							string s = Lang.GetString("MessageWindow.Errors.GenericDLFail0") + this.fileName + "\n" + ex.Message + "\n\n" + Lang.GetString("Lang.GetString(\"MessageWindow.Errors.GenericDLFail1");

							var error = new MessageWindow(Lang.GetString("MessageWindow.Errors.GenericDLFail.Title"), s, MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Error, MessageWindow.Buttons.RetryCancel);
							error.ShowDialog();
							retry = error.isRetry;
							return true;
						});
					}
				} while (retry == true);

			}
		}
	}
}
