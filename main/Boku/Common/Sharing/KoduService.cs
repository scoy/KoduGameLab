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
		public static string ServiceApiUrl = "https://koduworlds-api.azurewebsites.net/api/";
        //public static string ServiceApiUrl = "http://koduworlds-api.azurewebsites.net/api/";//For use with fiddler
        //public static string ServiceApiUrl = "http://localhost.fiddler:3000/api/";//Localhost for development

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
					using (var responseStream = response.GetResponseStream())
					using (var reader = new StreamReader(responseStream))
					{
						var text = reader.ReadToEnd();

						Newtonsoft.Json.Linq.JContainer returnObject = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
						callback(returnObject);
					}
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
					using (var responseStream = response.GetResponseStream())
					using (var reader = new StreamReader(responseStream))
					{
						var text = reader.ReadToEnd();

						//Newtonsoft.Json.Linq.JContainer returnObject = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
						callback(text);
					}
				}

			});//End of MakeApiRequest

		}   // end of GetWorlds()

		//GetThumbnail
		//Callback returns stream of thumb data or null if fail
		public static void GetThumbnail(string ThumbnailUrl, ResponseStreamCallback callback)
		{
			//Just pass to DownloadData
			DownloadData(ThumbnailUrl, callback);
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
					using (var responseStream = response.GetResponseStream())
					using (var reader = new StreamReader(responseStream))
					{
						var text = reader.ReadToEnd();

						//Newtonsoft.Json.Linq.JContainer returnObject = JsonConvert.DeserializeObject(text) as Newtonsoft.Json.Linq.JContainer;
						callback(text);
					}

				}
			});//End of MakeApiRequest

		}   // end of DeleteWorld()

		//Download world to disk
		//Callback returns a ResponseStream or null on fail.
		public static void DownloadWorld(object args, ResponseStreamCallback callback)
		{
			string url = ServiceApiUrl + "downloadWorld";

			//first do an api requrest
			KoduService.MakeApiRequest(url, args, (HttpWebResponse response) => {
				if (response == null)
				{
					//Download failed
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
							//handle response from DownloadWorld api
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

						response = null;//report fail
					}
					finally{
						//Pass on data url and callback to DownloadData
						if (dataUrl!=null)
							DownloadData(dataUrl, callback);
						else
							callback(null);
					}

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
					//Console.WriteLine("OK MS:"+ timer.Elapsed.Milliseconds+" "+url);

					var req = (HttpWebRequest)asyncResult.AsyncState;
					response = (HttpWebResponse)req.EndGetResponse(asyncResult);
				}, request);
			}
			catch (WebException ex)//WebException handled first
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
				response = null;//report fail
			}
			catch (Exception ex)
			{
				timer.Stop();

				//More serious exception. Service possibly not reached.
				//Console.WriteLine("EXCEPTION MS:" + timer.Elapsed + " " + url);
				//Console.WriteLine("EXCEPTION ARGS:" + args);
				//Console.WriteLine(ex.Message);
				Instrumentation.RecordException(new { type = "EX", url = url, args = args, message = ex.Message, time = timer.Elapsed });

				response = null;//report fail
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

		//Helper to handle data downloads of worlds and thumbnails 
		private static void DownloadData(string url, ResponseStreamCallback callback)
		{
			var timer = new System.Diagnostics.Stopwatch();
			timer.Start();
			//HttpWebResponse dataResponse = null;//respond with null in case of fail
			try
			{
				//get data from dataUrl
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
			catch (WebException ex)//WebException handled first
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

				callback(null);//report fail
			}
			catch (Exception ex)
			{
				timer.Stop();

				//More serious exception. Service possibly not reached.
				//Console.WriteLine("EXCEPTION MS:" + timer.Elapsed + " " + url);
				//Console.WriteLine("EXCEPTION ARGS:" + args);
				//Console.WriteLine(ex.Message);

				Instrumentation.RecordException(new { type = "EX", url = url, message = ex.Message, time = timer.Elapsed });
				callback(null);//report fail
			}
			finally
			{
			}
		}

			//Helper to handle uploads to storage 
		private static void UploadData(string url, string contentType, byte[] buffer, GenericObjectCallback callback)
		{
			var timer = new System.Diagnostics.Stopwatch();
			timer.Start();
			HttpWebResponse response = null;//respond with null in case of fail
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

					//Handle request response.
					Console.WriteLine("OK MS:" + timer.Elapsed.Milliseconds + " " + url);

					var req = (HttpWebRequest)asyncResult.AsyncState;
					response = (HttpWebResponse)req.EndGetResponse(asyncResult);
				}, request);
			}
			catch (WebException ex)//WebException handled first
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

				response = null;//report fail
			}
			catch (Exception ex)
			{
				timer.Stop();

				//More serious exception. Service possibly not reached.
				//Console.WriteLine("EXCEPTION MS:" + timer.Elapsed + " " + url);
				//Console.WriteLine("EXCEPTION ARGS:" + args);
				//Console.WriteLine(ex.Message);

				Instrumentation.RecordException(new { type = "EX", url = url, message = ex.Message, time = timer.Elapsed });

				response = null;//report fail
			}
			finally
			{
				//finally success or fail do the callback
				//Handle this outside of the try catch to only catch
				//comm related errors. This might not be the right thing to do.
				//If a mangled thumbnail or world causes an exception what will happen?
				callback(response);
			}
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

