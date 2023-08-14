﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using System.Diagnostics;
using SAModManager.Properties;
using System.Windows.Media;
using System.Threading.Tasks;
using SAModManager.Common;
using System.ComponentModel;
using System.Windows.Data;
using System.Security.Cryptography;
using System.Threading;
using Keyboard = System.Windows.Input.Keyboard;
using Key = System.Windows.Input.Key;
using SAModManager.UI;
using SAModManager.Updater;
using SAModManager.Elements;
using SevenZipExtractor;
using SAModManager.Ini;
using SAModManager.IniSettings;
using ICSharpCode.AvalonEdit.Editing;
using SAModManager.IniSettings.SA2;
using SAModManager.GameConfigs;
using System.Reflection;

namespace SAModManager
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>

	public partial class MainWindow : Window
	{
		#region Variables
		// Global Window Strings
		public readonly string titleName = "SA Mod Manager";
		private readonly string Version = App.VersionString;
		private static string updatePath = "mods/.updates";
		string codelstpath = "mods/Codes.lst";
		string codexmlpath = "mods/Codes.xml";
		string codedatpath = "mods/Codes.dat";
		string patchdatpath = "mods/Patches.dat";

		// Shared Variables
		public SetGame setGame = SetGame.SADX;
		CodeList mainCodes = null;
		List<Code> codes = null;
		public List<CodeData> codesSearch { get; set; }
		bool suppressEvent = false;
		private bool manualModUpdate;
		readonly Updater.ModUpdater modUpdater = new();
		private object gameConfigFile;
		private DebugSettings gameDebugSettings;
		MenuItem ModContextDev { get; set; }
		private bool displayedManifestWarning;
		public MainWindowViewModel ViewModel = new();
		Dictionary<string, string> GameProfiles = new();
		object GameProfile;

		// TODO: Cleanup the Game Variables and turn type'd variables to objects and cast as needed. Additionally, move anything we can to Game class.

		// SADX Related Variables
		public static string loaderinipath = "mods/SADXModLoader.ini";
		public IniSettings.SADX.GameSettings SADXSettings;
		public Dictionary<string, SADXModInfo> mods = null;
		SADXLoaderInfo loaderini;

		// SA2 Related Variables
		private string SA2SettingsPath;
		public IniSettings.SA2.GameSettings SA2Settings;
		#endregion

		public MainWindow()
		{
			InitializeComponent();

			LoadSettings();
			InitCodes();
			LoadModList();

			UpdateDLLData();
			SetOneClickBtnState();
		}

		#region Form: Functions
		private void MainWindowManager_ContentRendered(object sender, EventArgs e)
		{
			this.Resources.MergedDictionaries.Clear(); //this is very important to get Theme and Language swap to work on MainWindow
		}

		private async void MainWindowManager_Loaded(object sender, RoutedEventArgs e)
		{

			SetModManagerVersion();

			if (!Directory.Exists(App.CurrentGame.modDirectory) || App.CurrentGame == null)
			{
				UI_ToggleGameConfig(false);
				return;
			}

			new OneClickInstall(updatePath, App.CurrentGame.modDirectory);

			SetGameUI();
			SetBindings();

            if (await App.PerformUpdateManagerCheck() || await App.PerformUpdateLoaderCodesCheck())
            {
                return;
            }

            await CheckForModUpdates();
		}

		private void MainForm_FormClosing(object sender, EventArgs e)
		{
			App.UriQueue.Close();
			Save_AppUserSettings();
		}

		private void SaveButton_Click(object sender, RoutedEventArgs e)
		{
			Save();
		}

		private void SaveAndPlayButton_Click(object sender, RoutedEventArgs e)
		{
			Save();
			StartGame();
		}

		#region Form: Mods Tab Functions
		private void btnMoveTop_Click(object sender, RoutedEventArgs e)
		{
			if (listMods.SelectedItems.Count < 1)
				return;

			int index = listMods.SelectedIndex;

			if (index > 0)
			{
				var item = ViewModel.Modsdata[index];
				ViewModel.Modsdata.Remove(item);
				ViewModel.Modsdata.Insert(0, item);
			}
		}

		private void btnMoveUp_Click(object sender, RoutedEventArgs e)
		{
			if (listMods.SelectedItems.Count < 1)
				return;

			int index = listMods.SelectedIndex;

			if (index > 0)
			{
				var item = ViewModel.Modsdata[index];
				ViewModel.Modsdata.Remove(item);
				ViewModel.Modsdata.Insert(index - 1, item);
			}
		}

		private void btnMoveBottom_Click(object sender, RoutedEventArgs e)
		{
			if (listMods.SelectedItems.Count < 1)
				return;

			int index = listMods.SelectedIndex;

			if (index != listMods.Items.Count - 1)
			{
				var item = ViewModel.Modsdata[index];
				ViewModel.Modsdata.Remove(item);
				ViewModel.Modsdata.Insert(listMods.Items.Count, item);
			}
		}

		private void btnMoveDown_Click(object sender, RoutedEventArgs e)
		{
			if (listMods.SelectedItems.Count < 1)
				return;

			int index = listMods.SelectedIndex;

			if (index < listMods.Items.Count)
			{
				var item = ViewModel.Modsdata[index];
				ViewModel.Modsdata.Remove(item);
				ViewModel.Modsdata.Insert(index + 1, item);
			}
		}

		private async void btnSelectAll_Click(object sender, RoutedEventArgs e)
		{
			foreach (ModData mod in listMods.Items)
			{
				mod.IsChecked = true;
			}

			RefreshModList();
			btnSelectAll.IsEnabled = false;
			await Task.Delay(150);
			btnSelectAll.IsEnabled = true;
		}

		private async void btnDeselectAll_Click(object sender, RoutedEventArgs e)
		{
			foreach (ModData mod in listMods.Items)
			{
				mod.IsChecked = false;
			}

			RefreshModList();
			btnDeselectAll.IsEnabled = false;
			await Task.Delay(150);
			btnDeselectAll.IsEnabled = true;
		}

		private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
		{
			Refresh();
			RefreshBtn.IsEnabled = false;
			await Task.Delay(150);
			RefreshBtn.IsEnabled = true;
		}

		private void NewModBtn_Click(object sender, RoutedEventArgs e)
		{
			var form = new InstallModOptions();
			var choice = form.Ask();

			switch (choice)
			{
				case (int)InstallModOptions.Type.ModArchive:
					var archiveFile = new System.Windows.Forms.OpenFileDialog
					{
						Multiselect = true,

						Filter = "archive files|*.zip;*.7z;*.rar;*.tar"
					};
					System.Windows.Forms.DialogResult result_ = archiveFile.ShowDialog();

					if (result_ == System.Windows.Forms.DialogResult.OK)
					{
						string[] sFileName = archiveFile.FileNames;
						form.InstallMod(sFileName, App.CurrentGame.modDirectory);

					}
					break;
				case (int)InstallModOptions.Type.ModFolder:
					var newModFolder = new System.Windows.Forms.FolderBrowserDialog();

					System.Windows.Forms.DialogResult result = newModFolder.ShowDialog();

					if (result == System.Windows.Forms.DialogResult.OK)
					{
						string[] FileName = { newModFolder.SelectedPath };

						form.InstallMod(FileName, App.CurrentGame.modDirectory);
					}
					break;
				case (int)InstallModOptions.Type.NewMod: //create mod
					EditMod Edit = new(null);
					Edit.ShowDialog();
					Edit.Closed += EditMod_FormClosing;

					break;
			}

		}

		private void ConfigureModBtn_Click(object sender, RoutedEventArgs e)
		{
			InitModConfig();
		}

		private void ModContextConfigureMod_Click(object sender, RoutedEventArgs e)
		{
			if (ConfigureModBtn.IsEnabled)
				InitModConfig();
		}

		private void modChecked_Click(object sender, RoutedEventArgs e)
		{
			if (suppressEvent)
				return;

			codes = new List<Code>(mainCodes.Codes);
			codesSearch = new();
			string modDir = Path.Combine(App.CurrentGame.gameDirectory, "mods");
			List<string> modlistCopy = new();

			//backup mod list here
			foreach (ModData mod in listMods.Items)
			{
				if (mod?.IsChecked == true)
				{
					modlistCopy.Add(mod.Tag);
				}
			}

			if (sender is CheckBox check)
			{
				var curItem = check.DataContext as ModData;
				var index = listMods.Items.IndexOf(curItem);

				var curModCopy = listMods.Items[index] as ModData;
				if (check.IsChecked == false)
				{
					modlistCopy.Remove((string)curModCopy.Tag);
				}
			}

			foreach (string mod in modlistCopy)
			{
				if (mods.TryGetValue(mod, out SADXModInfo value))
				{
					SADXModInfo inf = value;
					if (!string.IsNullOrEmpty(inf.Codes))
						codes.AddRange(CodeList.Load(Path.Combine(Path.Combine(modDir, mod), inf.Codes)).Codes);
				}
			}


			loaderini.EnabledCodes = new List<string>(loaderini.EnabledCodes.Where(a => codes.Any(c => c.Name == a)));

			foreach (Code item in codes.Where(a => a.Required && !loaderini.EnabledCodes.Contains(a.Name)))
				loaderini.EnabledCodes.Add(item.Name);

			CodeListView.BeginInit();
			CodeListView.Items.Clear();

			foreach (Code item in codes)
			{
				CodeData extraItem = new()
				{
					codes = item,
					IsChecked = loaderini.EnabledCodes.Contains(item.Name),
				};

				CodeListView.Items.Add(extraItem);
				codesSearch.Add(extraItem);
			}

			CodeListView.EndInit();
		}

		private void EditMod_FormClosing(object sender, EventArgs e)
		{
			Refresh();
		}

		#region Form: Mods Tab: Context Menu
		private void ModsView_Item_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (!(GetModFromView(sender) is ModData mod))
				return;

			OpenAboutModWindow(mod);
		}

		private void ModList_MouseEnter(object sender, MouseEventArgs e)
		{
			if (!(GetModFromView(sender) is ModData mod))
				return;

			textModsDescription.Text = Lang.GetString("CommonStrings.Description") + " " + mods[mod.Tag].Description;
		}

		private void ModList_MouseLeave(object sender, MouseEventArgs e)
		{
			var item = GetModFromView(sender);
			textModsDescription.Text = (item is not null) ? $"{Lang.GetString("CommonStrings.Description")} {item.Description}" : Lang.GetString("Manager.Tabs.Mods.Text.NoModSelected");
		}

		private void modListView_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
		{
			int count = listMods.SelectedItems.Count;

			if (count == 0)
			{
				btnMoveTop.IsEnabled = btnMoveUp.IsEnabled = btnMoveDown.IsEnabled = btnMoveBottom.IsEnabled = ConfigureModBtn.IsEnabled = false;
			}
			else if (count == 1)
			{
				var mod = GetModFromView(sender);

				btnMoveTop.IsEnabled = listMods.Items.IndexOf(mod) != 0;
				btnMoveUp.IsEnabled = listMods.Items.IndexOf(mod) > 0;
				btnMoveDown.IsEnabled = listMods.Items.IndexOf(mod) < listMods.Items.Count - 1;
				btnMoveBottom.IsEnabled = listMods.Items.IndexOf(mod) != listMods.Items.Count - 1;

				ConfigureModBtn.IsEnabled = File.Exists(Path.Combine(App.CurrentGame.modDirectory, mod.Tag, "configschema.xml"));
				ConfigureModBtn_UpdateState();
			}
			else if (count > 1)
			{
				btnMoveTop.IsEnabled = btnMoveUp.IsEnabled = btnMoveDown.IsEnabled = btnMoveBottom.IsEnabled = true;
				ConfigureModBtn.IsEnabled = false;
			}
		}
		
		private void ModList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			bool isEnabled = ConfigureModBtn.IsEnabled;

			if (ModContextMenu is not null)
			{
				var item = ModContextMenu.Items.OfType<MenuItem>().FirstOrDefault(item => item.Name == "ModContextConfigureMod");

				if (item is not null)
				{
					UIHelper.ToggleMenuItem(ref item, isEnabled);
					Image iconConfig = FindName("menuIconConfig") as Image;
					UIHelper.ToggleImage(ref menuIconConfig, isEnabled);
				}

				if (checkDevEnabled.IsChecked == true)
				{
					if (ModContextDev is null)
					{
						/*
						ModContextDev = new();
						MenuItem manifest = new();
						ModContextDev.Name = "menuDev";
						ModContextDev.Header = Lang.GetString("ModsUIDev");
						manifest.Name = "menuManif";
						manifest.Header = Lang.GetString("ModsUISubDevManifest");
						ModContextDev.Items.Add(manifest);
						ModContextMenu.Items.Add(ModContextDev);
						*/
					}
				}
				else
				{
					if (ModContextDev is not null)
					{
						var modDev = ModContextMenu.Items.OfType<MenuItem>().FirstOrDefault(item => item.Name == "menuDev");

						if (modDev is not null)
							ModContextMenu.Items.Remove(modDev);
					}

				}

			}
		}

		private void ModsContextDeveloperManifest_Click(object sender, RoutedEventArgs e)
		{
			if (!displayedManifestWarning)
			{
				var result = new MessageWindow(Lang.GetString("Warning"), Lang.GetString("MessageWindow.Warnings.GenerateManifest"), MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Warning, MessageWindow.Buttons.YesNo);
				result.ShowDialog();
				if (result.isYes != true)
				{
					return;
				}

				displayedManifestWarning = true;
			}

			foreach (ModData item in listMods.SelectedItems)
			{

				var modPath = Path.Combine(App.CurrentGame.modDirectory, (string)item.Tag);
				var manifestPath = Path.Combine(modPath, "mod.manifest");

				List<Updater.ModManifestEntry> manifest;
				List<Updater.ModManifestDiff> diff;

				var progress = new Updater.ManifestDialog(modPath, $"Generating manifest: {(string)item.Tag}", true);
				progress.ShowDialog();

				bool? ManifDialog = progress.DialogResult;

				//progress.SetTask("Generating file index...");
				if (ManifDialog != true)
				{
					continue;
				}

				diff = progress.Diff;

				if (diff == null)
				{
					continue;
				}

				if (diff.Count(x => x.State != Updater.ModManifestState.Unchanged) <= 0)
				{
					continue;
				}

				var dialog = new ManifestChanges(diff);
				dialog.ShowDialog();
				bool? resultManifChange = dialog.DialogResult;

				if (resultManifChange != true)
				{
					continue;
				}

				manifest = dialog.MakeNewManifest();

				Updater.ModManifest.ToFile(manifest, manifestPath);
			}
		}

		private void ModContextOpenFolder_Click(object sender, RoutedEventArgs e)
		{
			var selectedMods = listMods.SelectedItems.OfType<ModData>();

			foreach (var mod in selectedMods.Where(mod => !string.IsNullOrEmpty(mod.Tag)))
			{
				string fullPath = Path.Combine(App.CurrentGame.modDirectory, mod.Tag);
				if (Directory.Exists(fullPath))
					Process.Start(new ProcessStartInfo { FileName = fullPath, UseShellExecute = true });
			}
		}

		private async void ModContextChkUpdate_Click(object sender, RoutedEventArgs e)
		{
			await UpdateSelectedMods();
		}

		private async void ModContextVerifyIntegrity_Click(object sender, RoutedEventArgs e)
		{

			List<Tuple<string, ModInfo>> items = listMods.SelectedItems.Cast<ModData>()
				.Select(x => (string)x.Tag)
				.Where(x => File.Exists(Path.Combine(App.CurrentGame.modDirectory, x, "mod.manifest")))
				.Select(x => new Tuple<string, ModInfo>(x, mods[x]))
				.ToList();

			if (items.Count < 1)
			{
				new MessageWindow(Lang.GetString("MessageWindow.Errors.MissingManifest.Title"), Lang.GetString("MessageWindow.Errors.MissingManifest"),
			MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Information).ShowDialog();
				return;
			}

			var progress = new Updater.VerifyModDialog(items, App.CurrentGame.modDirectory);

			progress.ShowDialog();

			bool? res = progress.DialogResult;

			if (res == false)
			{
				return;
			}

			List<Tuple<string, ModInfo, List<Updater.ModManifestDiff>>> failed = progress.Failed;

			if (failed.Count < 1)
			{
				new MessageWindow(Lang.GetString("MessageWindow.Information.ModPassedVerif.Title"), Lang.GetString("MessageWindow.Information.ModPassedVerif"),
					MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Information).ShowDialog();
			}
			else
			{

				var error = Lang.GetString("MessageWindow.Errors.ModFailedVerif0")
						+ string.Join("\n", failed.Select(x => $"{x.Item2.Name}: "
						+ Util.GetFileCountString(x.Item3.Count(y => y.State != Updater.ModManifestState.Unchanged), "MessageWindow.Errors.ModFailedVerif1")))
						+ Lang.GetString("MessageWindow.Errors.ModFailedVerif2");


				var result = new MessageWindow(Lang.GetString("MessageWindow.Errors.ModFailedVerif.Title"), error, MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Information, MessageWindow.Buttons.YesNo);
				result.ShowDialog();

				if (result.isYes != true)
				{
					return;
				}


				await ExecuteModsUpdateCheck();
				await UpdateChecker_DoWorkForced();
				modUpdater.ForceUpdate = true;
				btnCheckUpdates.IsEnabled = false;
			}

		}

		private async void ForceModUpdate_Click(object sender, RoutedEventArgs e)
		{
			var result = new MessageWindow(Lang.GetString("MessageWindow.Warnings.ForceModUpdateTitle"), Lang.GetString("MessageWindow.Warnings.ForceModUpdate"), MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Caution, MessageWindow.Buttons.YesNo);
			result.ShowDialog();

			if (result.isYes)
			{
				modUpdater.ForceUpdate = true;
				await UpdateSelectedMods();
			}
		}

		private void ModContextEditMod_Click(object sender, RoutedEventArgs e)
		{
			var mod = (ModData)listMods.SelectedItem;

			if (mod == null)
				return;

			SADXModInfo modInfo = mods[mod.Tag];
			EditMod Edit = new(modInfo);
			Edit.ShowDialog();
			Edit.Closed += EditMod_FormClosing;
		}

		private void ModContextDeleteMod_Click(object sender, RoutedEventArgs e)
		{
			var selectedItems = listMods.SelectedItems;
			var count = selectedItems.Count > 0;

			if (count)
			{
				var confirmMessage = Lang.GetString("MessageWindow.Warnings.DeleteMod");
				var deleteConfirmation = new MessageWindow(Lang.GetString("MessageWindow.DefaultTitle"), confirmMessage, MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Warning, MessageWindow.Buttons.YesNo);

				deleteConfirmation.ShowDialog();
				if (deleteConfirmation.isYes)
				{
					foreach (var selectedItem in selectedItems)
					{
						var item = (ModData)selectedItem;

						string fullPath = Path.Combine(App.CurrentGame.modDirectory, item.Tag);

						if (Directory.Exists(fullPath))
						{
							Directory.Delete(fullPath, true);
						}
					}

					LoadModList();
				}
			}
		}
		#endregion

		#region Form: Mods Tab: Shortcuts
		private void ModsList_OnPreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (listMods == null)
				return;

			ModData mod = (ModData)listMods.SelectedItem;

			if (mod == null)
				return;

			var ctrlKey = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

			if (Keyboard.IsKeyDown(Key.Space))
			{
				listMods.BeginInit();
				mod.IsChecked = !mod.IsChecked;
				listMods.EndInit();
			}
			else if (ctrlKey)
			{

				if (Keyboard.IsKeyDown(Key.E))
					ModContextEditMod_Click(null, null);
				else if (Keyboard.IsKeyDown(Key.O))
					ModContextOpenFolder_Click(null, null);
				else if (Keyboard.IsKeyDown(Key.C))
					ModContextConfigureMod_Click(null, null);
				else if (Keyboard.IsKeyDown(Key.U))
					ModContextChkUpdate_Click(null, null);
				else if (Keyboard.IsKeyDown(Key.Enter))
					AboutBtn_Click(null, null);

				e.Handled = true;
			}
			else if (Keyboard.IsKeyDown(Key.Enter))
			{
				OpenAboutModWindow(mod);
				e.Handled = true;
			}


			if (Keyboard.IsKeyDown(Key.Delete))
			{
				ModContextDeleteMod_Click(null, null);
				e.Handled = true;
			}

		}

		private void CodesList_OnPreviewKeyDown(object sender, KeyEventArgs e)
		{
			var code = GetCodeFromView(sender);

			if (code == null)
				return;

			if (Keyboard.IsKeyDown(Key.Space))
			{
				CodeListView.BeginInit();
				code.IsChecked = !code.IsChecked;
				CodeListView.EndInit();
			}


			if (Keyboard.IsKeyDown(Key.Enter))
				OpenAboutCodeWindow(code.codes);
		}

		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{

			var ctrlkey = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

			if (Keyboard.IsKeyDown(Key.F5))
			{
				Refresh();
			}
			else if (ctrlkey)
			{
				if (Keyboard.IsKeyDown(Key.F)) //enable or disable search feature
				{
					if (tcMain.SelectedItem == tabMain)
					{
						if (ModsFind.Visibility == Visibility.Visible)
						{
							ModsFind.Visibility = Visibility.Collapsed;
							FilterMods("");
						}
						else
						{
							ModsFind.Visibility = Visibility.Visible;
							FilterMods(TextBox_ModsSearch.Text.ToLowerInvariant());
							TextBox_ModsSearch.Focus();
						}

					}
					else if (tcMain.SelectedItem == tbCodes)
					{
						if (CodesFind.Visibility == Visibility.Visible)
						{
							CodesFind.Visibility = Visibility.Collapsed;
							FilterCodes("");
						}
						else
						{

							CodesFind.Visibility = Visibility.Visible;
							FilterCodes(TextBox_CodesSearch.Text.ToLowerInvariant());
							TextBox_CodesSearch.Focus();
						}
					}
				}
			}
			if (Keyboard.IsKeyDown(Key.Escape))
			{
				if (tcMain.SelectedItem == tabMain)
				{
					ModsFind.Visibility = Visibility.Collapsed;
					FilterMods("");
				}
				else if (tcMain.SelectedItem == tbCodes)
				{
					CodesFind.Visibility = Visibility.Collapsed;
					FilterCodes("");
				}
			}

		}
		#endregion

		#region Form: Mods Tab: Searchbar
		//more info in the keyboard shortcut functions above.
		private void TextBox_ModsSearch_LostFocus(object sender, RoutedEventArgs e)
		{
			// Close if focus is lost with no text
			if (TextBox_ModsSearch.Text.Length == 0)
				ModsFind.Visibility = Visibility.Collapsed;
		}

		private void TextBox_ModsSearch_TextChanged(object sender, TextChangedEventArgs e)
		{
			FilterMods(TextBox_ModsSearch.Text.ToLowerInvariant());
		}
		#endregion
		#endregion
		
		#region Form: Codes Tab Functions
		private void CodesView_Item_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var code = GetCodeFromView(sender);

			if (code == null)
				return;

			OpenAboutCodeWindow(code.codes);
		}

		private void CodesView_Item_MouseEnter(object sender, MouseEventArgs e)
		{
			var codes = GetCodeFromView(sender);

			if (codes == null)
				return;

			var code = codes.codes;

			CodeAuthorGrid.Text += " " + code.Author;
			CodeDescGrid.Text += " " + code.Description;
			CodeCategoryGrid.Text += " " + code.Category;
		}

		private void CodesView_Item_MouseLeave(object sender, MouseEventArgs e)
		{
			CodeAuthorGrid.Text = Lang.GetString("CommonStrings.Author");
			CodeDescGrid.Text = Lang.GetString("CommonStrings.Description");
			CodeCategoryGrid.Text = Lang.GetString("CommonStrings.Category");
		}

		private void btnSelectAllCode_Click(object sender, RoutedEventArgs e)
		{
			CodeListView.BeginInit();
			foreach (CodeData code in CodeListView.Items)
			{
				code.IsChecked = true;
			}
			CodeListView.EndInit();
		}

		private void btnDeselectAllCode_Click(object sender, RoutedEventArgs e)
		{
			CodeListView.BeginInit();
			foreach (CodeData code in CodeListView.Items)
			{
				code.IsChecked = false;
			}
			CodeListView.EndInit();
		}

		private void btnAddCode_Click(object sender, RoutedEventArgs e)
		{
			Common.NewCode newcodewindow = new Common.NewCode();
			newcodewindow.ShowDialog();
		}

		private void TextBox_CodesSearch_TextChanged(object sender, TextChangedEventArgs e)
		{
			FilterCodes(TextBox_CodesSearch.Text.ToLowerInvariant());
		}

		private void TextBox_CodesSearch_LostFocus(object sender, RoutedEventArgs e)
		{
			if (TextBox_CodesSearch.Text.Length == 0)
				CodesFind.Visibility = Visibility.Collapsed;
		}
		#endregion

		#region Form: Manager Tab: Functions
		private async void btnOneClick_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string execPath = Environment.ProcessPath;

				await Process.Start(new ProcessStartInfo(execPath, "urlhandler")
				{
					UseShellExecute = true,
					Verb = "runas"
				}).WaitForExitAsync();

				Image iconConfig = FindName("GB") as Image;
				UIHelper.ToggleImage(ref iconConfig, false);
				UIHelper.ToggleButton(ref btnOneClick, false);

			}
			catch { }
		}

		//To do, rework this mess to handle multiple games and clean everything.
		private async void btnBrowseGameDir_Click(object sender, RoutedEventArgs e)
		{
			// TODO: Rework to handle any game directory being input.
			var dialog = new System.Windows.Forms.FolderBrowserDialog();

			System.Windows.Forms.DialogResult result = dialog.ShowDialog();

			if (result == System.Windows.Forms.DialogResult.OK)
			{
				string GamePath = dialog.SelectedPath;
				string path = Path.Combine(GamePath, App.CurrentGame.exeName);

				if (File.Exists(path)) //game Path valid 
				{
					textGameDir.Text = GamePath;
					(GameProfile as IniSettings.SADX.GameSettings).GamePath = GamePath;
					SetGamePath();
					UpdatePathsStringsInfo();

					if (File.Exists(loaderinipath))
						loaderini = IniSerializer.Deserialize<SADXLoaderInfo>(loaderinipath);

					await SetLoaderFile();
					await InstallLoader(true);
					await GamesInstall.CheckAndInstallDependencies(App.CurrentGame);
					UpdateGameConfig(SetGame.SADX);
					Save();
				}
				else
				{
					new MessageWindow(Lang.GetString("MessageWindow.Errors.GamePathFailed.Title"), Lang.GetString("MessageWindow.Errors.GamePathFailed"), MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Error, MessageWindow.Buttons.OK).ShowDialog();
				}

				Update_PlayButtonsState();
			}
		}

		private async void btnCheckUpdates_Click(object sender, RoutedEventArgs e)
		{
			btnCheckUpdates.IsEnabled = false;

			if (await App.PerformUpdateManagerCheck() || await App.PerformUpdateLoaderCodesCheck())
			{
				return;
			}

			manualModUpdate = true;
			await CheckForModUpdates(true);
			Refresh();
		}

		private void AboutBtn_Click(object sender, RoutedEventArgs e)
		{
			new AboutManager().ShowDialog();
		}

		private async void btnInstallLoader_Click(object sender, RoutedEventArgs e)
		{
			// TODO: Check the todos within this function
			if (string.IsNullOrEmpty(App.CurrentGame.gameDirectory) || !File.Exists(Path.Combine(App.CurrentGame.gameDirectory, App.CurrentGame.exeName)))
			{
				new MessageWindow(Lang.GetString("MessageWindow.Errors.GamePathNotFound.Title"), Lang.GetString("MessageWindow.Errors.GamePathNotFound"), MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Error, MessageWindow.Buttons.OK).ShowDialog();
				return;
			}

			await InstallLoader(); //ToDo update to be less a mess
			btnBrowseGameDir.IsEnabled = false;

			if (App.CurrentGame.loader.installed)
			{
				await GamesInstall.InstallLoader(App.CurrentGame);
				await GamesInstall.CheckAndInstallDependencies(App.CurrentGame);
				UpdateGameConfig(SetGame.SADX); //To do change with "current selected game" when it's available
			}


			Update_PlayButtonsState();
			btnBrowseGameDir.IsEnabled = true;
			Save();
		}

		// TODO: Both of these now point to the Manager's Repo, but we could make options to open/report Loader related issues.
		// Something to discuss.
		private void btnSource_Click(object sender, RoutedEventArgs e)
		{
			var ps = new ProcessStartInfo("https://github.com/X-Hax/sa-mod-manager")
			{
				UseShellExecute = true,
				Verb = "open"
			};
			Process.Start(ps);
		}

		private void btnReport_Click(object sender, RoutedEventArgs e)
		{
			var ps = new ProcessStartInfo("https://github.com/X-Hax/sa-mod-manager/issues/new")
			{
				UseShellExecute = true,
				Verb = "open"
			};

			Process.Start(ps);
		}

		private void comboThemes_Loaded(object sender, RoutedEventArgs e)
		{
			comboThemes.GetBindingExpression(ComboBox.ItemsSourceProperty).UpdateTarget();
			comboThemes.GetBindingExpression(ComboBox.SelectedItemProperty).UpdateTarget();
		}

		private void comboThemes_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			App.SwitchTheme();
		}

		private void comboLanguage_Loaded(object sender, RoutedEventArgs e)
		{
			comboLanguage.GetBindingExpression(ComboBox.ItemsSourceProperty).UpdateTarget();
			comboLanguage.GetBindingExpression(ComboBox.SelectedItemProperty).UpdateTarget();
		}

		private void comboLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			App.SwitchLanguage();
			UpdateBtnInstallLoader_State();
			FlowDirectionHelper.UpdateFlowDirection();
		}

		private void btnProfileSettings_Click(object sender, RoutedEventArgs e)
		{
			if (!App.CurrentGame.loader.installed)
				return;

			new ModProfile(ref comboProfile).ShowDialog();
		}

		private void ModProfile_FormClosing(object sender, EventArgs e)
		{
			Refresh();
		}

		private void comboProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var selectedItem = comboProfile.SelectedItem as Profile;

			if (selectedItem != null)
			{
				loaderini = IniSerializer.Deserialize<SADXLoaderInfo>(selectedItem.iniPath);
				LoadSettings();
				Refresh();
			}
		}
		#endregion
		#endregion

		#region Private Functions
		private void StartGame()
		{
			if (string.IsNullOrEmpty(App.CurrentGame.gameDirectory))
			{
				new MessageWindow(Lang.GetString("MessageWindow.Errors.GamePathNotFound.Title"), Lang.GetString("MessageWindow.Errors.GamePathNotFound"), MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Error, MessageWindow.Buttons.OK).ShowDialog();
				return;
			}

			string executablePath = loaderini.Mods.Select(item => mods[item].EXEFile).FirstOrDefault(item => !string.IsNullOrEmpty(item)) ?? Path.Combine(App.CurrentGame.gameDirectory, App.CurrentGame.exeName);

			string folderPath = Path.GetDirectoryName(executablePath);
			Process.Start(new ProcessStartInfo(executablePath)
			{
				WorkingDirectory = folderPath
			});

			if ((bool)!checkManagerOpen.IsChecked)
				Close();
		}

		public void SetModManagerVersion()
		{
			if (string.IsNullOrEmpty(App.RepoCommit)) //dev
				Title = titleName + " " + "(Dev Build - " + Version + ")";
			else
				Title = titleName + " " + "(" + Version + " - " + App.RepoCommit[..7] + ")";
		}

		private void SetGameUI()
		{
			Grid stackPanel;
			Grid tsPanel;
			switch (setGame)
			{
				case SetGame.SADX:
					tabGame.Visibility = Visibility.Visible;
					gameDebugSettings = (GameProfile as IniSettings.SADX.GameSettings).DebugSettings;
					stackPanel = (Grid)tabGame.Content;
					stackPanel.Children.Add(new Elements.SADX.GameConfig(ref GameProfile, ref gameConfigFile));
					tsPanel = (Grid)tabTestSpawn.Content;
					tsPanel.Children.Add(new Elements.SADX.TestSpawn(GameProfile, mods));
					break;
				case SetGame.SA2:
				default:
					tabGame.Visibility = Visibility.Collapsed;
					break;
			}
		}

		private void UI_ToggleGameConfig(bool value)
		{
			tabGame.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
		}

		private void UpdatePathsStringsInfo()
		{
            if (!string.IsNullOrEmpty(App.CurrentGame.gameDirectory) && File.Exists(Path.Combine(App.CurrentGame.gameDirectory, App.CurrentGame.exeName)))
			{

				App.CurrentGame.modDirectory = Path.Combine(App.CurrentGame.gameDirectory, "mods");

				loaderinipath = Path.Combine(App.CurrentGame.gameDirectory, "mods/SADXModLoader.ini");
                App.CurrentGame.loader.dataDllOriginPath = Path.Combine(App.CurrentGame.gameDirectory, "system/CHRMODELS_orig.dll");
                App.CurrentGame.loader.loaderdllpath = Path.Combine(App.CurrentGame.gameDirectory, "mods/SADXModLoader.dll");
                App.CurrentGame.loader.dataDllPath = Path.Combine(App.CurrentGame.gameDirectory, "system/CHRMODELS.dll");
				updatePath = Path.Combine(App.CurrentGame.gameDirectory, "mods/.updates");

				codelstpath = Path.Combine(App.CurrentGame.gameDirectory, "mods/Codes.lst");
				codexmlpath = Path.Combine(App.CurrentGame.gameDirectory, "mods/Codes.xml");
				codedatpath = Path.Combine(App.CurrentGame.gameDirectory, "mods/Codes.dat");
				patchdatpath = Path.Combine(App.CurrentGame.gameDirectory, "mods/Patches.dat");

				Elements.SADX.GameConfig.UpdateD3D8Paths();

			}
			else
			{
				App.CurrentGame.gameDirectory = string.Empty;
			}

			App.CurrentGame.loader.installed = File.Exists(App.CurrentGame.loader.dataDllOriginPath);
			UpdateBtnInstallLoader_State();
			loaderini = File.Exists(loaderinipath) ? IniSerializer.Deserialize<SADXLoaderInfo>(loaderinipath) : new SADXLoaderInfo();
			Update_PlayButtonsState();
		}

		#region Private: Load & Save
		private void LoadGameProfile()
		{
			string defaultProfile = Path.Combine(App.CurrentGame.ProfilesDirectory, "default.ini");
			switch ((SetGame)App.configIni.GameManagement.CurrentSetGame)
			{
				case SetGame.SADX:
					GameProfile = File.Exists(defaultProfile) ? IniSerializer.Deserialize<IniSettings.SADX.GameSettings>(defaultProfile) : new();
					break;
				case SetGame.SA2:
					GameProfile = File.Exists(defaultProfile) ? IniSerializer.Deserialize<IniSettings.SA2.GameSettings>(defaultProfile) : new();
					break;
			}

			if (File.Exists(defaultProfile))
				SetProfileInComboBox();
		}

		private void SaveGameProfile()
		{
			if (!Directory.Exists(App.CurrentGame.ProfilesDirectory))
				Directory.CreateDirectory(App.CurrentGame.ProfilesDirectory);

			switch ((SetGame)App.configIni.GameManagement.CurrentSetGame)
			{
				case SetGame.SADX:
					IniSerializer.Serialize(GameProfile, GetSelectedProfile());
					break;
				case SetGame.SA2:
					IniSerializer.Serialize(GameProfile, GetSelectedProfile());
					break;

			}
		}

		private void LoadGameConfigFile()
		{
			// TODO: Properly update this for loading SA2's config file.
			List<string> gameConfig = new();
			foreach (string file in App.CurrentGame.GameConfigFile)
			{
				gameConfig.Add(Path.Combine(App.CurrentGame.gameDirectory, file));
			}

			switch (setGame)
			{
				case SetGame.SADX:
					gameConfigFile = File.Exists(gameConfig[0]) ? IniSerializer.Deserialize<GameConfigs.SADXConfigFile>(gameConfig[0]) : new SADXConfigFile();
					break;
				case SetGame.SA2:
					break;
			}
		}

		private void SaveGameConfig(string gamePath)
		{
			// TODO: Properly update this for loading SA2's config file.
			List<string> gameConfig = new();
			foreach (string file in App.CurrentGame.GameConfigFile)
			{
				gameConfig.Add(Path.Combine(App.CurrentGame.gameDirectory, file));
			}

			switch (setGame)
			{
				case SetGame.SADX:
					IniSerializer.Serialize(gameConfigFile, Path.Combine(gameConfig[0]));
					break;
				case SetGame.SA2:
					break;
			}
		}

		private void LoadCodes()
		{
			codes = new List<Code>(mainCodes.Codes);
			codesSearch = new();

			loaderini.EnabledCodes = new List<string>(loaderini.EnabledCodes.Where(a => codes.Any(c => c.Name == a)));

			CodeListView.BeginInit();
			CodeListView.Items.Clear();

			foreach (Code item in codes.Where(a => a.Required && !loaderini.EnabledCodes.Contains(a.Name)))
				loaderini.EnabledCodes.Add(item.Name);

			foreach (Code item in codes)
			{
				CodeData extraItem = new()
				{
					codes = item,
					IsChecked = loaderini.EnabledCodes.Contains(item.Name),
				};

				codesSearch.Add(extraItem);
				CodeListView.Items.Add(extraItem);
			}

			CodeListView.EndInit();
		}

		private void SaveCodes()
		{
			List<Code> selectedCodes = new List<Code>();
			List<Code> selectedPatches = new List<Code>();

			foreach (CodeData code in CodeListView.Items)
			{
				var Code = code.codes;

				if (code?.IsChecked == true)
				{

					if (Code.Patch)
						selectedPatches.Add(Code);
					else
						selectedCodes.Add(Code);

					if (!loaderini.EnabledCodes.Contains(Code.Name))
						loaderini.EnabledCodes.Add(Code.Name);
				}
				else
				{
					if (loaderini.EnabledCodes.Contains(Code.Name))
						loaderini.EnabledCodes.Remove(Code.Name);
				}
			}

			CodeList.WriteDatFile(patchdatpath, selectedPatches);
			CodeList.WriteDatFile(codedatpath, selectedCodes);

		}

		private void LoadSettings()
		{
			LoadGameProfile();
			SetGamePath();
			UpdatePathsStringsInfo();

			LoadGameConfigFile();

			textGameDir.Text = App.CurrentGame.gameDirectory;
		}

		public async void Save()
		{
			// Save Manager Settings
			IniSerializer.Serialize(App.configIni, App.ConfigPath);

			if (!Directory.Exists(App.CurrentGame.modDirectory))
				return;

			// Save Enabled Mods
			loaderini.Mods.Clear();
			foreach (ModData mod in ViewModel.Modsdata)
			{
				if (mod?.IsChecked == true)
				{
					loaderini.Mods.Add(mod.Tag);
				}
			}

			// Save Non-Bound Game Settings
			switch (setGame)
			{
				case SetGame.SADX:
					(GameProfile as IniSettings.SADX.GameSettings).GamePath = App.CurrentGame.gameDirectory;
					Elements.SADX.GameConfig gameConfig = (Elements.SADX.GameConfig)(tabGame.Content as Grid).Children[0];
					gameConfig.SavePatches(ref GameProfile);
					Elements.SADX.TestSpawn spawnConfig = (Elements.SADX.TestSpawn)(tabTestSpawn.Content as Grid).Children[0];
					spawnConfig.Save();
					break;
			}

			SaveCodes();

			// TODO: Proper function for saving to the original location. Regardless of updating the Loaders to use the new settings,
			// saving to this location is useful for the Loaders.
			loaderini.ConvertFromV1(App.configIni, GameProfile as IniSettings.SADX.GameSettings);
			IniSerializer.Serialize(loaderini, loaderinipath);

			// Save Game Config file (Game used settings file)
			SaveGameConfig(App.CurrentGame.gameDirectory);

			// Save the Game Profile.
			SaveGameProfile();

			await Task.Delay(200);

			Refresh();
		}

		private void LoadModList()
		{
			btnMoveTop.IsEnabled = btnMoveUp.IsEnabled = btnMoveDown.IsEnabled = btnMoveBottom.IsEnabled = ConfigureModBtn.IsEnabled = false;
			ViewModel.Modsdata.Clear();
			mods = new Dictionary<string, SADXModInfo>();

			bool modFolderExist = Directory.Exists(App.CurrentGame.modDirectory);

			//if mod folder doesn't exist and game path hasn't been set, give up the process of loading mods.

			if (!modFolderExist && string.IsNullOrEmpty(App.CurrentGame.gameDirectory))
			{
				UpdateMainButtonsState();
				return;
			}
			else if (Directory.Exists(App.CurrentGame.gameDirectory) && !modFolderExist)
			{
				Directory.CreateDirectory(App.CurrentGame.modDirectory);
			}

			if (File.Exists(Path.Combine(App.CurrentGame.modDirectory, "mod.ini")))
			{
				new MessageWindow(Lang.GetString("MessageWindow.DefaultTitle.Error"), Lang.GetString("MessageWindow.Errors.ModWithoutFolder0") + Lang.GetString("MessageWindow.Errors.ModWithoutFolder1") +
							Lang.GetString("MessageWindow.Errors.ModWithoutFolder2"), MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Error,
							MessageWindow.Buttons.OK).ShowDialog();

				Close();
				return;
			}

			//browse the mods folder and get each mod name by their ini file
			foreach (string filename in SADXModInfo.GetModFiles(new DirectoryInfo(App.CurrentGame.modDirectory)))
			{
				SADXModInfo mod = IniSerializer.Deserialize<SADXModInfo>(filename);
				mods.Add((Path.GetDirectoryName(filename) ?? string.Empty).Substring(App.CurrentGame.modDirectory.Length + 1), mod);
			}


			foreach (string mod in new List<string>(loaderini.Mods))
			{
				if (mods.ContainsKey(mod))
				{
					SADXModInfo inf = mods[mod];
					suppressEvent = true;
					var item = new ModData()
					{
						Name = inf.Name,
						Author = inf.Author,
						AuthorURL = inf.AuthorURL,
						Description = inf.Description,
						Version = inf.Version,
						Category = inf.Category,
						SourceCode = inf.SourceCode,
						IsChecked = true,
						Tag = mod
					};

					ViewModel.Modsdata.Add(item);

					//if a mod has a code, add it to the list
					if (!string.IsNullOrEmpty(inf.Codes))
					{
						var t = CodeList.Load(Path.Combine(Path.Combine(App.CurrentGame.modDirectory, mod), inf.Codes));
						codes.AddRange(t.Codes);

						foreach (var code in t.Codes)
						{
							CodeData extraItem = new()
							{
								codes = code,
								IsChecked = loaderini.EnabledCodes.Contains(item.Name),
							};

							extraItem.codes.Category = "Codes From " + inf.Name;
							codesSearch.Add(extraItem);
							CodeListView.Items.Add(extraItem);
						}
					}

					suppressEvent = false;
				}
				else
				{
					new MessageWindow(Lang.GetString("MessageWindow.DefaultTitle"), "Mod \"" + mod + "\"" + Lang.GetString("MessageWindow.Errors.ModNotFound"), MessageWindow.WindowType.Message, MessageWindow.Icons.Information, MessageWindow.Buttons.OK).ShowDialog();
					loaderini.Mods.Remove(mod);
				}
			}

			foreach (KeyValuePair<string, SADXModInfo> inf in mods.OrderBy(x => x.Value.Name))
			{
				if (!loaderini.Mods.Contains(inf.Key))
				{
					var item = new ModData()
					{
						Name = inf.Value.Name,
						Author = inf.Value.Author,
						AuthorURL = inf.Value.AuthorURL,
						Description = inf.Value.Description,
						Version = inf.Value.Version,
						Category = inf.Value.Category,
						SourceCode = inf.Value.SourceCode,
						IsChecked = false,
						Tag = inf.Key,
					};

					ViewModel.Modsdata.Add(item);
				}
			}


			DataContext = ViewModel;
			ConfigureModBtn_UpdateState();
		}

		private void Save_AppUserSettings()
		{
			Settings.Default.Save();
		}
		#endregion

		#region Private: Update

		private void Update_PlayButtonsState()
		{
			bool isInstalled = App.CurrentGame.loader.installed;
			UIHelper.ToggleButton(ref SaveAndPlayButton, isInstalled);
			Image iconSavePlay = FindName("savePlayIcon") as Image;
			UIHelper.ToggleImage(ref iconSavePlay, isInstalled);
		}

		private void UpdateMainButtonsState()
		{
			ConfigureModBtn_UpdateState();
			AddModBtn_UpdateState();
		}

		private void UpdateChecker_EnableControls()
		{
			btnCheckUpdates.IsEnabled = true;

			ModContextChkUpdate.IsEnabled = true;
			if (ModContextDev is not null)
			{
				ModContextDev.IsEnabled = true;
			}

			ModContextDeleteMod.IsEnabled = true;
			ModContextForceModUpdate.IsEnabled = true;
			ModContextVerifyIntegrity.IsEnabled = true;
		}

		private async Task UpdateSelectedMods()
		{
			manualModUpdate = true;
			modUpdater.updatableMods = listMods.SelectedItems
			.OfType<ModData>()
			.Select(mod => new KeyValuePair<string, ModInfo>(mod.Tag, mods[mod.Tag]))
			.ToList();

			await ExecuteModsUpdateCheck();
		}

		private async Task UpdateChecker_DoWork()
		{

			Dispatcher.Invoke(() =>
			{
				btnCheckUpdates.IsEnabled = false;
				ModContextChkUpdate.IsEnabled = false;
				ModContextDeleteMod.IsEnabled = false;
				if (ModContextDev is not null)
					ModContextDev.IsEnabled = false;
				ModContextForceModUpdate.IsEnabled = false;
				ModContextVerifyIntegrity.IsEnabled = false;
			});

			var tokenSource = new CancellationTokenSource();
			CancellationToken token = tokenSource.Token;
			// out updates, out errors,

			try
			{
				await Task.Run(() => modUpdater.GetModUpdates(App.CurrentGame.modDirectory, token), token);
			}
			catch (OperationCanceledException)
			{
				// Handle cancellation if needed
			}


			modUpdater.modUpdatesTuple = new(modUpdater.modUpdateHelper.updates, modUpdater.modUpdateHelper.errors);
		}

		private async Task UpdateChecker_DoWorkForced()
		{

			if (modUpdater.modUpdatesTuple is null)
			{
				return;
			}

			var updatableMods = modUpdater.modManifestTuple;

			var updates = new List<ModDownloadWPF>();
			var errors = new List<string>();

			using (var client = new UpdaterWebClient())
			{
				foreach (Tuple<string, ModInfo, List<Updater.ModManifestDiff>> info in updatableMods)
				{
					/*if (worker.CancellationPending)
					{
						e.Cancel = true;
						break;
					}*/

					ModInfo mod = info.Item2;
					if (!string.IsNullOrEmpty(mod.GitHubRepo))
					{
						if (string.IsNullOrEmpty(mod.GitHubAsset))
						{
							errors.Add($"[{mod.Name}] GitHubRepo specified, but GitHubAsset is missing.");
							continue;
						}

						ModDownloadWPF d = await modUpdater.GetGitHubReleases(mod, App.CurrentGame.modDirectory, info.Item1, client, errors);
						if (d != null)
						{
							updates.Add(d);
						}
					}
					else if (!string.IsNullOrEmpty(mod.GameBananaItemType) && mod.GameBananaItemId.HasValue)
					{
						ModDownloadWPF d = await modUpdater.GetGameBananaReleases(mod, App.CurrentGame.modDirectory, info.Item1, errors);
						if (d != null)
						{
							updates.Add(d);
						}
					}
					else if (!string.IsNullOrEmpty(mod.UpdateUrl))
					{
						List<Updater.ModManifestEntry> localManifest = info.Item3
							.Where(x => x.State == Updater.ModManifestState.Unchanged)
							.Select(x => x.Current)
							.ToList();

						ModDownloadWPF d = await modUpdater.CheckModularVersion(mod, App.CurrentGame.modDirectory, info.Item1, localManifest, client, errors);
						if (d != null)
						{
							updates.Add(d);
						}
					}
				}
			}

			modUpdater.modUpdatesTuple = new Tuple<List<ModDownloadWPF>, List<string>>(updates, errors);
		}

		private async Task ExecuteModsUpdateCheck()
		{
			await UpdateChecker_DoWork();
			await UpdateChecker_RunWorkerCompleted();
			UpdateChecker_EnableControls();
		}

		private async Task UpdateChecker_RunWorkerCompleted()
		{
			if (modUpdater.ForceUpdate)
			{
				modUpdater.ForceUpdate = false;
				modUpdater.Clear();
			}


			if (modUpdater.modUpdatesTuple is not Tuple<List<ModDownloadWPF>, List<string>> data)
			{
				return;
			}

			List<string> Errors = data.Item2;
			if (Errors.Count != 0)
			{
				string msgError = Lang.GetString("MessageWindow.Errors.CheckUpdateError");
				string title = Lang.GetString("MessageWindow.Errors.CheckUpdateError.Title");

				foreach (var error in Errors)
				{
					msgError += "\n\n" + error;
				}

                if (msgError.Contains("403"))
                {
                    title = "GitHub Rate Limit Exceeded";
                }

                new MessageWindow(title, msgError, MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Error, MessageWindow.Buttons.OK).ShowDialog();
			}

			bool manual = manualModUpdate;
			manualModUpdate = false;

			List<ModDownloadWPF> updates = data.Item1;
			if (updates.Count == 0)
			{
				if (manual)
				{
					new MessageWindow(Lang.GetString("MessageWindow.Information.NoAvailableUpdates.Title"), Lang.GetString("MessageWindow.Information.NoAvailableUpdates"), MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Information, MessageWindow.Buttons.OK).ShowDialog();
				}
				return;
			}

			var dialog = new ModChangelog(updates);
			dialog.ShowDialog();

			bool? res = dialog.DialogResult;
			if (res != true)
			{
				return;
			}

			updates = dialog.SelectedMods;


			if (updates.Count == 0)
			{
				return;
			}

			try
			{
				if (!Directory.Exists(updatePath))
				{
					Directory.CreateDirectory(updatePath);
				}
			}
			catch (Exception ex)
			{

			}


			new Updater.ModDownloadDialogWPF(updates, updatePath).ShowDialog();


			Refresh();
		}

		private async Task CheckForModUpdates(bool force = false)
		{
			if (!force && !App.configIni.UpdateSettings.EnableModsBootCheck)
			{
				return;
			}

			if (!force && !Updater.UpdateHelper.UpdateTimeElapsed(App.configIni.UpdateSettings.UpdateCheckCount, App.configIni.UpdateSettings.UpdateTimeOutCD))
			{
				UpdateHelper.HandleRefreshUpdateCD();
				IniSerializer.Serialize(App.configIni, App.ConfigPath);
				return;
			}

			modUpdater.updatableMods = mods.Select(x => new KeyValuePair<string, ModInfo>(x.Key, x.Value)).ToList();

            await ExecuteModsUpdateCheck();

            if (!force)
            {
                App.configIni.UpdateSettings.UpdateCheckCount++;
                UpdateHelper.HandleRefreshUpdateCD();
                IniSerializer.Serialize(App.configIni, App.ConfigPath);
            }
		}
		#endregion

		#region Setup Bindings
		private void SetGameDebugBindings()
		{
			checkEnableLogConsole.SetBinding(CheckBox.IsCheckedProperty, new Binding("EnableDebugConsole")
			{
				Source = gameDebugSettings,
				Mode = BindingMode.TwoWay
			});
			checkEnableLogScreen.SetBinding(CheckBox.IsCheckedProperty, new Binding("EnableDebugScreen")
			{
				Source = gameDebugSettings,
				Mode = BindingMode.TwoWay
			});
			checkEnableLogFile.SetBinding(CheckBox.IsCheckedProperty, new Binding("EnableDebugFile")
			{
				Source = gameDebugSettings,
				Mode = BindingMode.TwoWay
			});
			checkEnableCrashDump.SetBinding(CheckBox.IsCheckedProperty, new Binding("EnableDebugCrashLog")
			{
				Source = gameDebugSettings,
				Mode = BindingMode.TwoWay
			});
		}

		private void SetManagerBindings()
		{
			//comboProfile.SetBinding(ComboBox.SelectedIndexProperty, new Binding("ProfileIndex")
			//{
				//Source = App.configIni,
			//});
			comboLanguage.SetBinding(ComboBox.SelectedIndexProperty, new Binding("Language")
			{
				Source = App.configIni
			});
			comboThemes.SetBinding(ComboBox.SelectedIndexProperty, new Binding("Theme")
			{
				Source = App.configIni
			});
			chkUpdatesML.SetBinding(CheckBox.IsCheckedProperty, new Binding("EnableManagerBootCheck")
			{
				Source = App.configIni.UpdateSettings,
			});
			chkUpdatesMods.SetBinding(CheckBox.IsCheckedProperty, new Binding("EnableModsBootCheck")
			{
				Source = App.configIni.UpdateSettings
			});
			checkDevEnabled.SetBinding(CheckBox.IsCheckedProperty, new Binding("EnableDeveloperMode")
			{
				Source = App.configIni
			});
			checkManagerOpen.SetBinding(CheckBox.IsCheckedProperty, new Binding("KeepManagerOpen")
			{
				Source = App.configIni
			});
			tabTestSpawn.SetBinding(TabItem.VisibilityProperty, new Binding("IsChecked")
			{
				Source = checkDevEnabled,
				Mode = BindingMode.OneWay,
				Converter = new VisibilityConverter()
			});
		}

		private void SetBindings()
		{
			SetGameDebugBindings();
			SetManagerBindings();
		}
		#endregion

		#region Private: Mods Tab
		public void FilterMods(string text)
		{
			ViewModel.ModsSearch.Clear();

			foreach (var mod in ViewModel.Modsdata)
			{
				if (mod.Name.ToLowerInvariant().Contains(text) || mod.Author is not null && mod.Author.ToLowerInvariant().Contains(text))
				{
					ViewModel.ModsSearch.Add(mod); // Add filtered items to the ModsSearch collection.
				}
			}

			string path = BindingOperations.GetBinding(listMods, ListView.ItemsSourceProperty).Path.Path;
			string newPath = text.Length == 0 ? "Modsdata" : "ModsSearch";

			if (path != newPath)
			{
				Binding binding = new()
				{
					Path = new PropertyPath(newPath)
				};

				listMods.SetBinding(ListView.ItemsSourceProperty, binding);
				listMods.SetValue(GongSolutions.Wpf.DragDrop.DragDrop.IsDragSourceProperty, text.Length == 0 ? true : false);
				listMods.SetValue(GongSolutions.Wpf.DragDrop.DragDrop.IsDropTargetProperty, text.Length == 0 ? true : false);
			}
		}

		public void Refresh()
		{
			InitCodes();
			LoadModList();
			if (ModsFind.Visibility == Visibility.Visible)
			{
				FilterMods(TextBox_ModsSearch.Text.ToLowerInvariant());
			}
		}

		private void OpenAboutModWindow(ModData mod)
		{
			new AboutMod(mod).ShowDialog();
		}

		private void RefreshModList()
		{
			ICollectionView view = CollectionViewSource.GetDefaultView(listMods.Items);
			view.Refresh();
		}

		private ModData GetModFromView(object sender)
		{
			if (sender is ListViewItem lvItem)
				return lvItem.Content as ModData;
			else if (sender is ListView lv)
				return lv.SelectedItem as ModData;

			return null;
		}

		private void InitModConfig()
		{
			var mod = (ModData)listMods.SelectedItem;

			if (mod is not null)
			{
				SADXModInfo modInfo = mods[mod.Tag];
				string fullPath = Path.Combine(App.CurrentGame.modDirectory, mod.Tag);
				Common.ModConfig config = new(modInfo.Name, fullPath);
				config.ShowDialog();
			}
		}

		private void ConfigureModBtn_UpdateState()
		{
			bool configMod = ConfigureModBtn.IsEnabled;
			//get the config icon image, check if it's not null, then change its oppacity depending if the button is enabled or not.
			Image iconConfig = FindName("configIcon") as Image;
			UIHelper.ToggleImage(ref iconConfig, configMod);
			ConfigureModBtn.Opacity = ConfigureModBtn.IsEnabled ? 1 : 0.3;
		}

		private void AddModBtn_UpdateState()
		{
			bool isInstalled = App.CurrentGame.loader.installed;
			Image iconConfig = FindName("newIcon") as Image;
			UIHelper.ToggleImage(ref iconConfig, isInstalled);
			UIHelper.ToggleImgButton(ref NewModBtn, isInstalled);
		}
		#endregion

		#region Private: Codes Tab
		public void FilterCodes(string text)
		{
			CodeListView.Items.Clear();

			int enabledIndex = 0;

			foreach (CodeData item in codesSearch)
			{
				if (item.codes.Name.ToLowerInvariant().Contains(text) || (item.codes.Author != null && item.codes.Author.ToLowerInvariant().Contains(text)))
				{
					if (item.IsChecked)
						CodeListView.Items.Insert(enabledIndex++, item);
					else
						CodeListView.Items.Add(item);
				}
			}
		}

		private void InitCodes()
		{
			try
			{
				if (File.Exists(codelstpath))
					mainCodes = CodeList.Load(codelstpath);
				else if (File.Exists(codexmlpath))
					mainCodes = CodeList.Load(codexmlpath);
				else
					mainCodes = new CodeList();

				LoadCodes();
			}
			catch (Exception ex)
			{
				string msg = " " + ex.Message;
				new MessageWindow(Lang.GetString("MessageWindow.Errors.CodesListFailed.Title"), Lang.GetString("MessageWindow.Errors.CodesListFailed") + msg, MessageWindow.WindowType.IconMessage, MessageWindow.Icons.Error, MessageWindow.Buttons.OK).ShowDialog();
				mainCodes = new CodeList();
			}
		}

		private void OpenAboutCodeWindow(Code code)
		{
			new AboutCode(code).ShowDialog();
		}

		private CodeData GetCodeFromView(object sender)
		{
			if (sender is ListViewItem lvItem)
				return lvItem.Content as CodeData;
			else if (sender is ListView lv)
				return lv.SelectedItem as CodeData;


			return CodeListView.Items[CodeListView.SelectedIndex] as CodeData;
		}


		#endregion

		#region Private: Manager Config Tab

		private void SetOneClickBtnState()
		{
			try
			{
				using var hkcr = Microsoft.Win32.Registry.ClassesRoot;
				var sammKey = hkcr.OpenSubKey("sadxmm");

				if (sammKey != null)
				{
					var pathManagerKey = sammKey.OpenSubKey("DefaultIcon");
					if (pathManagerKey != null)
					{
						var managerPath = pathManagerKey.GetValue("").ToString();

						if (managerPath.ToLower().Contains(Environment.ProcessPath.ToLower()))
						{
							Image iconConfig = FindName("GB") as Image;
							UIHelper.ToggleImage(ref menuIconConfig, false);
							UIHelper.ToggleButton(ref btnOneClick, false);
						}

						pathManagerKey.Close();
						sammKey.Close();
					}
				}
			}
			catch { }
		}

		private void UpdateBtnInstallLoader_State()
		{
			if (btnInstallLoader is null)
				return;

			//Update Text Button of Mod Loader Installer
			string textKey = App.CurrentGame.loader.installed ? "Manager.Tabs.ManagerConfig.Group.Options.Buttons.UninstallLoader" : "Manager.Tabs.ManagerConfig.Group.Options.Buttons.InstallLoader";
			TextBlock txt = FindName("txtInstallLoader") as TextBlock;
			txt.Text = Lang.GetString(textKey);

			//update icon image depending if the Mod Loader is installed or not
			string iconName = App.CurrentGame.loader.installed ? "IconUninstall" : "IconInstall";
			var dic = App.CurrentGame.loader.installed ? Icons.Icons.UninstallIcon : Icons.Icons.InstallIcon;

			DrawingImage Icon = dic[iconName] as DrawingImage;

			Image imgInstall = FindName("imgInstall") as Image;

			if (imgInstall is not null)
				imgInstall.Source = Icon;
		}

		private void UpdateGameConfig(SetGame game)
		{
			Directory.CreateDirectory(App.CurrentGame.ProfilesDirectory);
			setGame = game; //TO DO get current game somehow
			LoadGameProfile();
			LoadGameConfigFile();
			SetGameUI();

		}

		private void SetProfileInComboBox()
		{
			GameProfiles.Clear();

			GameProfiles.Add("Default", Path.Combine(App.CurrentGame.ProfilesDirectory, "default.ini"));
			foreach (var item in Directory.EnumerateFiles(App.CurrentGame.ProfilesDirectory, "*.ini"))
				if (!item.EndsWith("default.ini", StringComparison.OrdinalIgnoreCase))
					GameProfiles.Add(Path.GetFileNameWithoutExtension(item), item);

			comboProfile.ItemsSource = GameProfiles;
			comboProfile.DisplayMemberPath = "Key";
		}

		private void UpdateDLLData()
		{
			if (File.Exists(App.CurrentGame.loader.loaderdllpath) && File.Exists(App.CurrentGame.loader.dataDllOriginPath))
			{
				File.Copy(App.CurrentGame.loader.loaderdllpath, App.CurrentGame.loader.dataDllPath, true);
			}
		}

		private async Task InstallLoader(bool force = false)
		{

			if (force)
				App.CurrentGame.loader.installed = false;

			if (!File.Exists(App.CurrentGame.loader.loaderdllpath))
			{
				await GamesInstall.InstallLoader(App.CurrentGame);

				if (File.Exists(App.CurrentGame.loader.dataDllOriginPath) && App.CurrentGame.loader.installed)
				{
					File.Copy(App.CurrentGame.loader.loaderdllpath, App.CurrentGame.loader.dataDllPath, true);
					UpdateBtnInstallLoader_State();
					return;
				}
			}

			SaveAndPlayButton.IsEnabled = false;
			// btnTSLaunch.IsEnabled = false;

			if (App.CurrentGame.loader.installed && File.Exists(App.CurrentGame.loader.dataDllOriginPath))
			{
				File.Delete(App.CurrentGame.loader.dataDllPath);
				await Util.MoveFile(App.CurrentGame.loader.dataDllOriginPath, App.CurrentGame.loader.dataDllPath);
			}
			else
			{
				if (!File.Exists(App.CurrentGame.loader.dataDllOriginPath))
				{
					File.Move(App.CurrentGame.loader.dataDllPath, App.CurrentGame.loader.dataDllOriginPath);
					await Util.CopyFileAsync(App.CurrentGame.loader.loaderdllpath, App.CurrentGame.loader.dataDllPath, false);
				}
			}

			await VanillaTransition.HandleVanillaManagerFiles(App.CurrentGame.loader.installed, App.CurrentGame.gameDirectory);
			App.CurrentGame.loader.installed = !App.CurrentGame.loader.installed;
			UpdateBtnInstallLoader_State();
		}

		private async void SetGamePath()
		{
			string path = string.Empty;
			switch (setGame)
			{
				case SetGame.SADX:
					path = (GameProfile as IniSettings.SADX.GameSettings).GamePath;
					break;
				case SetGame.SA2:
					//path = (GameProfile as IniSettings.SA2.GameSettings).GamePath;
					break;
			}
			if (Directory.Exists(path))
			{
				App.CurrentGame.gameDirectory = path;
			}
			else
			{
				if (File.Exists(App.CurrentGame.exeName)) //if current game path is wrong, check if the Mod Manager didn't get put in the game folder just in case.
				{
					App.CurrentGame.gameDirectory = Directory.GetCurrentDirectory();
				}
				else
				{
					//if none of the conditions are respected, try to look for sadx folder
					var fullPath = await Util.GetSADXGamePath();
					if (fullPath is not null)
					{
						App.CurrentGame.gameDirectory = fullPath;
					}
				}
			}
		}

		private async Task SetLoaderFile()
		{
			if (!File.Exists(App.CurrentGame.loader.loaderdllpath))
			{
				if (File.Exists("SADXModLoader.dll"))
				{
					await Util.MoveFile("SADXModLoader.dll", App.CurrentGame.loader.loaderdllpath);
				}
				else
				{
					await GamesInstall.InstallLoader(App.CurrentGame);
				}
			}
		}

		private string GetSelectedProfile()
		{
			if (comboProfile.SelectedIndex > -1 && comboProfile.Items.Count > 0)
			{
                return GameProfiles.Values.ToList()[comboProfile.SelectedIndex];
            }

			return Path.Combine(App.CurrentGame.ProfilesDirectory, "Default.ini");    
		}
		#endregion
		#endregion
	}
}