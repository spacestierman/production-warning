using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProductionWarning
{
	public class ProductionWarningInstance
	{
		private static readonly ILog Logger = LogManager.GetLogger(typeof(ProductionWarningInstance));

		public static ProductionWarningInstance FromConfig()
		{
			Logger.Info("FromConfig()");

			string hostURL = ConfigurationManager.AppSettings["hostURL"];
			if (string.IsNullOrEmpty(hostURL))
			{
				throw new Exception("No `hostURL` set in the configuration file!");
			}
			
			string threadsAsString = ConfigurationManager.AppSettings["maximumNumberOfListeningThreads"];
			if (string.IsNullOrEmpty(threadsAsString))
			{
				throw new Exception("No `maximumNumberOfListeningThreads` set in the configuration file!");
			}

			int threads = 0;
			if (!int.TryParse(threadsAsString, out threads))
			{
				throw new Exception("`maximumNumberOfListeningThreads` set in the configuration file was not a valid integer!");
			}
			else if (threads <= 0)
			{
				throw new Exception("`maximumNumberOfListeningThreads` set in the configuration file must be a positive integer value!");
			}

			ProductionWarningInstance instance = new ProductionWarningInstance(hostURL, threads);
			return instance;
		}

		private HttpListener _listener;
		private int _maximumNumberOfListeningThreads;
		private Dmx512Controller _controller;

		private bool _shouldBeRunning;
		private DateTime? _warningStarted;
		private float _redValue;
		private Thread _updateThread;
		
		public ProductionWarningInstance(string hostURL, int maximumNumberOfListeningThreads = 1)
		{
			if (string.IsNullOrEmpty(hostURL))
			{
				throw new ArgumentException("hostURL is required. Must include the protocol! Port is optional.");
			}

			_listener = BuildListener(hostURL);
			_shouldBeRunning = false;
			_maximumNumberOfListeningThreads = maximumNumberOfListeningThreads;

			_controller = new Dmx512Controller();

			IEnumerable<string> devices = _controller.GetAvailableSerialNumbers();
			string firstSerial = devices.FirstOrDefault();
			if (string.IsNullOrEmpty(firstSerial))
			{
				throw new Exception("No DMX controllers found.");
			}

			_controller.Connect(firstSerial);
			_redValue = 0;
			
			_updateThread = new Thread(UpdateThread);
			_updateThread.Start();

			_shouldBeRunning = true;
		}

		public void Start()
		{
			Logger.Info("Start()");

			_listener.Start();

			for (int i = 0; i < _maximumNumberOfListeningThreads; i++)
			{
				_listener.BeginGetContext(new AsyncCallback(GetContextThread), _listener);
			}
		}

		public void Stop()
		{
			Logger.Info("Stop()");
		}

		public void Dispose()
		{
			_shouldBeRunning = false;
			_updateThread.Join();
			_updateThread = null;
		}

		private void UpdateThread()
		{
			while (_shouldBeRunning)
			{
				if (_warningStarted.HasValue)
				{
					TimeSpan duration = DateTime.Now.Subtract(_warningStarted.Value);
					float target = (1.0f + (float)Math.Sin(duration.TotalSeconds)) / 2.0f;
					target += 0.2f; // Make sure we're at a minimum threshold when warning.

					if (_redValue < target)
					{
						_redValue += 0.01f;
					}
					else
					{
						_redValue -= 0.01f;
					}
				}
				else
				{
					_redValue -= 0.01f;
				}

				if (_redValue < 0.0f)
				{
					_redValue = 0.0f;
				}
				else if (_redValue > 1.0f)
				{
					_redValue = 1.0f;
				}


				if (_controller.IsConnected)
				{
					_controller.SetColorAtAddress(1, _redValue, 0.0f, 0.0f);
				}

				Thread.Sleep(1);
			}
		}

		private static HttpListener BuildListener(string hostURL)
		{
			if (string.IsNullOrEmpty(hostURL))
			{
				throw new Exception("hostURL is required.");
			}

			HttpListener listener = new HttpListener();
			listener.Prefixes.Add(hostURL);
			return listener;
		}

		private void GetContextThread(IAsyncResult result)
		{
			HttpListener listener = (HttpListener)result.AsyncState;
			try
			{
				HttpListenerContext context = listener.EndGetContext(result);
				ProcessRequest(context);
			}
			catch
			{
				// Intentionally silent. This catches when Stop() is called.
			}

			if (_shouldBeRunning)
			{
				listener.BeginGetContext(new AsyncCallback(GetContextThread), listener);
			}
		}

		private void ProcessRequest(HttpListenerContext context)
		{
			Logger.Info("ProcessRequest(context)");

			if (context.Request.HttpMethod != "GET")
			{
				WriteFailureResponse(context.Response, "Only GET methods are currently supported.");
				return;
			}

			if (context.Request.RawUrl.EndsWith("/start"))
			{
				_warningStarted = DateTime.Now;
				WriteSuccessResponse(context.Response, "Started.");
				_controller.SetColorAtAddress(1, 255, 0, 0);
			}
			else if (context.Request.RawUrl.EndsWith("/stop"))
			{
				_warningStarted = null;
				WriteSuccessResponse(context.Response, "Stopped.");
			}
			else
			{
				WriteFailureResponse(context.Response, "Unknown endpoint.");
			}
		}

		private static void WriteSuccessResponse(HttpListenerResponse response, string message)
		{
			ListenerResult data = new ListenerResult()
			{
				Success = true,
				Message = message
			};
			WriteResponseJSON(response, data, false);
		}

		private static void WriteFailureResponse(HttpListenerResponse response, string message)
		{
			ListenerResult data = new ListenerResult()
			{
				Success = false,
				Message = message
			};
			WriteResponseJSON(response, data, false);
		}

		private static void WriteResponseJSON(HttpListenerResponse response, object data, bool indicateSuccess)
		{
			response.Headers.Clear();
			response.Headers.Add("Content-Type", "application/json");

			string content = JsonConvert.SerializeObject(data);
			byte[] contentBytes = Encoding.UTF8.GetBytes(content);

			response.ContentLength64 = contentBytes.Length;
			response.OutputStream.Write(contentBytes, 0, contentBytes.Length);
			response.Close();
		}
	}

	class ListenerResult
	{
		public bool Success { get; set; }
		public string Message { get; set; }
	}
}
