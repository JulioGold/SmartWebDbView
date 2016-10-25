namespace SmartWebDbView
{
    using System;
    using System.Collections.Generic;

    public class PagedResult
    {
        public IEnumerable<Dictionary<string, Object>> Items { get; set; }

        public long? Count { get; set; }

        public Uri NextPageLink { get; set; }
    }
}
