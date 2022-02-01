﻿using System;
using System.Net;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading;

namespace Boku.Common.Sharing
{
	public class KoduService
	{
        public enum RequestState
        {
            None,

            Pending,
            Complete,

            NoInternet,
            Error,
        }

		#region Members

        // Address for web site and API content (news, version update, localization).
        public static string KGLUrl = @"https://www.kodugamelab.com";

		// Our service address
		public static string ServiceApiUrl = "https://api.koduworlds.com/api/";
		//public static string ServiceApiUrl = "http://koduapi-latency.azurewebsites.net/api/";//High latency test server.
		//public static string ServiceApiUrl = "http://koduapi-stage.azurewebsites.net/api/";
		//public static string ServiceApiUrl = "http://localhost.fiddler:3000/api/";//Localhost for development

        // Used by rest of system to keep track of state.  LoadLevelMenu
        // needs to poll this for Complete or Error and handle dialogs.
        // LoadLevelMenu then needs to reset this to None.
        public static RequestState ShareRequestState = RequestState.None;

		#endregion

		// Define callback that passes a WebResponse.
		public delegate void WebResponseCallback(HttpWebResponse response);

		// Define callback that passes a byte[].
		public delegate void ByteArrayCallback(byte[] response);

		// Define callback that passes a object.
		public delegate void GenericObjectCallback(object responseObject);

		// Define callback that passes a WebResponseStream.
		public delegate void ResponseStreamCallback(Stream response);

        // Define callback that passes a ResponseMessage.
        public delegate void ResponseMessageCallback(HttpResponseMessage responseMessage);

		// Used to connect to services.
		private static HttpClient httpClient = new HttpClient();

		#region Public

		/// <summary>
		/// Ping (async version)
		/// </summary>
		/// <param name="args"></param>
		/// <param name="callback">Callback gets server response json or null if fail</param>
		public static void Ping(object args, GenericObjectCallback callback)
        {
            string url = ServiceApiUrl + "ping";

            MakeHttpRequest(url, args, callback);

        }   // end of Ping()

        /// <summary>
        /// Ping (non asyn version)
        /// </summary>
        /// <param name="args"></param>
        /// <returns>ResponseObject if ping OK, null otherwise.</returns>
        public static Newtonsoft.Json.Linq.JContainer PingNonAsync(object args)
		{
			var pingPending = true;

			Newtonsoft.Json.Linq.JContainer responseObject = null;

			Ping(args, 
				// lambda callback
				(object returnObject) => {
					if (returnObject == null)
					{
						// Ping failed.
						responseObject = null;
					}
					else
					{
						// Ping ok.
						responseObject = (Newtonsoft.Json.Linq.JContainer)JsonConvert.DeserializeObject((string)returnObject) as Newtonsoft.Json.Linq.JContainer;
					}
					pingPending = false;
				}
			);

			while (pingPending)
			{
				Thread.Sleep(10);
			}

			return responseObject;
		}   // end of PingNonAsync()

        /// <summary>
        /// Search Worlds (formerly GetWorlds)
        /// </summary>
        /// <param name="args"></param>
        /// <param name="callback">Gets server response json or null if fail.</param>
		static public void Search(object args, GenericObjectCallback callback)
		{
			string url = ServiceApiUrl + "search";

            Instrumentation.RecordEvent(Instrumentation.EventId.SearchLevels, args.ToString());

			MakeHttpRequest(url, args, callback);

		}   // end of Search()

        /// <summary>
        /// Get thumbnail for world.
        /// </summary>
        /// <param name="ThumbnailUrl"></param>
        /// <param name="callback">Gets stream of thumb data or null if fail.</param>
		public static void GetThumbnail(Guid worldID, string thumbnailUrl, ResponseStreamCallback callback)
		{
			if (thumbnailUrl == null)
			{
				//sort of a hack for beta.
				//if thumb url is null it probably hasn't been converted from old db yet
				//in that case fetch directly. 
				//This will also convert and update the thumburl service side.
				thumbnailUrl = ServiceApiUrl + "thumbnail/" + worldID.ToString();
			}
			// Pass callback to DownloadData.
			DownloadData(thumbnailUrl, callback);
		}   // end of GetThumbnail()

        /// <summary>
        /// Delete world from Community.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="callback">Gets response text on success, null on failure.</param>
		public static void DeleteWorld(object args, GenericObjectCallback callback)
		{
			string url = ServiceApiUrl + "deleteWorld";

			MakeHttpRequest(url, args, callback);

		}   // end of DeleteWorld()

        /// <summary>
        /// Download world to disk.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="callback">Get ResponseStream or null on failure.</param>
		public static void DownloadWorld(object args, ResponseStreamCallback callback)
		{
			string url = ServiceApiUrl + "downloadWorld";

			//Make api call to get data url
			MakeHttpRequest(url, args, (response)=> {
				if (response == null)
				{
					callback(null);//Failed. 404 or something.
				}
				else
				{
					try
					{
						//get returned data url
						var container = (Newtonsoft.Json.Linq.JContainer)JsonConvert.DeserializeObject((string)response) as Newtonsoft.Json.Linq.JContainer;
						var dataUrl = container.Value<string>("dataUrl");

						// Pass on data url and callback to DownloadData.
						DownloadData(dataUrl, callback);

						Instrumentation.RecordEvent(Instrumentation.EventId.LevelDownloaded, args.ToString());
					}
					catch
					{
						callback(null);//should never happen but...
					}
				}
			});

		}   // end of DownloadWorld()

        /// <summary>
        /// Uploads a world (including thumb and screen) from disk.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="levelPath">Path to .Kodu2 file to upoload.</param>
        /// <param name="thumbPath">Path to thumbnail image to upload.</param>
        /// <param name="screenPath">Path to screenshot to upload.</param>
        /// <param name="callback">Gets null on failure.</param>
		public static void UploadWorld(object args, string levelPath,string thumbPath,string screenPath, GenericObjectCallback callback)
		{
            ShareRequestState = RequestState.Pending;

            Instrumentation.RecordEvent(Instrumentation.EventId.LevelUploaded, args.ToString());

			// Create an upload request.
			string url = ServiceApiUrl + "authorizeUpload/";

			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					// Failed.
                    ShareRequestState = RequestState.Error;
					callback(null);
				}
				else
				{
					string text = "";
					using (var responseStream = response.GetResponseStream())
					using (var reader = new StreamReader(responseStream))
					{
						text = reader.ReadToEnd();
					}

					//todo allow for rejected.

					// Get SAS strings from response.
					Newtonsoft.Json.Linq.JContainer container = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
					string uploadDataUrl = container.Value<string>("dataUrl");
					string uploadThumbUrl = container.Value<string>("thumbUrl");
					string uploadScreenUrl = container.Value<string>("screenUrl");

					bool uploadFailed = false;

					bool levelUploaded = false;
					bool thumbUploaded = false;
					bool screenUploaded = false;

					UploadDataFromFile(uploadDataUrl, @"application/octet-stream", levelPath, (uploadResponse) => {
						if(uploadResponse == null)
                        {
							// Data upload failed.
							uploadFailed = true;
                        }
						levelUploaded = true;
						// Check if all uploads have finished...
						if (levelUploaded && thumbUploaded && screenUploaded)
						{
                            if (uploadFailed)
                            {
                                ShareRequestState = RequestState.Error;
                                callback(null); // Report failure to caller.
                            }
                            else
                            {
                                FinalizeUpload(args, callback);
                            }
						}
					});

					UploadDataFromFile(uploadScreenUrl, @"image/jpeg", thumbPath, (uploadResponse) => {
						if (uploadResponse == null)
						{
							// Thumbnail upload failed.
                            ShareRequestState = RequestState.Error;
                            uploadFailed = true;
						}
						thumbUploaded = true;
						// Check if all uploads have finished...
						if (levelUploaded && thumbUploaded && screenUploaded)
						{
                            if (uploadFailed)
                            {
                                ShareRequestState = RequestState.Error;
                                callback(null); // Report failure to caller.
                            }
                            else
                            {
                                FinalizeUpload(args, callback);
                            }
						}
					});

					UploadDataFromFile(uploadThumbUrl, @"image/jpeg", screenPath, (uploadResponse) => {
						if (uploadResponse == null)
						{
							// Screen image upload failed.
                            ShareRequestState = RequestState.Error;
                            uploadFailed = true;
						}
						screenUploaded = true;

						// Check if all uploads have finished...
						if (levelUploaded && thumbUploaded && screenUploaded)
						{
                            if (uploadFailed)
                            {
                                ShareRequestState = RequestState.Error;
                                callback(null); // Report failure to caller.
                            }
                            else
                            {
                                FinalizeUpload(args, callback);
                            }
						}
					});

				}
			});//End of MakeApiRequest

		}   // end of UploadWorld()

        /// <summary>
        /// NonAsync uploading of instrumentation data.
        /// </summary>
        /// <param name="args"></param>
        /// <returns>True if OK, false on failure.</returns>
		public static bool UploadInstrumentationNonAsync(object args)
		{
			var pending = true;
			var ok = false;
			//var communityAvailable = false;

			//Newtonsoft.Json.Linq.JContainer responseObject = null;

			UploadInstrumentation(args,
				//lambda callback
				(object returnObject) => {
					if (returnObject == null)
					{
						// Failed.
						ok = false;

					}
					else
					{
						// Ok.
						ok = true;
					}
					pending = false;
				}
			);

			while (pending)
			{
				Thread.Sleep(10);
			}

			return ok;
		}   // end of UploadInstrumentationNonAsync()

		#endregion

		#region Private

        /// <summary>
        /// Once all the parts of a world have been uploaded successfully, this is called
        /// so the services can add the world to the database.
        /// Should only be called by internals.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="callback"></param>
		static void FinalizeUpload(object args, GenericObjectCallback callback)
		{
			string url = ServiceApiUrl + "finalizeUpload";

            Instrumentation.RecordEvent(Instrumentation.EventId.LevelDeleted, args.ToString());

			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					// Call failed.
                    ShareRequestState = RequestState.Error;
					callback(null);
				}
				else
				{
					string text = "";
					using (var responseStream = response.GetResponseStream())
					using (var reader = new StreamReader(responseStream))
					{
						text = reader.ReadToEnd();
                        ShareRequestState = RequestState.Complete;
						callback(text);
					}
				}
			});//End of MakeApiRequest

		}   // end of DeleteWorld()

        /// <summary>
        /// Async version.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="callback">Gets server responce json or null on failure.</param>
		public static void UploadInstrumentation(object args, GenericObjectCallback callback)
		{
			string url = ServiceApiUrl + "uploadInstrumentation";

			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					// Failed.
					callback(null);
				}
				else
				{
					// No response obj expected so check status code.
					if (response.StatusCode == HttpStatusCode.OK)
						callback(true);
					else
						callback(null);
				}
			});//End of MakeApiRequest
		}   // end of UploadInstrumentation()

        /// <summary>
        /// Helper function for making API calls easier.
        /// </summary>
        /// <param name="url">Url for request.</param>
        /// <param name="args">Object which will be turned into a JSON packet and attached to the request.</param>
        /// <returns>The created WebRequest.</returns>
		static HttpWebRequest CreateApiRequest(string url, object args)
		{
			var request = (HttpWebRequest)WebRequest.Create(url);
			request.ContentType = "application/json";
			request.Method = "POST";

			// Create and attach json payload.
			using (var streamWriter = new StreamWriter(request.GetRequestStream()))
			{
				// Turn into json string.
				string json = JsonConvert.SerializeObject(args);

				// Attatch json to request.
				streamWriter.Write(json);
			}
			return request;
		}   // end of CreateApiRequest()

        /// <summary>
        /// Makes an API request.
        /// </summary>
        /// <param name="url">The URL for the request.</param>
        /// <param name="args">Object which is serialized into a JSON packet and sent with the request.</param>
        /// <param name="callback">Always called, gets response.</param>
		public static void MakeApiRequest(string url, object args, WebResponseCallback callback)
		{
			var timer = new System.Diagnostics.Stopwatch();
			timer.Start();
			HttpWebResponse response = null;    //respond with null in case of fail.
			try
			{
				var request = CreateApiRequest(url, args);

				// Send request.
				var result = request.BeginGetResponse(asyncResult => {
					timer.Stop();

					// Handle request response.
					//Console.WriteLine("OK MS:"+ timer.Elapsed.Milliseconds+" "+url);
					try
					{
						var req = (HttpWebRequest)asyncResult.AsyncState;
						response = (HttpWebResponse)req.EndGetResponse(asyncResult);
					}
					catch (Exception ex)
					{
						LogException(ex, url, null, timer.Elapsed);
						callback(null);
					}
				}, request);
			}
			catch (Exception ex)
			{
				timer.Stop();

				LogException(ex, url, args, timer.Elapsed);

				response = null;    // Report fail.
			}
			finally
			{
				// Finally success or fail do the callback.
				// Handle this outside of the try catch to only catch
				// comm related errors. This might not be the right thing to do.
				// If a mangled thumbnail or world causes an exception what will happen?
				callback(response);
			}

		}   // end of MakeApiRequest()

		/// <summary>
		/// Makes an API request via HttpClient.
		/// </summary>
		/// <param name="url">The URL for the request.</param>
		/// <param name="args">Object which is serialized into a JSON packet and sent with the request.</param>
		/// <param name="callback">Always called, gets response.</param>
		private static void MakeHttpRequest(string url, object args, GenericObjectCallback callback)
		{
			var httpContent = new StringContent(JsonConvert.SerializeObject(args), Encoding.UTF8, "application/json");
			var timer = new System.Diagnostics.Stopwatch();
			timer.Start();
			var instrumentationTimer = Instrumentation.StartTimer(Instrumentation.TimerId.ResponseTime);

			httpClient.PostAsync(url, httpContent).ContinueWith(responseTask =>
			{
				Instrumentation.StopTimer(instrumentationTimer);
				timer.Stop();
				var response = responseTask.Result;
				if (!response.IsSuccessStatusCode)
				{
					//Log error.
					Instrumentation.RecordException(new { type = "HTP", url = url, args = args, message = response.ReasonPhrase, body = "", time = timer.Elapsed });
					//failed
					callback(null);
				}
				else
				{
					response.Content.ReadAsStringAsync().ContinueWith(jsonTask =>
					{
						var json = jsonTask.Result;

						//Newtonsoft.Json.Linq.JContainer returnObject = JsonConvert.DeserializeObject(json) as Newtonsoft.Json.Linq.JContainer;
						callback(json);
					});
				}
			});
		}

		/// <summary>
		/// Helper function to handle data downloads of worlds and thumbnails.
        /// Note that this is async.  Should probably rename.
		/// </summary>
		/// <param name="url"></param>
		/// <param name="callback"></param>
		public static void DownloadData(string url, ResponseStreamCallback callback)
		{
			//var timer = new System.Diagnostics.Stopwatch();
			//timer.Start();
			
			// Force protocol to Tls12 to support GitHub
			ServicePointManager.Expect100Continue = true;
			ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;  // 3072 = Tls12. The library we are using doesn't have Tls12 enum value.

			httpClient.GetAsync(url).ContinueWith(responseTask =>
			{
				var response = responseTask.Result;
				if (!response.IsSuccessStatusCode)
				{
					// Failed.
					callback(null);
				}
				else
				{
					response.Content.ReadAsStreamAsync().ContinueWith(streamTask =>
					{
						var res= streamTask.Result;

						// Note Result will be none if readstream failed?
						callback(res);
					});
				}
			});
		}   // end of DownloadData()

        /// <summary>
        /// Helper function.  Calls callback with HttpResponceMessage.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="callback"></param>
        public static void DownloadDataAsync(string url, ResponseMessageCallback callback)
        {
            // Force protocol to Tls12 to support GitHub
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;  // 3072 = Tls12. The library we are using doesn't have Tls12 enum value.

            httpClient.GetAsync(url).ContinueWith(responseTask =>
            {
                var response = responseTask.Result;
                callback(response);
            });
        }   // end of DownloadDataAsync()

		public static void xDownloadData(string url, ResponseStreamCallback callback)
		{
			var timer = new System.Diagnostics.Stopwatch();
			timer.Start();
			//HttpWebResponse dataResponse = null;//respond with null in case of fail
			try
			{
				// Get data from dataUrl.
				Uri uri = new Uri(url);

				var dataRequest = (HttpWebRequest)WebRequest.Create(uri);
				dataRequest.BeginGetResponse((asyncResult) => {
					timer.Stop();
					//Console.WriteLine("OK MS:" + timer.Elapsed + " " + url);
					try
					{
						using (HttpWebResponse dataResponse = (HttpWebResponse)dataRequest.EndGetResponse(asyncResult))
						{
							using (Stream dataResponseStream = dataResponse.GetResponseStream())
							{
								callback(dataResponseStream);
							}
						}
					}
					catch (Exception ex)
					{
						LogException(ex, url, null, timer.Elapsed);
						callback(null);
					}

				}, dataRequest);
			}
			catch (Exception ex)
			{
				timer.Stop();

				LogException(ex, url, null, timer.Elapsed);
				callback(null); // Report fail.
			}
			finally
			{
			}
		}   // end of DownloadData()

		/// <summary>
		/// Helper function to handle logging exception messages.
		/// </summary>
		/// <param name="ex"></param>
		/// <param name="url"></param>
		/// <param name="args"></param>
		/// <param name="elapsed"></param>
		private static void LogException(Exception ex,string url, object args, TimeSpan elapsed) {
			if (ex is WebException)
			{
				var wex = (WebException)ex;
				string body = "";
				if (wex.Response != null)
				{
					using (var stream = wex.Response.GetResponseStream())
					using (var reader = new StreamReader(stream))
					{
						body = reader.ReadToEnd();
					}
				}
				Instrumentation.RecordException(new { type = "WEX", url = url, args = args, message = ex.Message, body = "", time = elapsed });
            }
            else
            {
				Instrumentation.RecordException(new { type = "EX", url = url, args = args, message = ex.Message, time = elapsed });
			}
		}
        /// <summary>
        /// Helper function to handle world uploads to blob storage.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="contentType"></param>
        /// <param name="buffer"></param>
        /// <param name="callback"></param>
		static void UploadData(string url, string contentType, byte[] buffer, GenericObjectCallback callback)
		{
			var timer = new System.Diagnostics.Stopwatch();
			timer.Start();
			HttpWebResponse response = null;    // Respond with null in case of fail.
			try
			{
				var request = (HttpWebRequest)WebRequest.Create(url);
				request.ContentType = "application/json";
				request.Method = "PUT";
				request.Headers.Add("x-ms-blob-type", "BlockBlob");
				request.Headers.Add("x-ms-blob-content-type", contentType);
				
				// Create and attatch binary payload.
				using (var streamWriter = new BinaryWriter(request.GetRequestStream()))
				{
					// Attach buffer to request.
					streamWriter.Write(buffer);
				}

				// Send request.
				var result = request.BeginGetResponse(asyncResult => {
					timer.Stop();

					// Handle request response.
					Console.WriteLine("OK MS:" + timer.Elapsed.Milliseconds + " " + url);

					var req = (HttpWebRequest)asyncResult.AsyncState;
					response = (HttpWebResponse)req.EndGetResponse(asyncResult);
				}, request);
			}
			catch (Exception ex)
			{
				timer.Stop();

				LogException(ex, url, null, timer.Elapsed);

				response = null;    // Report fail.
			}
			finally
			{
				// Finally success or fail do the callback.
				// Handle this outside of the try catch to only catch
				// comm related errors. This might not be the right thing to do.
				// If a mangled thumbnail or world causes an exception what will happen?
				callback(response);
			}
		}   // end of UploadData()

        /// <summary>
        /// Wrapper for UploadData that takes a file path.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="contentType"></param>
        /// <param name="filePath"></param>
        /// <param name="callback"></param>
		private static void UploadDataFromFile(string url, string contentType, string filePath, GenericObjectCallback callback)
		{
			byte[] buffer = File.ReadAllBytes(filePath);
			UploadData(url, contentType, buffer, callback);
        }   // end of UploadData()

        #endregion // Private

    }   // end of class KoduService

}   // end of namespace Boku.Common.Sharing


