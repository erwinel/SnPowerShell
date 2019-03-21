using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Management.Automation;
using System.Xml;
using System.Collections.ObjectModel;

namespace SnWebServices
{
    public class SnRestTableAPI : SnRestAPIBase
    {
        public const string BaseURL = "/now/table/";

        public class GetRecordsURIParams : GetUriParamsBase
        {
            /// <summary>
            /// /api/now/table/{tableName}
            /// </summary>
            public string TableName { get; }

            /// <summary>
            /// /api/now/table/{tableName}?sysparm_display_value={true|false}
            /// </summary>
            public bool? DisplayValue { get; set; }

            /// <summary>
            /// /api/now/table/{tableName}?sysparm_exclude_reference_link={true|false}
            /// </summary>
            public bool? ExcludeReferenceLink { get; set; }

            /// <summary>
            /// /api/now/table/{tableName}?sysparm_limit={number}
            /// </summary>
            public int? Limit { get; set; }

            /// <summary>
            /// /api/now/table/{tableName}?sysparm_offset={number}
            /// </summary>
            public int? Offset { get; set; }

            /// <summary>
            /// /api/now/table/{tableName}?sysparm_query={encoded_data}
            /// </summary>
            public SnQuery Query { get; set; }

            /// <summary>
            /// /api/now/table/{tableName}?sysparm_fields={comma_separated}
            /// </summary>
            public Collection<string> Fields { get; set; }

            /// <summary>
            /// /api/now/table/{tableName}?sysparm_suppress_pagination_header={true|false}
            /// </summary>
            public bool? SuppressPaginationHeader { get; set; }

            /// <summary>
            /// /api/now/table/{tableName}?sysparm_view={encoded_string}
            /// </summary>
            public string View {get; set; }

            public GetRecordsURIParams(string tableName, params string[] fields)
            {
                throw new NotImplementedException();
            }
        }
        
        public class GetRecordByIdURIParams : GetUriParamsBase
        {
            /// <summary>
            /// /api/now/table/{tableName}/{sys_id}
            /// </summary>
            public string TableName { get; }

            /// <summary>
            /// /api/now/table/{tableName}/{sys_id}
            /// </summary>
            public string SysId { get; }

            /// <summary>
            /// /api/now/table/{tableName}/{sys_id}?sysparm_display_value={true|false}
            /// </summary>
            public bool? DisplayValue { get; set; }

            /// <summary>
            /// /api/now/table/{tableName}/{sys_id}?sysparm_exclude_reference_link={true|false}
            /// </summary>
            public bool? ExcludeReferenceLink { get; set; }

            /// <summary>
            /// /api/now/table/{tableName}/{sys_id}?sysparm_fields={comma_separated}
            /// </summary>
            public Collection<string> ResponseFields { get; set; }

            /// <summary>
            /// /api/now/table/{tableName}/{sys_id}?sysparm_view={encoded_string}
            /// </summary>
            public string View {get; set; }

            public GetRecordByIdURIParams(string tableName, string sys_id, params string[] responseFields)
            {
                throw new NotImplementedException();
            }
        }
        
        public class InsertRecordURIParams : PostUriParamsBase
        {
            /// <summary>
            /// /api/now/table/{tableName}
            /// </summary>
            public string TableName { get; }

            /// <summary>
            /// /api/now/table/{tableName}?sysparm_display_value={true|false}
            /// </summary>
            public bool? DisplayValue { get; set; }

            /// <summary>
            /// /api/now/table/{tableName}?sysparm_exclude_reference_link={true|false}
            /// </summary>
            public bool? ExcludeReferenceLink { get; set; }

            /// <summary>
            /// /api/now/table/{tableName}?sysparm_fields={comma_separated}
            /// </summary>
            public Collection<string> ResponseFields { get; set; }

            /// <summary>
            /// /api/now/table/{tableName}?sysparm_input_display_value={true|false}
            /// </summary>
            public bool? InputDisplayValue { get; set; }

            /// <summary>
            /// /api/now/table/{tableName}?sysparm_view={encoded_string}
            /// </summary>
            public string View { get; set; }

            public InsertRecordURIParams(string tableName, IDictionary<string, object> values, params string[] responseFields)
            {
                throw new NotImplementedException();
            }
        }
        
        public class SaveRecordURIParams : PutUriParamsBase
        {
            public string TableName { get; }

            public string SysId { get; }

            public SaveRecordURIParams(string tableName, string sys_id, IDictionary<string, object> values, params string[] responseFields)
            {
                throw new NotImplementedException();
            }
        }
        
        public class UpdateFieldsURIParams : PatchUriParamsBase
        {
            public string TableName { get; }

            public string SysId { get; }

            public UpdateFieldsURIParams(string tableName, string sys_id, IDictionary<string, object> values, params string[] responseFields)
            {
                throw new NotImplementedException();
            }
        }
        
        public class DeleteRecordURIParams : DeleteUriParamsBase
        {
            public string TableName { get; }

            public string SysId { get; }

            public DeleteRecordURIParams(string tableName, string sys_id)
            {
                throw new NotImplementedException();
            }
        }
    }

    public class SnQuery
    {

    }
}