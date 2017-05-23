using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace jsConnectdotNet
{
    public class Global : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            RouteTable.Routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            RouteTable.Routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "SSO", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
