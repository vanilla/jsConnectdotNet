using System;
using System.Configuration;
using System.Web.Configuration;
using System.Collections;

using Vanilla;

/// <summary>
/// This is an example page for using Vanilla's jsConnect library.
/// Please note that this pagee must serve its output in a special format.
/// Do not include your own calls to Response.Write() or else you may encounter unexpected behaviour.
/// </summary>
public partial class _Default : System.Web.UI.Page {
    protected void Page_Init(object sender, EventArgs e) {
    	try {
		// 1. Get your client ID and secret here. These must match those in your jsConnect settings.
		string clientID = "123";
		string secret = "123";

		// 2. Grab the current user from your session management system or database here.

		bool signedIn = true; // this is just a placeholder
		
		// YOUR CODE HERE.

		// 3. Fill in the user information in a way that Vanilla can understand.
		SortedList user = new SortedList();
		
		if (signedIn) {
			// CHANGE THESE FOUR LINES.
			user["uniqueid"] = 1;
			user["name"] = "JohnDoe";
			user["email"] = "john.doe@anonymous.com";
			user["photourl"] = "";
		}

		// 4. Generate the jsConnect string.
		bool secure = false; // this should be true unless you are testing
		string js = jsConnect.GetJsConnectString(user, Request.QueryString, clientID, secret, secure);
		
		Response.Write(js);
		Response.End();
		
		} catch (Exception ex) {
			SortedList exCollection = new SortedList();
			exCollection['error'] = 'exception';
			exCollection['message'] = ex.Message;
			
			string exString = jsConnect.JsonEncode(exCollection);
			Response.Write(exString);
			Response.End();
		}
    }
}
