
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using Newtonsoft.Json;

using Boku;
using Boku.Common.Localization;

namespace Boku.Common.Sharing
{
    public class CommunityServices
    {
        public enum RequestState
        {
            None,

            PendingUpload,
            UploadComplete,

            Pending,
            Complete,

            NoInternet,
            Error,
        }

        #region Members

        //const string ServiceApiUrl = "https://koduworlds.azurewebsites.net/api/";
        const string ServiceApiUrl = "https://koduworlds-api.azurewebsites.net/api/";

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
            string url = ServiceApiUrl + "ping";
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
                
                request = CreateApiRequest(url, args);

                internetAvailable = true;
            }
            catch (WebException e)
            {
                if (e != null)
                {
                    // No internet connection:  "The remote name could not be resolved: 'koduworlds.azurewebsites.net'"
                    internetAvailable = false;
                    communityAvailable = false;
                }
            }

            // Send request.
            if (request != null)
            {
                var result = request.BeginGetResponse(new AsyncCallback(PingCallback), request);
            }

        }	// end of Ping()

        static void PingCallback(IAsyncResult asyncResult)
        {
            string text = "";

            try
            {
                var request = (HttpWebRequest)asyncResult.AsyncState;
                var response = (HttpWebResponse)request.EndGetResponse(asyncResult);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    communityAvailable = true;
                }
                else
                {
                    communityAvailable = false;
                }
                var responseStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(responseStream);
                text = reader.ReadToEnd();

                Newtonsoft.Json.Linq.JContainer foo = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
                string systemMessage = foo.Value<string>("systemMessage");
                if (!string.IsNullOrWhiteSpace(systemMessage))
                {
                    // TODO (scoy) Alert user!?
                }
            }
            catch (WebException e)
            {
                if (e != null)
                {
                    // 404 error: "The remote server returned an error: (404) Not Found."
                    communityAvailable = false;
                }
            }

        }	// end of PingCallback()

        #endregion Ping

        #region Share

        //
        // Static values used to carry info across share calls and callbacks.
        //
        // Level we're sharing.
        static LevelMetadata level = null;
        static string pathToKodu2File = null;

        static RequestState kodu2RequestState = RequestState.None;
        static RequestState thumbRequestState = RequestState.None;
        static RequestState largeRequestState = RequestState.None;

        // Used by rest of system to keep track of state.  LoadLevelMenu
        // needs to poll this for Complete or Error and handle dialogs.
        // LoadLevelMenu then needs to reset this to None.
        public static RequestState ShareRequestState = RequestState.None;

        /// <summary>
        /// Shares a world to the Community.
        /// If the world is a part of a linked chain, all the linked levels are shared.
        /// Assumes that the level has already been checked for broken links.
        /// </summary>
        /// <param name="level">Level to share to Community.</param>
        /// <returns>True if successful, false if fails.</returns>
        static public bool ShareWorld(LevelMetadata level)
        {
            Debug.Assert(ShareRequestState == RequestState.None, "Previous call was not handled cleanly.  Figure out why.");

            ShareRequestState = RequestState.Pending;
            // Ensure individual states are reset.
            kodu2RequestState = RequestState.None;
            thumbRequestState = RequestState.None;
            largeRequestState = RequestState.None;

            CommunityServices.level = level;

            // Ensure we have a .jpg thumbnail and a _800.jpg image.
            LevelPackage.FixUpThumbAndLargeImage(level);

            string url = ServiceApiUrl + "authorizeUpload";
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "application/json";
            request.Method = "POST";

            // Create and attach json payload with metadata.
            try
            {
                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    // Give the community server the metadata about the level we want to upload.
                    var args = new
                    {
                        worldId = level.WorldId.ToString(),
                        created = level.LastWriteTime.ToUniversalTime().ToString(),
                        name = level.Name,
                        creator = level.Creator,
                        lastWriteTime = level.LastWriteTime.ToUniversalTime().ToString(),
                        checksum = level.Checksum,
                        numLevels = level.CalculateTotalLinkLength(),
                        description = level.Description
                    };
                    // Turn into json string.
                    string json = JsonConvert.SerializeObject(args);

                    // Attatch json to request.
                    streamWriter.Write(json);
                }
            }
            catch (WebException e)
            {
                if (e != null)
                {
                    // No internet connection:  "The remote name could not be resolved: 'koduworlds.azurewebsites.net'"
                    ShareRequestState = RequestState.NoInternet;
                    return false;
                }
            }

            // Send request.
            var result = request.BeginGetResponse(new AsyncCallback(ShareWorldCallback), request);

            return true;
        }   // end of ShareWorld()

        /// <summary>
        /// This is the callback from the authorizeUpload call.  The return should
        /// include SAS based URLs for uploading the parts of the new world.
        /// </summary>
        /// <param name="asyncResult"></param>
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

                // Get SAS strings from response.
                Newtonsoft.Json.Linq.JContainer container = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
                string koduSAS = container.Value<string>("dataUri");
                string thumbSAS = container.Value<string>("thumbUri");
                string largeSAS = container.Value<string>("screenUri");

                // Generate temp Kodu2 file.
                pathToKodu2File = Path.Combine(Storage4.UserLocation, LevelPackage.ExportsPath, level.WorldId.ToString() + ".Kodu2");
                LoadLevelMenu.Shared.ExportLevel(level, pathToKodu2File);
                // Get paths for image files.
                string pathToThumb = Path.Combine(Storage4.UserLocation, @"Content\Xml\Levels\MyWorlds", level.WorldId.ToString() + ".Jpg");
                string pathToLarge = Path.Combine(Storage4.UserLocation, @"Content\Xml\Levels\MyWorlds", level.WorldId.ToString() + "_800.Jpg");

                // Set pending state before sending any uploads.
                kodu2RequestState = RequestState.PendingUpload;
                thumbRequestState = RequestState.PendingUpload;
                largeRequestState = RequestState.PendingUpload;

                // Create storage requests.
                HttpWebRequest kodu2Request = CreateStorageRequest(koduSAS, @"application/octet-stream", pathToKodu2File);
                var kodu2Result = kodu2Request.BeginGetResponse(new AsyncCallback(Kodu2Callback), kodu2Request);
                
                byte[] buffer = File.ReadAllBytes(pathToThumb);
                HttpWebRequest thumbRequest = CreateStorageRequest(thumbSAS, @"image/jpeg", buffer);
                var thumbResult = thumbRequest.BeginGetResponse(new AsyncCallback(ThumbCallback), thumbRequest);
                
                byte[] buffer2 = File.ReadAllBytes(pathToLarge);
                HttpWebRequest largeRequest = CreateStorageRequest(largeSAS, @"image/jpeg", buffer2);
                var largeResult = largeRequest.BeginGetResponse(new AsyncCallback(LargeCallback), largeRequest);
            }
            catch (WebException e)
            {
                if (e != null)
                {
                    // 404 error: "The remote server returned an error: (404) Not Found."

                    // Something went wrong.  
                    ShareRequestState = RequestState.Error;

                    // Delete the temp Kodu2 file.
                    File.Delete(pathToKodu2File);

                    // Reset individual states.
                    kodu2RequestState = RequestState.None;
                    thumbRequestState = RequestState.None;
                    largeRequestState = RequestState.None;
                }
            }

            // Busywait for upload.
            while ((kodu2RequestState == RequestState.PendingUpload || thumbRequestState == RequestState.PendingUpload || largeRequestState == RequestState.PendingUpload) && ShareRequestState != RequestState.Error)
            {
                Thread.Sleep(10);    
            }

            // Clean up.
            // Delete the temp Kodu2 file.
            File.Delete(pathToKodu2File);

            // Reset individual states.
            kodu2RequestState = RequestState.None;
            thumbRequestState = RequestState.None;
            largeRequestState = RequestState.None;

            if (ShareRequestState == RequestState.Error)
            {
                return;
            }

            try
            {
                // Recreate args?  Seems like a waste
                var args = new
                {
                    worldId = level.WorldId.ToString(),
                    created = level.LastWriteTime.ToUniversalTime().ToString(),
                    name = level.Name,
                    creator = level.Creator,
                    lastWriteTime = level.LastWriteTime.ToUniversalTime().ToString(),
                    checksum = level.Checksum,
                    numLevels = level.CalculateTotalLinkLength(),
                    description = level.Description
                };

                // Finalize share.
                var finalizeRequest = CreateApiRequest(ServiceApiUrl + "finalizeUpload/", args);
                // Send request.
                var finalizeResult = finalizeRequest.BeginGetResponse(finalizeAsyncResult =>
                {
                    // Get Response stream.
                    var finalizeReq = (HttpWebRequest)finalizeAsyncResult.AsyncState;
                    var finalizeResponse = (HttpWebResponse)finalizeReq.EndGetResponse(finalizeAsyncResult);
                    var finalizeResponseStream = finalizeResponse.GetResponseStream();

                    // Deserialize response into an object.
                    StreamReader finalizeReader = new StreamReader(finalizeResponseStream);
                    string finalizeText = finalizeReader.ReadToEnd();

                    // Indicate we're fully complete.
                    ShareRequestState = RequestState.Complete;
                    // Rest for next share.
                    // TODO (scoy) Should we do this here or in the LoadLevelMenu?
                    ShareRequestState = RequestState.None;

                }, finalizeRequest);
            }
            catch(Exception e)
            {
                if (e != null)
                {
                    ShareRequestState = RequestState.Error;
                }
            }

        }	// end of ShareWorldCallback()

        static void Kodu2Callback(IAsyncResult asyncResult)
        {
            try
            {
                // Check response to see if upload worked.
                var uploadReq = (HttpWebRequest)asyncResult.AsyncState;
                var uploadResponse = (HttpWebResponse)uploadReq.EndGetResponse(asyncResult);

                if (uploadResponse.StatusDescription == "Created")
                {
                    kodu2RequestState = RequestState.UploadComplete;
                }
                else
                {
                    kodu2RequestState = RequestState.Error;
                }
            }
            catch (Exception e)
            {
                if (e != null)
                {
                    ShareRequestState = RequestState.Error;
                    kodu2RequestState = RequestState.Error;
                }
            }

        }   // end of Kodu2Callback()

        static void ThumbCallback(IAsyncResult asyncResult)
        {
            try
            {
                // Check response to see if upload worked.
                var uploadReq = (HttpWebRequest)asyncResult.AsyncState;
                var uploadResponse = (HttpWebResponse)uploadReq.EndGetResponse(asyncResult);

                if (uploadResponse.StatusDescription == "Created")
                {
                    thumbRequestState = RequestState.UploadComplete;
                }
                else
                {
                    thumbRequestState = RequestState.Error;
                }
            }
            catch (Exception e)
            {
                if (e != null)
                {
                    ShareRequestState = RequestState.Error;
                    thumbRequestState = RequestState.Error;
                }
            }

        }   // end of ThumbCallback()

        static void LargeCallback(IAsyncResult asyncResult)
        {
            try
            {
                // Check response to see if upload worked.
                var uploadReq = (HttpWebRequest)asyncResult.AsyncState;
                var uploadResponse = (HttpWebResponse)uploadReq.EndGetResponse(asyncResult);

                if (uploadResponse.StatusDescription == "Created")
                {
                    largeRequestState = RequestState.UploadComplete;
                }
                else
                {
                    largeRequestState = RequestState.Error;
                }
            }
            catch (Exception e)
            {
                if (e != null)
                {
                    ShareRequestState = RequestState.Error;
                    largeRequestState = RequestState.Error;
                }
            }

        }   // end of LargeCallback()

        #endregion Share

        #region Community GetWorlds

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
            string uri = ServiceApiUrl + "search";
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
                request = CreateApiRequest(uri, args);
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
        static HttpWebRequest CreateApiRequest(string url, object args)
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

        /// <summary>
        /// Helper function for storing data to Azure blob storage.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        static HttpWebRequest CreateStorageRequest(string url, string contentType, byte[] buffer)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "application/json";
            request.Method = "PUT";
            request.Headers.Add("x-ms-blob-type", "BlockBlob");
            request.Headers.Add("x-ms-blob-content-type", "image/jpeg");

            // Create and attatch binary payload.
            using (var streamWriter = new BinaryWriter(request.GetRequestStream()))
            {
                // Attach buffer to request.
                streamWriter.Write(buffer);
            }
            return request;
        }   // end of CreateStorageRequest()

                /// <summary>
        /// Helper function for storing data to Azure blob storage.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="filePath">Full path for file to send.</param>
        /// <returns></returns>
        static HttpWebRequest CreateStorageRequest(string url, string contentType, string filePath)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "application/json";
            request.Method = "PUT";
            request.Headers.Add("x-ms-blob-type", "BlockBlob");
            request.Headers.Add("x-ms-blob-content-type", contentType);

            // Create and attatch binary payload.
            byte[] buffer = File.ReadAllBytes(filePath);
            using (var streamWriter = new BinaryWriter(request.GetRequestStream()))
            {
                // Attach buffer to request.
                streamWriter.Write(buffer);
            }

            return request;
        }   // end of CreateStorageRequest()

        #endregion

    }   // end of class CommunityServices

}   // end of namespace Boku.Common.Sharing
