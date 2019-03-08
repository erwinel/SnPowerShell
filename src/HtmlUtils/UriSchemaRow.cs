using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Management.Automation;
using System.Xml;

namespace HtmlUtils
{
    public partial class UriParserDataSet : DataSet
    {
        public partial class UriSchemaRow : DataRow
        {
            public const string ColumnName_Value = "Value";
            public long ID { get { return (long)(this[ColumnName_ID]); } }
            public string Value { get { return (string)(this[ColumnName_Value]); } }
            private UriSchemaRow(DataRowBuilder builder) : base(builder) { }
        }
    }
}