namespace SmartWebDbView
{
    using System;
    using System.Collections.Generic;

    public interface IServiceOfDbView
    {
        string Schema { get; set; }

        PagedResult View(string viewName, Net.Http.WebApi.OData.Query.ODataQueryOptions options);

        Uri GetNextPageLink(Uri requestUri, IEnumerable<KeyValuePair<string, string>> queryParameters, int pageSize);
    }
}
