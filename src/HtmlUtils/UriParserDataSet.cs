using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Management.Automation;
using System.Xml;

namespace HtmlUtils
{
    public partial class UriParserDataSet : DataSet
    {
        public const string TableName_UriSchema = "UriSchema";
        public const string ColumnName_ID = "ID";
        public const string PropertyName_CurrentSchemaValue = "CurrentSchemaValue";
        public const string PropertyName_SchemeIsValid = "SchemeIsValid";
        public const string PropertyName_SelectedSchemaValue = "SelectedSchemaValue";
        public const string PropertyName_SelectedSchemaId = "SelectedSchemaId";
        public const string PropertyName_IsRelative = "IsRelative";
        public const string PropertyName_SchemeErrorMessage = "SchemeErrorMessage";
        private string _currentSchemaValue = null;
        private string _schemeErrorMessage = null;
        private bool? _schemeIsValid = false;
        private bool? _isRelative = false;
        private string _selectedSchemaValue = null;
        private long? _selectedSchemaId = null;
        private UriBuilder _uriBuilder;
        private UriSchemaRow.UriSchemeTable _uriSchemes = new UriSchemaRow.UriSchemeTable();
        private T GetExtendedProperty<T>(string propertyName) { return GetExtendedProperty(propertyName, default(T)); }
        private T GetExtendedProperty<T>(string propertyName, T defaultValue)
        {
            if (ExtendedProperties.ContainsKey(propertyName))
            {
                object obj = ExtendedProperties[propertyName];
                if (obj != null && obj is T)
                    return (T)obj;
                ExtendedProperties.Remove(propertyName);
            }
            return null;
        }
        private void SetExtendedProperty<T>(string propertyName, T? value)
            where T : struct
        {
            if (value.HasValue)
            {
                if (ExtendedProperties.ContainsKey(propertyName))
                    ExtendedProperties[propertyName] = value.Value;
                else
                    ExtendedProperties.Add(propertyName, value.Value);
            }
            else if (ExtendedProperties.ContainsKey(propertyName))
                ExtendedProperties.Remove(propertyName);
        }
        private void SetExtendedProperty<T>(string propertyName, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                if (ExtendedProperties.ContainsKey(propertyName))
                    ExtendedProperties.Remove(propertyName);
            }
            else if (ExtendedProperties.ContainsKey(propertyName))
                ExtendedProperties[propertyName] = value;
            else
                ExtendedProperties.Add(propertyName, value);
        }
        public string CurrentSchemaValue
        {
            get
            {
                if (_currentSchemaValue == null && (_currentSchemaValue = GetExtendedProperty(PropertyName_CurrentSchemaValue)) == null)
                {
                    _currentSchemaValue = "";
                    _currentSchemaValue = SelectedSchemaValue;
                    SetExtendedProperty(PropertyName_CurrentSchemaValue, _currentSchemaValue);
                }
                return _currentSchemaValue;
            }
            set
            {
                bool notChanged;
                if (string.IsNullOrWhiteSpace(value))
                {
                    notChanged = CurrentSchemaValue.Length == 0;
                    _currentSchemaValue = "";
                }
                else 
                {
                    notChanged = CurrentSchemaValue == value;
                    _currentSchemaValue = value;
                }
                SetExtendedProperty(PropertyName_CurrentSchemaValue, _currentSchemaValue);
                if (notChanged)
                    return;
                string c = _currentSchemaValue.Trim();
                try
                {
                    SelectedSchemaId = (c.Length == 0) ? null : _uriSchemes.GetAllSchemes().Where(s => string.Equals(c, s.Value, StringComparison.InvariantCultureIgnoreCase))
                        .Select(s => (long?)s.ID).DefaultIfEmpty(null).First();
                }
                finally
                {
                    if (c.Length == 0)
                        SchemeErrorMessage = (IsRelative) ? "" : "Scheme not provided.";
                    else
                    {
                        IsRelative = false;
                        SchemeErrorMessage = (Uri.CheckSchemeName(_currentSchemaValue)) ? "" : "Invalid URI scheme";
                    }
                }
            }
        }
        public string SchemeErrorMessage
        {
            get
            {
                if (_schemeErrorMessage == null && (_schemeErrorMessage = GetExtendedProperty(PropertyName_SchemeErrorMessage)) == null)
                {
                    string c = CurrentSchemaValue.Trim();
                    if (c.Length == 0)
                        _schemeErrorMessage = (IsRelative) ? "" : "Scheme not provided.";
                    else
                        _schemeErrorMessage = (Uri.CheckSchemeName(CurrentSchemaValue)) ? "" : "Invalid URI scheme";
                    SetExtendedProperty(PropertyName_SchemeErrorMessage, _schemeErrorMessage);
                }
                return _schemeErrorMessage;
            }
            private set
            {
                bool notChanged;
                if (string.IsNullOrWhiteSpace(value))
                {
                    notChanged = SchemeErrorMessage.Length == 0;
                    _schemeErrorMessage = "";
                }
                else 
                {
                    string m = value.Trim();
                    notChanged = SchemeErrorMessage == m;
                    _schemeErrorMessage = m;
                }
                SetExtendedProperty(PropertyName_SchemeErrorMessage, _schemeErrorMessage);
                if (notChanged)
                    return;
                SchemeIsValid = _schemeErrorMessage.Length == 0;
            }
        }
        public bool SchemeIsValid
        {
            get
            {
                if (!_schemeIsValid.HasValue)
                    _schemeIsValid = SchemeErrorMessage.Length > 0;
                return _schemeIsValid.Value;
            }
            private set
            {
                _schemeIsValid = value;
            }
        }
        public bool IsRelative
        {
            get
            {
                if (!_isRelative.HasValue)
                    _isRelative = GetExtendedProperty(PropertyName_IsRelative, (bool?)false);
                return _isRelative.Value;
            }
            private set
            {
                bool notChanged = value == IsRelative;
                _isRelative = value;
                if (notChanged)
                    return;
                
                string c = CurrentSchemaValue.Trim();
                if (c.Length == 0)
                    SchemeErrorMessage = (value) ? "" : "Scheme not provided.";
                else
                    SchemeErrorMessage = (Uri.CheckSchemeName(CurrentSchemaValue)) ? "" : "Invalid URI scheme";
            }
        }
        public string SelectedSchemaValue
        {
            get
            {
                if (_selectedSchemaValue == null)
                {
                    string current = CurrentSchemaValue.Trim();
                    IEnumerable<UriSchemaRow> rows = _uriSchemes.GetAllSchemes().Where(s => string.Equals(s.Value, current, StringComparison.InvariantCultureIgnoreCase));
                    if (rows.Any())
                    {
                        UriSchemaRow r = rows.First();
                        _selectedSchemaValue = r.Value;
                        _selectedSchemaId = r.ID;
                    }
                    else
                        _selectedSchemaValue = "";
                }
                return _selectedSchemaValue;
            }
            set
            {
                string v = (value == null) ? "" : value.Trim();
                IEnumerable<UriSchemaRow> rows = _uriSchemes.GetAllSchemes().Where(s => string.Equals(s.Value, v, StringComparison.InvariantCultureIgnoreCase));
                bool valueChanged, idChanged;
                if (rows.Any())
                {
                    UriSchemaRow r = rows.First();
                    valueChanged = _selectedSchemaValue != null && _selectedSchemaValue != r.Value;
                    idChanged = _selectedSchemaId.HasValue && _selectedSchemaId.Value != r.ID;
                    _selectedSchemaValue = r.Value;
                    _selectedSchemaId = r.ID;
                }
                else
                {
                    valueChanged = !string.IsNullOrEmpty(_selectedSchemaValue);
                    idChanged = _selectedSchemaId.HasValue;
                    _selectedSchemaId = null;
                    _selectedSchemaValue = null;
                }
                if (valueChanged)
                    RaisePropertyChanged(PropertyName_SelectedSchemaValue);
                if (idChanged)
                    RaisePropertyChanged(PropertyName_SelectedSchemaId);
            }
        }
        public long? SelectedSchemaId
        {
            get
            {
                if (_selectedSchemaValue == null)
                {
                    string current = CurrentSchemaValue.Trim();
                    IEnumerable<UriSchemaRow> rows = _uriSchemes.GetAllSchemes().Where(s => string.Equals(s.Value, current, StringComparison.InvariantCultureIgnoreCase));
                    if (rows.Any())
                    {
                        UriSchemaRow r = rows.First();
                        _selectedSchemaValue = r.Value;
                        _selectedSchemaId = r.ID;
                    }
                    else
                        _selectedSchemaValue = "";
                }
                return _selectedSchemaId;
            }
            set
            {
                bool valueChanged, idChanged;
                if (value.HasValue)
                {
                    IEnumerable<UriSchemaRow> rows = _uriSchemes.GetAllSchemes().Where(s => s.ID == value.Value);
                    if (rows.Any())
                    {
                        UriSchemaRow r = rows.First();
                        valueChanged = _selectedSchemaValue != null && _selectedSchemaValue != r.Value;
                        idChanged = _selectedSchemaId.HasValue && _selectedSchemaId.Value != value.Value;
                        _selectedSchemaValue = r.Value;
                        _selectedSchemaId = value.Value;
                    }
                    else
                    {
                        valueChanged = !string.IsNullOrEmpty(_selectedSchemaValue);
                        idChanged = _selectedSchemaId.HasValue;
                        _selectedSchemaId = null;
                        _selectedSchemaValue = null;
                    }
                }
                else
                {
                    valueChanged = !string.IsNullOrEmpty(_selectedSchemaValue);
                    idChanged = _selectedSchemaId.HasValue;
                    _selectedSchemaId = null;
                    _selectedSchemaValue = null;
                }
                if (valueChanged)
                    RaisePropertyChanged(PropertyName_SelectedSchemaValue);
                if (idChanged)
                    RaisePropertyChanged(PropertyName_SelectedSchemaId);
            }
        }
        public UriSchemaRow.UriSchemeTable UriSchemes { get { return _uriSchemes; } }
        public UriParserDataSet() : this(null, null) { }
        public UriParserDataSet(Uri uri) : this(null, uri) { }
        public UriParserDataSet(UriBuilder builder) : this(builder, null) { }
        public UriParserDataSet(UriBuilder builder, Uri uri) : base("UriParser")
        {
            if (uri == null)
                _uriBuilder = (builder == null)  ? new UriBuilder() : builder;
            else if (uri.IsAbsoluteUri)
                _uriBuilder = new UriBuilder(uri);
            else
                _uriBuilder = new UriBuilder(new Uri(((builder == null)  ? new UriBuilder() : builder).Uri, uri));
            Tables.Add(_uriSchemes);
            EnforceConstraints = true;
            CurrentSchemaValue = _uriBuilder.Scheme;
        }
        private void RaisePropertyChanged(string propertyName)
        {
            string value;
            UriSchemaRow row;
            switch (propertyName)
            {
                case PropertyName_CurrentSchemaValue:
                    value = CurrentSchemaValue;
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        if (IsRelative)
                            SchemeErrorMessage = null;
                        else
                            SchemeErrorMessage = "Scheme not specified.";
                        SelectedSchemaId = null;
                    }
                    else
                    {
                        IsRelative = false;
                        if ((row = _uriSchemes.GetAllSchemes().FirstOrDefault(r => string.Equals(r.Value, value, StringComparison.InvariantCultureIgnoreCase))) == null)
                        {
                            if (Uri.CheckSchemeName(value))
                                SchemeErrorMessage = null;
                            else
                                SchemeErrorMessage = "Invalid scheme name.";
                            SelectedSchemaId = null;
                        }
                        else
                        {
                            SchemeErrorMessage = null;
                            SelectedSchemaId = row.ID;
                        }
                    }
                    break;
                case PropertyName_SchemeErrorMessage:
                    value = SchemeErrorMessage;
                    SchemeIsValid = string.IsNullOrWhiteSpace(SchemeErrorMessage);
                    break;
                case PropertyName_IsRelative:
                    value = CurrentSchemaValue;
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        if (IsRelative)
                            SchemeErrorMessage = null;
                        else
                            SchemeErrorMessage = "Scheme not specified.";
                    }
                    else if (Uri.CheckSchemeName(value) || (row = _uriSchemes.GetAllSchemes().FirstOrDefault(r => string.Equals(r.Value, value, StringComparison.InvariantCultureIgnoreCase))) != null)
                        SchemeErrorMessage = null;
                    else
                        SchemeErrorMessage = "Invalid scheme name.";
                    break;
                case PropertyName_SelectedSchemaValue:
                    value = SelectedSchemaValue;
                    if (value == null || (row = _uriSchemes.GetAllSchemes().FirstOrDefault(r => string.Equals(r.Value, value, StringComparison.InvariantCultureIgnoreCase))) == null)
                        SelectedSchemaId = null;
                    else
                        SelectedSchemaId = row.ID;
                    break;
                case PropertyName_SelectedSchemaId:
                    long? id = SelectedSchemaId;
                    if (id.HasValue)
                    {
                        if ((row = _uriSchemes.GetAllSchemes().FirstOrDefault(r => r.ID == id.Value)) == null)
                            SelectedSchemaId = null;
                        else
                            SelectedSchemaValue = CurrentSchemaValue = row.Value;
                    }
                    else
                    {
                        string x = CurrentSchemaValue;
                        if (x != null)
                        {
                            string y = SelectedSchemaValue;
                            if (y != null && string.Equals(x, y, StringComparison.InvariantCultureIgnoreCase))
                                CurrentSchemaValue = null;
                        }
                        SelectedSchemaValue = null;
                    }
                    break;
            }
        }
        protected override void OnRemoveTable(DataTable table)
        {
            if (ReferenceEquals(table, _uriSchemes))
                throw new NotSupportedException("URI schemes table cannot be removed.");
            base.OnRemoveTable(table);
        }
    }
}