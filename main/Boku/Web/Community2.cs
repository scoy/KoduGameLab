
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

using Boku.Base;
using Boku.Common;

using BokuShared;

namespace Boku.Web
{
    /// <summary>
    /// IO functions for new community.
    /// 
    /// Should these be here or in with the code they work with?
    /// </summary>
    public class Community2
    {
        public static void GetWorlds(int first, int count, string sortby = "modified", string sortdir = "desc", string creator = null, string searchText = null)
        {
            using (WebClient webClient = new WebClient())
            {
                StringBuilder sb = new StringBuilder(@"https://koduworlds.azurewebsites.net/search/scoy?");
                sb.AppendFormat("first={0}&count={1}", first, count);
                Stream stream = webClient.OpenRead(sb.ToString());

                Console.WriteLine("\nDisplaying Data :\n");
                StreamReader sr = new StreamReader(stream);
                Console.WriteLine(sr.ReadToEnd());

                stream.Close();
            }
        }   // end of GetWorlds()

    }   // end of class Community2

}   // end of namespace Boku.Web
