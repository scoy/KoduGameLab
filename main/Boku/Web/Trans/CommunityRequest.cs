// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Threading;

using System.Collections.Generic;

using BokuShared;
using BokuShared.Wire;


namespace Boku.Web.Trans
{
    /// <summary>
    /// Provides services shared by all community web requests.
    /// </summary>
    public abstract class CommunityRequest : Request
    {

        // Moved from SiteOptions.
        public static string CommunityUrl = @"https://kodu.cloudapp.net/Community2.asmx";    // Production, deployed to all.
        // public static string CommunityUrl = @"http://kodu.cloudapp.net/Community2.asmx";    // Production, deployed to all.
        
        // public static string CommunityUrl = @"https://koduworlds.azurewebsites.net/Community3.asmx";    // Works!

        // public static string CommunityUrl = @"https://worlds.koduworlds.com/Community3.asmx";    // Works!
        // public static string CommunityUrl = @"http://worlds.koduworlds.com/Community3.asmx";    // Works with HTTPS Only turned off.
        
        // public static string CommunityUrl = @"https://koduworlds.com/Community3.asmx";    // Doesn't work.
        // public static string CommunityUrl = @"http://koduworlds.com/Community3.asmx";    // Doesn't work.

        //public static string CommunityUrl = @"http://0475e2071afa49d69241177aba07b626.cloudapp.net/Community2.asmx";   // Staging.
        //public static string CommunityUrl = @"https://koduclientapi-int.cloudapp.net/Community.asmx"; //Internal
        //public static string CommunityUrl = @"http://localhost.fiddler:50000/Community2.asmx"; //Local
        //public static string CommunityUrl = @"http://koduclientapi-int.cloudapp.net/Community2.asmx"; //Internal
        //public static string CommunityUrl = @"http://localhost:27855/Community2.asmx"; //Local

        #region Public Methods

        public CommunityRequest(bool needsCryptoKey, SendOrPostCallback callback, object userState)
            : base(CommunityUrl, needsCryptoKey, callback, userState) //From siteOptions. NO LONGER USED.
        {
        }

        public override bool Send()
        {
            if (!Program2.SiteOptions.CommunityEnabled)
            {
                return false;
            }

            return base.Send();
        }

        #endregion
    }
}
