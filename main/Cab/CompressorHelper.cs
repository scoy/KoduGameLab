// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace Cab
{

    public interface ICompressorHelper
    {
        Stream Open(string filename, FileMode fileMode);
        Stream Open(string filename, FileMode fileMode, FileAccess fileAccess, FileShare fileShare);

        void Delete(string filename);

        FileAttributes GetAttributes(string filename);

        DateTime GetLastWriteTimeUtc(string filename);

        string GetTempFileName();
    }

    public class FileHelper : ICompressorHelper
    {
        public Stream Open(string filename, FileMode fileMode)
        {
            return File.Open(filename, fileMode);
        }
        public Stream Open(string filename, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            return File.Open(filename, fileMode, fileAccess, fileShare);
        }

        public void Delete(string filename)
        {
            File.Delete(filename);
        }

        public FileAttributes GetAttributes(string fileName)
        {
            return File.GetAttributes(fileName);
        }

        public DateTime GetLastWriteTimeUtc(string filename)
        {
            return File.GetLastWriteTimeUtc(filename);
        }

        public string GetTempFileName()
        {
            return Path.GetTempFileName();
        }
    }

    public class MemoryHelper : ICompressorHelper
    {
        interface IPseudoFile
        {
            bool IsExpandable { get; }
            bool CanWrite { get; }
            string Name { get; }

            Stream Open(SeekOrigin seek);
            void Delete();
        }
        class PseudoFile : MemoryStream, IPseudoFile
        {
            bool isOpened;
            bool isDeleted;

            public string Name
            {
                get { return name; }
            }
            readonly string name;

            public bool IsExpandable
            {
                get
                {
                    return isExpandable;
                }
            }
            readonly bool isExpandable;

            public override bool CanWrite
            {
                get
                {
                    return IsExpandable && base.CanWrite;
                }
            }

            public PseudoFile(string name) : base()
            {
                this.name = name;
                isExpandable = true;
            }
            public PseudoFile(string name, byte[] bytes) : base(bytes, false)
            {
                this.name = name;
                isExpandable = false;
            }

            public override void Close()
            {
                if (!isOpened)
                {
                    //Why are we closing a closed stream?
                }
                isOpened = false;
            }

            public Stream Open(SeekOrigin seek)
            {
                if (isDeleted)
                {
                    throw new Exception("Trying to open a deleted 'file'!");
                }
                if (isOpened)
                {
                    //Why are we trying to open two streams?
                }
                isOpened = true;

                this.Seek(0, seek);

                return this;
            }

            public void Delete()
            {
                isOpened = false;
                isDeleted = true;
                base.Close();
            }
        }

        readonly Dictionary<string, IPseudoFile> pseudoFiles;

        public MemoryHelper()
        {
            pseudoFiles = new Dictionary<string, IPseudoFile>();
        }

        /// <summary>
        /// Adds a readonly pseudo file.
        /// </summary>
        public void AddPseudoFile(string filename, byte[] bytes)
        {
            IPseudoFile pseudoFile = new PseudoFile(filename, bytes);
            pseudoFiles.Add(filename, pseudoFile);
        }

        public Stream Open(string filename, FileMode fileMode)
        {
            return Open(filename, fileMode, FileAccess.ReadWrite);
        }
        public Stream Open(string filename, FileMode fileMode, FileAccess fileAccess)
        {
            Stream result;

            IPseudoFile pseudoFile;

            bool fileExists = pseudoFiles.TryGetValue(filename, out pseudoFile);

            bool wantsWritable = (fileAccess & FileAccess.Write) != 0;

            switch (fileMode)
            {
                case FileMode.Append:
                    if (fileExists)
                    {
                        if (wantsWritable && !pseudoFile.CanWrite)
                        {
                            throw new Exception("'File' is read-only");
                        }

                        result = pseudoFile.Open(SeekOrigin.End);
                    }
                    else
                    {
                        throw new Exception("'File' doesn't exist");
                    }
                    break;

                case FileMode.Create:
                    if (fileExists)
                    {
                        if (wantsWritable && !pseudoFile.CanWrite)
                        {
                            throw new Exception("'File' is read-only");
                        }

                        result = pseudoFile.Open(SeekOrigin.Begin);
                    }
                    else
                    {
                        pseudoFile = new PseudoFile(filename);
                        pseudoFiles.Add(filename, pseudoFile);

                        result = pseudoFile.Open(SeekOrigin.Begin);
                    }
                    break;

                case FileMode.CreateNew:
                    if (fileExists)
                    {
                        throw new Exception("'File' exists");
                    }
                    else
                    {
                        pseudoFile = new PseudoFile(filename);
                        pseudoFiles.Add(filename, pseudoFile);

                        result = pseudoFile.Open(SeekOrigin.Begin);
                    }
                    break;

                case FileMode.Open:
                    if (fileExists)
                    {
                        if (wantsWritable && !pseudoFile.CanWrite)
                        {
                            throw new Exception("'File' is read-only");
                        }

                        result = pseudoFile.Open(SeekOrigin.Begin);
                    }
                    else
                    {
                        throw new Exception("'File' doesn't exist");
                    }
                    break;

                case FileMode.OpenOrCreate:
                    if (fileExists)
                    {
                        if (wantsWritable && !pseudoFile.CanWrite)
                        {
                            throw new Exception("'File' is read-only");
                        }

                        result = pseudoFile.Open(SeekOrigin.Begin);
                    }
                    else
                    {
                        pseudoFile = new PseudoFile(filename);
                        pseudoFiles.Add(filename, pseudoFile);

                        result = pseudoFile.Open(SeekOrigin.Begin);
                    }
                    break;

                case FileMode.Truncate:
                    if (fileExists)
                    {
                        pseudoFile.Delete();

                        pseudoFile = new PseudoFile(filename);
                        pseudoFiles[filename] = pseudoFile;

                        result = pseudoFile.Open(SeekOrigin.Begin);
                    }
                    else
                    {
                        throw new Exception("'File' doesn't exist");
                    }
                    break;

                default:
                    throw new Exception("Unexpected FileMode");
            }

            return result;
        }
        public Stream Open(string filename, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            return Open(filename, fileMode, fileAccess);
        }

        public void Delete(string filename)
        {
            IPseudoFile pseudoFile;

            if (pseudoFiles.TryGetValue(filename, out pseudoFile))
            {
                pseudoFile.Delete();
                pseudoFiles.Remove(filename);
            }
            else
            {
                throw new Exception("'File' doesn't exist");
            }
        }

        public FileAttributes GetAttributes(string filename)
        {
            IPseudoFile pseudoFile;

            if (pseudoFiles.TryGetValue(filename, out pseudoFile))
            {
                return FileAttributes.Normal; //ToDo(DZ): What kind of attributes do we want to return here?
            }
            else
            {
                throw new Exception("'File' doesn't exist");
            }
        }

        public DateTime GetLastWriteTimeUtc(string filename)
        {
            return DateTime.UtcNow; //ToDo(DZ): How do we find the latest write time? Or should we just return today's date?
        }

        long tempNameCounter = 0;
        public string GetTempFileName()
        {
            //string tempName = Guid.NewGuid().ToString();
            string tempName;
            do
            {
                tempName = tempNameCounter++.ToString();
            }
            while (pseudoFiles.ContainsKey(tempName));

            pseudoFiles.Add(tempName, new PseudoFile(tempName));

            return tempName;
        }

        //TEMP
        public void WriteStreamsToDisk(string path)
        {
            for (int i = 0; i < pseudoFiles.Count; i++)
            {
                var pair = pseudoFiles.ElementAt(i);

                string name = Path.GetFileName(pair.Key);
                IPseudoFile pseudoFile = pair.Value;

                Stream stream = pseudoFile.Open(SeekOrigin.Begin);

                FileStream file = File.Open(Path.Combine(path, name), FileMode.Create);

                for (int j = 0; j < stream.Length; j++)
                {
                    file.WriteByte((byte)stream.ReadByte());
                }

                stream.Close();
                file.Close();
            }
        }
    }   // end of class MemoryHelper

}   // end of namespace Cab
