# SmartWebDbView
Expose an db view at easy way  
  
```
Install-Package SmartWebDbView
```  


## You need to use `OData` pattern at `URI`

```  
http://localhost:28393/api/dbviews/Product?$select=Id,Description&$orderby=Id%20asc,Description%20desc&$filter=Id%20eq%2020
```  

```  
http://localhost:28393/api/dbviews/Product?$orderby=Id&$top=5&$skip=1
```  

```  
http://localhost:28393/api/dbviews/Product?$orderby=Description&$top=5&$skip=1
```  
  
  
## Your controller need to be like this

```csharp
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
```
  
## Db view prefix name
Take care, always use this prefix and adopt this pattern on views name, to avoid malicious selection and just expose the views that you really want.  
Do not allow to use full name of an view on http request, like: `vw_Product`, always concatenate the http `view name` `param` with the prefix view name.  
If you don't set the `prefixVwName` param constructor of the `ServiceOfDbView` the default prefix will be `vw_` .  

## Schema
If you don't set an schema, the default schema will be `dbo`.  

  
### News  
    
- 0.0.1 Created the project.
  
Danke  
