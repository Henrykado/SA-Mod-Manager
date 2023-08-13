﻿using SAModManager.Languages;
using SAModManager.Themes;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using SAModManager.Common;
using SAModManager.Updater;
using SAModManager.Ini;
using System.Reflection;
using SAModManager.IniSettings;
using System.Diagnostics;

namespace SAModManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    /// 

    public partial class App : Application
    {
        private const string pipeName = "sa-mod-manager";
        private const string protocol = "sadxmm:";
        public static Version Version = Assembly.GetExecutingAssembly().GetName().Version;
        public static string VersionString = $"{Version.Major}.{Version.Minor}.{Version.Revision}";
        public static readonly string ConfigFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SAManager");
        public static readonly string extLibPath = Path.Combine(ConfigFolder, "extlib");
        public static readonly string ConfigPath = Path.Combine(ConfigFolder, "config.ini");
        public static ManagerSettings configIni { get; set; }

        private static readonly Mutex mutex = new(true, pipeName);
        public static Updater.UriQueue UriQueue;
        public static string RepoCommit = SAModManager.Properties.Resources.Version.Trim();

        public static LangEntry CurrentLang { get; set; }
        public static LanguageList LangList { get; set; }

        public static ThemeEntry CurrentTheme { get; set; }
        public static ThemeList ThemeList { get; set; }
        public static Common.Game CurrentGame = new();

        [STAThread]
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// 
        protected override async void OnStartup(StartupEventArgs e)
        {
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            string[] args = Environment.GetCommandLineArgs();

            if (CheckinstallURLHandler(args)) //we check if the program has been launched just to enable One Click Install
            {
                Application.Current.Shutdown(); //we don't want to continue if so
                return;
            }

            bool alreadyRunning;
            try { alreadyRunning = !mutex.WaitOne(0, true); }
            catch (AbandonedMutexException) { alreadyRunning = false; }

            if (await DoUpdate(args, alreadyRunning))
            {
                return;
            }


            SetupLanguages();
            SetupThemes();

            configIni = LoadManagerConfig();
            if (await ExecuteDependenciesCheck() == false)
            {
                return;
            }


            await InitUriAsync(args, alreadyRunning);

            if (alreadyRunning)
            {
                Current.Shutdown();
                return;
            }

            Steam.Init();
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            MainWindow = new MainWindow(args is not null ? args : null);
            base.OnStartup(e);
            MainWindow.Show();


        }

        public static void SetupLanguages()
        {
            var resource = Current.TryFindResource("Languages");

            if (resource is LanguageList langs)
            {
                LangList = langs;
            }
        }

        public static void SwitchLanguage()
        {
            if (LangList is null)
                return;

            string name = "Languages/" + CurrentLang.FileName + ".xaml";
            ResourceDictionary dictionary = new()
            {
                Source = new Uri(name, UriKind.Relative)
            };

            //if a language different than english is set, remove the previous one.
            if (Current.Resources.MergedDictionaries.Count >= 5)
            {
                Current.Resources.MergedDictionaries.RemoveAt(4);
            }

            //if we go back to english, give up the process as it's always in the list.
            if (Current.Resources.MergedDictionaries[3].Source.ToString().Contains("en-EN") && name.Contains("en-EN"))
            {
                return;
            }

            //add new language
            Current.Resources.MergedDictionaries.Insert(4, dictionary);
        }

        public static void SwitchTheme()
        {
            if (ThemeList is null)
                return;

            string name = "Themes/" + CurrentTheme.FileName + ".xaml";
            ResourceDictionary dictionary = new()
            {
                Source = new Uri(name, UriKind.Relative)
            };

            Current.Resources.MergedDictionaries.RemoveAt(1);
            Current.Resources.MergedDictionaries.Insert(1, dictionary);
        }

        public static void SetupThemes()
        {
            var resource = Current.TryFindResource("Themes");

            if (resource is ThemeList themes)
                ThemeList = themes;
        }

        private static bool CheckinstallURLHandler(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg == "urlhandler")
                {
                    using var hkcr = Microsoft.Win32.Registry.ClassesRoot;
                    using var key = hkcr.CreateSubKey("sadxmm");
                    key.SetValue(null, "URL:SADX Mod Manager Protocol");
                    key.SetValue("URL Protocol", string.Empty);
                    using (var k2 = key.CreateSubKey("DefaultIcon"))
                        k2.SetValue(null, Environment.ProcessPath + ",1");
                    using var k3 = key.CreateSubKey("shell");
                    using var k4 = k3.CreateSubKey("open");
                    using var k5 = k4.CreateSubKey("command");
                    k5.SetValue(null, $"\"{Environment.ProcessPath}\" \"%1\"");
                    key.Close();
                    return true;
                }
            }

            return false;
        }

        private async Task InitUriAsync(string[] args, bool alreadyRunning)
        {
            if (!alreadyRunning)
            {
                UriQueue = new UriQueue(pipeName);
            }

            List<string> uris = args
                .Where(x => x.Length > protocol.Length && x.StartsWith(protocol, StringComparison.Ordinal))
                .ToList();

            if (uris.Count > 0)
            {
                using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out))
                {
                    try
                    {
                        await pipe.ConnectAsync();

                        using (var writer = new StreamWriter(pipe))
                        {
                            foreach (string s in uris)
                            {
                                await writer.WriteLineAsync(s);
                            }
                            await writer.FlushAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Handle connection or writing errors here
                        Console.WriteLine($"Error sending URIs: {ex.Message}");
                    }
                }
            }
        }

        private static async Task<(bool, WorkflowRunInfo, GitHubArtifact)> CheckManagerUpdate()
        {
            var workflowRun = await GitHub.GetLatestWorkflowRun();

            if (workflowRun is null)
                return (false, null, null);

            bool hasUpdate = RepoCommit != workflowRun.HeadSHA;

            GitHubAction latestAction = await GitHub.GetLatestAction();
            GitHubArtifact info = null;

            if (latestAction != null)
            {
                List<GitHubArtifact> artifacts = await GitHub.GetArtifactsForAction(latestAction.Id);

                if (artifacts != null)
                {
                    info = artifacts.FirstOrDefault(t => t.Expired == false && t.Name.Contains("Release"));
                }
            }

            return (hasUpdate, workflowRun, info);
        }

        public static async Task<bool> PerformUpdateManagerCheck()
        {
            var update = await CheckManagerUpdate();

            if (update.Item1 == false)
            {
                return false;
            }

            string changelog = await GitHub.GetGitChangeLog(update.Item2.HeadSHA);
            var manager = new InfoManagerUpdate(update.Item2, update.Item3, changelog);
            manager.ShowDialog();

            if (manager.DialogResult != true)
                return false;

            string dlLink = string.Format(SAModManager.Properties.Resources.URL_SAMM_UPDATE, update.Item2.CheckSuiteID, update.Item3.Id);
            Directory.CreateDirectory(".SATemp");
            var dl = new ManagerUpdate(dlLink, ".SATemp", update.Item3.Name + ".zip");
            await dl.StartManagerDL();
            dl.ShowDialog();
            ((MainWindow)System.Windows.Application.Current.MainWindow).Close();

            return true;
        }

        public static void DoEvents()
        {
            Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
        }

        private static async Task<bool> DoUpdate(string[] args, bool alreadyRunning)
        {
            foreach (var arg in args)
            {
                if (arg == "doupdate")
                {

                    if (alreadyRunning)
                        try { mutex.WaitOne(); }
                        catch (AbandonedMutexException) { }


                    var dialog = new InstallManagerUpdate(args[2], args[3]);
                    await dialog.InstallUpdate();

                    Application.Current.Shutdown();

                    return true;
                }

                if (arg == "cleanupdate")
                {
                    if (alreadyRunning)
                        try { mutex.WaitOne(); }
                        catch (AbandonedMutexException) { }

                    alreadyRunning = false;
                }
            }

            return false;
        }

        public static async Task<bool> ExecuteDependenciesCheck()
        {
            return await SAModManager.Startup.StartupCheck();
        }

        private ManagerSettings LoadManagerConfig()
        {
            ManagerSettings settings = File.Exists(ConfigPath) ? IniSerializer.Deserialize<ManagerSettings>(ConfigPath) : new ManagerSettings();

            switch (settings.GameManagement.CurrentSetGame)
            {
                default:
                case (int)SetGame.SADX:
                    CurrentGame = GamesInstall.SonicAdventure;
                    break;
                case (int)SetGame.SA2:
                    CurrentGame = GamesInstall.SonicAdventure2;
                    break;
            }

            return settings;
        }



        private void MinimizeWindow(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow((DependencyObject)sender);
            window.WindowState = WindowState.Minimized;
        }

        private void MaximizeWindow(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow((DependencyObject)sender);
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow((DependencyObject)sender);
            window.Close();
        }
    }
}

