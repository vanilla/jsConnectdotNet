using System;
using System.Configuration;
using System.Web.Configuration;
using System.Collections;

using Vanilla;

/// <summary>
/// Customize this page
/// </summary>
public partial class _Default : System.Web.UI.Page {
    protected void Page_Init(object sender, EventArgs e) {
		// 1. Get your client ID and secret here. These must match those in your jsConnect settings.
		string clientID = "123";
		string secret = "123";

		// 2. Grab the current user from your session management system or database here.

		// YOUR CODE HERE.

		// 3. Fill in the user information in a way that Vanilla can understand.
		SortedList user = new SortedList();
		// CHANGE THESE FOUR LINES.
		user["uniqueid"] = 1;
		user["name"] = "JohnDoe";
		user["email"] = "john.doe@anonymous.com";
		user["photourl"] = "";

		// 4. Generate the jsConnect string.
		string js = jsConnect.GetJsConnectString(user, Request.QueryString, clientID, secret, false);
		Response.Write(js);
		Response.End();
    }
}
