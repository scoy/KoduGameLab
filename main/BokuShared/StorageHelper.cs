// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.


using System;
using System.Diagnostics;
using System.IO;

namespace BokuShared
{
    public abstract class StorageHelper
    {
        public abstract Stream OpenRead(string filename);
        public abstract Stream OpenRead(string filename, int flags);
        public abstract Stream OpenWrite(string filename);
        public abstract void Close(Stream stream);
    }

    public class FileStorageHelper : StorageHelper
    {
        private static FileStorageHelper instance;

        public static FileStorageHelper Instance
        {
            get
            {
                if (instance == null)
                    instance = new FileStorageHelper();
                return instance;
            }
        }

        public override Stream OpenRead(string filename)
        {
            return File.OpenRead(filename);
        }

        public override Stream OpenRead(string filename, int flags)
        {
            return File.OpenRead(filename);
        }

        public override Stream OpenWrite(string filename)
        {
            return File.OpenWrite(filename);
        }

        public override void Close(Stream stream)
        {
            stream.Close();
        }

    }
}
