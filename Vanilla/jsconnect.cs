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
	/// Version 1.0.2
	/// </summary>
	public class jsConnect {
		public static bool Debug = false;

		protected static IDictionary Error(string code, string message) {
			IDictionary result = new SortedList();
			result["error"] = code;
			result["message"] = message;
			
			return result;
		}
		
		public static string GetJsConnectString(IDictionary user, NameValueCollection request, string clientID, string secret, bool secure, string hash) {
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
			
			if (secure) {
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
					string timestampSig = jsConnect.Hash(timestamp.ToString() + secret, hash);
					if(timestampSig != request["signature"])
						error = jsConnect.Error("access_denied", "Signature invalid.");
				}
			}

			IDictionary result;
			
			if(error != null)
				result = error;
			else if(user != null && user.Count > 0) {
				result = new SortedList(user);
				SignJsConnect(result, clientID, secret, true, hash);
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
		}

		/// <summary>
		/// Backwards compatability version of GetJsConnectString().
		/// </summary>
		public static string GetJsConnectString(IDictionary user, NameValueCollection request, string clientID, string secret, bool secure) {
			return GetJsConnectString (user, request, clientID, secret, secure, "md5");
		}
		
		/// <summary>
		/// Encode a dictionary as json.
		/// </summary>
		/// <param name="d">The data to encode.</param>
		/// <returns>The json encoded string.</returns>
		public static string JsonEncode(IDictionary data) {
			var json = new System.Web.Script.Serialization.JavaScriptSerializer();

			string result = json.Serialize(data);
			return result;
		}

		public static string Hash(string password, string method) {
			byte[] textBytes = System.Text.Encoding.Default.GetBytes(password);
			try {
				System.Security.Cryptography.HashAlgorithm cryptHandler;

				switch (method.ToLower()) {
				case "":
				case "md5":
					cryptHandler = System.Security.Cryptography.MD5.Create();
					break;
				case "sha1":
					cryptHandler = System.Security.Cryptography.SHA1.Create();
					break;
				case "sha256":
					cryptHandler = System.Security.Cryptography.SHA256.Create();
					break;
				
				}

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
		
		public static string StrUpper(System.Text.RegularExpressions.Match m) {
			return m.ToString().ToUpper();
		}
		
		public static string SignJsConnect(IDictionary data, string clientID, string secret, bool setData, string hash) {
			// Generate a sorted list of the keys.
			string[] keys = new string[data.Count];
			data.Keys.CopyTo(keys, 0);
			Array.Sort(keys, new CaseInsensitiveComparer());
			
			// Generate the string to sign.
			StringBuilder sigStr = new StringBuilder();
			foreach(string key in keys) {
				if(sigStr.Length > 0)
					sigStr.Append("&");
				
				string encValue = jsConnect.UrlEncode(data[key].ToString());
				
				sigStr.AppendFormat("{0}={1}", jsConnect.UrlEncode(key.ToLower()), encValue);
			}
			
			// Sign the string with the secret.
			string signature = jsConnect.Hash(sigStr.ToString() + secret, hash);
			
			if(setData) {
				data["clientid"] = clientID;
				data["signature"] = signature;

				if (Debug)
					data["sigStr"] = sigStr.ToString();
			}
			return signature;
		}
		
		public static int Timestamp() {
			DateTime epoch = new DateTime(1970, 1, 1);
			TimeSpan span = (DateTime.UtcNow - epoch);
			return (int)span.TotalSeconds;
		}

		public static string UrlEncode(string s) {
			System.Text.RegularExpressions.MatchEvaluator me = new System.Text.RegularExpressions.MatchEvaluator(jsConnect.StrUpper);

			string result = HttpUtility.UrlEncode(s);
			result = System.Text.RegularExpressions.Regex.Replace(result, "%[0-9a-f][0-9a-f]", me);
			result = result.Replace("'", "%27");
			result = result.Replace("!", "%21");
			result = result.Replace("*", "%2A");
			result = result.Replace("(", "%28");
			result = result.Replace(")", "%29");

			return result;
		}
	}
}
