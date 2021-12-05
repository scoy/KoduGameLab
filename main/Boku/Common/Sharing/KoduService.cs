using System;
using System.Net;
using System.IO;
using System.Text;
using Newtonsoft.Json;
//using System.Net.Http;
using System.Threading;

namespace Boku.Common.Sharing
{
	public class KoduService
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

		// Our service address
		public static string ServiceApiUrl = "https://api.koduworlds.com/api/";
		//public static string ServiceApiUrl = "https://koduworlds-api.azurewebsites.net/api/";
		//public static string ServiceApiUrl = "http://koduworlds-api.azurewebsites.net/api/";//For use with fiddler
		//public static string ServiceApiUrl = "http://localhost.fiddler:3000/api/";//Localhost for development

		#endregion

		// Define callback that passes a WebResponse.
		public delegate void WebResponseCallback(HttpWebResponse response);

		// Define callback that passes a byte[].
		public delegate void ByteArrayCallback(byte[] response);

		// Define callback that passes a object.
		public delegate void GenericObjectCallback(object responseObject);

		// Define callback that passes a WebResponseStream.
		public delegate void ResponseStreamCallback(Stream response);


		#region Public

        /// <summary>
        /// Ping (async version)
        /// </summary>
        /// <param name="args"></param>
        /// <param name="callback">Callback gets server response json or null if fail</param>
		public static void Ping(object args, GenericObjectCallback callback)
		{
			string url = ServiceApiUrl + "ping";

			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					// Ping failed.
					callback(null);
				}
				else
				{
					using (var responseStream = response.GetResponseStream())
					using (var reader = new StreamReader(responseStream))
					{
						var text = reader.ReadToEnd();

						Newtonsoft.Json.Linq.JContainer returnObject = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
						callback(returnObject);
					}
				}
			}); // end of MakeApiRequest
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
						responseObject = (Newtonsoft.Json.Linq.JContainer)returnObject;
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

			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					// Search failed.
					callback(null);
				}
				else
				{
					using (var responseStream = response.GetResponseStream())
					using (var reader = new StreamReader(responseStream))
					{
						var text = reader.ReadToEnd();

						//Newtonsoft.Json.Linq.JContainer returnObject = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
						callback(text);
					}
				}

			}); //End of MakeApiRequest

		}   // end of GetWorlds()

        /// <summary>
        /// Get thumbnail for world.
        /// </summary>
        /// <param name="ThumbnailUrl"></param>
        /// <param name="callback">Gets stream of thumb data or null if fail.</param>
		public static void GetThumbnail(string ThumbnailUrl, ResponseStreamCallback callback)
		{
			//Just pass to DownloadData
			DownloadData(ThumbnailUrl, callback);
		}   // end of GetThumbnail()

        /// <summary>
        /// Delete world from Community.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="callback">Gets response text on success, null on failure.</param>
		public static void DeleteWorld(object args, GenericObjectCallback callback)
		{
			string url = ServiceApiUrl + "deleteWorld";

			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					// Search failed.
					callback(null);
				}
				else
				{
					using (var responseStream = response.GetResponseStream())
					using (var reader = new StreamReader(responseStream))
					{
						var text = reader.ReadToEnd();

						//Newtonsoft.Json.Linq.JContainer returnObject = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
						callback(text);
					}

				}
			}); // end of MakeApiRequest

		}   // end of DeleteWorld()

        /// <summary>
        /// Download world to disk.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="callback">Get ResponseStream or null on failure.</param>
		public static void DownloadWorld(object args, ResponseStreamCallback callback)
		{
			string url = ServiceApiUrl + "downloadWorld";

			// First do an api requrest.
			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					// Download failed.
					callback(null);
				}
				else
				{
					var timer = new System.Diagnostics.Stopwatch();
					timer.Start();
					string dataUrl = null;
					try
					{
						using (var responseStream = response.GetResponseStream())
						using (var reader = new StreamReader(responseStream))
						{
							// Handle response from DownloadWorld api.
							var text = reader.ReadToEnd();

							// Get the URL for downloading the .Kodu2 file.
							Newtonsoft.Json.Linq.JContainer container = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
							dataUrl = container.Value<string>("dataUrl");
						}

					}
					catch (Exception ex)
					{
						timer.Stop();

						//More serious exception. Service possibly not reached.
						//Console.WriteLine("EXCEPTION MS:" + timer.Elapsed + " " + url);
						//Console.WriteLine("EXCEPTION ARGS:" + args);
						//Console.WriteLine(ex.Message);
						
						Instrumentation.RecordException(new { type = "EX", url = url, args = args, message = ex.Message, time = timer.Elapsed });

						response = null;    // Report fail.
					}
					finally{
						// Pass on data url and callback to DownloadData.
                        if (dataUrl != null)
                        {
                            DownloadData(dataUrl, callback);
                        }
                        else
                        {
                            callback(null);
                        }
					}

				}
			});//End of MakeApiRequest


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
			// Create an upload request.
			string url = ServiceApiUrl + "authorizeUpload/";

			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					// Failed.
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
							uploadFailed = true;
						}
						thumbUploaded = true;
						// Check if all uploads have finished...
						if (levelUploaded && thumbUploaded && screenUploaded)
						{
                            if (uploadFailed)
                            {
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
							uploadFailed = true;
						}
						screenUploaded = true;

						// Check if all uploads have finished...
						if (levelUploaded && thumbUploaded && screenUploaded)
						{
                            if (uploadFailed)
                            {
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

			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					//Search failed
					callback(null);
				}
				else
				{
					string text = "";
					using (var responseStream = response.GetResponseStream())
					using (var reader = new StreamReader(responseStream))
					{
						text = reader.ReadToEnd();
						callback(text);
					}
				}
			});//End of MakeApiRequest

		}   // end of DeleteWorld()

		//UploadInstrumentation (async version)
		//Callback returns server response json or null if fail
		public static void UploadInstrumentation(object args, GenericObjectCallback callback)
		{
			string url = ServiceApiUrl + "uploadInstrumentation";

			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					//failed
					callback(null);
				}
				else
				{
					//No response obj expected so check status code
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

		//Make the api request.
		//Callback is always called 
		//Response object passed to callback is null in case of failure
        /// <summary>
        /// Makes an API request.
        /// </summary>
        /// <param name="url">The URL for the request.</param>
        /// <param name="args">Object which is serialized into a JSON packet and sent with the request.</param>
        /// <param name="callBack">Always called, gets response.</param>
		static void MakeApiRequest(string url, object args, WebResponseCallback callBack)
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

					var req = (HttpWebRequest)asyncResult.AsyncState;
					response = (HttpWebResponse)req.EndGetResponse(asyncResult);
				}, request);
			}
			catch (WebException ex) // WebException handled first.
			{
				timer.Stop();
				//Console.WriteLine("WebEx MS:" + timer.Elapsed + " " + url);
				//Console.WriteLine("WebEx ARGS:" + args);

				string body = "";
				using (var stream = ex.Response.GetResponseStream())
				using (var reader = new StreamReader(stream))
				{
					body=reader.ReadToEnd();
				}
				Instrumentation.RecordException(new {type="WEX", url = url,args=args, message = ex.Message,body=body, time = timer.Elapsed });
				response = null;    // Report fail.
			}
			catch (Exception ex)
			{
				timer.Stop();

				//More serious exception. Service possibly not reached.
				//Console.WriteLine("EXCEPTION MS:" + timer.Elapsed + " " + url);
				//Console.WriteLine("EXCEPTION ARGS:" + args);
				//Console.WriteLine(ex.Message);
				Instrumentation.RecordException(new { type = "EX", url = url, args = args, message = ex.Message, time = timer.Elapsed });

				response = null;    // Report fail.
			}
			finally
			{
				// Finally success or fail do the callback.
				// Handle this outside of the try catch to only catch
				// comm related errors. This might not be the right thing to do.
				// If a mangled thumbnail or world causes an exception what will happen?
				callBack(response);
			}

		}   // end of MakeApiRequest()

        /// <summary>
        /// Helper function to handle data downloads of worlds and thumbnails .
        /// </summary>
        /// <param name="url"></param>
        /// <param name="callback"></param>
		static void DownloadData(string url, ResponseStreamCallback callback)
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

					using (HttpWebResponse dataResponse = (HttpWebResponse)dataRequest.EndGetResponse(asyncResult))
					{
						using (Stream dataResponseStream = dataResponse.GetResponseStream())
						{
							callback(dataResponseStream);
						}
					}

				}, dataRequest);
			}
			catch (WebException ex) // WebException handled first.
			{
				timer.Stop();
				//Console.WriteLine("WebEx MS:" + timer.Elapsed + " " + url);
				//Console.WriteLine("WebEx ARGS:" + args);

				string body = "";
				using (var stream = ex.Response.GetResponseStream())
				using (var reader = new StreamReader(stream))
				{
					body = reader.ReadToEnd();
				}
				Instrumentation.RecordException(new { type = "WEX", url = url, message = ex.Message, body = body, time = timer.Elapsed });

				callback(null); // Report fail.
			}
			catch (Exception ex)
			{
				timer.Stop();

				// More serious exception. Service possibly not reached.
				//Console.WriteLine("EXCEPTION MS:" + timer.Elapsed + " " + url);
				//Console.WriteLine("EXCEPTION ARGS:" + args);
				//Console.WriteLine(ex.Message);

				Instrumentation.RecordException(new { type = "EX", url = url, message = ex.Message, time = timer.Elapsed });
				callback(null); // Report fail.
			}
			finally
			{
			}
		}   // end of DownloadData()

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
			catch (WebException ex) // WebException handled first.
			{
				timer.Stop();
				Console.WriteLine("WebEx MS:" + timer.Elapsed + " " + url);
				//Console.WriteLine("WebEx ARGS:" + args);

				string body = "";
				using (var stream = ex.Response.GetResponseStream())
				using (var reader = new StreamReader(stream))
				{
					body = reader.ReadToEnd();
				}
				Instrumentation.RecordException(new { type = "WEX", url = url, message = ex.Message, body = body, time = timer.Elapsed });

				response = null;    // Report fail.
			}
			catch (Exception ex)
			{
				timer.Stop();

				// More serious exception. Service possibly not reached.
				//Console.WriteLine("EXCEPTION MS:" + timer.Elapsed + " " + url);
				//Console.WriteLine("EXCEPTION ARGS:" + args);
				//Console.WriteLine(ex.Message);

				Instrumentation.RecordException(new { type = "EX", url = url, message = ex.Message, time = timer.Elapsed });

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


