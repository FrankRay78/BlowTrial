﻿using BlowTrial.Domain.Providers;
using BlowTrial.Security;
using BlowTrial.View;
using BlowTrial.ViewModel;
using BlowTrial.Models;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Linq;
using System.IO;
using BlowTrial.Helpers;
using log4net;
using log4net.Appender;
using System.Deployment.Application;
using BlowTrial.Infrastructure.Extensions;
using BlowTrial.Migrations;
using System.Net.NetworkInformation;

namespace BlowTrial
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            // This code is used to test the app when using other cultures.
            //
            //System.Threading.Thread.CurrentThread.CurrentCulture =
            //    System.Threading.Thread.CurrentThread.CurrentUICulture =
            //        new System.Globalization.CultureInfo("it-IT");


            // Ensure the current culture passed into bindings is the OS culture.
            // By default, WPF uses en-US as the culture, regardless of the system settings.
            //
            FrameworkElement.LanguageProperty.OverrideMetadata(
              typeof(FrameworkElement),
              new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

            //Setup application variables
            DataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\BlowTrial";
            CurrentClickOnceVersion = GetClickOnceVersion();
            CurrentAppVersion = VersionNumberExtensions.GetVersionInt();
            if (!Directory.Exists(DataDirectory))
            {
                Directory.CreateDirectory(DataDirectory);
            }

        }
        protected override void OnStartup(StartupEventArgs e)
        {
            _log = LogManager.GetLogger(typeof(App));
#if !DEBUG
            if (CurrentClickOnceVersion != null)
            {
                ThreadContext.Properties["deploymentVersion"] = CurrentClickOnceVersion;
            }
            this.DispatcherUnhandledException += Application_DispatcherUnhandledException;
            log4net.Config.XmlConfigurator.Configure();
#endif
            base.OnStartup(e);

            // AppDomain.CurrentDomain.SetData("DataDirectory", baseDir);

            //Application initialisation
 
            AutoMapperConfiguration.Configure();
            /*
            catch (Exception ex)
            {
                _log.Error("App_AutomapperConfigurationException", ex);
                MessageBox.Show("An error has occured with automapper. An error has been logged, but this file will have to be attached and emailed to the application developer");
                throw;
            }
            */

            //Security
            CustomPrincipal customPrincipal = new CustomPrincipal();
            AppDomain.CurrentDomain.SetThreadPrincipal(customPrincipal);

            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            //test if wizard needs to run
#if !DEBUG
            try
            {
#endif
                CodeBasedMigration.ApplyPendingMigrations<BlowTrial.Migrations.TrialData.TrialDataConfiguration>(TrialDataContext.GetConnectionString(), ContextCeConfiguration.ProviderInvariantName, true);

                CodeBasedMigration.ApplyPendingMigrations<BlowTrial.Migrations.Membership.MembershipConfiguration>(MembershipContext.GetConnectionString(), ContextCeConfiguration.ProviderInvariantName, true);
#if !DEBUG
            }

            catch (Exception ex)
            {
                string usrErrMsg = null;
                if (ex.Message.StartsWith("Access to the database file is not allowed"))
                {
                    string administratorInstructions = string.Format("User:{0}\nRequies modify permissions be allowed for folder:\n{1}\nAnd all files within", System.Security.Principal.WindowsIdentity.GetCurrent().Name, DataDirectory);
                    usrErrMsg = "Cannot access database: \nPlease contact the systems administrator with the following message:\n" + administratorInstructions;
                }
                else
                {
                    usrErrMsg = "An error has occured trying to access the database: \n" + ex.Message;
                }
                if (usrErrMsg != null)
                {
                    _log.Error("App_FirstDatabaseAccessException", ex);
                    MessageBox.Show(usrErrMsg);
                    throw;
                }
            }
#endif

            var backDetails = BlowTrialDataService.GetBackupDetails();
            bool displayWizard = (backDetails.BackupData == null);
            if (!displayWizard  && backDetails.BackupData.IsBackingUpToCloud)
            {
                using (var t = new TrialDataContext())
                {
                    displayWizard = !t.StudyCentres.Any();
                }
            }
            if (displayWizard && !DisplayAppSettingsWizard())
            {
                Shutdown(259);
                return;
            }
            
            // Create the ViewModel to which 
            // the main window binds.
            var mainWindowVm = new MainWindowViewModel();
            MainWindow window = new MainWindow(mainWindowVm);

            // When the ViewModel asks to be closed, 
            // close the window.
            EventHandler handler = null;
            handler = delegate
            {
                window.Close();
                if (!window.IsLoaded) //in case user cancelled
                {
                    mainWindowVm.RequestClose -= handler;
                }
            };
            mainWindowVm.RequestClose += handler;
            _log.InfoFormat("Application started {0}", DateTime.Now);
            window.Show();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>true if app settings were succesfully added to the appropriate repositories</returns>
        static bool DisplayAppSettingsWizard()
        {
            //testfor and display starup wizard
            var wizard = new GetAppSettingsWizard();
            GetAppSettingsViewModel appSettings = new GetAppSettingsViewModel();
            wizard.DataContext = appSettings;
            EventHandler wizardHandler = null;
            wizardHandler = delegate
            {
                wizard.Close();
                wizard = null;
                appSettings.RequestClose -= wizardHandler;
            };
            appSettings.RequestClose += wizardHandler;
            wizard.ShowDialog();
            return !appSettings.WasCancelled; // user cancel
        }
        static ILog _log;
        void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _log.Fatal("Application_DispatcherUnhandledException", e.Exception);
        }
        protected override void OnExit(ExitEventArgs e)
        {
            MoveLogFileToCloud();
            base.OnExit(e);
        } 
        internal static void MoveLogFileToCloud()
        {
            try
            {
                string fn = GetLogOutputPath();
                if (fn != null)
                {
                    MoveLogFileToCloud(fn);
                }
            }
            catch (Exception ex)
            {
                _log.Error("Application_DispatcherUnhandled_ImplementationException", ex);
                return;
            }
        }

        static string GetLogOutputPath()
        {
            BackupDataSet bak = BlowTrialDataService.GetBackupDetails();
            if (bak != null)
            {
                string dir = bak.CloudDirectories.FirstOrDefault();
                if (dir != null)
                {
                    // if environment.machinename not working due to duplicate names, could try
                    //var searcher = new System.Management.ManagementObjectSearcher("select * from " + Key);
                    // key = Win32_DiskDrive or Win32_Processor
                    return Path.Combine(dir, StringExtensions.GetSafeFilename($"log_{Environment.MachineName}_{Environment.UserName}_{MAC()}.txt"));
                }
            }
            return null;
        }
        static string SafeFilename(string instr)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                instr = instr.Replace(c, '-');
            }
            return instr;
        }
        static string MAC()
        {
            return (from nic in NetworkInterface.GetAllNetworkInterfaces()
                    where nic.OperationalStatus == OperationalStatus.Up
                    select nic.GetPhysicalAddress().ToString()).FirstOrDefault();
        }
        static bool MoveLogFileToCloud(string cloudFileName)
        {
            foreach (var appender in LogManager.GetRepository().GetAppenders())
            {
                if (appender is FileAppender fileAppender)
                {
                    try
                    {
                        FileInfo log = new FileInfo(fileAppender.File);
                        if (!log.Exists) { return true; }
                        FileInfo cloudLog = new FileInfo(cloudFileName);
                        if (!cloudLog.Exists || log.LastWriteTimeUtc > cloudLog.LastWriteTimeUtc)
                        {
                            log.CopyTo(cloudFileName, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error("MoveLogFileToCloud", ex);
                        return false;
                    }
                    return true;
                }
            }
            return false;

        }
        public static string DataDirectory; 
        public static string CurrentClickOnceVersion;
        public static int CurrentAppVersion;
        static string GetClickOnceVersion()
        {
            if (ApplicationDeployment.IsNetworkDeployed)
            {
                var myVersion = ApplicationDeployment.CurrentDeployment.CurrentVersion;
                return string.Format("v{0}.{1}.{2}.{3}",
                    myVersion.Major,
                    myVersion.Minor,
                    myVersion.Build,
                    myVersion.Revision);
            }
            return null;
        }
    }
}
