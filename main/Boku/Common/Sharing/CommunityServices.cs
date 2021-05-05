
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace Boku.Common.Sharing
{
    public class CommunityServices
    {
        #region Members

        //const string CommunityURL = "https://koduworlds.azurewebsites.net/api/";
        const string CommunityURL = "https://koduworlds-staging.azurewebsites.net/api/";

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

        public static void Ping(string version, string language)
        {
            Debug.WriteLine("POST ping");

            string uri = CommunityURL + "ping";
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.ContentType = "application/json";
            request.Method = "POST";

            // Create and attach json payload.
            try
            {
                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    // Make an object to serialize.
                    var args = new
                    {
                        clientVersion = version,
                        lang = language
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
                }
            }

            // Send request.
            var result = request.BeginGetResponse(new AsyncCallback(PingCallback), request);

        }	// end of Main()

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
        static public void ShareWorld(LevelMetadata level)
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
                        lastWriteTime = level.LastWriteTime.ToUniversalTime().ToString(),
                        levelName = level.Name,
                        description = level.Description,
                        creator = level.Creator,
                        checksum = level.Checksum,
                        lastSaveTime = level.LastSaveTime.ToUniversalTime().ToString(),
                        numLevels = level.CalculateTotalLinkLength(),

                        kodu2Filename = level.WorldId + ".Kodu2",
                        thumbnailFilename = level.WorldId + ".jpg",
                        largeImageFilename = level.WorldId + "_800.jpg"
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
                }
            }

            // Send request.
            var result = request.BeginGetResponse(new AsyncCallback(ShareWorldCallback), request);

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
        #endregion Community

        #region Download
        #endregion Download

        #region Delete World
        #endregion Delete World

        #region Instrumentation
        #endregion Instrumentation

        #endregion

        #region Internal
        #endregion

    }   // end of class CommunityServices

}   // end of namespace Boku.Common.Sharing
