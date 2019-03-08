using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            public class UriSchemeTable : DataTable
            {
                private DataColumn _idDataColumn = new DataColumn(ColumnName_ID);
                private DataColumn _valueDataColumn = new DataColumn(ColumnName_Value);
                private ReadOnlyCollection<DataRow> _knownSchemes;
                public DataColumn IdDataColumn { get { return _idDataColumn; } }
                public DataColumn ValueDataColumn { get { return _valueDataColumn; } }
                public ReadOnlyCollection<DataRow> KnownSchemes { get { return _knownSchemes; } }
                public UriSchemeTable() : base(TableName_UriSchema)
                {
                    _idDataColumn.Caption = "ID";
                    _idDataColumn.DataType = typeof(long);
                    _idDataColumn.AutoIncrement = _idDataColumn.ReadOnly = _idDataColumn.Unique = true;
                    _idDataColumn.ColumnMapping = MappingType.Attribute;
                    Columns.Add(_idDataColumn);
                    _valueDataColumn.Caption = "Scheme";
                    _valueDataColumn.DataType = typeof(string);
                    _idDataColumn.Unique = _valueDataColumn.ReadOnly = true;
                    _valueDataColumn.ColumnMapping = MappingType.SimpleContent;
                    Columns.Add(_valueDataColumn);
                    PrimaryKey = new DataColumn[] { _idDataColumn };
                    _knownSchemes = new ReadOnlyCollection<DataRow>((new string[]
                    {
                        Uri.UriSchemeHttp,
                        Uri.UriSchemeHttps,
                        Uri.UriSchemeFile,
                        Uri.UriSchemeNetPipe,
                        Uri.UriSchemeNetTcp,
                        Uri.UriSchemeFtp,
                        Uri.UriSchemeNntp,
                        Uri.UriSchemeMailto,
                        Uri.UriSchemeNews,
                        Uri.UriSchemeGopher
                    }).Select(scheme =>
                    {
                        UriSchemaRow row = (UriSchemaRow)NewRow();
                        row[_valueDataColumn] = scheme;
                        Rows.Add(row);
                        row.AcceptChanges();
                        return row;
                    }).ToArray());
                    AcceptChanges();
                    /*Boolean AllowDBNull
Boolean AutoIncrement
Int64 AutoIncrementSeed
Int64 AutoIncrementStep
System.String Caption
System.String ColumnName
System.String Prefix
System.Type DataType
System.Data.DataSetDateTime DateTimeMode
System.Object DefaultValue
System.String Expression
System.Data.PropertyCollection ExtendedProperties
Int32 MaxLength
System.String Namespace
Int32 Ordinal
Boolean ReadOnly
System.Data.DataTable Table
Boolean Unique
System.Data.MappingType ColumnMapping
System.ComponentModel.ISite Site
System.ComponentModel.IContainer Container
Boolean DesignMode */
                }
                protected override DataRow NewRowFromBuilder(DataRowBuilder builder) { return new UriSchemaRow(builder); }
                protected override void OnRowDeleting(DataRowChangeEventArgs e)
                {
                    if (_knownSchemes.Any(r => ReferenceEquals(e.Row, r)))
                        throw new NotSupportedException("Known schema rows cannot be removed");
                    base.OnRowDeleting(e);
                }
                protected override void OnRemoveColumn(DataColumn column)
                {
                    if (ReferenceEquals(column, _idDataColumn) || ReferenceEquals(column, _valueDataColumn))
                        throw new NotSupportedException("Built-in columns cannot be removed");
                    base.OnRemoveColumn(column);
                }
                protected override void OnTableClearing(DataTableClearEventArgs e)
                {
                        throw new NotSupportedException("Known schema rows cannot be removed");
                }
                protected override void OnColumnChanging(DataColumnChangeEventArgs e)
                {
                    // if (ReferenceEquals(e.Column, _idDataColumn) || (ReferenceEquals(e.Column, _valueDataColumn) && _valueDataColumn.ReadOnly))
                    //     throw new NotSupportedException("Column is read-only");
                    base.OnColumnChanging(e);
                }
                public IEnumerable<UriSchemaRow> GetAllSchemes() { return Rows.OfType<UriSchemaRow>(); }
            }
        }
    }
}