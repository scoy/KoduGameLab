using System;
using System.Net;
using System.IO;
using System.Text;
using Newtonsoft.Json;
//using System.Net.Http;
using System.Threading;

namespace Boku.Common.Sharing
{
	class KoduService
	{
		#region Members
		//Our service address
		const string ServiceApiUrl = "https://koduworlds-api.azurewebsites.net/api/";
		//const string ServiceApiUrl = "http://koduworlds-api.azurewebsites.net/api/";//For use with fiddler
		//const string ServiceApiUrl = "http://localhost.fiddler:3000/api/";//Localhost for development
        #endregion

        //define callback that passes a WebResponse
        public delegate void WebResponseCallback(HttpWebResponse response);

		//define callback that passes a byte[]
		public delegate void ByteArrayCallback(byte[] response);

		//define callback that passes a object
		public delegate void GenericObjectCallback(object responseObject);

		//define callback that passes a WebResponseStream
		public delegate void ResponseStreamCallback(Stream response);


		#region Public

		//Ping (async version)
		//Callback returns server response json or null if fail
		public static void Ping(object args, GenericObjectCallback callback)
		{
			string url = ServiceApiUrl + "ping";

			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					//ping failed
					callback(null);
				}
				else
				{
					var responseStream = response.GetResponseStream();
					StreamReader reader = new StreamReader(responseStream);
					var text = reader.ReadToEnd();

					Newtonsoft.Json.Linq.JContainer returnObject = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
					callback(returnObject);
				}
			});//End of MakeApiRequest
		}   // end of Ping()

		//Ping (non async version)
		//returns true/false if ping OK.
		public static bool PingNonAsync(object args)
		{
			var pingPending = true;
			var communityAvailable = false;
			Ping(args, 
				//lambda callback
				(object returnObject) => {
					if (returnObject == null)
					{
						//ping failed
					}
					else
					{
						//ping ok.
						communityAvailable = true;
					}
					pingPending = false;
				}
			);

			while (pingPending)
			{
				Thread.Sleep(10);
			}
			return (communityAvailable);
		}   // end of PingNonAsync()

		//Search worlds
		//Callback returns server response json or null if fail
		static public void Search(object args, GenericObjectCallback callback)
		{
			string url = ServiceApiUrl + "search";

			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					//Search failed
					callback(null);
				}
				else
				{
					var responseStream = response.GetResponseStream();
					StreamReader reader = new StreamReader(responseStream);
					var text = reader.ReadToEnd();

					//Newtonsoft.Json.Linq.JContainer returnObject = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
					callback(text);

				}

			});//End of MakeApiRequest

		}   // end of GetWorlds()

		//GetThumbnail
		//Callback returns byte array of thumb data or null if fail
		public static void GetThumbnail(string ThumbnailUrl, ByteArrayCallback callback)
		{
			//const int timeout = 10000;   // 10 seconds.
			try
			{
				Uri uri = new Uri(ThumbnailUrl);
				var request = (HttpWebRequest)WebRequest.Create(uri);

				var result = request.BeginGetResponse((asyncResult) => {
					using (HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(asyncResult))
					{
						using (Stream responseStream = response.GetResponseStream())
						{
							byte[] data;
							//todo. Better way to return data?
							using (MemoryStream ms = new MemoryStream())
							{
								responseStream.CopyTo(ms);
								data = ms.ToArray();
							}
							callback(data);
						}
					}

				}, request);
				//todo timeouts!
				//ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, TimeoutCallback, request, timeout, true);
			}
			catch (Exception e)//Should this be WebException?
			{
				callback(null);//failed to load for ANY reason
				if (e != null)
				{
				}
			}

		}   // end of GetThumbnail()

		//DeleteWorld
		//Callback returns null on fail.
		public static void DeleteWorld(object args, GenericObjectCallback callback)
		{
			string url = ServiceApiUrl + "deleteWorld";

			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					//Search failed
					callback(null);
				}
				else
				{
					var responseStream = response.GetResponseStream();
					StreamReader reader = new StreamReader(responseStream);
					var text = reader.ReadToEnd();

					//Newtonsoft.Json.Linq.JContainer returnObject = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
					callback(text);

				}
			});//End of MakeApiRequest

		}   // end of DeleteWorld()

		//Download world to disk
		//Callback returns a ResponseStream or null on fail.
		public static void DownloadWorld(object args, ResponseStreamCallback callback)
		{
			string url = ServiceApiUrl + "downloadWorld";

			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					//Download failed
					callback(null);
				}
				else
				{
					//handle response from DownloadWorld api
					var responseStream = response.GetResponseStream();
					StreamReader reader = new StreamReader(responseStream);
					var text = reader.ReadToEnd();

					// Get the URL for downloading the .Kodu2 file.
					Newtonsoft.Json.Linq.JContainer container = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
					string dataUrl = container.Value<string>("dataUrl");

					//get data from dataUrl
					Uri uri = new Uri(dataUrl);
					var dataRequest = (HttpWebRequest)WebRequest.Create(uri);
					dataRequest.BeginGetResponse((asyncResult) => {
						using (HttpWebResponse dataResponse = (HttpWebResponse)dataRequest.EndGetResponse(asyncResult))
						{
							using (Stream dataResponseStream = dataResponse.GetResponseStream())
							{
								callback(dataResponseStream);
							}
						}

					}, dataRequest);

					//todo. exception handling.

				}
			});//End of MakeApiRequest


		}   // end of DownloadWorld()

		//Upload a world(including thumb and screen) from disk
		//returns null on failure
		public static void UploadWorld(object args, string levelPath,string thumbPath,string screenPath, GenericObjectCallback callback)
		{
			//Create an upload request
			string url = ServiceApiUrl + "authorizeUpload/";

			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					//failed
					callback(null);
				}
				else
				{
					var responseStream = response.GetResponseStream();
					StreamReader reader = new StreamReader(responseStream);
					var text = reader.ReadToEnd();

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
							//data upload failed
							uploadFailed = true;
                        }
						levelUploaded = true;
						//Check if all uploads have finished...
						if (levelUploaded && thumbUploaded && screenUploaded)
						{
							if (uploadFailed)
								callback(null);//report failure to caller
							else
								FinalizeUpload(args, callback);
						}
					});

					UploadDataFromFile(uploadScreenUrl, @"image/jpeg", thumbPath, (uploadResponse) => {
						if (uploadResponse == null)
						{
							//data upload failed
							uploadFailed = true;
						}
						thumbUploaded = true;
						//Check if all uploads have finished...
						if (levelUploaded && thumbUploaded && screenUploaded)
						{
							if (uploadFailed)
								callback(null);//report failure to caller
							else
								FinalizeUpload(args,callback);
						}
					});

					UploadDataFromFile(uploadThumbUrl, @"image/jpeg", screenPath, (uploadResponse) => {
						if (uploadResponse == null)
						{
							//data upload failed
							uploadFailed = true;
						}
						screenUploaded = true;

						//Check if all uploads have finished...
						if (levelUploaded && thumbUploaded && screenUploaded)
						{
							if (uploadFailed)
								callback(null);//report failure to caller
							else
								FinalizeUpload(args, callback);
						}
					});

				}
			});//End of MakeApiRequest


		}

        #endregion

        #region Private

		//Finalize an world upload.
		//Private and should only be called by internals
        private static void FinalizeUpload(object args, GenericObjectCallback callback)
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
					var responseStream = response.GetResponseStream();
					StreamReader reader = new StreamReader(responseStream);
					var text = reader.ReadToEnd();

					//Newtonsoft.Json.Linq.JContainer returnObject = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
					callback(text);

				}
			});//End of MakeApiRequest

		}   // end of DeleteWorld()

		//Private helper to make API calls easier
		private static HttpWebRequest CreateApiRequest(string url, object args)
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
		}

		//Make the api request.
		//Callback is always called 
		//Response object passed to callback is null in case of failure
		private static void MakeApiRequest(string url, object args, WebResponseCallback callBack)
		{
			var timer = new System.Diagnostics.Stopwatch();
			timer.Start();
			HttpWebResponse response = null;//respond with null in case of fail
			try
			{
				var request = CreateApiRequest(url, args);

				// Send request.
				var result = request.BeginGetResponse(asyncResult => {
					timer.Stop();

					//Handle request response.
					Console.WriteLine("OK MS:"+ timer.Elapsed.Milliseconds+" "+url);

					var req = (HttpWebRequest)asyncResult.AsyncState;
					response = (HttpWebResponse)req.EndGetResponse(asyncResult);
				}, request);
			}
			catch (WebException ex)//WebException handled first
			{
				timer.Stop();
				Console.WriteLine("WebEx MS:" + timer.Elapsed + " " + url);
				Console.WriteLine("WebEx ARGS:" + args);

				using (var stream = ex.Response.GetResponseStream())
				using (var reader = new StreamReader(stream))
				{
					Console.WriteLine(reader.ReadToEnd());
				}
				response = null;//report fail
			}
			catch (Exception ex)
			{
				timer.Stop();

				//More serious exception. Service possibly not reached.
				Console.WriteLine("EXCEPTION MS:" + timer.Elapsed + " " + url);
				Console.WriteLine("EXCEPTION ARGS:" + args);
				Console.WriteLine(ex.Message);
				response=null;//report fail
			}
			finally
			{
				//finally success or fail do the callback
				//Handle this outside of the try catch to only catch
				//comm related errors. This might not be the right thing to do.
				//If a mangled thumbnail or world causes an exception what will happen?
				callBack(response);
			}

		}

		//Helper to handle uploads to storage 
		private static void UploadData(string url, string contentType, byte[] buffer, GenericObjectCallback callback)
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
			request.BeginGetResponse(asyncResult => {
				try
				{
					//Handle request response.
					Console.WriteLine("UploadData Callback");
					callback(true);
				}
				catch (Exception ex)//todo should this be WebException?
				{
					//it looks like this is the "normal" failure for stuff like service is not up. 
					Console.WriteLine("UploadData EXCEPTION");
					Console.WriteLine(ex.Message);
					callback(null);//report fail
				}
				finally
				{
				}
			}, request);
		}   // end of UploadData()

		//Wrapper for UploadData that takes a file path
		private static void UploadDataFromFile(string url, string contentType, string filePath, GenericObjectCallback callback)
		{
			byte[] buffer = File.ReadAllBytes(filePath);
			UploadData(url, contentType, buffer, callback);
		}

        #endregion //Private
    }
}

