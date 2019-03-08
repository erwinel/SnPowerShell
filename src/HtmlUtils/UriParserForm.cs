using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Management.Automation;
using System.Windows.Forms;
using System.Xml;

namespace HtmlUtils
{
    public class UriParserForm : Form
    {
        private UriParserDataSet _uriData;

        public UriParserDataSet UriData { get { return _uriData; } }
        public UriParserForm() : this((UriParserDataSet)null) { }
        public UriParserForm(UriParserDataSet uriData)
        {
            _uriData = (uriData == null) ? new UriParserDataSet() : uriData;
        }
        public UriParserForm(Uri uri)
        {
            _uriData = new UriParserDataSet(uri);
        }
        public UriParserForm(UriBuilder builder)
        {
            _uriData = new UriParserDataSet(builder);
        }
    }
}