using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace NuGet.Search.Website
{
    public partial class NuGetSearch : System.Web.UI.MasterPage
    {
        protected void Page_PreInit(object sender, EventArgs e)
        {
            String theme = ConfigurationManager.AppSettings["Theme"] ?? "NuGet";
            Page.Theme = theme;
        }

        protected void Page_Load(object sender, EventArgs e)
        {

        }
    }
}