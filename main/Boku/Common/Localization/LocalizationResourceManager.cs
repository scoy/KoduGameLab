// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

//#define LOCALES_DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Net;
using System.Globalization;

using Boku.Common.Sharing;

namespace Boku.Common.Localization
{
    /// <summary>
    /// This class manages localization resource files (ex: Strings.xml) and 
    /// ensures that all most recent versions of all supported langauges are 
    /// available on the client machine
    /// NOTE: This class is NOT thread-safe
    /// </summary>
    public class LocalizationResourceManager
    {
        #region Constants

        public const string LanguageDir = @"Content\Xml\Localizable";
        public const string DefaultLanguage = "EN"; //The English ISO 639-1 language code
        public const string DefaultLanguageDir = LanguageDir + @"\" + DefaultLanguage;
        const string LocalesFileName = @"Locales.xml";
        const string LocalesFilePath = LanguageDir + @"\" + LocalesFileName;
        static readonly string LocalesUrl = KoduService.KGLUrl + "/API/Languages.xml";          // URL to locales file with current languages and update times.
        static readonly string LocalizationsUrl = KoduService.KGLUrl + "/API/Localizations";    // URL root to individual language folders.
        const int Timeout = 5000;

        #endregion

        /// <summary>
        /// List of Supported Locales, could be null. For guaranteed non-nullness, use the 
        /// Property "Locales" instead.
        /// </summary>
        static IList<Locale> _locales;

        /// <summary>
        /// Guarantees non-null access to _locales
        /// </summary>
        static IList<Locale> Locales {
            get
            {
                LocalesDebugPrint("Locales.Get");
                LocalesDebugPrint("    _locales " + (_locales != null ? "valid" : "null"));
                if (_locales == null)
                {
                    LocalesDebugPrint("    _locales = null");
                    // Wait for outstanding network requests (heuristic)
                    LocalesDebugPrint("    waiting...");
                    LocalesSet.WaitOne(Timeout);
                    LocalesDebugPrint("    done waiting.");

                    LocalesDebugPrint("    _locales " + (_locales != null ? "valid" : "null"));
                    if (_locales == null)
                    {
                        LocalesDebugPrint("    _locales = null (still)");
                        // Give up and get from local file
                        SafeGetLocalesFromFile();

                        LocalesDebugPrint("    _locales " + (_locales != null ? "valid" : "null"));
                    }

                    // Ok, fine, nothing seems to be working so let's try another approach.
                    if (_locales == null)
                    {
                        LocalesDebugPrint("    Hard coded list...");
                        // Final chance, hard code languages.
                        List<Locale> locs = new List<Locale>();

                        locs.Add(new Locale("AR", "Arabic", "عربي", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("CS", "Czech", "Čeština", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("CY", "Welsh", "Cymraeg", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("DE", "German", "Deutsch", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("EL", "Greek", "ελληνικά", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("EN", "English", "English", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("ES", "Spanish", "Español", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("EU", "Basque", "Euskara", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("FR", "French", "Le Français", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("HE", "Hebrew", "עִבְרִית", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("HU", "Hungarian", "Magyar", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("IS", "Icelandic", "Íslenska", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("IT", "Italian", "Italiano", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("JA", "Japanese", "日本語", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("KO", "Korean", "한글", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("LT", "Lithuanian", "Lietuvių Kalba", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("NL", "Dutch", "Nederlands", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("NO", "Norwegian", "Norsk", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("PL", "Polish", "Język Polski", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("PT", "Portuguese", "Português", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("RU", "Russian", "ру́сский язы́к", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("TR", "Turkish", "Türkçe", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("VN", "Vietnamese", "Tiếng Việt", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("ZH-TW", "Chinese (trad)", "繁體中文", new DateTime(2017, 2, 22)));
                        locs.Add(new Locale("ZH-CN", "Chinese (simp)", "简体中文", new DateTime(2017, 2, 22)));
                            
                        _locales = locs;
                        LocalesSet.Set();
                    }
                }
                return _locales;
            }
            set
            {
                if (value != null)
                {
                    LocalesDebugPrint("Locales.Set");
                    _locales = value;
                    LocalesDebugPrint("    _locales " + (_locales != null ? "valid" : "null"));
                    LocalesSet.Set();
                }
            }}

        static readonly AutoResetEvent LocalesSet = new AutoResetEvent(false);

        /// <summary>
        /// List of Supported Languages
        /// </summary>
        public static IEnumerable<SupportedLanguage> SupportedLanguages
        {
            get
            {
                LocalesDebugPrint("SuportedLanguages.Get"); 
                return Locales.Select(locale => new SupportedLanguage { Language = locale.Directory, NameInEnglish = locale.Language, NameInNative = locale.Native });
            }
        }

        public static void Init()
        {
            LocalesDebugPrint("LocalizationResourceManager.Init()");
            GetLocalesFromServer();
        }

#if LOCALES_DEBUG
        static public void LocalesDebugPrint(string text)
        {
            try
            {
                string debugPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\LocalesDebug.txt";
                TextWriter tw = new StreamWriter(debugPath, true);
                tw.WriteLine(text);
                tw.Close();
            }
            catch(Exception e)
            {
                // May throw if another process has the debug text file already open.
                if (e != null)
                {
                    // Sleep a bit then try again.  Infinite loop?
                    Thread.Sleep(100);
                    LocalesDebugPrint(text);
                }
            }
        }
#else
        static public void LocalesDebugPrint(string text)
        {
            // Do nothing...
        }
#endif


        #region Retreive Locales From Server

        /// <summary>
        /// Attempts to read Locales.xml from the remote server
        /// </summary>
        static void GetLocalesFromServer()
        {
            LocalesDebugPrint("\nEntering GetLocalesFromServer()");
            try
            {
                KoduService.DownloadDataAsync(LocalesUrl, (responseMessage) =>
                {
                    if (responseMessage == null)
                    {
                        // Failed.  Nothing to do here.
                        LocalesDebugPrint("    Failed to download Languages.Xml.");
                    }
                    else
                    {
                        LocalesDebugPrint("    Succeeded to download Languages.Xml.");

                        bool persist = true;
                        // If disk version is newer than online version, don't persist.  This
                        // should only happen when a user is adding a new language.  In this 
                        // case we want them to be able to modify their local copy.
                        DateTime lastModTime = Storage4.GetLastWriteTimeUtc(LocalesFilePath, StorageSource.UserSpace);
                        if (lastModTime > responseMessage.Content.Headers.LastModified)
                        {
                            persist = false;
                        }

                        LocalesDebugPrint("    persist : " + persist.ToString());

                        if (persist)
                        {
                            LocalesDebugPrint("    Populating from XML stream.");
                            responseMessage.Content.ReadAsStreamAsync().ContinueWith(streamTask =>
                            {
                                Stream result = streamTask.Result;
                                PopulatesLocalesFromXmlStream(result, persist);
                            });
                        }
                        else
                        {
                            LocalesDebugPrint("    Getting from file.");
                            GetLocalesFromFile();
                        }

                    }

                });
            }
            catch (Exception e)
            {
                // Keep the compiler quiet when LOCALES_DEBUG not defined.
                if (e != null)
                {
                    // DebugLog.WriteException(e, "GetLocalesFromServer()");
                }
                LocalesDebugPrint("Exception thrown in GetLocalesFromServer()\n" + e.ToString());
            }
        }   // end of GetLocalesFromServer()

        /// <summary>
        /// Attempts to load Locales from the local file without throwing
        /// </summary>
        static void SafeGetLocalesFromFile()
        {
            // DebugLog.WriteLine("SafeGetLocalesFromFile()");
            lock (LanguageDir)  // Just need some object to lock on...
            {
                try
                {
                    // Locales couldn't be retrieved from the server, read from local file
                    GetLocalesFromFile();
                }
                catch (Exception e)
                {
                    if (e != null)
                    {
                        // DebugLog.WriteException(e, "SafeGetLocalesFromFile");
                    }
                    LocalesDebugPrint("Exception thrown in SafeGetLocalesFromFile()\n" + e.ToString());
                }
            }   // end of lock.
        }


        /// <summary>
        /// Attempts to get the locale for a specific resource from the remote server.
        /// </summary>
        static void GetLocaleFromServer(Resource resource, Locale languageLocale)
        {
            // DebugLog.WriteLine("GetLocaleFromServer()");
            try
            {
                string url = string.Format("{0}/{1}/{2}", LocalizationsUrl, languageLocale.Directory, resource.Name);

                KoduService.DownloadData(url, (result) =>
                {
                    if (result == null)
                    {
                        // Failed.  Nothing to do here.
                    }
                    else
                    {
                        using (var streamReader = new StreamReader(result))
                        {
                            string resourceXml = streamReader.ReadToEnd();
                            if (string.IsNullOrEmpty(resourceXml))
                                return;
                            resource.Update(languageLocale.Directory, resourceXml);
                        }
                    }

                });
            }
            catch (Exception e)
            {
                // Keep the compiler quiet when LOCALES_DEBUG not defined.
                if (e != null)
                {
                    // DebugLog.WriteException(e, "GetLocaleFromServer()");
                }
                LocalesDebugPrint("Exception thrown in GetLocaleFromServer()\n" + e.ToString());
            }
            finally
            {
                DecrementPendingResourceUpdates(languageLocale);
            }
        }   // end of GetLocaleFromServer()

        #endregion

        #region Retrieve Locales From Local File

        /// <summary>
        /// Attempts to read Locales.xml from local storage.
        /// </summary>
        static void GetLocalesFromFile()
        {
            LocalesDebugPrint("\nEntering GetLocalesFromFile()");
            try
            {
                // DebugLog.WriteLine("    open stream for read.");
                var localesFileStream = Storage4.OpenRead(LocalesFilePath, StorageSource.All);
                // DebugLog.WriteLine("    read.");
                PopulatesLocalesFromXmlStream(localesFileStream);

                LocalesDebugPrint("In GetLocalesFromFile(), looping over each locale.");
                foreach (Locale loc in Locales)
                {
                    LocalesDebugPrint(loc.ToString());
                }

            }
            catch (Exception e)
            {
                // Keep the compiler quiet when LOCALES_DEBUG not defined.
                if (e != null)
                {
                    // DebugLog.WriteException(e, "GetLocalesFromFile()");
                }
                LocalesDebugPrint("Exception thrown in GetLocalesFromFile()\n" + e.ToString());
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Given a stream containing Locales.xml data, tries to to update the List of supported Locales
        /// </summary>
        static void PopulatesLocalesFromXmlStream(Stream localesXmlStream, bool persistFile = false)
        {
            LocalesDebugPrint("\nEntering PopulatesLocalesFromXmlStream()");

            // DebugLog.WriteLine("PopulatesLocalesFromXmlStream()");
            // DebugLog.WriteLine("    localesXmlStream : " + (localesXmlStream == null ? "null" : "valid"));
            if (localesXmlStream == null)
            {
                LocalesDebugPrint("    localesXmlStream == null in PopulatesLocalesFromXmlStream()");

                return;
            }

            using (localesXmlStream)
            using (var streamReader = new StreamReader(localesXmlStream))
            {
                var localesXml = streamReader.ReadToEnd();
                if (string.IsNullOrEmpty(localesXml))
                {
                    LocalesDebugPrint("    null read.  Returning.");
                    return;
                }

                var localesXmlParser = new LocalesXmlParser();
                var locales = localesXmlParser.Parse(localesXml);


                // Fix up for bad dates.  The thought here is that for some locales we're getting
                // the DateTime string translated wrong which is causing radically invalid dates.
                // So, adjust the dates to something reasonable.
                DateTime minDateTime = new DateTime(2019, 1, 1, 11, 11, 11);
                for(int i=0; i<locales.Count; i++)
                {
                    if (locales[i].LastUpdated < minDateTime)
                    {
                        locales[i].LastUpdated = minDateTime;
                    }
                }

                LocalesDebugPrint("    pre filtering count = " + locales.Count.ToString());
                // If the localized version of the language name isn't supported by
                // our current font set then remove it from the list.  This will
                // prevent people with older versions of Kodu from trying to select
                // languages that can't be displayed (eg with Asian fonts).
                //
                // Loop over the list backwards so we safely remove elements without
                // losing our place.
                //
                // Commented out because I think this is what is causing problems with
                // Vietnamese showing up properly in the list.
                /*
                for (int i = locales.Count - 1; i >= 0; i--)
                {
                    if (!TextHelper.StringIsValid(locales[i].Native))
                    {
                        LocalesDebugPrint("    removing " + locales[i].Language);
                        locales.RemoveAt(i);
                    }
                }
                */
                Debug.Assert(locales.Count > 0, "Why aren't we seeing files from the localization server?");

                if (locales == null)
                {
                    LocalesDebugPrint("    post filtering, locales = null");
                }
                else
                {
                    LocalesDebugPrint("    post filtering count = " + locales.Count.ToString());
                }

                if (locales != null && locales.Count > 0)
                {
                    Locales = locales;

                    if (!persistFile)
                    {
                        return;
                    }

                    LocalesDebugPrint("    persisting to : " + LocalesFilePath);
                    using (var streamWriter = Storage4.OpenStreamWriter(LocalesFilePath))
                    {
                        streamWriter.Write(localesXml);
                    }
                }
            }
        }   // end of PopulatesLocalesFromXmlStream()


        #endregion


        #region Update Local Resources

        /// <summary>
        /// AutoResetEvent to ensure that a single thread cannot update the resources of more than one language simultaneously
        /// </summary>
        static readonly AutoResetEvent updateResourcesEvent = new AutoResetEvent(false);

        /// <summary>
        /// Counter to ensure that all resources have been updated before calling the callback
        /// </summary>
        static int pendingResources;

        /// <summary>
        /// Callback after updating resources to allow external callers to get signaled
        /// </summary>
        static Action updateResourcesCallback;

        /// <summary>
        /// Updates all resources for a given language.
        /// </summary>
        public static void UpdateResources(string language, Action callback = null)
        {
            LocalesDebugPrint("\nEntering UpdateResources()");
            LocalesDebugPrint("    language : " + language);
            if (Locales == null)
            {
                LocalesDebugPrint("    Locales is returning null!!!  How? Why?");
                Debug.Assert(false, "This should never happen but yet it still does on some systems.  Hmm.");
                //return;
                LocalesDebugPrint("    Forcing EN");
                language = "EN";
            }

            if (Locales == null)
            {
                LocalesDebugPrint("    Locales is still null, calling SafeGetLocalesFromFile");
                SafeGetLocalesFromFile();
                LocalesDebugPrint("    Locales : " + (Locales == null ? "null" : "valid with " + Locales.Count.ToString() + " elements"));
            }

            LocalesDebugPrint("    getting locale language");
            //Locale languageLocale = Locales.FirstOrDefault(locale => locale.Directory == languageUpperCase);
            Locale languageLocale = null;
            foreach (Locale locale in Locales)
            {
                if(string.Compare(language, locale.Directory, ignoreCase: true) == 0)
                {
                    languageLocale = locale;
                    break;
                }
            }

            if (languageLocale == null)
            {
                LocalesDebugPrint("    localelanguage = null");
            }
            else
            {
                LocalesDebugPrint("    localelanguage.Directory : " + languageLocale.Directory);
            }
            
            // No point in trying to update a Language if we don't have a Locale for it
            if (languageLocale == null)
            {
                if (callback != null)
                {
                    callback();
                }
                return;
            }

            LocalesDebugPrint("    pendingResources : " + pendingResources.ToString());
            if (pendingResources != 0)
            {
                LocalesDebugPrint("    waiting...");
                updateResourcesEvent.WaitOne();
                LocalesDebugPrint("    done waiting");
            }

            updateResourcesCallback = callback;
            pendingResources = AllResources.Count;
            UpdateResource(AllResources[pendingResources-1], languageLocale);
        }

        /// <summary>
        /// Updates a certain resource for a given language only if necessary
        /// </summary>
        static void UpdateResource(Resource resource, Locale languageLocale)
        {
            LocalesDebugPrint("\nEntering UpdateResource()");
            LocalesDebugPrint("    language : " + languageLocale.Language);

            var serverLastUpdated = languageLocale.LastUpdated;
            var localResourceLastUpdated = resource.LastUpdated(languageLocale.Directory);

            if (serverLastUpdated == null || (localResourceLastUpdated != null && serverLastUpdated < localResourceLastUpdated))
            {
                DecrementPendingResourceUpdates(languageLocale);
            }
            else
            {
                GetLocaleFromServer(resource, languageLocale);
            }
        }

        static void DecrementPendingResourceUpdates(Locale languageLocale)
        {
            if (Interlocked.Decrement(ref pendingResources) == 0)
            {
                if (updateResourcesCallback != null)
                    updateResourcesCallback();
                // Release Lock allowing other callers to update resources
                updateResourcesEvent.Set();

            }
            else
            {
                UpdateResource(AllResources[pendingResources - 1], languageLocale);
            }
        }

#endregion

        public class SupportedLanguage
        {
            public string Language { get; set; }
            public string NameInEnglish { get; set; }
            public string NameInNative { get; set; }
        }

        public class Resource
        {
            public readonly string Name;
            public string LastUpdate { get; set; }

            public Resource(string resourceName)
            {
                this.Name = resourceName;
            }

            public DateTime? LastUpdated(string language)
            {
                var resourcePath = Path.Combine(LanguageDir, language, Name);
                if (Storage4.FileExists(resourcePath, StorageSource.All))
                {
                    return Storage4.GetLastWriteTimeUtc(resourcePath, StorageSource.All);
                }
                return null;
            }

            public void Update(string language, string newResource)
            {
                var resourcePath = Path.Combine(LanguageDir, language, Name);
                var resourceXml = XDocument.Load(XmlReader.Create(new StringReader(newResource)));

                Encoding encoding = null;
                // Ensure that the declared XML encoding is used while saving the file when available
                if (resourceXml.Declaration != null && !string.IsNullOrEmpty(resourceXml.Declaration.Encoding))
                {
                    encoding = GetEncoding(resourceXml.Declaration.Encoding);
                }
                using (var streamWriter = Storage4.OpenStreamWriter(resourcePath, encoding))
                {
                    streamWriter.Write(newResource);
                }
            }

            static Encoding GetEncoding(string xmlEncoding)
            {
                switch (xmlEncoding.ToUpper())
                {
                    case "UTF-8":
                        return Encoding.UTF8;
                    case "UTF-16":
                        return Encoding.Unicode;
                }
                return null;
            }

        }

        public static readonly Resource CardsResource = new Resource("Cards.xml");
        public static readonly Resource HelpResource = new Resource("Help.xml");
        public static readonly Resource HelpOverlaysResource = new Resource("HelpOverlays.xml");
        public static readonly Resource StringsResource = new Resource("Strings.xml");
        public static readonly Resource TutorialCrumbsResource = new Resource("TutorialCrumbs.xml");
        public static readonly Resource TutorialStringsResource = new Resource("TutorialStrings.xml");
        public static readonly Resource TweakScreenHelpResource = new Resource("TweakScreenHelp.xml");

        static readonly List<Resource> AllResources = new List<Resource>
        {
            CardsResource,
            HelpResource,
            HelpOverlaysResource,
            StringsResource,
            TutorialCrumbsResource, 
            TutorialStringsResource, 
            TweakScreenHelpResource
        };



    }


#region Data Deserialization
    /// <summary>
    /// Efficient Parser for Locales.xml. Expected format for Locales.xml is:
    /// 
    /// Begin Locales.xml
    /// =================
    ///  <Locales>
    ///        <Locale>
    ///            <Language>
    ///                French
    ///            </Language>
    ///            <Directory>
    ///                fr
    ///           </Directory>
    ///            <Native>
    ///                Francais
    ///            </Native>
    ///            <LastUpdated>
    ///                1/23/2014 11:29:23 AM
    ///            </LastUpdated>
    ///        </Locale>
    ///   </Locales>
    /// ===============
    /// End Locales.xml
    /// </summary>
    class LocalesXmlParser
    {
#region Data Contract Constants
        protected const string LocalesTag = "Locales";
        protected const string LocaleTag = "Locale";
        protected const string LanguageTag = "Language";
        protected const string DirectoryTag = "Directory";
        protected const string NativeTag = "Native";
        protected const string LastUpdatedTag = "LastUpdated";
#endregion

        public List<Locale> Parse(string localesXml)
        {
            LocalizationResourceManager.LocalesDebugPrint("\nEntering Parse");
            
            var localesDocument = XDocument.Load(XmlReader.Create(new StringReader(localesXml)));

            // Validate Root
            if (localesDocument.Root == null || localesDocument.Root.Name != LocalesTag)
            {
                LocalizationResourceManager.LocalesDebugPrint("Root not valid in Parse()");
                if (localesDocument.Root == null)
                {
                    LocalizationResourceManager.LocalesDebugPrint("    localesDocument.Root == null");
                }
                if (localesDocument.Root != null && localesDocument.Root.Name != LocalesTag)
                {
                    LocalizationResourceManager.LocalesDebugPrint("    localesDocument.Root.Name != LocalesTag");
                    LocalizationResourceManager.LocalesDebugPrint("    localesDocument.Root.Name : " + localesDocument.Root.Name.ToString());
                    LocalizationResourceManager.LocalesDebugPrint("    LocalesTag : " + LocalesTag.ToString());
                }

                return null;
            }

            LocalizationResourceManager.LocalesDebugPrint("  Parsing.  LocalTag is : " + LocaleTag.ToString());

            var locales = new List<Locale>();
            foreach (var localeElement in localesDocument.Root.Elements())
            {
                if (localeElement.Name != LocaleTag)
                {
                    LocalizationResourceManager.LocalesDebugPrint("    skipping " + localeElement.Name.ToString());

                    continue;
                }

                var locale = new Locale();
                foreach (var localeSubelement in localeElement.Elements())
                {
                    var localeSubelementName = localeSubelement.Name.LocalName;
                    if (_localeSubelementDelegates.ContainsKey(localeSubelementName))
                    {
                        _localeSubelementDelegates[localeSubelementName](locale, localeSubelement.Value);
                    }
                }

                // For some reason the last updated time sometimes comes out null.  So, if 
                // it is missing, just write in the default time.  So far this only seems
                // to be happening with people running Windows in Vietnam.
                if (!locale.LastUpdated.HasValue)
                {
                    locale.LastUpdated = new DateTime(2020, 1, 9, 11, 11, 11, DateTimeKind.Utc);
                }

                if (locale.IsComplete())
                {
                    locales.Add(locale);
                    LocalizationResourceManager.LocalesDebugPrint("    adding locale : " + locale.ToString());
                }
                else
                {
                    LocalizationResourceManager.LocalesDebugPrint("    adding locale failed, not complete : " + locale.ToString());
                }

            }

            LocalizationResourceManager.LocalesDebugPrint("Done Parsing");

            return locales;
        }

        readonly Dictionary<string, Action<Locale, string>> _localeSubelementDelegates = new Dictionary<string, Action<Locale, string>>
        {
            {LanguageTag, (locale, language) => { locale.Language = language; }},
            {DirectoryTag, (locale, directory) => { locale.Directory = directory; }},
            {NativeTag, (locale, native) => { locale.Native = native; }},
            {LastUpdatedTag, (locale, lastUpdated) =>
                {
                    DateTime parsed;
                    CultureInfo culture = new CultureInfo("en-US");
                    if (DateTime.TryParse(lastUpdated, culture, DateTimeStyles.None, out parsed))
                    {
                        locale.LastUpdated = parsed;
                    }
                }
            }
        };
    }

    class Locale
    {
        public string Language { get; set; }
        public string Directory { get; set; }
        public string Native { get; set; }
        public DateTime? LastUpdated { get; set; }

        /// <summary>
        /// Empty c'tor for serialization.
        /// </summary>
        public Locale()
        {
        }

        public Locale(string directory, string language, string native, DateTime lastUpdated)
        {
            this.Directory = directory;
            this.Language = language;
            this.Native = native;
            this.LastUpdated = lastUpdated;
        }

        public bool IsComplete()
        {
            bool result =   !string.IsNullOrEmpty(Language) &&
                            !string.IsNullOrEmpty(Directory) &&
                            !string.IsNullOrEmpty(Native) &&
                            LastUpdated.HasValue;

            return result;
        }

        public override string ToString()
        {
            string str = "";

            str = Language + " " + Native + " " + Directory;
            if (LastUpdated.HasValue)
            {
                str += " " + LastUpdated.Value.ToString();
            }

            return str;
        }
    }

#endregion

}   // end of namespace Boku.Common.Localization
