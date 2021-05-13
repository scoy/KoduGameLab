
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;

using Boku;
using Boku.Common.Localization;

namespace Boku.Common.Sharing
{
    public class CommunityServices
    {
        #region Members

        //const string CommunityURL = "https://koduworlds.azurewebsites.net/api/";
        const string CommunityURL = "https://koduworlds-api.azurewebsites.net/api/";

        static bool internetAvailable = false;
        static bool communityAvailable = false;

        #endregion

        #region Accessors

        public static bool InternetAvailable
        {
            get { return internetAvailable; }
        }

        public static bool CommunityAvailable
        {
            get { return communityAvailable; }
        }

        #endregion

        #region Public

        #region Ping

        public static void Ping(bool startup = false)
        {
            string uri = CommunityURL + "ping";
            HttpWebRequest request = null;

            // Create and attach json payload.
            try
            {
                // Make an object to serialize.
                var args = new
                {
                    startup = startup.ToString(),
                    clientVersion = Program2.ThisVersion.ToString(),
                    lang = Localizer.LocalLanguage,
                    siteId = SiteID.Instance.Value.ToString()
                };
                
                request = CreateRequest(uri, args);
            }
            catch (WebException e)
            {
                if (e != null)
                {
                    // No internet connection:  "The remote name could not be resolved: 'koduworlds.azurewebsites.net'"
                }
            }

            // Send request.
            if (request != null)
            {
                //var result = request.BeginGetResponse(new AsyncCallback(PingCallback), request);
                var result = request.BeginGetResponse(asyncResult =>
                {
                    try
                    {
                        var req = (HttpWebRequest)asyncResult.AsyncState;
                        var response = (HttpWebResponse)req.EndGetResponse(asyncResult);
                        var responseStream = response.GetResponseStream();
                        StreamReader reader = new StreamReader(responseStream);
                        string text = reader.ReadToEnd();

                        Newtonsoft.Json.Linq.JContainer foo = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
                        string systemMessage = foo.Value<string>("systemMessage");
                        if (!string.IsNullOrWhiteSpace(systemMessage))
                        {
                            // TODO (scoy) Alert user!?
                        }

                        communityAvailable = true;
                    }
                    catch (WebException e)
                    {
                        if (e != null)
                        {
                            // 404 error: "The remote server returned an error: (404) Not Found."

                        }
                    }
                }, request);
            }

        }	// end of Ping()

        static void PingCallback(IAsyncResult asyncResult)
        {
            string text = "";

            try
            {
                var request = (HttpWebRequest)asyncResult.AsyncState;
                var response = (HttpWebResponse)request.EndGetResponse(asyncResult);
                var responseStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(responseStream);
                text = reader.ReadToEnd();

                Newtonsoft.Json.Linq.JContainer foo = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
                string systemMessage = foo.Value<string>("systemMessage");
                if (!string.IsNullOrWhiteSpace(systemMessage))
                {
                    // TODO (scoy) Alert user!?
                }

                communityAvailable = true;
            }
            catch (WebException e)
            {
                if (e != null)
                {
                    // 404 error: "The remote server returned an error: (404) Not Found."

                }
            }

        }	// end of PingCallback()

        #endregion Ping

        #region Share

        /// <summary>
        /// Shares a world to the Community.
        /// If the world is a part of a linked chain, all the linked levels are shared.
        /// Assumes that the level has already been chacked for broken links.
        /// </summary>
        /// <param name="level">Level to share to Community.</param>
        /// <returns>True if successful, false if fails.</returns>
        static public bool ShareWorld(LevelMetadata level)
        {
            string uri = CommunityURL + "ping";
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.ContentType = "application/json";
            request.Method = "POST";

            // Create and attach json payload.
            try
            {
                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    // Give the community server the data about the level we want to upload.
                    var args = new
                    {
                        worldId = level.WorldId.ToString(),
                        createdTime = level.LastWriteTime.ToUniversalTime().ToString(),
                        lastWriteTime = level.LastWriteTime.ToUniversalTime().ToString(),
                        lastSaveTime = level.LastSaveTime.ToUniversalTime().ToString(),
                        levelName = level.Name,
                        description = level.Description,
                        creator = level.Creator,
                        checksum = level.Checksum,
                        numLevels = level.CalculateTotalLinkLength()
                    };
                    // Turn into json string.
                    string json = JsonConvert.SerializeObject(args);

                    // Attatch json to request.
                    streamWriter.Write(json);

                    internetAvailable = true;
                }
            }
            catch (WebException e)
            {
                if (e != null)
                {
                    // No internet connection:  "The remote name could not be resolved: 'koduworlds.azurewebsites.net'"
                    return false;
                }
            }

            // Send request.
            var result = request.BeginGetResponse(new AsyncCallback(ShareWorldCallback), request);

            return true;
        }   // end of ShareWorld()

        static void ShareWorldCallback(IAsyncResult asyncResult)
        {
            string text = "";

            try
            {
                var request = (HttpWebRequest)asyncResult.AsyncState;
                var response = (HttpWebResponse)request.EndGetResponse(asyncResult);
                var responseStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(responseStream);
                text = reader.ReadToEnd();

                Newtonsoft.Json.Linq.JContainer foo = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
                string systemMessage = foo.Value<string>("systemMessage");
                if (!string.IsNullOrWhiteSpace(systemMessage))
                {
                    // TODO (scoy) Alert user!?
                }

                communityAvailable = true;
            }
            catch (WebException e)
            {
                if (e != null)
                {
                    // 404 error: "The remote server returned an error: (404) Not Found."
                }
            }
        }	// end of ShareWorldCallback()


        #endregion Share

        #region Community

        /// <summary>
        /// 
        /// </summary>
        /// <param name="first"></param>
        /// <param name="count"></param>
        /// <param name="sortBy">"date"</param>
        /// <param name="sortDir">"asc" or "desc"</param>
        /// <param name="dateRange">"all", "year", "month", "week"</param>
        /// <param name="keywords">Full text search string.</param>
        /// <param name="creator">Creator name iff MyWorlds tab is selected.</param>
        /// <returns></returns>
        static public bool GetWorlds(int first, int count, string sortBy, string sortDir, string dateRange = "all", string keywords = "", string creator = "")
        {
            string uri = CommunityURL + "search";
            var args = new
            {
                first = first,
                count = count,
                sortBy = sortBy,
                sortDir = sortDir,
                range = dateRange,
                keywords = keywords,
                creator = creator
            };

            HttpWebRequest request = null;
            try
            {
                request = CreateRequest(uri, args);
            }
            catch (WebException e)
            {
                if (e != null)
                {
                    // No internet connection:  "The remote name could not be resolved: 'koduworlds.azurewebsites.net'"
                    return false;
                }
            }

            // Send request.
            var result = request.BeginGetResponse(new AsyncCallback(GetWorldsCallback), request);

            return true;
        }   // end of GetWorlds()

        static void GetWorldsCallback(IAsyncResult asyncResult)
        {
            try
            {
                var request = (HttpWebRequest)asyncResult.AsyncState;
                var response = (HttpWebResponse)request.EndGetResponse(asyncResult);
                var responseStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(responseStream);
                string results = reader.ReadToEnd();

                // TODO (scoy) This feels dirty.  Is there a better way to tie the browser to the call?
                // I guess I could pass in the browser with each call and save it locally for the callback...
                CommunityLevelBrowser browser = BokuGame.bokuGame.community.shared.srvBrowser;
                browser.FetchComplete(asyncResult, results);
            }
            catch (WebException e)
            {
                if (e != null)
                {
                    // 404 error: "The remote server returned an error: (404) Not Found."

                }
            }

        }	// end of GetWorldsCallback()

        #endregion Community

        #region Download
        #endregion Download

        #region Delete World
        #endregion Delete World

        #region Instrumentation
        #endregion Instrumentation

        #endregion

        #region Internal

        /// <summary>
        /// Helper function to make building API call easier.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        static HttpWebRequest CreateRequest(string url, object args)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "application/json";
            request.Method = "POST";

            // Create and attatch json payload.
            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                // Turn into json string.
                string json = JsonConvert.SerializeObject(args);

                // Attatch json to request.
                streamWriter.Write(json);
            }
            return request;
        }   // end of CreateRequest()


        #endregion

    }   // end of class CommunityServices

}   // end of namespace Boku.Common.Sharing
