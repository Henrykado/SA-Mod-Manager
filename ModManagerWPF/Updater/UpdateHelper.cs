﻿using SAModManager.Common;
using System;
using System.IO;
using System.Net;

namespace SAModManager.Updater
{
	public class UpdateHelper
	{
		public static bool UpdateTimeElapsed(UpdateUnit unit, int amount, DateTime start)
		{
			if (unit == UpdateUnit.Always)
			{
				return true;
			}

			TimeSpan span = DateTime.UtcNow - start;

			switch (unit)
			{
				case UpdateUnit.Hours:
					return span.TotalHours >= amount;

				case UpdateUnit.Days:
					return span.TotalDays >= amount;

				case UpdateUnit.Weeks:
					return span.TotalDays / 7.0 >= amount;

				default:
					throw new ArgumentOutOfRangeException(nameof(unit), unit, null);
			}
		}

		//to do change with new method
		public static bool DoModManagerUpdate(string updatePath)
		{

			using (var wc = new WebClient())
			{
				try
				{
					string msg = wc.DownloadString("http://mm.reimuhakurei.net/toolchangelog.php?tool=sadxml&rev=" + File.ReadAllText("sadxmlver.txt"));

					if (msg.Length > 0)
					{
						/*var dlg = new UpdateMessageDialog("SADX", msg.Replace("\n", "\r\n"));
						dlg.ShowDialog();
						bool? dlgRes = dlg.DialogResult;
						
							if (dlgRes == true)
							{
				
								
									try
									{
										if (!Directory.Exists(updatePath))
										{
											Directory.CreateDirectory(updatePath);
										}
									}
									catch (Exception ex)
									{
										//result = MessageBox.Show(this, "Failed to create temporary update directory:\n" + ex.Message
																	   + "\n\nWould you like to retry?", "Directory Creation Failed", MessageBoxButtons.RetryCancel);
										if (result == DialogResult != true) 
											return false;
									}


							var dlg2 = new LoaderDownloadDialog("http://mm.reimuhakurei.net/sadxmods/SADXModLoader.7z", updatePath);
							dgl2.show();		
							if (dlg2.ShowDialog(this) == DialogResult.OK)
									{
										Close();
										return true;
									}
						}*/

					}
				}
				catch
				{
					new MessageWindow(Lang.GetString("MessageWindow.DefaultTitle"), Lang.GetString("MessageWindow.Errors.ManagerUpdateFail"), MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Error).ShowDialog();
				}
			}

			return false;
		}





	}
}
