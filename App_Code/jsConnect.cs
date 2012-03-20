using System;
using System.Data;
using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Collections;
using System.Text;
using System.Collections.Specialized;

namespace Vanilla {
	/// <summary>
	/// This object contains the client code for Vanilla jsConnect signle-sign-on.
	/// Version 1.0
	/// </summary>
	public class jsConnect {
		protected static IDictionary Error(string code, string message) {
			IDictionary result = new SortedList();
			result["error"] = code;
			result["message"] = message;

			return result;
		}


		public static string GetJsConnectString(IDictionary user, NameValueCollection request, string clientID, string secret, bool secure) {
			if(request == null)
				request = System.Web.HttpContext.Current.Request.QueryString;

			IDictionary error = null;
			
			int timestamp = 0;
			try {
				timestamp = System.Convert.ToInt32(request["timestamp"]);
			} catch {
				timestamp = 0;
			}
			int currentTimestamp = jsConnect.Timestamp();

			if(secure) {
				if(request["client_id"] == null)
					error = jsConnect.Error("invalid_request", "The client_id parameter is missing.");
				else if(request["client_id"] != clientID)
					error = jsConnect.Error("invalid_client", "Unknown client " + request["client_id"] + ".");
				else if(request["timestamp"] == null && request["signature"] == null) {
					if(user != null && user.Count > 0) {
						error = new SortedList();
						error["name"] = user["name"];
						error["photourl"] = user.Contains("photourl") ? user["photourl"] : "";
					} else {
						error = new SortedList();
						error["name"] = "";
						error["photourl"] = "";
					}
				} else if(timestamp == 0) {
					error = jsConnect.Error("invalid_request", "The timestamp is missing or invalid.");
				} else if(request["signature"] == null)
					error = jsConnect.Error("invalid_request", "The signature is missing.");
				else if(Math.Abs(currentTimestamp - timestamp) > 30 * 60)
					error = jsConnect.Error("invalid_request", "The timestamp is invalid.");
				else {
					// Make sure the timestamp's signature checks out.
					string timestampSig = jsConnect.MD5(timestamp.ToString() + secret);
					if(timestampSig != request["signature"])
						error = jsConnect.Error("access_denied", "Signature invalid.");
				}
			}

			IDictionary result;

			if(error != null)
				result = error;
			else if(user != null && user.Count > 0) {
				result = new SortedList(user);
				SignJsConnect(result, clientID, secret, true);
			} else {
				result = new SortedList();
				result["name"] = "";
				result["photourl"] = "";
			}

			string json = jsConnect.JsonEncode(result);
			if(request["callback"] == null)
				return json;
			else
				return request["callback"] + "(" + json + ")";

			return "";
		}

		/// <summary>
		/// Encode a dictionary as json.
		/// </summary>
		/// <param name="d">The data to encode.</param>
		/// <returns>The json encoded string.</returns>
		public static string JsonEncode(IDictionary data) {
			StringBuilder result = new StringBuilder();

			foreach(DictionaryEntry entry in data) {
				if(result.Length > 0)
					result.Append(", ");

				string key = Convert.ToString(entry.Key);
				string value = Convert.ToString(entry.Value);

				result.AppendFormat("\"{0}\": \"{1}\"", key.Replace("\"", "\\\""), value.Replace("\"", "\\\""));
			}

			return "{ " + result.ToString() + " }";
		}

		public static string MD5(string password) {
			byte[] textBytes = System.Text.Encoding.Default.GetBytes(password);
			try {
				System.Security.Cryptography.MD5CryptoServiceProvider cryptHandler;
				cryptHandler = new System.Security.Cryptography.MD5CryptoServiceProvider();
				byte[] hash = cryptHandler.ComputeHash(textBytes);
				string ret = "";
				foreach(byte a in hash) {
					if(a < 16)
						ret += "0" + a.ToString("x");
					else
						ret += a.ToString("x");
				}
				return ret;
			} catch {
				throw;
			}
		}

		public static string SignJsConnect(IDictionary data, string clientID, string secret, bool setData) {
			// Generate a sorted list of the keys.
			string[] keys = new string[data.Count];
			data.Keys.CopyTo(keys, 0);
			Array.Sort(keys, new CaseInsensitiveComparer());

			// Generate the string to sign.
			StringBuilder sigStr = new StringBuilder();
			foreach(string key in keys) {
				if(sigStr.Length > 0)
					sigStr.Append("&");
				sigStr.AppendFormat("{0}={1}", HttpUtility.UrlEncode(key.ToLower()), HttpUtility.UrlEncode(data[key].ToString()));
			}

			// MD5 sign the string with the secret.
			string signature = jsConnect.MD5(sigStr.ToString() + secret);

			if(setData) {
				data["clientid"] = clientID;
				data["signature"] = signature;
			}
			return signature;
		}

		public static int Timestamp() {
			HttpResponse response = HttpContext.Current.Response;

			DateTime epoch = new DateTime(1970, 1, 1);
			TimeSpan ts = new TimeSpan(DateTime.UtcNow.Ticks - epoch.Ticks);
			return Convert.ToInt32(ts.TotalSeconds);
		}
	}
}
