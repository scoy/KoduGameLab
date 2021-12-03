// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.


#if !PREBOOT
//#define IMPORT_DEBUG
#endif

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Win32;

using Boku;

namespace Boku.Common
{
    [Flags]
    public enum StorageSource
    {
        TitleSpace = 1 << 0,
        UserSpace = 1 << 1,
        All = TitleSpace | UserSpace
    }

    public partial class Storage4
    {
        #region Members

        static string startupDir;               // Directory where exe started.  Used as root for titlespace
        static string userLocation;             // Root path of userspace.
        static string titleLocation;            // Root path of titlespace.
        static string userOverrideLocation;     // Location given by the user to override normal location.

        static string uniqueMachineID = String.Empty;   // Filled in during Init()

        #endregion

        #region Accessors

        /// <summary>
        /// Root path to user space.
        /// </summary>
        public static string UserLocation
        {
            get { return userLocation; }
        }

        /// <summary>
        /// Root path to title space.
        /// </summary>
        public static string TitleLocation
        {
            get { return titleLocation; }
        }

        /// <summary>
        /// Set during startup, this is where the exe resides and
        /// acts as the root of title space.
        /// </summary>
        public static string StartupDir
        {
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    startupDir = value;
                    titleLocation = value;
                }
            }
        }

        public static string UserOverrideLocation
        {
            set
            {
                if(!string.IsNullOrEmpty(value))
                {
                    // TODO (****) Should we add more validation that the path is good?
                    // What should we check for?
                    userOverrideLocation = value;
                    userLocation = value;
                }
            }
            get { return userOverrideLocation; }
        }

        /// <summary>
        /// Returns the hashed MACAddress for this machine.
        /// </summary>
        public static string UniqueMachineID
        {
            get { return uniqueMachineID; }
        }

        #endregion

        #region Public


        /// <summary>
        /// One time init of storage.
        /// </summary>
        public static void Init()
        {
            Debug.Assert(userOverrideLocation == null, "Init should be called before this is set.");

            // Create default user location.
            userLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"SavedGames\Boku\Player1");

            // For some reason, MAC addresses seem to change regularly so
            // changing this to use a hash of the MachineGuid registry entry.
            // The reason we need a unique number is so that in a school setting where we
            // have multiple machines writing to the same network disk space we get per machine
            // settings and per machine AutoSave stacks.

            string id = "";
            try
            {
                RegistryKey regKey = Microsoft.Win32.RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64);
                RegistryKey subKey = regKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                id = subKey.GetValue("MachineGuid").ToString();
            }
            catch 
            { 
            }

            // If the registry entry fails, fall back to the MAC address.
            if (String.IsNullOrEmpty(id))
            {
                id = GetHashedMACAddress();
            }

            uniqueMachineID = id.GetHashCode().ToString();
        }

        /// <summary>
        /// Resets the userOverrideLocation and userLocation to default values.
        /// We have this because there are some startup ordering issues we're
        /// trying to work around related to importing files.
        /// </summary>
        public static void ResetUserOverrideLocation()
        {
            // Create default user location.
            userLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"SavedGames\Boku\Player1");
            userOverrideLocation = null;
        }

        /// <summary>
        /// Open file for reading.
        /// </summary>
        /// <param name="filePath">Path relative to storage source location.</param>
        /// <param name="sources">Which source(s) to look in.  If both, will look in UserSpace first.</param>
        /// <returns></returns>
        public static Stream OpenRead(string filePath, StorageSource sources)
        {
            Stream stream = null;

            try
            {
                // If both StorageSource flags are set, we want to try user space first.

                // Try UserSpace.
                if ((sources & StorageSource.UserSpace) != 0)
                {
                    string fullPath = Path.Combine(UserLocation, filePath);
                    if (File.Exists(fullPath))
                    {
                        stream = File.OpenRead(fullPath);
                    }
                }

                // Try TitleSpace.
                if (stream == null && (sources & StorageSource.TitleSpace) != 0)
                {
                    string fullPath = Path.Combine(TitleLocation, filePath);
                    if (File.Exists(fullPath))
                    {
                        stream = File.OpenRead(fullPath);
                    }
                }
            }
            catch (Exception e)
            {
                string str = e.Message;
                if (e.InnerException != null)
                {
                    str += e.InnerException.Message;
                }
                Debug.Assert(false, str);
            }

            return stream;
        }   // end of OpenRead()

        /// <summary>
        /// Open file for write.  Since TitleSpace is read-only it is
        /// assumed that filePath is relative to UserSpace.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static Stream OpenWrite(string filePath)
        {
            Stream stream = null;

            try
            {
                string fullPath = Path.Combine(UserLocation, filePath);
                // If file exists, delete.
                if(FileExists(filePath, StorageSource.UserSpace))
                {
                    File.Delete(fullPath);
                }
                
                // Ensure the directory exists.
                string dirPath = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                // Open the stream.
                stream = File.OpenWrite(fullPath);
            }
            catch (Exception e)
            {
                if (e != null)
                {
                    // Debug.Assert(false, e.Message);
                }
            }

            return stream;
        }   // end of OpenWrite()

        public static Stream Open(string filePath, FileMode fileMode)
        {
            Stream stream = null;

            try
            {
                string fullPath = Path.Combine(UserLocation, filePath);

                // Ensure the directory exists.
                string dirPath = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                // Open the stream.
                stream = File.Open(fullPath, fileMode);
            }
            catch (Exception e)
            {
                Debug.Assert(false, e.Message);
            }

            return stream;
        }   // end of Open()

        /// <summary>
        /// Close a stream.
        /// </summary>
        /// <param name="stream"></param>
        public static void Close(Stream stream)
        {
            if (stream != null)
            {
                stream.Close();
            }
        }   // end of Close()


        public static String[] GetFiles(string path, StorageSource sources)
        {
            return GetFiles(path, null, sources, SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Find all files with given relative path and filter. Checks title space FIRST, then user.
        /// NOTE This returns full path names, not relative ones.
        /// NOTE Does not look in subdirectories.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pattern"></param>
        /// <param name="sources"></param>
        /// <returns></returns>
        public static String[] GetFiles(string path, string pattern, StorageSource sources)
        {
            return GetFiles(path, pattern, sources, SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Find all files with given relative path and filter. Checks title space FIRST, then user.
        /// NOTE This returns full path names, not relative ones.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pattern"></param>
        /// <param name="sources"></param>
        /// <param name="searchOption"></param>
        /// <returns></returns>
        public static String[] GetFiles(string path, string pattern, StorageSource sources, SearchOption searchOption)
        {
            String[] list = null;

            if ((sources & StorageSource.TitleSpace) != 0)
            {
                string fullPath = Path.Combine(TitleLocation, path);
                if(string.IsNullOrEmpty(pattern))
                {
                    if (Directory.Exists(fullPath))
                    {
                        list = Directory.GetFiles(fullPath);
                    }
                }
                else
                {
                    if (Directory.Exists(fullPath))
                    {
                        list = Directory.GetFiles(fullPath, pattern);
                    }
                }
            }

            if ((sources & StorageSource.UserSpace) != 0)
            {
                string fullPath = Path.Combine(UserLocation, path);
                if (Directory.Exists(fullPath))
                {
                    string[] userList = null;
                    if (string.IsNullOrEmpty(pattern))
                    {
                        if (Directory.Exists(fullPath))
                        {
                            userList = Directory.GetFiles(fullPath);
                        }
                    }
                    else
                    {
                        if (Directory.Exists(fullPath))
                        {
                            userList = Directory.GetFiles(fullPath, pattern);
                        }
                    }
                    list = Concat(userList, list);
                }
            }

            return list;
        }

        /// <summary>
        /// Return whether a file exists in either title or user space.
        /// Looks in user space first.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool FileExists(string filePath, StorageSource sources)
        {
            bool result = false;

            try
            {
                if (!string.IsNullOrEmpty(filePath))
                {
                    // Test user space first.
                    if ((sources & StorageSource.UserSpace) != 0)
                    {
                        string fullPath = Path.Combine(UserLocation, filePath);
                        result = File.Exists(fullPath);
                    }

                    // If not found, try title space.
                    if (result == false && (sources & StorageSource.TitleSpace) != 0)
                    {
                        string fullPath = Path.Combine(TitleLocation, filePath);
                        result = File.Exists(fullPath);
                    }
                }
            }
            catch (Exception e)
            {
                string str = e.Message;
                if (e.InnerException != null)
                {
                    str += e.InnerException.Message;
                }
                Debug.Assert(false, str);

#if IMPORT_DEBUG
                LevelPackage.DebugPrint("FileExists threw an error");
                LevelPackage.DebugPrint(e.ToString());
#endif
            }

#if IMPORT_DEBUG
            if (result == false)
            {
                LevelPackage.DebugPrint("FileExists cant't find : " + filePath);
                LevelPackage.DebugPrint("    UserLocation : " + UserLocation);
                LevelPackage.DebugPrint("    TitleLocation : " + TitleLocation);

                string dirPath = Path.GetDirectoryName(filePath);
                string[] files = GetFiles(dirPath, sources);
                LevelPackage.DebugPrint("==Files in : " + dirPath);
                if (files == null || files.Length == 0)
                {
                    LevelPackage.DebugPrint("    none");
                }
                else
                {
                    foreach (string file in files)
                    {
                        LevelPackage.DebugPrint("    " + file);
                    }
                }
            }
#endif

            return result;
        }   // end of FileExists()

        /// <summary>
        /// Checks if a directory exists.  If both storage sources
        /// are specified, will check user space first.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="sources"></param>
        /// <returns></returns>
        public static bool DirExists(string path, StorageSource sources)
        {
            bool result = false;

            try
            {
                // Test user space first.
                if ((sources & StorageSource.UserSpace) != 0)
                {
                    string fullPath = Path.Combine(UserLocation, path);
                    result = Directory.Exists(fullPath);
                }

                // Test title space.
                if (result == false && (sources & StorageSource.TitleSpace) != 0)
                {
                    string fullPath = Path.Combine(TitleLocation, path);
                    result = Directory.Exists(fullPath);
                }
            }
            catch (Exception e)
            {
                string str = e.Message;
                if (e.InnerException != null)
                {
                    str += e.InnerException.Message;
                }
                Debug.Assert(false, str);
            }

            return result;
        }   // end of DirExists()

        /// <summary>
        /// Internal - create the writable directory if it doesn't exist.
        /// Note that since it's writable, it's in user space, not title space.
        /// NOTE dirPath should not be a filename.
        /// </summary>
        /// <param name="dirPath"></param>
        /// <returns></returns>
        public static void CreateDirectory(string dirPath)
        {
            try
            {
                string fullPath = Path.Combine(UserLocation, dirPath);
                Directory.CreateDirectory(fullPath);
            }
            catch (Exception e)
            {
                string str = e.Message;
                if (e.InnerException != null)
                {
                    str += e.InnerException.Message;
                }
                Debug.Assert(false, str);
            }
        }   // end of CreateDirectory()

        /// <summary>
        /// Deletes the specified file.  Assumes
        /// it must be userspace.  
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>true on success</returns>
        public static bool Delete(string filePath)
        {
            bool result = false;

            try
            {
                string fullPath = Path.Combine(UserLocation, filePath);

                if (IsReadOnly(fullPath))
                {
                    ClearReadOnly(fullPath);
                }

                File.Delete(fullPath);
                result = true;
            }
            catch (Exception e)
            {
                string str = e.Message;
                if (e.InnerException != null)
                {
                    str += e.InnerException.Message;
                }
                Debug.Assert(false, str);
            }

            return result;
        }   // end of Delete

        /// <summary>
        /// Looks at a file and returns true if the file is read only.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static bool IsReadOnly(string filePath)
        {
            if (File.Exists(filePath))
            {
                FileAttributes attr = File.GetAttributes(filePath);
                if ((attr & FileAttributes.ReadOnly) != 0)
                {
                    return true;
                }
            }

            return false;
        }   // end of IsReadOnly()

        /// <summary>
        /// Ensures that a file is writable (deletable)
        /// </summary>
        /// <param name="filePath"></param>
        public static void ClearReadOnly(string filePath)
        {
            if (File.Exists(filePath))
            {
                FileAttributes attr = File.GetAttributes(filePath);
                attr &= ~FileAttributes.ReadOnly;
                File.SetAttributes(filePath, attr);
            }
        }   // end of ClearReadOnly()

        public static string[] ReadAllLines(string filePath, StorageSource sources)
        {
            string[] lines = null;

            if ((sources & StorageSource.UserSpace) != 0)
            {
                if(FileExists(filePath, StorageSource.UserSpace))
                {
                    string fullPath = Path.Combine(UserLocation, filePath);
                    lines = File.ReadAllLines(fullPath);
                }
            }

            if (lines == null && (sources & StorageSource.TitleSpace) != 0)
            {
                if (FileExists(filePath, StorageSource.TitleSpace))
                {
                    string fullPath = Path.Combine(TitleLocation, filePath);
                    lines = File.ReadAllLines(fullPath);
                }
            }

            return lines;
        }   // end of ReadAllLines()

        /// <summary>
        /// Get write time. Looks in user space first
        /// then in title space.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static DateTime GetLastWriteTimeUtc(string filePath, StorageSource sources)
        {
            DateTime time = DateTime.MinValue;

            if ((sources & StorageSource.UserSpace) != 0)
            {
                if (FileExists(filePath, StorageSource.UserSpace))
                {
                    string fullPath = Path.Combine(UserLocation, filePath);
                    time = File.GetLastWriteTimeUtc(fullPath);
                }
            }

            if (time == DateTime.MinValue && (sources & StorageSource.TitleSpace) != 0)
            {
                if (FileExists(filePath, StorageSource.TitleSpace))
                {
                    string fullPath = Path.Combine(TitleLocation, filePath);
                    time = File.GetLastWriteTimeUtc(fullPath);
                }
            }

            return time;
        }   // end of GetLastWriteTimeUtc()

        /// <summary>
        /// Set write time. Assumes file is in user space.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static void SetLastWriteTimeUtc(string filePath, DateTime dateTimeUtc)
        {
            if (FileExists(filePath, StorageSource.UserSpace))
            {
                string fullPath = Path.Combine(UserLocation, filePath);
                File.SetLastAccessTimeUtc(fullPath, dateTimeUtc);
            }
        }   // end of SetLastWriteTimeUtc()

        /// <summary>
        /// Gets the hashed MAC address of the current machine.  Used to make autosave files unique.
        /// No longer used
        /// </summary>
        /// <returns></returns>
        private static string GetHashedMACAddress()
        {
            string MACAddress = String.Empty;

            try
            {
                List<string> macs = new List<string>();

                ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
                ManagementObjectCollection moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    if ((bool)mo["IPEnabled"] == true)
                    {
                        string mac = mo["MacAddress"].ToString();
                        if (!String.IsNullOrEmpty(mac))
                        {
                            macs.Add(mac);
                        }
                    }

                    if (MACAddress == String.Empty) // only return MAC Address from first card
                    {
                        if ((bool)mo["IPEnabled"] == true) MACAddress = mo["MacAddress"].ToString();
                    }
                    mo.Dispose();
                }

                MACAddress = MACAddress.Replace(":", "");
            }
            catch(Exception e)
            {
                if (e != null)
                {
                }
            }

            MACAddress = MACAddress.GetHashCode().ToString();

            return MACAddress;
        }   // end of GetHashedMACAddress()


        public static StreamWriter OpenStreamWriter(string filePath, Encoding encoding = null)
        {
            Stream stream = OpenWrite(filePath);
            StreamWriter sw = null;
            if (stream != null)
            {
                sw = encoding == null ? new StreamWriter(stream) : new StreamWriter(stream, encoding);
            }
            return sw;
        }

        #endregion

        #region Internal

        /// <summary>
        /// Combine two arrays of file names
        /// </summary>
        /// <param name="user"></param>
        /// <param name="title"></param>
        /// <returns></returns>
        private static String[] Concat(String[] user, String[] title)
        {
            String[] total = null;
            int totalLength = 0;
            if (user != null)
                totalLength += user.Length;
            if (title != null)
                totalLength += title.Length;
            if (totalLength > 0)
            {
                total = new String[totalLength];
                int i = 0;
                if (user != null)
                {
                    foreach (string s in user)
                    {
                        total[i++] = s;
                    }
                }
                if (title != null)
                {
                    foreach (string s in title)
                    {
                        total[i++] = s;
                    }
                }
            }
            return total;
        }   // end of Concat()

        #endregion

    }   // end of class Storage4



    public class XnaStorageHelper : BokuShared.StorageHelper
    {
        private static XnaStorageHelper instance;

        public static XnaStorageHelper Instance
        {
            get
            {
                if (instance == null)
                    instance = new XnaStorageHelper();
                return instance;
            }
        }

        public override Stream OpenRead(string filename)
        {
            return Storage4.OpenRead(filename, StorageSource.All);
        }

        public override Stream OpenRead(string filename, int flags)
        {
            return Storage4.OpenRead(filename, (StorageSource)flags);
        }

        public override Stream OpenWrite(string filename)
        {
            return Storage4.OpenWrite(filename);
        }

        public override void Close(Stream stream)
        {
            Storage4.Close(stream);
        }
    }   // end of class XnaStorageHelper


}   // end of namespace Boku.Common
