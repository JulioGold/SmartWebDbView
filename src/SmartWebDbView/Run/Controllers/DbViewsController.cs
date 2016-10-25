namespace Run.Controllers
{
    using SmartWebDbView;
    using System.Net.Http;
    using System.Web.Http;

    [RoutePrefix("Api/DbViews")]
    public class DbViewsController : ApiController
    {
        private readonly IServiceOfDbView _serviceOfDbView;

        public DbViewsController()
        {
            // If you had an DbContext here, you can pass the connection string  of this like:
            // string connectionString = _context.Database.Connection.ConnectionString;

            // Get connection string from Web.config
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DBModel"].ConnectionString;

            // This is a just sample, i have no DI configured here, I'll create my own instance :P
            _serviceOfDbView = new ServiceOfDbView(connectionString);

            // If you want to set a schema
            //_serviceOfDbView.Schema = "dbo";
        }

        [HttpGet]
        [Route("{ViewName}")]
        public PagedResult Get(string viewName, Net.Http.WebApi.OData.Query.ODataQueryOptions options)
        {
            // Get view paged result from database view
            var pagedResult = _serviceOfDbView.View(viewName, options);

            // Set next page link in paged result
            pagedResult.NextPageLink = _serviceOfDbView.GetNextPageLink(Request.RequestUri, Request.GetQueryNameValuePairs(), 10);

            return pagedResult;
        }
    }
}
