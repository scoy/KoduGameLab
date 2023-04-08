// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Cab
{
    public static class Win32
    {
        [DllImport("kernel32.dll")]
        public static extern bool FileTimeToDosDateTime(
            [In] ref System.Runtime.InteropServices.ComTypes.FILETIME lpFileTime,
            out ushort lpFatDate,
            out ushort lpFatTime);
    }
}
