namespace SmartWebDbView
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Data.SqlClient;
    using Net.Http.WebApi.OData.Query;
    using Net.Http.WebApi.OData.Query.Expressions;
    using System.Data;
    using System.Text;

    // https://www.asp.net/web-api/overview/odata-support-in-aspnet-web-api/supporting-odata-query-options
    // https://github.com/TrevorPilley/Net.Http.WebApi.OData
    public class ServiceOfDbView : IServiceOfDbView
    {
        private readonly string _connectionString;
        private const int TOP_LIMIT = 1000;
        private string _schema;

        public string Schema
        {
            get { return _schema = String.IsNullOrEmpty(_schema) ? "dbo" : _schema; }

            set { _schema = String.IsNullOrEmpty(value) ? _schema : value; }
        }

        // Take care, always use this prefix and adopt this pattern on views name, to avoid malicious selection and just expose the views that you really want.
        // Do not allow to use full name of an view on http request, like: vw_Product, always concatenate the http view name param with the prefix view name.
        private readonly string _prefixVwName; // Ex.:: vw_, VW, view_

        // http://localhost:28393/api/dbviews/Product?$select=Id,Description&$orderby=Id%20asc,Description%20desc&$filter=Id%20eq%2020
        // http://localhost:28393/api/dbviews/Product?$orderby=Id&$top=5&$skip=1
        // http://localhost:28393/api/dbviews/Product?$orderby=Description&$top=5&$skip=1
        public ServiceOfDbView(string connectionString, string prefixVwName = null)
        {
            _connectionString = connectionString;
            _prefixVwName = String.IsNullOrEmpty(prefixVwName) ? "vw_" : prefixVwName;
        }

        public PagedResult View(string viewName, ODataQueryOptions options)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                int skip = 0;
                if (options.Skip != null)
                {
                    skip = options.Skip.Value;
                }

                int top = TOP_LIMIT;
                if (options.Top != null)
                {
                    top = Math.Min(options.Top.Value, TOP_LIMIT);
                }

                var query = @"
                    SELECT * 
                    FROM ( SELECT
                        {totalRegisters}
                        ROW_NUMBER() OVER(ORDER BY {orderby}) AS [ROW_NUMBER], 
                        {fields} 
                        FROM [{schema}].[{prefixViewName}{viewName}] {filter}
                    ) AS TBL
                    WHERE[ROW_NUMBER] BETWEEN (@Skip) AND (@Skip + @RowspPage - 1)
                    ORDER BY[ROW_NUMBER];";

                query = query.Replace("{totalRegisters}", options.InlineCount != null ? "COUNT(0) OVER() [TOTAL_REGISTERS]," : String.Empty);
                query = query.Replace("{schema}", Schema);
                query = query.Replace("{prefixViewName}", _prefixVwName);
                query = query.Replace("{viewName}", viewName);
                query = query.Replace("{fields}", options.Select == null ? "*" : string.Join(",", options.Select.Properties));
                query = query.Replace("{orderby}", string.Join(",", options.OrderBy.Properties.Select(a => a.Name + " " + ToDir(a.Direction))));
                query = query.Replace("{filter}", options.Filter == null ? "" : "WHERE " + CreateFilter(options.Filter.Expression));

                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Skip", skip);
                command.Parameters.AddWithValue("@RowspPage", top);

                try
                {
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        return ReadResult(reader);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    throw;
                }
            }
        }

        private PagedResult ReadResult(SqlDataReader reader)
        {
            var items = new List<Dictionary<string, object>>();

            var result = new PagedResult()
            {
                Items = items
            };

            while (reader.Read())
            {
                var dict = new Dictionary<string, object>();

                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var colName = reader.GetName(i);

                    if (colName != "ROW_NUMBER" && colName != "TOTAL_REGISTERS")
                    {
                        dict[colName] = reader[i];
                    }

                    if (colName == "TOTAL_REGISTERS")
                    {
                        result.Count = (long)(int)reader[i];
                    }
                }

                items.Add(dict);
            }

            return result;
        }

        private string ToDir(OrderByDirection direction)
        {
            return (direction == OrderByDirection.Ascending) ? "asc" : "desc";
        }

        private string CreateFilter(QueryNode filter)
        {
            if (filter is BinaryOperatorNode)
            {
                BinaryOperatorNode node = (BinaryOperatorNode)filter;
                return CreateFilter(node.Left) + GetOperator(node.OperatorKind) + CreateFilter(node.Right);
            }
            else if (filter is SingleValuePropertyAccessNode)
            {
                return ((SingleValuePropertyAccessNode)filter).PropertyName;
            }
            else if (filter is ConstantNode)
            {
                var constant = ((ConstantNode)filter);
                if (constant.EdmType == EdmType.DateTime)
                {
                    return String.Format("'{0}'", ((DateTime)constant.Value).ToString("yyyy-MM-dd HH:mm:ss"));
                }
                else return constant.LiteralText;
            }
            else if (filter is SingleValueFunctionCallNode)
            {
                var fn = ((SingleValueFunctionCallNode)filter);
                if (fn.Name == "substringof")
                {
                    return String.Format("{0} LIKE '%{1}%'", CreateFilter(fn.Parameters[1]), CreateFilter(fn.Parameters[0]).Replace("'", ""));
                }
                else if (fn.Name == "tolower")
                {
                    return String.Format("LOWER({0})", CreateFilter(fn.Parameters[0]));
                }
                else if (fn.Name == "toupper")
                {
                    return String.Format("UPPER({0})", CreateFilter(fn.Parameters[0]));
                }
                else if (fn.Name == "trim")
                {
                    return String.Format("RTRIM(LTRIM({0}))", CreateFilter(fn.Parameters[0]));
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            throw new NotImplementedException();
        }

        private string GetOperator(BinaryOperatorKind operatorKind)
        {
            switch (operatorKind)
            {
                case BinaryOperatorKind.None:
                    return String.Empty;
                case BinaryOperatorKind.Or:
                    return " OR ";
                case BinaryOperatorKind.And:
                    return " AND ";
                case BinaryOperatorKind.Equal:
                    return " = ";
                case BinaryOperatorKind.NotEqual:
                    return " <> ";
                case BinaryOperatorKind.GreaterThan:
                    return " > ";
                case BinaryOperatorKind.GreaterThanOrEqual:
                    return " >= ";
                case BinaryOperatorKind.LessThan:
                    return " < ";
                case BinaryOperatorKind.LessThanOrEqual:
                    return " <= ";
                case BinaryOperatorKind.Add:
                    return " + ";
                case BinaryOperatorKind.Subtract:
                    return " - ";
                case BinaryOperatorKind.Multiply:
                    return " * ";
                case BinaryOperatorKind.Divide:
                    return " / ";
                case BinaryOperatorKind.Modulo:
                    return " % ";
                default:
                    return String.Empty;
            }
        }

        public Uri GetNextPageLink(Uri requestUri, IEnumerable<KeyValuePair<string, string>> queryParameters, int pageSize)
        {
            StringBuilder stringBuilder = new StringBuilder();

            int num = pageSize;

            foreach (KeyValuePair<string, string> current in queryParameters)
            {
                string text = current.Key;
                string text2 = current.Value;
                string a;

                if ((a = text) != null)
                {
                    if (!(a == "$top"))
                    {
                        if (a == "$skip")
                        {
                            int num2;
                            if (int.TryParse(text2, out num2))
                            {
                                num += num2;
                                continue;
                            }
                            continue;
                        }
                    }
                }

                if (text.Length > 0 && text[0] == '$')
                {
                    text = '$' + Uri.EscapeDataString(text.Substring(1));
                }
                else
                {
                    text = Uri.EscapeDataString(text);
                }

                text2 = Uri.EscapeDataString(text2);

                stringBuilder.Append(text);
                stringBuilder.Append('=');
                stringBuilder.Append(text2);
                stringBuilder.Append('&');
            }

            stringBuilder.AppendFormat("$skip={0}", num);

            UriBuilder uriBuilder = new UriBuilder(requestUri)
            {
                Query = stringBuilder.ToString()
            };

            return uriBuilder.Uri;
        }
    }
}
