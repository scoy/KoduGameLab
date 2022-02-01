// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using System.Net;
using System.Threading;

using System.Xml;
using System.Xml.Serialization;
using System.Diagnostics;
using System.Data;
using System.Web;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Storage;

using Boku.Base;
using Boku.Common;
using Boku.Common.Localization;
using Boku.Common.Sharing;
using Boku.UI2D;
using Boku.Fx;
using Boku.Web;
using Boku.Web.Trans;


namespace Boku
{
    public class NewsFeeds
    {
        public enum OpState
        {
            Idle,
            Retrieving,
            Retrieved,
            Failed
        }

        string rawGetData = "";
        OpState currentState = OpState.Idle;

        public OpState CurrentState
        {
            get { return currentState; }
        }
       
        public void BeginFetchNews()
        {
            using (WebClient webClient = new WebClient())
            {
                try
                {
                    string url = KoduService.KGLUrl + "/API/GetLatestNews";
                    KoduService.DownloadData(url, (result)=> {
                        if (result == null)
                        {
                            currentState = OpState.Failed;
                        }
                        else
                        {
                            StreamReader sr = new StreamReader(result);
                            rawGetData = sr.ReadToEnd();
                            currentState = OpState.Retrieved;
                            Console.WriteLine(rawGetData);
                        }
                    });
                }
                catch (Exception e)
                {
                    if (e != null)
                    {
                        currentState = OpState.Failed;
                    }
                }
            }
        }   // end of BeginFetchNews()

        public List<FeedMs> GetFeedList(int width, Shared.GetFont titleFont, Shared.GetFont dateFont, Shared.GetFont bodyFont)
        {
            TextBlob textBlob = new TextBlob(bodyFont, "label", width);

            // Parse JSON string into List of Dictionaries.
            List<FeedMs> allFeeds = new List<FeedMs>();
            try
            {
                var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                var items = js.Deserialize<List<Dictionary<string, string>>>(rawGetData);

                // Build news feed.
                foreach (var item in items)
                {
                    FeedMs feedItem = new FeedMs(new Vector2(24, 0), textBlob, titleFont, dateFont, bodyFont);
                    feedItem.Title = item["Title"];
                    feedItem.Body = item["Text"];
                    feedItem.DateString = item["Date"];
                    feedItem.CreateHyperlink(HyperlinkType.URL, Strings.Localize("mainMenu.readMoreHere"), item["URL"]);
                    allFeeds.Add(feedItem);
                }
            }
            catch { }
            return allFeeds;
        }   // end of GetFeedList()

    }   // end of class NewsFeeds

}   // end of namespace Boku
