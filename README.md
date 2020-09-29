# Vanilla jsConnect Client Library for .NET

This repository contains the files you need to use Vanilla's jsConnect with a .NET project.

*Note: This library implements the version 3 protocol of jsConnect. If you need the legacy v2 protocol you can download the [old release](https://github.com/vanilla/jsConnectdotNet/releases/tag/v2.0).*

## Requirements

This project depends on the following other libraries:

- [Json.Net](https://www.nuget.org/packages/Json.Net)
- [JWT](https://www.nuget.org/packages/JWT)

## Installation

This project is distributed as sources. You will need to bring in the sources from the [jsConnectDotNet](./jsConnectDotNet) folder and also the dependencies listed above.

## Usage

To use jsConnect you will need to make a web page that gives information about the currently signed in user of your site. To do this you'll need the following information:

- You will need the client ID and secret that you configured from within Vanilla's dashboard.
- The currently signed in user or if there is no signed in user you'll also need that.

### Basic Usage

To use the library you follow the basic process:

1. Create an instance of the `JsConnectV3` class.
2. Assign your client ID and secret to configure it.
3. Assign details of the current user.
4. Call `GenerateResponseLocation()` to get the URL to redirect to.
5. Perform a 302 redirect to the given location.
6. If `GenerateResponseLocation()` throws an exception then you will need to display that to your users. This should only happen if there is a misconfiguration.

The following code snipped demonstrates the basic usage using 

```c#
public class ExampleController: Controller {
        // In this example the "jwt" parameter is mapped to the "?jwt=..." query string.
        public ActionResult Index(string jwt) {
            jsc = new JsConnectV3();
            
            // First set your client ID and secret that you configured in your dashboard.
            jsc.SetSigningCredentials(ClientID, Secret);

            // Set the current user information.
            jsc.SetUniqueID("123");
            jsc.SetName("Username");
            jsc.SetEmail("user@example.com");
            jsc.SetPhotoUrl("https://example.com/avaar.jpg");
            jsc.SetUserField("customField", "Some custom field");
    
            try {
                // Generate the redirect URL and redirect.
                string redirectUrl = jsc.GenerateResponseLocation(jwt);
                new RedirectResult(url: redirectUrl, permanent: true);
            } catch (Exception ex) {
                // Display the exception in some custom way to your app.
                return new View(...);
            }
        }
    }
```

The method instantiates a `JsConnectV3` object and sets it up. It then calls `JsConnectV3::GenerateResponseLocation()` with the `jwt` querystring parameter to process the request. You need to 302 redirect to that location.

If there is an exception you will need to display that on your page. Remember to escape the message.

### Configuring Vanilla

Once you've made your authentication page you will need to add that URL to your jsConnect settings in Vanilla's dashboard. This is the **authentication URL**.