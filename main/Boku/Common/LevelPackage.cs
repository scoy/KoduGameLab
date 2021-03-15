// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

//#define IMPORT_DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Xml.Serialization;

using Ionic.Zip;
using System.IO.Packaging;

using Boku.Audio;
using BokuShared;

namespace Boku.Common
{
    /// <summary>
    /// Manages the process of importing and exporting Kodu levels.
    /// </summary>
    public class LevelPackage
    {
        static string LevelFolder = "Level";
        static string StuffFolder = "Stuff";
        static string TerrainFolder = "Terrain";
        static string ThumbnailFolder = "Thumbnail";

        static string DownloadsFolder = "Downloads";
        static string HeightMapsFolder = "TerrainHeightMaps";

        static public string importsPath;

        static string exportsPath;

        static public string ExportsPath
        {
            get { return exportsPath; }
        }

        /// <summary>
        /// Creates working folders if necessary. If a kodu package was given on the command line,
        /// the file will be copied to the imports folder.
        /// </summary>
        /// <param name="cmdLine"></param>
        /// <returns>true on success</returns>
        public static bool Initialize(CmdLine cmdLine)
        {
            string localSpacePath = Storage4.UserLocation;

            // Store the paths of our import/export working folders.
            importsPath = "Imports";

            // Put the exported levels in an easier to find directory.
            exportsPath = "Exports";

            // If we can get to the user location, make sure the full path is created.
            if (!Storage4.DirExists("", StorageSource.UserSpace))
            {
                Storage4.CreateDirectory("");
            }

            // Verify we can get to the user location.  If this fails it means the
            // above code wasn't able to write to the user location.
            if (!Storage4.DirExists("", StorageSource.UserSpace))
            {
                // User path is not found.  Fail.
                System.Windows.Forms.MessageBox.Show(
                    "The user's Save Folder cannot be accessed : " + Storage4.UserLocation
                    + "\nPlease run the Configuration tool and change to a valid location.", 
                    "Kodu : Save Folder Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Asterisk);

                return false;
            }

            // Create our working folders if necessary.
            if (!Storage4.DirExists(importsPath, StorageSource.UserSpace))
            {
                Storage4.CreateDirectory(importsPath);
            }
            if (!Storage4.DirExists(exportsPath, StorageSource.UserSpace))
            {
                Storage4.CreateDirectory(exportsPath);
            }

            // If an import was specified on the command line, copy it to our imports folder.
            string srcName = cmdLine.GetString("Import", null);

            // HACK HACK : If srcName is null, check for a Kodu2 file as an arg.
            // This is because the file association which prepends the /Import flag 
            // is only working for .kodu and not .kodu2.
            if (srcName == null)
            {
                foreach (string str in cmdLine.MultiLine)
                {
                    if (str.EndsWith(".kodu2", StringComparison.InvariantCultureIgnoreCase))
                    {
                        srcName = str;
                        break;
                    }
                }
            }


            if (!String.IsNullOrEmpty(srcName) && File.Exists(srcName))
            {
                string dstFilename = Path.Combine(Storage4.UserLocation, "Imports", Path.GetFileName(srcName));
                try
                {
                    File.Copy(srcName, dstFilename);
                }
                catch { }
            }
            return true;
        }

        /// <summary>
        /// Import all .kodu2 packages in the Imports folder into the Downloads bucket.
        /// Note this is the non-WinRT version which uses ZipPackage rather than ZipArchive.
        /// </summary>
        /// <param name="importedLevels">A list of the levels we imported.  This is used on launch to just directly to the imported level.</param>
        /// <returns>True if verything ok, false if one or more levels are from a newer version of Kodu.</returns>
        public static bool ImportLevels2(List<Guid> importedLevels)
        {
            bool result = true;

            // Get list of files in the imports folder.
            string[] files = Storage4.GetFiles(importsPath, "*.kodu2", StorageSource.UserSpace);

            if (files != null && files.Length > 0)
            {
                foreach (string filename in files)
                {
                    bool success = true;
                    Guid guid = Guid.Empty;

                    // Open file via DotNetZip and copy level info into proper place (DOWNLOADS folder)
                    try
                    {
                        string path = Path.Combine(importsPath, filename);

                        //System.Windows.Forms.DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(
                        //    "Importing file : " + path);

                        using (ZipFile zip = ZipFile.Read(path))
                        {
                            foreach (ZipEntry e in zip)
                            {
                                string targetPath = @"Content\Xml\Levels";
                                string mainFilePath = null;

                                string partFullName = e.FileName;
                                string partFilename = Path.GetFileName(partFullName);

                                // If the zip file hase been hand edited we get entries for all the folders
                                // as well as the files.  Skip over the folders.
                                if (string.IsNullOrEmpty(partFilename))
                                {
                                    continue;
                                }

                                if (partFullName.StartsWith(LevelFolder))
                                {
                                    targetPath = Path.Combine(targetPath, DownloadsFolder, partFilename);
                                    // Save path for testing version.
                                    mainFilePath = targetPath;
                                }
                                else if (partFullName.StartsWith(StuffFolder))
                                {
                                    targetPath = Path.Combine(targetPath, DownloadsFolder, StuffFolder, partFilename);
                                }
                                else if (partFullName.StartsWith(ThumbnailFolder))
                                {
                                    targetPath = Path.Combine(targetPath, DownloadsFolder, partFilename);
                                }
                                else if (partFullName.StartsWith(TerrainFolder))
                                {
                                    // Note this is in in Downloads
                                    targetPath = Path.Combine(targetPath, StuffFolder, HeightMapsFolder, partFilename);
                                }
                                else
                                {
                                    if (!partFilename.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                                    {
                                        Debug.Assert(false, "Unknown folder found while importing .kodu2 file : " + partFilename);
                                    }
                                    targetPath = null;
                                }

                                if (mainFilePath == null)
                                {
                                    // Not a main file, just copy over with no changes.
                                    if (e != null && targetPath != null)
                                    {
                                        using (var reader = e.OpenReader())
                                        {
                                            byte[] bytes = new byte[reader.Length];
                                            reader.Read(bytes, 0, (int)reader.Length);

                                            Stream fileStream = Storage4.OpenWrite(targetPath);
                                            fileStream.Write(bytes, 0, (int)reader.Length);
                                            fileStream.Close();
                                        }
                                    }
                                }
                                else
                                {
                                    // It's a main file so we need to:
                                    //      Change the stuff file path from MyWorlds to Downloads.
                                    //      Change genres to be Downloads rather than MyWorlds.
                                    //      Check the version number and set result to false if it is newer than the client we're running.
                                    Stream fileStream = Storage4.OpenWrite(targetPath);

                                    using (StreamWriter sw = new StreamWriter(fileStream))
                                    {
                                        // Break file into lines.
                                        string[] lines;
                                        using (var reader = e.OpenReader())
                                        {
                                            byte[] bytes = new byte[reader.Length];
                                            reader.Read(bytes, 0, (int)reader.Length);

                                            System.Text.Encoding encoding = new System.Text.UTF8Encoding();
                                            string fullFileString = encoding.GetString(bytes);

                                            char[] seperators = { '\n', '\r' };
                                            lines = fullFileString.Split(seperators);
                                        }

                                        for (int i = 0; i < lines.Length; i++)
                                        {
                                            string line = lines[i];

                                            if (ProcessMainFileLine(ref line, ref guid, partFilename) == false)
                                            {
                                                result = false;
                                            }

                                            if (line != null && line.Length > 0)
                                            {
                                                sw.WriteLine(line);
                                            }

                                        }   // end of while loop over lines in file.
                                    }   // end of using statements for StreamReader/Writer

                                }   // end else for main files.

                            }   // end loop over each entry.
                        }   // end of using ZipFile
                    }
                    catch (Exception e)
                    {
                        if (e != null)
                        {
                            success = false;
                        }
                    }

                    if (success)
                    {
                        // Add world to list of successful imports.
                        if (importedLevels != null)
                        {
                            importedLevels.Add(guid);
                        }

                        // Remove the file from the Imports dir.
                        string path = Path.Combine(importsPath, filename);
                        Storage4.Delete(path);
                    }   // end if success

                }   // end loop over each file.
            }   // end if any files

            return result;
        }   // end of ImportLevels2()

        /// <summary>
        /// Filters a line from the main xml file for import.  An effort to 
        /// remove a bunch of code duplication.
        ///
        ///     Change the stuff file path from MyWorlds to Downloads.
        ///     Change genres to be Downloads rather than MyWorlds.
        ///     Check the version number and set result to false if it is newer than the client we're running.
        /// </summary>
        /// <param name="line"></param>
        /// <param name="guid"></param>
        /// <param name="partFilename"></param>
        /// <returns>true if ok, false if from newer version than current client</returns>
        static bool ProcessMainFileLine(ref string line, ref Guid guid, string partFilename)
        {
            bool result = true;

            // Is this level from a single level game or is it the first
            // level of a multi-level game?
            if (line.Contains("<LinkedFromLevel xsi:nil=\"true\" />"))
            {
                // Save away guid.
                if (partFilename != null)
                {
                    guid = new Guid(Path.GetFileNameWithoutExtension(partFilename));
                }
            }

            //
            // Fix up path of Stuff file.
            //
            int startIndex = line.IndexOf("<stuffFilename>");
            int endIndex = -1;
            if (startIndex >= 0)
            {
                endIndex = line.IndexOf(@"</stuffFilename>");
                // Replace MyWorlds with Downloads.
                // We do it like this to ensure we only replace the correct "MyWorlds".
                // The Replace function has no way of limiting this.
                int index = line.IndexOf("MyWorlds", startIndex);
                if (index >= 0 && index < endIndex)
                {
                    line = line.Remove(index, "MyWorlds".Length);
                    line = line.Insert(index, "Downloads");
                }
            }

            //
            // Fix up Genres.
            //
            startIndex = line.IndexOf("<genres>");
            if (startIndex >= 0)
            {
                int startPos = startIndex + "<genres>".Length;
                int endPos = line.IndexOf("</", startPos);
                if (startPos >= 0 && endPos > startPos)
                {
                    int genre = Int32.Parse(line.Substring(startPos, endPos - startPos));
                    // Clear MyWorlds
                    genre &= ~((int)Genres.MyWorlds);
                    // Set Downloads.
                    genre |= (int)Genres.Downloads;
                    line = line.Substring(0, startPos) + genre.ToString() + line.Substring(endPos);
                }
            }

            //
            // Check version number.
            //
            startIndex = line.IndexOf("<KCodeVersion>");
            if (startIndex >= 0)
            {
                int startPos = startIndex + "<KCodeVersion>".Length;
                int endPos = line.IndexOf("</", startPos);
                if (startPos >= 0 && endPos > startPos)
                {
                    int version = int.Parse(line.Substring(startPos, endPos - startPos));

                    if (version > int.Parse(Program2.CurrentKCodeVersion))
                    {
                        // Spawn warning dialog for user.
                        result = false;
                    }
                }
            }   // end of test for version

            return result;
        }   // end of ProcessMainFileLine()


#if IMPORT_DEBUG
        static public void DebugPrint(string text)
        {
            string debugPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\ImportDebug.txt";
            TextWriter tw = new StreamWriter(debugPath, true);
            tw.WriteLine(text);
            tw.Close();
        }
#else
        static public void DebugPrint(string text)
        {
            // Do nothing...
        }
#endif


        /// <summary>
        /// Import all .kodu packages in the Imports folder into the Downloads bucket.
        /// </summary>
        /// <param name="importedLevels"></param>
        /// <returns>True if verything ok, false if one or more levels are from a newer version of Kodu.</returns>
        public static bool ImportLevels(List<Guid> importedLevels)
        {
            bool result = true;

#if IMPORT_DEBUG
            DebugPrint("\n\nNew Run\n\n");
            DebugPrint("imports path : " + importsPath);
#endif

            string[] files = Storage4.GetFiles(importsPath, "*.kodu", StorageSource.UserSpace, SearchOption.TopDirectoryOnly);

#if IMPORT_DEBUG
            if (files == null)
            {
                DebugPrint("no files to import");
                DebugPrint("exiting");
                return result;
            }

            // Write a line of text to the file
            DebugPrint("Importing the following files:");
            foreach (string file in files)
            {
                DebugPrint("  " + file);
            }
            DebugPrint("...");
#endif

            if (files != null && files.Length > 0)
            {
                Decompressor decomp = new Decompressor();

                foreach (string file in files)
                {
                    string fullPathToCab = Path.GetFullPath(file);

                    //System.Windows.Forms.DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(
                    //    "Importing file : " + fullPathToCab);


#if IMPORT_DEBUG
                    DebugPrint("file : " + file);
                    DebugPrint("fullPathToCab : " + fullPathToCab);
#endif

                    try
                    {
                        string destFolderPath = Path.Combine(Storage4.UserLocation, BokuGame.Settings.MediaPath, BokuGame.DownloadsPath);

#if IMPORT_DEBUG
                        DebugPrint("destFolderPath : " + destFolderPath);
                        DebugPrint("Creating Decompressor...");
#endif

                        // Need to clear this here otherwise we keep trying to delete the same temp files over and over.
                        decomp.ImportedLevels.Clear();
                        decomp.Create();
                        decomp.Expand(destFolderPath, fullPathToCab);
                        decomp.Destroy();

#if IMPORT_DEBUG
                        DebugPrint("...done decompressing.");
#endif

                        try
                        {
#if IMPORT_DEBUG
                            DebugPrint("Deleting " + fullPathToCab);
#endif
                            // Delete original now that the level has been imported.
                            File.Delete(fullPathToCab);
                        }
                        catch { }

#if IMPORT_DEBUG
                        DebugPrint(" ");
                        DebugPrint("decomp.ImportedLevels.Count : " + decomp.ImportedLevels.Count.ToString());
#endif

                        // Cabs may contain more than one level.  So, for each level in the cab
                        // fix up path info.
                        for (int i = 0; i < decomp.ImportedLevels.Count; ++i)
                        {
#if IMPORT_DEBUG
                            DebugPrint("i = " + i.ToString());
                            DebugPrint("decomp.ImportedLevels[i]" + decomp.ImportedLevels[i].ToString());
#endif
                            // Find the stuffFilename entry in the level xml and fix it's path information (changing from MyWorlds to Downloads)
                            string fullPathToLevelFilename = destFolderPath + decomp.ImportedLevels[i].ToString() + ".xml";
                            FileStream level = File.OpenRead(fullPathToLevelFilename + ".tmp");
                            StreamReader reader = new StreamReader(level);
                            // Delete existing file if there.
                            File.Delete(fullPathToLevelFilename);
                            StreamWriter writer = File.CreateText(fullPathToLevelFilename);
#if IMPORT_DEBUG
                            DebugPrint("fullPathToLevelFilename : " + fullPathToLevelFilename);
                            DebugPrint("level is " + (level == null ? "null" : "not null"));
                            DebugPrint("reader is " + (reader == null ? "null" : "not null"));
                            DebugPrint("writer is " + (writer == null ? "null" : "not null"));
#endif

                            while (!reader.EndOfStream)
                            {
                                string line = reader.ReadLine();

                                Guid guid = Guid.Empty;
                                if (ProcessMainFileLine(ref line, ref guid, null) == false)
                                {
                                    result = false;
                                }

                                writer.WriteLine(line);
                            }

                            reader.Close();
                            writer.Close();

#if IMPORT_DEBUG
                            DebugPrint("Done importing, removing temp file : " + fullPathToLevelFilename + ".tmp");
#endif
                            File.Delete(fullPathToLevelFilename + ".tmp");
                        }
                    }
                    catch (Exception e)
                    {
                        if (e != null)
                        {
#if IMPORT_DEBUG
                            DebugPrint("ERROR");
                            if (e != null)
                            {
                                if (e.Message != null)
                                {
                                    DebugPrint("  Message : " + e.Message);
                                }
                                if (e.InnerException != null && e.InnerException.Message != null)
                                {
                                    DebugPrint("  InnerException.Message : " + e.InnerException.Message);
                                }
                                if (e.StackTrace != null)
                                {
                                    DebugPrint("  Stack : " + e.StackTrace);
                                }
                                DebugPrint("  ");
                                DebugPrint("  Full : " + e.ToString());
                            }
#endif
                        }
                    }
                }   // end of loop over files being imported.

#if IMPORT_DEBUG
                DebugPrint("importedLevels " + (importedLevels == null ? "is null" : "is not null"));
#endif

                if (importedLevels != null)
                {
#if IMPORT_DEBUG
                    foreach (Guid g in decomp.ImportedLevels)
                    {
                        DebugPrint("importing : " + g.ToString());
                    }
#endif
                    importedLevels.AddRange(decomp.ImportedLevels);
                }

            }
#if IMPORT_DEBUG
            DebugPrint("result is " + (result ? "good" : "bad"));
#endif

            return result;
        }   // end of ImportLevels()


        /// <summary>
        /// 
        /// </summary>
        /// <returns>True if all ok, false if one or more levels from newer version of Kodu.</returns>
        public static bool ImportAllLevels(List<Guid> importedLevelList)
        {
            bool result = true;
#if IMPORT_DEBUG
            DebugPrint("========");
            DebugPrint("user location " + Storage4.UserLocation);
            DebugPrint("title location " + Storage4.TitleLocation);
            try
            {
#endif
                result = ImportLevels(importedLevelList);   // Get any .kodu levels.
                result &= ImportLevels2(importedLevelList); // Get any .kodu2 levels.
#if IMPORT_DEBUG
            }
            catch (Exception e)
            {
                // We should never get here.  All errors should be caught in the individual
                // import methods and handled there so we can continue and process the next file.
                string str = "ERROR\n" + e.ToString();
                System.Windows.Forms.MessageBox.Show(str);
            }
#endif
            return result;
        }

        public static string CreateExportFilenameWithoutExtension(string levelTitle, string levelCreator)
        {
            string filenameWithoutExtension = null;

            // Ensure we have a base string from which to create the package filename.
            if (String.IsNullOrEmpty(levelTitle))
            {
                levelTitle = "Level";
            }

            if (String.IsNullOrEmpty(levelCreator))
            {
                levelCreator = "Unknown";
            }

            // Remove leading and trailing spaces.
            levelTitle = levelTitle.Trim();
            levelCreator = levelCreator.Trim();

            // Cap length of each filename component to 32 chars.
            if (levelTitle.Length > 32)
            {
                levelTitle = levelTitle.Substring(0, 32);
            }

            if (levelCreator.Length > 32)
            {
                levelCreator = levelCreator.Substring(0, 32);
            }

            // Remove leading and trailing spaces again.
            levelTitle = levelTitle.Trim();
            levelCreator = levelCreator.Trim();

            char[] invalidChars = Path.GetInvalidFileNameChars();

            // Replace invalid filename characters in level title with a dash.
            StringBuilder sbLevelTitle = new StringBuilder();
            foreach (char ch in levelTitle)
            {
                if (invalidChars.Contains(ch))
                    sbLevelTitle.Append('-');
                else
                    sbLevelTitle.Append(ch);
            }

            // Replace invalid filename characters in level creator with an underscore.
            StringBuilder sbLevelCreator = new StringBuilder();
            foreach (char ch in levelCreator)
            {
                if (invalidChars.Contains(ch))
                    sbLevelCreator.Append('-');
                else
                    sbLevelCreator.Append(ch);
            }

            // Create the base filename in the format "Level Title by Creator Name"
            filenameWithoutExtension = sbLevelTitle.ToString() + ", by " + sbLevelCreator.ToString();

            return filenameWithoutExtension;
        }   // end of CreateExportFilename()

        static void AddFileToZipPackage(Package package, string folder, string path)
        {
            string filename = Path.GetFileName(path);
            //Uri partUri = new Uri(Path.Combine(folder, filename), UriKind.Relative);
            Uri partUri = new Uri(@"/" + folder + "/" + filename, UriKind.Relative);
            PackagePart part = package.CreatePart(partUri, System.Net.Mime.MediaTypeNames.Text.Xml, CompressionOption.Maximum);
            Stream partStream = part.GetStream();
            using (Stream fileStream = Storage4.OpenRead(path, StorageSource.All))
            {
                // May be null for imported files that don't have hires screenshot.
                if (fileStream != null)
                {
                    fileStream.CopyTo(partStream);
                }
            }

        }   // end of AddFileToZipPackage()


        /// <summary>
        /// Export the given level, writing it to the Exports folder.
        /// </summary>
        /// <param name="levelTitle"></param>
        /// <param name="fullPathToLevelFilename"></param>
        /// <param name="fullPathToStuffFilename"></param>
        /// <param name="fullPathToTerrainFilename"></param>
        /// <param name="fullPathToThumbnailFilename"></param>
        /// <param name="userPath">User specified path for resulting file.  If null, calc default.</param>
        public static void ExportLevel(
            List<string> levelFiles,
            List<string> stuffFiles,
            List<string> thumbnailFiles,
            List<string> screenshotFiles,
            List<string> terrainFiles,
            string fileName,
            Stream outStream)
        {
            // Note We may allow multiple screenshots per level in the future
            // so don't assume a fixed number of them.
            Debug.Assert(levelFiles.Count == stuffFiles.Count &&
                            levelFiles.Count == thumbnailFiles.Count &&
                            levelFiles.Count == terrainFiles.Count);

            /*
            // Non-WinRT, use DotNetZip.
            using (ZipFile zip = new ZipFile(fileName, Encoding.UTF8))
            {
                foreach (string path in levelFiles)
                {
                    if (!String.IsNullOrEmpty(path))
                    {
                        zip.AddFile(path, LevelFolder);
                    }
                }

                Stream str = Storage4.Open(fileName, FileMode.Create);
                zip.Save(str);
            }
            */
            Stream stream = Storage4.Open(fileName, FileMode.Create);
            using (Package package = ZipPackage.Open(stream, FileMode.Create))
            {
                foreach (string path in levelFiles)
                {
                    if (!String.IsNullOrEmpty(path))
                    {
                        AddFileToZipPackage(package, LevelFolder, path);
                    }
                }
                foreach (string path in stuffFiles)
                {
                    if (!String.IsNullOrEmpty(path))
                    {
                        AddFileToZipPackage(package, StuffFolder, path);
                    }
                }
                foreach (string path in thumbnailFiles)
                {
                    if (!String.IsNullOrEmpty(path))
                    {
                        AddFileToZipPackage(package, ThumbnailFolder, path);
                    }
                }
                // Put screenshots in same folder as thumbnails.
                foreach (string path in screenshotFiles)
                {
                    if (!String.IsNullOrEmpty(path))
                    {
                        AddFileToZipPackage(package, ThumbnailFolder, path);
                    }
                }
                foreach (string path in terrainFiles)
                {
                    if (!String.IsNullOrEmpty(path))
                    {
                        AddFileToZipPackage(package, TerrainFolder, path);
                    }
                }

            }   // end of using around archive.

#if WRITE_CAB_FILES
            // Create the cab archive.
            try
            {
                Compressor comp = new Compressor();

                // Split user path into path and filename bits.
                int slash = fileName.LastIndexOf(Path.DirectorySeparatorChar) + 1;
                string path = fileName.Substring(0, slash);
                fileName = fileName.Substring(slash);
                comp.Create(path, fileName);

                for (int i = 0; i < levelFiles.Count; ++i)
                {
                    // Note that we can't keep the .tmp files in the normal Storage space
                    // since the cab compressor wants full path names.  So, need to find
                    // a safe place to put some temp files.  Since XNA supports Personal
                    // we'll use this.  Note this should be the same as the My Documents folder.
                    string tmpFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

                    //
                    // Find the stuffFilename entry in the level xml and fix it's path information (changing from MyWorlds to Downloads)
                    //
                    FileStream level = File.OpenRead(levelFiles[i]);
                    StreamReader reader = new StreamReader(level);
                    File.Delete(levelFiles[i] + ".tmp");
                    StreamWriter writer = File.CreateText(levelFiles[i] + ".tmp");

                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        if (line.Contains("<stuffFilename>"))
                        {
                            line = line.Replace("MyWorlds", "Downloads");
                        }
                        //replace genres of exported files to be "Downloads"
                        if (line.Contains("<genres>"))
                        {
                            int startPos = line.IndexOf("<genres>") + "<genres>".Length;
                            int endPos = line.IndexOf("</", startPos);
                            if (startPos >= 0 && endPos > startPos)
                            {
                                line = line.Substring(0, startPos) + ((int)Genres.Downloads).ToString() + line.Substring(endPos);
                            }
                        }
                        writer.WriteLine(line);
                    }

                    reader.Close();
                    writer.Close();

                    comp.AddFile("Level", levelFiles[i] + ".tmp", levelFiles[i], true);
                    comp.AddFile("Stuff", stuffFiles[i], null, true);
                    comp.AddFile("Thumbnail", thumbnailFiles[i], null, true);

                    // Terrain file will only be provided if the level isn't using a builtin terrain.
                    if (!String.IsNullOrEmpty(terrainFiles[i]))
                    {
                        comp.AddFile("Terrain", terrainFiles[i], null, true);
                    }

                }   // end of loop over level files.

                comp.Destroy();

                // ??? Brad doesn't like this idea...
                // Put the path of the exported file into the clipboard.
                // System.Windows.Forms.Clipboard.SetText(exportsPath + filename);

                // Make a sound acknowledging the export.
                Foley.PlayEndGame();
            }
            catch { }
#endif
        }   // end of ExportLevel()

    }   // end of class LevelPackage

    class Compressor : Cab.Compressor
    {
        public Compressor() : base(new Cab.FileHelper()) { }

        public override void Progress()
        {
            // TODO: Callback to renderer?
        }
    }

    class Decompressor : Cab.Decompressor
    {
        // A Cab file may have more than a single level in it.  This is
        // a list of the GUIDs of all the levels in the current file.
        public List<Guid> ImportedLevels = new List<Guid>();

        public Guid MostRecentImportedLevel;

        public override void Progress()
        {
            // TODO: Callback to renderer?
        }

        public override bool QueryExpandFile(ref string filepath, ref string filename)
        {
            if (filepath.EndsWith(@"Level\"))
            {
                filepath = Path.Combine(Storage4.UserLocation, BokuGame.Settings.MediaPath, BokuGame.DownloadsPath);
                MostRecentImportedLevel = new Guid(filename.Substring(0, filename.Length - 4));
                ImportedLevels.Add(MostRecentImportedLevel);
                // Write the level file with a .tmp extension, we'll copy it to its final filename in a post-process step wherein we fix some path information in the file.
                filename += ".tmp";
                return true;
            }
            else if (filepath.EndsWith(@"Stuff\"))
            {
                filepath = Path.Combine(Storage4.UserLocation, BokuGame.Settings.MediaPath, BokuGame.DownloadsStuffPath);
                return true;
            }
            else if (filepath.EndsWith(@"Terrain\"))
            {
                filepath = Path.Combine(Storage4.UserLocation, BokuGame.Settings.MediaPath, BokuGame.TerrainPath);
                return true;
            }
            else if (filepath.EndsWith(@"Thumbnail\"))
            {
                filepath = Path.Combine(Storage4.UserLocation, BokuGame.Settings.MediaPath, BokuGame.DownloadsPath);
                return true;
            }

            return false;
        }

    }   // end of class Decompressor

}   // end of namespace Boku.Common
