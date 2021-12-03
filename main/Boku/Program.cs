// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

//#define IMPORT_DEBUG

#if EXTERNAL || true
# define GLOBAL_CATCH    // include the global exception handler.
# define GLOBAL_CATCH_PC
#endif

#if EXTERNAL
# define UPDATE_CHECK    // check for new version at startup.
#endif

//#define DISABLE_STUDIOK 

using System;
using System.Net;
using System.Threading;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Serialization;
using BokuShared.Wire;
using System.Globalization;
using System.Windows.Forms;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Storage;

using Boku.Analyses;
using Boku.Common;
using Boku.Common.Localization;
using Boku.Common.Sharing;
using Boku.Common.Xml;
using Boku.Web;

using BokuShared;

namespace Boku
{
    //Class that holds version information from service. 
    public class UpdateInfo
    {
        public string releaseNotesUrl = "";
        public string updateUrl = "";
        public Version latestVersion;

        //Construct from wire message.
        public UpdateInfo(Message_Version version)
        {
            latestVersion = new Version(version.Major,version.Minor,version.Build,version.Revision);
            releaseNotesUrl = version.ReleaseNotesUrl;
            updateUrl = version.UpdateUrl;
        }

    }
    static partial class Program2
    {
        public static Mutex InstanceMutex;
        private static string kOptInForUpdatesFilename = @"Options\1F2B5B79-6EB0-45c4-A8BD-0EBDF4EE10C3.opt";
        private static string kOptInForInstrumentationFilename = @"Options\C90D3C0E-D0B4-4aa6-B35D-0A1D9931FB38.opt";

        public static Version ThisVersion;
        public static string CurrentKCodeVersion="10";  // Version of the KCode.
                                                        // 4 -> 5 : Add local variables and Squash.
                                                        // 5 -> 6 : New movement code.  Make missiles targetable.
                                                        // 6 -> 7 : Add Settings slider tiles as well as some settings as scores.
                                                        // 7 -> 8 : Add naming of characters and the ability to sense named characters.
                                                        // 8 -> 9 : Move linked level target from XmlWorldData to ReflexData.
                                                        // 9 -> 10 : Change terrain files from .Raw to .Map.
        
        public static string UpdateCode;

        public static UpdateInfo updateInfo=null;

        public static CmdLine CmdLine;

        public static string MicrobitCmdLine = null;

        public static SiteOptions SiteOptions;

        public static bool InstallerOptCheckForUpdates;
        public static bool InstallerOptSendInstrumentation;

        public static bool bShowVersionWarning = false;

        static bool localizedFilesUpdated = false;
        static public void langCallback()
        {
            localizedFilesUpdated = true;
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        // Must specify STA threading model to be allowed clipboard access.
        [STAThread]
        static public void Main(string[] args)
        {
#if GLOBAL_CATCH
            try
            {
#endif

                ThisVersion = Assembly.GetExecutingAssembly().GetName().Version;
                Assembly asm = Assembly.GetExecutingAssembly();
                var attr = (asm.GetCustomAttributes(typeof(GuidAttribute), true));
                UpdateCode = (attr[0] as GuidAttribute).Value;

                // Fake command line args to test double-click to launch
                //args = new string[3] { args[0], @"/Import", @"C:\Users\scoy\My Documents\New World 3, by Stephen Coy.Kodu2" };

                CmdLine = new CmdLine(args);

                if (CmdLine.Exists("?") || CmdLine.Exists("HELP"))
                {
                    System.Windows.Forms.MessageBox.Show(
                        "  /FPS \t- display FPS\r\n" +
                        "  /F \t- full screen\r\n" +
                        "  /S \t- sync refresh\r\n" +
                        "  /W 1280 \t- width\r\n" +
                        "  /H 1024 \t- height\r\n" +
                        "  /EFFECTS \t- turn on depth of field and bloom effects\r\n" +
                        "  /NOEFFECTS \t- turn off depth of field and bloom effects\r\n" +
                        "  /NOAUDIO \t- turn off audio\r\n" +
                        "  /PATH <save folder> \t- override save folder\r\n" +
                        "  /UPDATE \t- check for updates\r\n" +
                        "  /NOUPDATE \t- do not check for updates\r\n" +
                        "  /INSTRUMENTATION \t- send usage information\r\n" +
                        "  /NOINSTRUMENTATION \t- do not send usage information\r\n" +
                        "  /IMPORT <filename> \t- unpack the kodu level package to your downloads area\r\n" +
                        "  /LOGON \t- ask player for username\r\n" +
                        "  /ANALYTICS \t- run analytics on game being loaded\r\n" +
                        "  /LOCALIZATION <language> \t- report localization information that is missing in the specified language.\r\n" +
                        "  /PIESIZE <int> \t- pie menu maximum size.\r\n" +
                        "  /NOMICROBIT \t- Do not scan for attached BBC micro:bits\r\n" +
                        "  /MICROBIT \"COM3 E:\"\t- Try to enable micro:bit with given com port and drive letter.  The quotes are required.\r\n" +
                        "  /COMMUNITY <URL>\r\n" +
                        "  /SERVICE_API_URL <URL>\r\n" +
                        "");

                    return;
                }

                {
                    // Initialize level import/export facility
                    // ====================================================
                    // This is done before preventing multiple instances
                    // so that if an import was specified on the command
                    // line then it will be moved to the imports folder,
                    // allowing the already running instance of Kodu to
                    // pick it up the next time the user enters the load
                    // level menu.
                    Storage4.Init();
                    Storage4.StartupDir = Application.StartupPath;

                    // Note, we need to get the user override location before
                    // import otherwise we send the files to the wrong place.
                    // We don't need to do this for WinRT since we can't change
                    // the user location.
                    BokuSettings settings = BokuSettings.Settings;
                    if (!string.IsNullOrEmpty(settings.UserFolder))
                    {
                        Storage4.UserOverrideLocation = settings.UserFolder;
                    }

                    if (!LevelPackage.Initialize(CmdLine))
                    {
                        // Must be bad folder.
                        return;
                    }

                    // Restore default state for now.
                    Storage4.ResetUserOverrideLocation();
                    // ====================================================
                }

                {
                    // Prevent multiple instances of Kodu
                    // ====================================================
                    bool instanceMutexCreated;
                    InstanceMutex = new Mutex(false, @"Local\Boku", out instanceMutexCreated);

                    // If we didn't create the shared mutex, then another
                    // instance of Boku already exists.
                    if (!instanceMutexCreated)
                        return;
                    // ====================================================
                }

                {
                    // Load Site Options
                    // ====================================================
                    SiteOptions = SiteOptions.Load(StorageSource.All);
                    // ====================================================
                }

                {
                    // Load the unique site id.
                    // ====================================================
                    SiteID.Initialize();
                    // ====================================================
                }

                {
                    // Process the Import Directive
                    // ====================================================
                    // We're importing a level from the command line. Do the
                    // import and set it as the startup world so that we can
                    // jump right into it.

                    // First, set the userOverrideLocation so we import to the correct location.
                    BokuSettings settings = BokuSettings.Settings;
                    if (!string.IsNullOrEmpty(settings.UserFolder))
                    {
                        Storage4.UserOverrideLocation = settings.UserFolder;
                    }

                    List<Guid> importedLevels = new List<Guid>();
                    bool importOk = LevelPackage.ImportAllLevels(importedLevels);

                    if (!importOk)
                    {
                        bShowVersionWarning = true;
                    }

#if IMPORT_DEBUG
                LevelPackage.DebugPrint("Done importing");
                LevelPackage.DebugPrint("Files imported");
                foreach (Guid guid in importedLevels)
                {
                    LevelPackage.DebugPrint("    " + guid.ToString());
                }
#endif

                    if (importedLevels.Count > 0)
                    {
                        MainMenu.StartupWorldFilename = BokuGame.Settings.MediaPath + BokuGame.DownloadsPath + importedLevels[0].ToString() + ".Xml";
#if IMPORT_DEBUG
                    LevelPackage.DebugPrint("StartupWorldFilename : " + MainMenu.StartupWorldFilename);
#endif
                    }
                    // check here for the Analytics flag
                    if (CmdLine.Exists("ANALYTICS"))
                    {
                        //run my code here?
                        //Console.WriteLine("Begin Analytics");
                        //ObjectAnalysis oa = new ObjectAnalysis();
                        //oa.beginAnalysis(MainMenu.StartupWorldFilename.ToString());
                    }

                    // Override path to community.
                    if (CmdLine.Exists("COMMUNITY"))
                    {
                        Boku.Web.Trans.CommunityRequest.CommunityUrl = CmdLine.GetString("COMMUNITY", @"https://kodu.cloudapp.net/Community2.asmx");
                    }

                }

                {
                    // DebugLog.NewRun();
                    //Community2.GetWorlds(10, 22);

                    // Initialize Localization Resources.
                    Unicode.Init(); // Needed for loading localizations.
                    LocalizationResourceManager.Init();

                    // Update to Latest resources of the Default Language
                    LocalizationResourceManager.UpdateResources(LocalizationResourceManager.DefaultLanguage);

                    // Localization options
                    // ====================================================
                    // Allow command line option to override user choice iff user choise is "".
                    // If XmlOptionsData has a valid choice, always use it.
                    string lang = XmlOptionsData.Language;
                    string commandLineLang = CmdLine.GetString("LOCALIZATION", "");

                    // If we haven't previously set a language preference, select one 
                    // from the current locale.
                    if (string.IsNullOrEmpty(lang))
                    {
                        if (string.IsNullOrEmpty(commandLineLang))
                        {
                            {
                                try
                                {
                                    // Get current language.
                                    lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

                                    // Verify that it's a supported language.
                                    bool valid = false;
                                    foreach (LocalizationResourceManager.SupportedLanguage supportedLang in LocalizationResourceManager.SupportedLanguages)
                                    {
                                        if (string.Compare(lang, supportedLang.Language, StringComparison.OrdinalIgnoreCase) == 0)
                                        {
                                            valid = true;
                                            break;
                                        }
                                    }

                                    if (!valid)
                                    {
                                        lang = "EN";
                                    }
                                }
                                catch
                                {
                                    lang = "EN";
                                }
                            }
                        }
                        else
                        {
                            lang = commandLineLang;
                        }
                        // Persist language choice.
                        XmlOptionsData.Language = lang;
                    }

                    // Always create missing loc report except when English is the language.
                    if (string.Compare(lang, "EN", true) != 0)
                    {
                        Localizer.ShouldReportMissing = true;
                    }

                    if (!String.IsNullOrEmpty(lang))
                    {
                        Localizer.LocalLanguage = lang;
                        if (lang != LocalizationResourceManager.DefaultLanguage)
                        {
                            localizedFilesUpdated = false;
                            LocalizationResourceManager.UpdateResources(lang, langCallback);

                            while (!localizedFilesUpdated)
                            {
                                Thread.Sleep(10);
                            }
                        }
                    }

                    // Record current language to instrumentation.
                    if (!String.IsNullOrEmpty(lang))
                    {
                        Instrumentation.RecordDataItem(Instrumentation.DataItemId.Language, lang);
                    }
                }

                {
                    BokuSettings settings = BokuSettings.Settings;

                    // Apply Settings from the command Line
                    // ====================================================
                    //XmlOptionsData.ShowFramerate = CmdLine.GetBool("FPS", XmlOptionsData.ShowFramerate);
                    settings.FullScreen = CmdLine.GetBool("F", settings.FullScreen);
                    BokuGame.syncRefresh = CmdLine.GetBool("S", BokuGame.syncRefresh);
                    BokuGame.Logon = CmdLine.GetBool("Logon", SiteOptions.Logon);
                    DateTime endMarsMode = new DateTime(2012, 10, 1, 0, 0, 0);
                    if (CmdLine.Exists("MARS") || DateTime.Now < endMarsMode)
                    {
                        BokuGame.bMarsMode = true;
                    }
                    if (CmdLine.Exists("W"))
                    {
                        settings.ResolutionX = CmdLine.GetInt("W", settings.ResolutionX);
                    }
                    if (CmdLine.Exists("H"))
                    {
                        settings.ResolutionY = CmdLine.GetInt("H", settings.ResolutionY);
                    }
                    settings.PostEffects = CmdLine.GetBool("Effects", settings.PostEffects);
                    settings.PostEffects = !CmdLine.GetBool("NoEffects", !settings.PostEffects);
                    settings.LowModels = CmdLine.GetBool("LowModels", settings.LowModels);
                    settings.Audio = !CmdLine.GetBool("NoAudio", !settings.Audio);

                    // Update flags for update checking and instrumentation gathering from both the command line arguments and privacy options chosen during installation.

                    // XmlOptionsData will default to these values if these options have not been overridden in the Options screen.
                    InstallerOptCheckForUpdates = File.Exists(Storage4.TitleLocation + @"\" + kOptInForUpdatesFilename);
                    InstallerOptSendInstrumentation = File.Exists(Storage4.TitleLocation + @"\" + kOptInForInstrumentationFilename);

                    // XmlOptionData.CheckForUpdates combines the installer option
                    // as well as any user override.
                    SiteOptions.CheckForUpdates = XmlOptionsData.CheckForUpdates;

#if !UPDATE_CHECK
                    // Internal builds override this.  Why?
                    //SiteOptions.CheckForUpdates = false;
#endif

                    if (XmlOptionsData.SendInstrumentationWasSet)
                    {
                        // Note that this seems inverted because of the stupid naming.
                        SiteOptions.InstrumentationUnchecked = XmlOptionsData.SendInstrumentation;
                    }

                    // Allow command line arguments to override in-game settings.
                    if (CmdLine.Exists("Update"))
                    {
                        SiteOptions.CheckForUpdates = true;
                    }
                    if (CmdLine.Exists("NoUpdate"))
                    {
                        SiteOptions.CheckForUpdates = false;
                    }
                    if (CmdLine.Exists("Instrumentation"))
                    {
                        // Note that this seems inverted because of the stupid naming.
                        SiteOptions.InstrumentationUnchecked = true;
                    }
                    if (CmdLine.Exists("NoInstrumentation"))
                    {
                        // Note that this seems inverted because of the stupid naming.
                        SiteOptions.InstrumentationUnchecked = false;
                    }
                    if (CmdLine.Exists("MICROBIT"))
                    {
                        MicrobitCmdLine = CmdLine.GetString("MICROBIT", null);
                    }

                    /// This is fortuitously timed. We have already pulled the settings file
                    /// out of the real user folder (somewhere in Documents\Saved Games\...).
                    /// If we override the user path now to some central shared spot, we
                    /// get individualized settings from BokuSettings, but then shared levels
                    /// from the central source.
                    string userPath = CmdLine.GetString("PATH", "");
                    if (!string.IsNullOrEmpty(userPath))
                    {
                        settings.UserFolder = userPath;
                    }
                    if (!string.IsNullOrEmpty(settings.UserFolder))
                    {
                        Storage4.UserOverrideLocation = settings.UserFolder;
                    }

                    if (!XmlOptionsData.ShowMicrobitTiles)
                    {
                        // Scan for attached microbits (but don't connect to them yet). If any are found,
                        // RefreshDevices will modify XmlOptionsData to make the microbit programming tiles
                        // permanently visible in the tile picker.
                        Input.MicrobitManager.RefreshDevices(false);
                    }
                    // ====================================================
                }

                {
                    // Record this installation's unique ID to instrumentation.
                    Instrumentation.RecordDataItem(Instrumentation.DataItemId.InstallationUniqueId, SiteID.Instance.Value.ToString());

                    StartupForm.Startup();
                    StartupForm.EnableCancelButton(false);
                    StartupForm.SetProgressStyle(System.Windows.Forms.ProgressBarStyle.Marquee);

                    // Get the latest version number.
                    // ====================================================

                    // See if an update is available.  Note, we always get the file even if not checking
                    // for updates since it also contains the ServiceApiUrl.
                    FetchLatestVersionFromServer(SiteOptions.Product);

                    // We just fetched the latest ServiceApiUrl.  Now override it if needed.
                    if (CmdLine.Exists("SERVICE_API_URL"))
                    {
                        XmlOptionsData.ServiceApiUrl = CmdLine.GetString("SERVICE_API_URL", "");
                    }

                    if (SiteOptions.CheckForUpdates && !WinStoreHelpers.RunningAsUWP)
                    {
                        var ignoreVersion = new Version(SiteOptions.IgnoreVersion);
                        if (updateInfo != null && ThisVersion < updateInfo.latestVersion
                            && updateInfo.latestVersion != ignoreVersion
                        )
                        {
                            StartupForm.Shutdown();

                            var updateForm = new UpdateForm();

                            //Localized update dialog.
                            updateForm.Text = Strings.Localize("Update.FormTitle");

                            var text = Strings.Localize("Update.UpdateMessage");
                            updateForm.MessageLabel.Text = text.Replace("^", "");//Remove link delimiters.
                            updateForm.MessageLabel.LinkArea = new System.Windows.Forms.LinkArea(text.IndexOf("^"), text.LastIndexOf("^") - text.IndexOf("^") - 1); //Set link area based on ^ delimiters.

                            text = Strings.Localize("Update.ReleaseNotesMessage");
                            updateForm.RelaseNotesLabel.Text = text.Replace("^", "");//Remove link delimiters.
                            updateForm.RelaseNotesLabel.LinkArea = new System.Windows.Forms.LinkArea(text.IndexOf("^"), text.LastIndexOf("^") - text.IndexOf("^") - 1);//Set link area based on ^ delimiters.

                            updateForm.CurrentVersionLabel.Text = Strings.Localize("Update.CurrentVersion");
                            updateForm.NewVersionLabel.Text = Strings.Localize("Update.LatestVersion");

                            updateForm.UpdateButton.Text = Strings.Localize("Update.UpdateButtonText");
                            updateForm.IgnoreButton.Text = Strings.Localize("Update.IgnoreButtonText");
                            updateForm.RemindButton.Text = Strings.Localize("Update.RemindButtonText");

                            //Set version info in dialog.
                            updateForm.CurrentVersion.Text = ThisVersion.ToString();
                            updateForm.NewVersion.Text = updateInfo.latestVersion.ToString();

                            //Setup links in dialog from UpdateInfo.
                            updateForm.RelaseNotesLabel.Links[0].LinkData = updateInfo.releaseNotesUrl;
                            updateForm.RelaseNotesLabel.LinkClicked += (s, e) =>
                            {
                                System.Diagnostics.Process.Start(e.Link.LinkData.ToString());
                            };
                            updateForm.MessageLabel.Links[0].LinkData = SiteOptions.KGLUrl;
                            updateForm.MessageLabel.LinkClicked += (s, e) =>
                            {
                                System.Diagnostics.Process.Start(e.Link.LinkData.ToString());
                            };

                            var dialogResult = updateForm.ShowDialog();

                            if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                            {
                                //Show update page and exit.
                                Process.Start(updateInfo.updateUrl);
                                Process.GetCurrentProcess().Kill();
                            }

                            if (dialogResult == System.Windows.Forms.DialogResult.Ignore)
                            {
                                //Write ignore version to options.
                                SiteOptions.IgnoreVersion = updateInfo.latestVersion.ToString();
                                SiteOptions.Save();
                            }

                        }
                    }

                    StartupForm.SetStatusText("Starting up...");

                    // ====================================================


                    // TODO (****) *** See notes!!!!
                    // Consider starting MainForm here and putting init of BokuGame into XNAControl.
                    // Do we still need/want StartForm?
                    //BokuGame game = new BokuGame();

                    // Move these to be called from XNAControl so that device and content manager exist first?!?
                    //BokuGame.bokuGame.Initialize();
                    //BokuGame.bokuGame.LoadContent();
                    //BokuGame.bokuGame.BeginRun();

                    if (WinStoreHelpers.RunningAsUWP)
                    {
                        string applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    }

                    StartupForm.Shutdown();
                    Application.Run(MainForm.Instance);


                    /*
                    using (BokuGame game = new BokuGame())
                    {
                        //try
                        {
                            game.Run();
                        }
                        //catch (Exception ex)
                        {
                        //    Console.WriteLine(ex.InnerException);
                        }
                    }
                    */

                    // In case the app was closed while in play mode with a microbit attached. Release microbits
                    // so that the serial port receive thread doesn't block application exit.
                    Boku.Input.MicrobitManager.ReleaseDevices();

                    FlushInstrumentation();

                    // ====================================================
                }
#if GLOBAL_CATCH
            }
            catch (Exception ex)
            {
                // For both Xbox and PC write out a file to act as the crash cookie.
                {
                    Stream stream = Storage4.OpenWrite(MainMenu.CrashCookieFilename);
                    byte[] buffer = { 42 };
                    stream.Write(buffer, 0, 1);
                    stream.Close();
                }

                // Be sure mouse cursor is on regardless of current input mode.
                BokuGame.bokuGame.IsMouseVisible = true;

                StartupForm.Shutdown();
                
                // Show the crash report dialog box unless we're running the debugger.
                if (!Debugger.IsAttached)
                {

                    string gfxString;
                    try
                    {
                        gfxString = String.Format("Adapter: {0}", GraphicsAdapter.DefaultAdapter.Description);
                    }
                    catch
                    {
                        gfxString = "(Error getting graphics adapter information)";
                    }

                    // On PC, show the crash report dialog box.

                    string errorReport =
                        ex.Message + "\r\n" +
                        ThisVersion.ToString() + "\r\n" +
                        gfxString + "\r\n\r\n" +
                        ex.StackTrace;
                    ErrorForm errorForm = new ErrorForm();
                    errorForm.textBoxError.Text = errorReport;
                    if (System.Windows.Forms.DialogResult.OK == errorForm.ShowDialog())
                    {
                        string addInfo =
                            ex.GetType().Name + "\r\n" +
                            "Kodu: " + ThisVersion.ToString() + "\r\n" +
                            gfxString + "\r\n" +
                            "WLID: " + errorForm.textBoxLiveId.Text + "\r\n\r\n" + 
                            errorForm.textBoxAddInfo.Text;
                        SendErrorReport(ex.Message, ex.StackTrace, addInfo);
                    }

                    Process.GetCurrentProcess().Kill();
                }
            }
#endif // GLOBAL_CATCH

            // Prevent the garbage collector from optimizing away our shared mutex instance.
            GC.KeepAlive(InstanceMutex);

        }   // end of Main()

        /// <summary>
        /// Copies any files that match the searchPattern string from src to dst.
        /// </summary>
        /// <param name="srcDir"></param>
        /// <param name="dstDir"></param>
        /// <param name="searchPattern"></param>
        static void CopyFiles(string srcDir, string dstDir, string searchPattern)
        {
            try
            {
                string[] filePaths = Directory.GetFiles(srcDir, searchPattern);

                foreach (string srcPath in filePaths)
                {
                    string dstPath = Path.Combine(dstDir, Path.GetFileName(srcPath));
                    File.Copy(srcPath, dstPath);
                }
            }
            catch (Exception e)
            {
                // If the file has already been copied over, this will throw.
                // No worries...
                if (e != null)
                {
                }
            }
        }   // end of CopyFiles()

    }   // end of class Program2

    /// This chunk of the Program class manages the task of fetching the latest
    /// version number from the server to determine whether an update is available.
    static partial class Program2
    {
        private static bool versionPending = true;

        private static void FetchLatestVersionFromServer(string productName)
        {
            const int timeout = 5000;   // 5 seconds

            try
            {
                versionPending = true;
                string url = Program2.SiteOptions.KGLUrl + "/API/LatestVersion.xml";
                Uri uri = new Uri(url);
                var request = (HttpWebRequest)WebRequest.Create(uri);
                var result = request.BeginGetResponse(GetLatestVersionCallback, request);
                ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, TimeoutCallback, request, timeout, true);

                // Sleep until version info comes back or we time out.
                while (versionPending)
                {
                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                if (e != null)
                {
                    versionPending = false;
                }
            }

        }   // end of FetchLatestVersionFromServer()

        private static void GetLatestVersionCallback(IAsyncResult asyncResult)
        {
            try
            {
                var request = (HttpWebRequest)asyncResult.AsyncState;
                var response = (HttpWebResponse)request.EndGetResponse(asyncResult);
                var responseStream = response.GetResponseStream();

                Message_Version messageVersion = Message_Version.Load(responseStream);

                // If we've got a valid new URL, save it in OptionsData.
                if(!String.IsNullOrEmpty(messageVersion.ServiceApiUrl))
                {
                    XmlOptionsData.ServiceApiUrl = messageVersion.ServiceApiUrl;
                }

                updateInfo = new UpdateInfo(messageVersion);
            }
            catch (Exception e)
            {
                if (e != null)
                {
                }
            }

            versionPending = false;

        }   // end of GetLatestVersionCallback()

        // Abort the request if the timer fires. 
        private static void TimeoutCallback(object state, bool timedOut)
        {
            if (timedOut)
            {
                var request = state as HttpWebRequest;
                if (request != null)
                {
                    request.Abort();
                }
                versionPending = false;
            }
        
        }   // end of TimeoutCallback()

    }   // end of class Program2


    /// This chunk of the Program class manages the task of sending crash reports and instrumentation.
    static partial class Program2
    {
        static bool instrumentationFlushed = false;
        static void InstrumentationFlushed(object param)
        {
            instrumentationFlushed = true;
        }

        static void FlushInstrumentation()
        {
            try
            {
                if (SiteOptions.Instrumentation)
                {
                    int timeSpent = 0;
                    if (Common.Instrumentation.Flush(InstrumentationFlushed))
                    {
                        // Give it 30 seconds to complete.
                        while (!instrumentationFlushed && timeSpent < 30 * 1000)
                        {
                            // Pump web request callbacks.
                            Web.Trans.Request.Update();
                            System.Threading.Thread.Sleep(10);
                            timeSpent += 10;
                        }
                    }
                }
            }
            catch { }
        }

#if GLOBAL_CATCH
        static bool errorReportSent = false;

        static void ErrorReportSent(object param)
        {
            errorReportSent = true;
        }

        static void SendErrorReport(string errorMessage, string stackTrace, string addInfo)
        {
            try
            {
                Web.Trans.ReportError trans = new Web.Trans.ReportError(
                    errorMessage,
                    stackTrace,
                    addInfo,
                    ErrorReportSent,
                    null);

                if (trans.Send())
                {
                    int timeSpent = 0;
                    while (!errorReportSent && timeSpent < 30 * 1000)
                    {
                        // Pump web request callbacks.
                        Web.Trans.Request.Update();
                        System.Threading.Thread.Sleep(10);
                        timeSpent += 10;
                    }
                }
            }
            catch { }
        }
#endif
    }

}   // end of namespace Boku
