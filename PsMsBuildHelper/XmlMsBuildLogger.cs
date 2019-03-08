using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace PsMsBuildHelper
{
    public class XmlMsBuildLogger : Logger, INodeLogger
    {
        public static object BuildProject(string projectPath, string outputPath, string configuration = null, string platform = null, string[] targets = null)
        {
            if (projectPath == null)
                throw new ArgumentNullException("projectPath");
            if (outputPath == null)
                throw new ArgumentNullException("outputPath");
            if (projectPath.Trim().Length == 0 || !File.Exists(projectPath))
                throw new FileNotFoundException("Project file not found", projectPath);
            if (outputPath.Trim().Length == 0 || !Directory.Exists(Path.GetDirectoryName(outputPath)))
                throw new DirectoryNotFoundException("Output Directory not found");
            try {
                XmlMsBuildLogger logger = new XmlMsBuildLogger();
                logger.Parameters = outputPath;
                Project project;
                if (ProjectCollection.GlobalProjectCollection.Count > 0)
                    ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
                project = ProjectCollection.GlobalProjectCollection.LoadProject(projectPath);
                if (configuration != null && (configuration = configuration.Trim()).Length > 0)
                    project.SetProperty("Configuration", configuration);
                if (configuration != null && (platform = platform.Trim()).Length > 0)
                    project.SetProperty("Platform", platform);
                return (targets.Length == 0) ? project.Build(logger) : project.Build(targets, new ILogger[] { logger });
            } catch (Exception exception) {
                return exception;
            }
        }
        public const string RoundTrimDateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffzzzzzz";
        private XmlWriter _writer = null;
        private object _syncRoot = new object();
        private IEventSource _eventSource = null;
        private int _cpuCount = -1;
        private string _outputPath = "";
        private StopwatchDictionary _stopwatches = null;
        static class ParameterParseHelper
        {
            public static readonly Regex _boolRegex = new Regex(@"^((?<false>f(alse)?|no?|-|[+\-]?0+(\.0+)?))|(?<true>)t(rue)?|y(es)?|\+|[+\-]?(0*[1-9]\d*(\.\d+)?|0+\.0*[1-9]\d*))$", RegexOptions.IgnoreCase);
            internal static bool ParseBoolean(string parameterName, string text)
            {
                Match m = _boolRegex.Match(text.Trim());
                if (!m.Success)
                    throw new Exception("'" + text + "' is not recognized yes/no value for the '" + parameterName + "' parameter.");
                return m.Groups["true"].Success;
            }
            internal static TEnum ParseEnum<TEnum>(string parameterName, string text)
                where TEnum : struct
            {
                TEnum result;
                if (!Enum.TryParse<TEnum>(text.Trim(), out result))
                    throw new Exception("'" + text + "' is not recognized option for the '" + parameterName + "' parameter.");
                return result;
            }
        }
        
        private void _Initialize(IEventSource eventSource, Int32 nodeCount)
        {
            if (eventSource == null)
                throw new ArgumentNullException("eventSource");
            Monitor.Enter(_syncRoot);
            try
            {
                if (_eventSource != null)
                {
                    if (!ReferenceEquals(_eventSource, eventSource))
                        throw new ArgumentException("Only one event source can be logged at a time", "eventSource");
                    _cpuCount = nodeCount;
                    return;
                }
                XmlWriterSettings settings = new XmlWriterSettings
                {
                    Async = true,
                    CheckCharacters = false,
                    Indent = true,
                    WriteEndDocumentOnClose = true
                };
                
                bool emitIdentifier = false;
                bool bigEndian = false;
                bool allowOpt = false;
                foreach (var g in ((Parameters == null) ? "" : Parameters.Trim()).Split(Path.PathSeparator).Select(p =>
                {
                    string[] kvp = p.Split(new char[] { '=' }, 2);
                    if (kvp.Length == 1)
                    {
                        if ((kvp[0]= kvp[0].Trim()).Length > 0)
                            return new { Key = "Path", Value = kvp[0] };
                        return new { Key = "Path", Value = null as string };
                    }
                    return new { Key = kvp[0].Trim(), Value = kvp[1].Trim() };
                }).GroupBy(kvp => kvp.Key, StringComparer.InvariantCultureIgnoreCase))
                {
                    if (g.Count() > 1)
                        throw new Exception("Parameter '" + g.Key + "' cannot be defined more than once.");
                    string text = g.First().Value;
                    if (text == null)
                        continue;
                    switch (g.Key.ToLower())
                    {
                        case "path":
                            if (text.Length == 0)
                                throw new Exception("Parameter '" + g.Key + "' cannot be a zero-length string.");
                            _outputPath = text.Trim();
                            break;
                        case "writeenddocumentonclose":
                            settings.WriteEndDocumentOnClose = ParameterParseHelper.ParseBoolean(g.Key, text);
                            break;
                        case "namespacehandling":
                            settings.NamespaceHandling = ParameterParseHelper.ParseEnum<NamespaceHandling>(g.Key, text);
                            break;
                        case "checkcharacters":
                            settings.CheckCharacters = ParameterParseHelper.ParseBoolean(g.Key, text);
                            break;
                        case "conformancelevel":
                            settings.ConformanceLevel = ParameterParseHelper.ParseEnum<ConformanceLevel>(g.Key, text);
                            break;
                        case "newlineonattributes":
                            settings.NewLineOnAttributes = ParameterParseHelper.ParseBoolean(g.Key, text);
                            break;
                        case "indentchars":
                            if (text.Length == 0)
                                throw new Exception("Parameter '" + g.Key + "' cannot be a zero-length string.");
                            settings.IndentChars = text;
                            break;
                        case "indent":
                            settings.Indent = ParameterParseHelper.ParseBoolean(g.Key, text);
                            break;
                        case "newlinehandling":
                            settings.NewLineHandling = ParameterParseHelper.ParseEnum<NewLineHandling>(g.Key, text);
                            break;
                        case "omitxmldeclaration":
                            settings.OmitXmlDeclaration = ParameterParseHelper.ParseBoolean(g.Key, text);
                            break;
                        case "donotescapeuriattributes":
                            settings.DoNotEscapeUriAttributes = ParameterParseHelper.ParseBoolean(g.Key, text);
                            break;
                        case "emitidentifier":
                            emitIdentifier = ParameterParseHelper.ParseBoolean(g.Key, text);
                            break;
                        case "bigendian":
                            bigEndian = ParameterParseHelper.ParseBoolean(g.Key, text);
                            break;
                        case "allowopt":
                            allowOpt = ParameterParseHelper.ParseBoolean(g.Key, text);
                            break;
                        case "encoding":
                            if ((text = text.Trim()).Length == 0)
                                throw new Exception("Parameter '" + g.Key + "' cannot be a zero-length string.");
                            Encoding encoding;
                            try { encoding = Encoding.GetEncoding(text.Trim()); }
                            catch { encoding = null; }
                            if (encoding == null)
                                throw new Exception("'" + text + "' is not recognized yes/no value for the '" + g.Key + "' parameter.");
                            break;
                    }
                }
                if (settings.Encoding is UTF8Encoding)
                    settings.Encoding = new UTF8Encoding(emitIdentifier);
                else if (settings.Encoding is UnicodeEncoding)
                    settings.Encoding = new UnicodeEncoding(bigEndian, emitIdentifier);
                else if (settings.Encoding is UTF32Encoding)
                    settings.Encoding = new UTF32Encoding(bigEndian, emitIdentifier);
                else if (settings.Encoding is UTF7Encoding)
                    settings.Encoding = new UTF7Encoding(allowOpt);
                else
                if (String.IsNullOrEmpty(_outputPath))
                    throw new Exception("Path parameter not provided");
                _writer = XmlWriter.Create(_outputPath, settings);
                _writer.WriteStartElement("BuildResult");
                _writer.WriteAttributeString("Started", XmlConvert.ToString(DateTime.Now, RoundTrimDateTimeFormat));
                _writer.WriteAttributeString("CpuCount", XmlConvert.ToString(nodeCount));
                _eventSource = eventSource;
                _cpuCount = nodeCount;
                _stopwatches = new StopwatchDictionary();
                eventSource.MessageRaised += OnMessageRaised;
                eventSource.ErrorRaised += OnErrorRaised;
                eventSource.WarningRaised += OnWarningRaised;
                eventSource.BuildStarted += OnBuildStarted;
                eventSource.BuildFinished += OnBuildFinished;
                eventSource.ProjectStarted += OnProjectStarted;
                eventSource.ProjectFinished += OnProjectFinished;
                eventSource.TargetStarted += OnTargetStarted;
                eventSource.TargetFinished += OnTargetFinished;
                eventSource.TaskStarted += OnTaskStarted;
                eventSource.TaskFinished += OnTaskFinished;
                eventSource.CustomEventRaised += OnCustomEventRaised;
            }
            catch (Exception exception)
            {
                throw new LoggerException((String.IsNullOrEmpty(exception.Message)) ? "Unexpected " + exception.GetType().Name : exception.Message, exception);
            }
            finally { Monitor.Exit(_syncRoot); }
        }

        public void Initialize(IEventSource eventSource, Int32 nodeCount)
        {
            _Initialize(eventSource, nodeCount);
        }
        public override void Initialize(IEventSource eventSource)
        {
            _Initialize(eventSource, -1);
        }
        public override void Shutdown()
        {
            Monitor.Enter(_syncRoot);
            try
            {
                if (_eventSource != null)
                {
                    if (_writer != null)
                    {
                        try
                        {
                            _writer.WriteStartElement("Completed");
                            try
                            {
                                _writer.WriteAttributeString("TotalDuration", XmlConvert.ToString(_stopwatches.Stop()));
                            }
                            finally { _writer.WriteEndElement(); }
                        }
                        finally
                        {
                            try
                            {
                                _writer.WriteEndElement();
                                _writer.Flush();
                            }
                            finally
                            {
                                try { _writer.Close(); }
                                finally { _writer = null; }
                            }
                        }
                    }
                    _eventSource.MessageRaised -= OnMessageRaised;
                    _eventSource.ErrorRaised -= OnErrorRaised;
                    _eventSource.WarningRaised -= OnWarningRaised;
                    _eventSource.BuildStarted -= OnBuildStarted;
                    _eventSource.BuildFinished -= OnBuildFinished;
                    _eventSource.ProjectStarted -= OnProjectStarted;
                    _eventSource.ProjectFinished -= OnProjectFinished;
                    _eventSource.TargetStarted -= OnTargetStarted;
                    _eventSource.TargetFinished -= OnTargetFinished;
                    _eventSource.TaskStarted -= OnTaskStarted;
                    _eventSource.TaskFinished -= OnTaskFinished;
                    _eventSource.CustomEventRaised -= OnCustomEventRaised;
                }
            }
            finally
            {
                _eventSource = null;
                Monitor.Exit(_syncRoot);
            }
        }
        public static readonly Regex RequiresCDataRegex = new Regex(@"^(\s+|[^\p{C}<>&""]*([\r\n\t]+[^\p{C}<>&""]+)*[^\p{C}<>&""]|\S+(\s+\S+)*\s)", RegexOptions.Compiled);
        private void WriteBuildEventContext(BuildEventContext context, string elementName = "Context")
        {
            if (context == null)
                return;
            _writer.WriteStartElement(elementName);
            try
            {
                _writer.WriteAttributeString("BuildRequest", XmlConvert.ToString(context.BuildRequestId));
                if (context.SubmissionId != BuildEventContext.InvalidSubmissionId)
                    _writer.WriteAttributeString("Submission", XmlConvert.ToString(context.SubmissionId));
                if (context.ProjectContextId != BuildEventContext.InvalidProjectContextId)
                    _writer.WriteAttributeString("ProjectContext", XmlConvert.ToString(context.ProjectContextId));
                if (context.ProjectInstanceId != BuildEventContext.InvalidProjectInstanceId)
                    _writer.WriteAttributeString("ProjectInstance", XmlConvert.ToString(context.ProjectInstanceId));
                if (context.TargetId != BuildEventContext.InvalidTargetId)
                    _writer.WriteAttributeString("Target", XmlConvert.ToString(context.TargetId));
                if (context.TaskId != BuildEventContext.InvalidTaskId)
                    _writer.WriteAttributeString("Task", XmlConvert.ToString(context.TaskId));
            }
            finally { _writer.WriteEndElement(); }
        }
        private void WriteEventArgsProperties(BuildEventArgs e, bool doNotWriteBuildEventContext = false)
        {
            _writer.WriteAttributeString("ThreadId", XmlConvert.ToString(e.ThreadId));
            _writer.WriteAttributeString("Timestamp", XmlConvert.ToString(e.Timestamp, RoundTrimDateTimeFormat));
            if (e.HelpKeyword != null)
                _writer.WriteAttributeString("HelpKeyword", e.HelpKeyword);
            if (e.HelpKeyword != null)
                _writer.WriteAttributeString("SenderName", e.SenderName);
            if (e.Message != null)
            {
                _writer.WriteStartElement("Message");
                try
                {
                    if (e.Message.Length > 0 && RequiresCDataRegex.IsMatch(e.Message))
                        _writer.WriteCData(e.Message);
                    else
                        _writer.WriteString(e.Message);
                }
                finally { _writer.WriteEndElement(); }
            }
            if (!doNotWriteBuildEventContext)
                WriteBuildEventContext(e.BuildEventContext);
        }
        private void OnBuildStarted(object sender, BuildStartedEventArgs e)
        {
            Monitor.Enter(_syncRoot);
            try
            {
                _stopwatches.StartNewBuild(e.BuildEventContext);
                _writer.WriteStartElement("BuildStarted");
                try
                {
                    
                    WriteEventArgsProperties(e);
                    if (e.BuildEnvironment != null && e.BuildEnvironment.Count > 0)
                    {
                        _writer.WriteStartElement("Environment");
                        try
                        {
                            foreach (string key in e.BuildEnvironment.Keys)
                            {
                                _writer.WriteStartElement("Var");
                                try
                                {
                                    _writer.WriteAttributeString("Name", key);
                                    string value = e.BuildEnvironment[key];
                                    if (e.BuildEnvironment[key] != null)
                                    {
                                        if (value.Length > 0 && RequiresCDataRegex.IsMatch(value))
                                            _writer.WriteCData(value);
                                        else
                                            _writer.WriteString(value);
                                    }
                                }
                                finally { _writer.WriteEndElement(); }
                            }
                        }
                        finally { _writer.WriteEndElement(); }
                    }
                }
                finally { _writer.WriteEndElement(); }
            }
            finally { Monitor.Exit(_syncRoot); }
        }
        private void OnBuildFinished(object sender, BuildFinishedEventArgs e)
        {
            Monitor.Enter(_syncRoot);
            try
            {
                _writer.WriteStartElement("BuildFinished");
                try
                {
                    _writer.WriteAttributeString("Succeeded", XmlConvert.ToString(e.Succeeded));
                    _writer.WriteAttributeString("Duration", XmlConvert.ToString(_stopwatches.StopBuild(e.BuildEventContext)));
                    WriteEventArgsProperties(e);
                }
                finally { _writer.WriteEndElement(); }
            }
            finally { Monitor.Exit(_syncRoot); }
        }

        private string ObjectToXmlString(object value, out string typeName)
        {
            if (value == null || value is DBNull)
            {
                typeName = "null";
                return "";
            }
            if (value is string)
            {
                typeName = null;
                return (string)value;
            }
            if (value is int)
            {
                typeName = null;
                return XmlConvert.ToString((int)value);
            }
            if (value is TimeSpan)
            {
                typeName = "duration";
                return XmlConvert.ToString((TimeSpan)value);
            }
            if (value is double)
            {
                typeName = "double";
                return XmlConvert.ToString((double)value);
            }
            if (value is decimal)
            {
                typeName = "decimal";
                return XmlConvert.ToString((decimal)value);
            }
            if (value is bool)
            {
                typeName = "bool";
                return XmlConvert.ToString((bool)value);
            }
            if (value is sbyte)
            {
                typeName = "sbyte";
                return XmlConvert.ToString((sbyte)value);
            }
            if (value is short)
            {
                typeName = "short";
                return XmlConvert.ToString((short)value);
            }
            if (value is char)
            {
                typeName = "char";
                return XmlConvert.ToString((char)value);
            }
            if (value is byte)
            {
                typeName = "byte";
                return XmlConvert.ToString((byte)value);
            }
            if (value is ushort)
            {
                typeName = "ushort";
                return XmlConvert.ToString((ushort)value);
            }
            if (value is uint)
            {
                typeName = "uint";
                return XmlConvert.ToString((uint)value);
            }
            if (value is ulong)
            {
                typeName = "ulong";
                return XmlConvert.ToString((ulong)value);
            }
            if (value is float)
            {
                typeName = "float";
                return XmlConvert.ToString((float)value);
            }
            if (value is long)
            {
                typeName = "long";
                return XmlConvert.ToString((long)value);
            }
            Type t = value.GetType();
            typeName = t.GetType().Name;
            if (t.IsEnum)
                return Enum.GetName(t, value);
            if (value is DateTime)
                return XmlConvert.ToString((DateTime)value, RoundTrimDateTimeFormat);
            if (value is Guid)
                return XmlConvert.ToString((Guid)value);
            if (value is DateTimeOffset)
                return XmlConvert.ToString((DateTimeOffset)value);
            if (value is IConvertible)
            {
                IConvertible convertible = (IConvertible)value;
                try
                {
                    object obj;
                    IFormatProvider fmt = System.Globalization.CultureInfo.CurrentCulture;
                    switch (convertible.GetTypeCode())
                    {
                        case TypeCode.Boolean:
                            if ((obj = convertible.ToBoolean(fmt)) != null && obj is bool)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                        case TypeCode.Byte:
                            if ((obj = convertible.ToByte(fmt)) != null && obj is byte)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                        case TypeCode.Char:
                            if ((obj = convertible.ToChar(fmt)) != null && obj is char)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                        case TypeCode.DateTime:
                            if ((obj = convertible.ToDateTime(fmt)) != null && obj is DateTime)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                        case TypeCode.DBNull:
                                return ObjectToXmlString(null, out typeName);
                        case TypeCode.Decimal:
                            if ((obj = convertible.ToDecimal(fmt)) != null && obj is decimal)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                        case TypeCode.Double:
                            if ((obj = convertible.ToDouble(fmt)) != null && obj is double)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                        case TypeCode.Int16:
                            if ((obj = convertible.ToInt16(fmt)) != null && obj is short)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                        case TypeCode.Int32:
                            if ((obj = convertible.ToInt32(fmt)) != null && obj is int)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                        case TypeCode.Int64:
                            if ((obj = convertible.ToInt64(fmt)) != null && obj is long)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                        case TypeCode.SByte:
                            if ((obj = convertible.ToSByte(fmt)) != null && obj is bool)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                        case TypeCode.Single:
                            if ((obj = convertible.ToSingle(fmt)) != null && obj is float)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                        case TypeCode.String:
                            if ((obj = convertible.ToString(fmt)) != null && obj is string)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                        case TypeCode.UInt16:
                            if ((obj = convertible.ToUInt16(fmt)) != null && obj is ushort)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                        case TypeCode.UInt32:
                            if ((obj = convertible.ToUInt32(fmt)) != null && obj is uint)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                        case TypeCode.UInt64:
                            if ((obj = convertible.ToUInt64(fmt)) != null && obj is ulong)
                                return ObjectToXmlString(obj, out typeName);
                            break;
                    }
                }
                catch { }
            }

            return value.ToString();
        }

        private void WriteObjectElement(object value, string elementName, int maxDepth = 16)
        {
            _writer.WriteStartElement(elementName);
            try
            {
                if (value != null && value is DictionaryEntry)
                    WriteDictionaryEntry((DictionaryEntry)value, elementName, maxDepth);
                else
                    WriteObjectElementValue(value, maxDepth);
            }
            finally { _writer.WriteEndElement(); }
        }
        private static readonly string _keyValuePairName = (typeof(System.Collections.Generic.KeyValuePair<,>)).AssemblyQualifiedName;
        private static readonly string _dictionaryName = (typeof(System.Collections.Generic.IDictionary<,>)).AssemblyQualifiedName;
        private void WriteObjectElementValue(object value, int maxDepth = 16)
        {
            if (value == null || value is DBNull)
                _writer.WriteAttributeString("Type", "null");
            else if (value is string)
            {
                string s = (string)value;
                if (RequiresCDataRegex.IsMatch(s))
                    _writer.WriteCData(s);
                else
                    _writer.WriteString(s);

            }
            else if (value is int)
            {
                _writer.WriteAttributeString("Type", "int");
                _writer.WriteString(XmlConvert.ToString((int)value));
            }
            else if (value is TimeSpan)
            {
                _writer.WriteAttributeString("Type", "duration");
                _writer.WriteString(XmlConvert.ToString((TimeSpan)value));
            }
            else if (value is DateTime)
            {
                _writer.WriteAttributeString("Type", "DateTime");
                _writer.WriteString(XmlConvert.ToString((DateTime)value, RoundTrimDateTimeFormat));
            }
            else if (value is Guid)
            {
                _writer.WriteAttributeString("Type", "Guid");
                _writer.WriteString(XmlConvert.ToString((Guid)value));
            }
            else if (value is DateTimeOffset)
            {
                _writer.WriteAttributeString("Type", "DateTimeOffset");
                _writer.WriteString(XmlConvert.ToString((DateTimeOffset)value));
            }
            else
            {
                Type t = value.GetType();
                string typeName;
                if (t.IsPrimitive)
                {
                    string txt = ObjectToXmlString(value, out typeName);
                    if (typeName != null)
                        _writer.WriteAttributeString("Type", typeName);
                    if (txt != null)
                        _writer.WriteString(txt);
                }
                else if (t.IsArray)
                {
                    Array array = (Array)value;
                    _writer.WriteAttributeString("Type", t.Name);
                    if (maxDepth < 1)
                        _writer.WriteString("[" + t.FullName + "].Length = " + XmlConvert.ToString(array.Length));
                    else
                    {
                        foreach (object obj in array)
                            WriteObjectElement(obj, "Element", maxDepth - 1);
                    }
                }
                if (t.IsEnum)
                {
                    _writer.WriteAttributeString("Type", t.Name);
                    _writer.WriteString(Enum.GetName(t, value));
                }
                else if (value is IConvertible)
                {
                    IConvertible convertible = (IConvertible)value;
                    try
                    {
                        object obj;
                        IFormatProvider fmt = System.Globalization.CultureInfo.CurrentCulture;
                        switch (convertible.GetTypeCode())
                        {
                            case TypeCode.Boolean:
                                if ((obj = convertible.ToBoolean(fmt)) != null && obj is bool)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                            case TypeCode.Byte:
                                if ((obj = convertible.ToByte(fmt)) != null && obj is byte)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                            case TypeCode.Char:
                                if ((obj = convertible.ToChar(fmt)) != null && obj is char)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                            case TypeCode.DateTime:
                                if ((obj = convertible.ToDateTime(fmt)) != null && obj is DateTime)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                            case TypeCode.DBNull:
                                WriteObjectElementValue(DBNull.Value, maxDepth);
                                return;
                            case TypeCode.Decimal:
                                if ((obj = convertible.ToDecimal(fmt)) != null && obj is decimal)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                            case TypeCode.Double:
                                if ((obj = convertible.ToDouble(fmt)) != null && obj is double)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                            case TypeCode.Int16:
                                if ((obj = convertible.ToInt16(fmt)) != null && obj is short)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                            case TypeCode.Int32:
                                if ((obj = convertible.ToInt32(fmt)) != null && obj is int)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                            case TypeCode.Int64:
                                if ((obj = convertible.ToInt64(fmt)) != null && obj is long)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                            case TypeCode.SByte:
                                if ((obj = convertible.ToSByte(fmt)) != null && obj is bool)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                            case TypeCode.Single:
                                if ((obj = convertible.ToSingle(fmt)) != null && obj is float)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                            case TypeCode.String:
                                if ((obj = convertible.ToString(fmt)) != null && obj is string)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                            case TypeCode.UInt16:
                                if ((obj = convertible.ToUInt16(fmt)) != null && obj is ushort)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                            case TypeCode.UInt32:
                                if ((obj = convertible.ToUInt32(fmt)) != null && obj is uint)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                            case TypeCode.UInt64:
                                if ((obj = convertible.ToUInt64(fmt)) != null && obj is ulong)
                                {
                                    WriteObjectElementValue(obj, maxDepth);
                                    return;
                                }
                                break;
                        }
                    }
                    catch { }
                    _writer.WriteAttributeString("Type", t.Name);
                    _writer.WriteString(value.ToString());
                }
                else if (maxDepth < 1)
                {
                    _writer.WriteAttributeString("Type", t.Name);
                    if (value is ICollection)
                        _writer.WriteString("(" + t.FullName + ").Count = " + XmlConvert.ToString(((ICollection)value).Count));
                    else
                        _writer.WriteString("(" + t.FullName + ")");
                }
                else if (t.IsGenericType && t.GetGenericTypeDefinition().AssemblyQualifiedName == _keyValuePairName)
                {
                    WriteObjectElement(t.GetProperty("Key").GetValue(value), "Key", maxDepth - 1);
                    WriteObjectElement(t.GetProperty("Value").GetValue(value), "Value", maxDepth - 1);
                }
                else if (value is DictionaryEntry)
                {
                    DictionaryEntry de = (DictionaryEntry)value;
                    _writer.WriteAttributeString("Type", t.Name);
                    WriteObjectElement(de.Key, "Key", maxDepth - 1);
                    WriteObjectElement(de.Value, "Value", maxDepth - 1);
                }
                else if (value is IDictionary)
                {
                    _writer.WriteAttributeString("Type", t.Name);
                    IDictionary dictionary = (IDictionary)value;
                    foreach (object k in dictionary.Keys)
                        WriteDictionaryEntry(k, dictionary[k], "DictionaryEntry", maxDepth - 1);
                }
                else if (value is IEnumerable)
                {
                    _writer.WriteAttributeString("Type", t.Name);
                    IEnumerable enumerable = (IEnumerable)value;
                    Type it = t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().AssemblyQualifiedName == _dictionaryName);
                    if (it != null)
                    {
                        t = (typeof(KeyValuePair<,>)).MakeGenericType(it.GetGenericArguments());
                        PropertyInfo kp = t.GetProperty("Key");
                        PropertyInfo vp = t.GetProperty("Value");
                        foreach (object kvp in enumerable)
                            WriteDictionaryEntry(kp.GetValue(value), vp.GetValue(value), "KeyValuePair", maxDepth - 1);
                    }
                    else
                    {
                        foreach (object obj in enumerable)
                            WriteObjectElement(obj, "Item", maxDepth - 1);
                    }
                }
                else
                {
                    _writer.WriteAttributeString("Type", t.Name);
                    _writer.WriteString(value.ToString());
                }
            }
        }

        private void WriteDictionaryEntry(object entryKey, object value, string elementName, int maxDepth = 16)
        {
            _writer.WriteStartElement(elementName);
            try
            {
                string typeName;
                string key;
                if (entryKey is string)
                    _writer.WriteAttributeString("Name", (string)entryKey);
                else if (entryKey is int)
                    _writer.WriteAttributeString("Number", XmlConvert.ToString((int)entryKey));
                else
                {
                    key = ObjectToXmlString(entryKey, out typeName);
                    if (key == null)
                        key = "";
                    _writer.WriteAttributeString("Key", key);
                    if (typeName != null)
                        _writer.WriteAttributeString("KeyType", typeName);
                }
                WriteObjectElementValue(value, maxDepth);
            }
            finally { _writer.WriteEndElement(); }
        }

        private void WriteDictionaryEntry(DictionaryEntry entry, string elementName = "Item", int maxDepth = 16)
        {
            WriteDictionaryEntry(entry.Key, entry.Value, elementName, maxDepth);
        }
        private void WriteTypeInfo(Type type, string elementName = "Type", int maxDepth = 12)
        {
            if (type == null)
                return;
            _writer.WriteStartElement(elementName);
            try
            {
                _writer.WriteAttributeString("Name", type.Name);
                if (type.Namespace != null)
                    _writer.WriteAttributeString("Namespace", type.Namespace);
                if (type.IsClass && type.FullName != "System.String")
                    _writer.WriteAttributeString("Assembly", type.Assembly.FullName);
                if (type.IsGenericType)
                {
                    foreach (Type t in type.GetGenericArguments())
                        WriteTypeInfo(t, "GenericArgument", maxDepth - 1);
                }
                if (type.IsArray)
                    WriteTypeInfo(type.GetElementType(), "ElementType", maxDepth);
                else if (maxDepth > 0 && !type.IsPrimitive && type.FullName != "System.String")
                {
                    foreach (Type t in type.GetInterfaces())
                        WriteTypeInfo(t, "Interface", maxDepth - 1);
                    if (type.IsClass)
                        WriteTypeInfo(type.BaseType, "BaseType", maxDepth - 1);
                }
            }
            finally { _writer.WriteEndElement(); }
        }
        private void OnProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            Monitor.Enter(_syncRoot);
            try
            {
                _stopwatches.StartNewProject(e.BuildEventContext);
                _writer.WriteStartElement("ProjectStarted");
                try
                {
                    _writer.WriteAttributeString("Id", XmlConvert.ToString(e.ProjectId));
                    if (e.ProjectFile != null)
                        _writer.WriteAttributeString("File", e.ProjectFile);
                    if (e.ToolsVersion != null)
                        _writer.WriteAttributeString("ToolsVersion", e.ToolsVersion);
                    WriteEventArgsProperties(e, true);
                    if (e.TargetNames != null && e.TargetNames.Length > 0)
                    {
                        _writer.WriteStartElement("TargetNames");
                        try
                        {
                            if (e.Message.Length > 0 && RequiresCDataRegex.IsMatch(e.TargetNames))
                                _writer.WriteCData(e.TargetNames);
                            else
                                _writer.WriteString(e.TargetNames);
                        }
                        finally { _writer.WriteEndElement(); }
                    }
                    WriteBuildEventContext(e.BuildEventContext);
                    if (e.ParentProjectBuildEventContext != null)
                        WriteBuildEventContext(e.ParentProjectBuildEventContext, "Parent");
                    if (e.Items != null)
                    {
                        _writer.WriteStartElement("Items");
                        try
                        {
                            foreach (object obj in e.Properties)
                                WriteObjectElement(obj, "Item");
                        }
                        finally { _writer.WriteEndElement(); }
                    }
                    if (e.Properties != null)
                    {
                        _writer.WriteStartElement("Properties");
                        try
                        {
                            foreach (object obj in e.Properties)
                                WriteObjectElement(obj, "Property");
                        }
                        finally { _writer.WriteEndElement(); }
                    }
                    if (e.GlobalProperties != null && e.GlobalProperties.Count > 0)
                    {
                        _writer.WriteStartElement("GlobalProperties");
                        try
                        {
                            foreach (string key in e.GlobalProperties.Keys)
                            {
                                _writer.WriteStartElement("Property");
                                try
                                {
                                    _writer.WriteAttributeString("Name", key);
                                    string value = e.GlobalProperties[key];
                                    if (e.GlobalProperties[key] != null)
                                    {
                                        if (value.Length > 0 && RequiresCDataRegex.IsMatch(value))
                                            _writer.WriteCData(value);
                                        else
                                            _writer.WriteString(value);
                                    }
                                }
                                finally { _writer.WriteEndElement(); }
                            }
                        }
                        finally { _writer.WriteEndElement(); }
                    }
                }
                finally { _writer.WriteEndElement(); }
            }
            finally { Monitor.Exit(_syncRoot); }
        }

        private void OnProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            Monitor.Enter(_syncRoot);
            try
            {
                _writer.WriteStartElement("ProjectFinished");
                try
                {
                    _writer.WriteAttributeString("Succeeded", XmlConvert.ToString(e.Succeeded));
                    _writer.WriteAttributeString("Duration", XmlConvert.ToString(_stopwatches.StopProject(e.BuildEventContext)));
                    if (e.ProjectFile != null)
                        _writer.WriteAttributeString("File", e.ProjectFile);
                    WriteEventArgsProperties(e);
                }
                finally { _writer.WriteEndElement(); }
            }
            finally { Monitor.Exit(_syncRoot); }
        }
        private void OnTargetStarted(object sender, TargetStartedEventArgs e)
        {
            Monitor.Enter(_syncRoot);
            try
            {
                _stopwatches.StartNewTarget(e.BuildEventContext);
                _writer.WriteStartElement("TargetStarted");
                try
                {
                    if (e.TargetName != null)
                        _writer.WriteAttributeString("Name", e.TargetName);
                    if (e.TargetFile != null)
                        _writer.WriteAttributeString("File", e.TargetFile);
                    if (e.ProjectFile != null)
                        _writer.WriteAttributeString("ProjectFile", e.ProjectFile);
                    if (e.ParentTarget != null)
                        _writer.WriteAttributeString("Parent", e.ParentTarget);
                    WriteEventArgsProperties(e);
                }
                finally { _writer.WriteEndElement(); }
            }
            finally { Monitor.Exit(_syncRoot); }
        }
        private void OnTargetFinished(object sender, TargetFinishedEventArgs e)
        {
            Monitor.Enter(_syncRoot);
            try
            {
                _writer.WriteStartElement("TargetFinished");
                try
                {
                    _writer.WriteAttributeString("Succeeded", XmlConvert.ToString(e.Succeeded));
                    if (e.TargetName != null)
                        _writer.WriteAttributeString("Name", e.TargetName);
                    if (e.TargetFile != null)
                        _writer.WriteAttributeString("File", e.TargetFile);
                    if (e.ProjectFile != null)
                        _writer.WriteAttributeString("ProjectFile", e.ProjectFile);
                    _writer.WriteAttributeString("Duration", XmlConvert.ToString(_stopwatches.StopTarget(e.BuildEventContext)));
                    WriteEventArgsProperties(e);
                }
                finally { _writer.WriteEndElement(); }
            }
            finally { Monitor.Exit(_syncRoot); }
        }
        private void OnTaskStarted(object sender, TaskStartedEventArgs e)
        {
            Monitor.Enter(_syncRoot);
            try
            {
                _stopwatches.StartNewTask(e.BuildEventContext);
                _writer.WriteStartElement("TaskStarted");
                try
                {
                    if (e.TaskName != null)
                        _writer.WriteAttributeString("Name", e.TaskName);
                    if (e.TaskFile != null)
                        _writer.WriteAttributeString("File", e.TaskFile);
                    if (e.ProjectFile != null)
                        _writer.WriteAttributeString("ProjectFile", e.ProjectFile);
                    WriteEventArgsProperties(e);
                }
                finally { _writer.WriteEndElement(); }
            }
            finally { Monitor.Exit(_syncRoot); }
        }
        private void OnTaskFinished(object sender, TaskFinishedEventArgs e)
        {
            Monitor.Enter(_syncRoot);
            try
            {
                _writer.WriteStartElement("TaskFinished");
                try
                {
                    _writer.WriteAttributeString("Succeeded", XmlConvert.ToString(e.Succeeded));
                    _writer.WriteAttributeString("Duration", XmlConvert.ToString(_stopwatches.StopTask(e.BuildEventContext)));
                    if (e.TaskName != null)
                        _writer.WriteAttributeString("Name", e.TaskName);
                    if (e.TaskFile != null)
                        _writer.WriteAttributeString("File", e.TaskFile);
                    if (e.ProjectFile != null)
                        _writer.WriteAttributeString("ProjectFile", e.ProjectFile);
                    WriteEventArgsProperties(e);
                }
                finally { _writer.WriteEndElement(); }
            }
            finally { Monitor.Exit(_syncRoot); }
        }
        private void OnMessageRaised(object sender, BuildMessageEventArgs e)
        {
            Monitor.Enter(_syncRoot);
            try
            {
                _writer.WriteStartElement("Message");
                try
                {
                    _writer.WriteAttributeString("Importance", Enum.GetName(e.Importance.GetType(), e.Importance));
                    if (e.LineNumber > 0)
                        _writer.WriteAttributeString("Line", XmlConvert.ToString(e.LineNumber));
                    if (e.ColumnNumber > 0)
                        _writer.WriteAttributeString("Column", XmlConvert.ToString(e.ColumnNumber));
                    if (e.EndLineNumber > 0 && e.EndLineNumber != e.LineNumber)
                        _writer.WriteAttributeString("EndLine", XmlConvert.ToString(e.EndLineNumber));
                    if (e.EndColumnNumber > 0 && e.EndColumnNumber != e.ColumnNumber)
                        _writer.WriteAttributeString("EndColumn", XmlConvert.ToString(e.EndColumnNumber));
                    if (e.Code != null)
                        _writer.WriteAttributeString("Code", e.Code);
                    if (e.Subcategory != null)
                        _writer.WriteAttributeString("Subcategory", e.Subcategory);
                    if (e.File != null)
                        _writer.WriteAttributeString("File", e.File);
                    if (e.ProjectFile != null)
                        _writer.WriteAttributeString("ProjectFile", e.ProjectFile);
                    WriteEventArgsProperties(e);
                }
                finally { _writer.WriteEndElement(); }
            }
            finally { Monitor.Exit(_syncRoot); }
        }
        private void OnErrorRaised(object sender, BuildErrorEventArgs e)
        {
            Monitor.Enter(_syncRoot);
            try
            {
                _writer.WriteStartElement("Error");
                try
                {
                    if (e.LineNumber > 0)
                        _writer.WriteAttributeString("Line", XmlConvert.ToString(e.LineNumber));
                    if (e.ColumnNumber > 0)
                        _writer.WriteAttributeString("Column", XmlConvert.ToString(e.ColumnNumber));
                    if (e.EndLineNumber > 0 && e.EndLineNumber != e.LineNumber)
                        _writer.WriteAttributeString("EndLine", XmlConvert.ToString(e.EndLineNumber));
                    if (e.EndColumnNumber > 0 && e.EndColumnNumber != e.ColumnNumber)
                        _writer.WriteAttributeString("EndColumn", XmlConvert.ToString(e.EndColumnNumber));
                    if (e.Code != null)
                        _writer.WriteAttributeString("Code", e.Code);
                    if (e.Subcategory != null)
                        _writer.WriteAttributeString("Subcategory", e.Subcategory);
                    if (e.File != null)
                        _writer.WriteAttributeString("File", e.File);
                    if (e.ProjectFile != null)
                        _writer.WriteAttributeString("ProjectFile", e.ProjectFile);
                    WriteEventArgsProperties(e);
                }
                finally { _writer.WriteEndElement(); }
            }
            finally { Monitor.Exit(_syncRoot); }
        }
        private void OnWarningRaised(object sender, BuildWarningEventArgs e)
        {
            Monitor.Enter(_syncRoot);
            try
            {
                _writer.WriteStartElement("Warning");
                try
                {
                    if (e.LineNumber > 0)
                        _writer.WriteAttributeString("Line", XmlConvert.ToString(e.LineNumber));
                    if (e.ColumnNumber > 0)
                        _writer.WriteAttributeString("Column", XmlConvert.ToString(e.ColumnNumber));
                    if (e.EndLineNumber > 0 && e.EndLineNumber != e.LineNumber)
                        _writer.WriteAttributeString("EndLine", XmlConvert.ToString(e.EndLineNumber));
                    if (e.EndColumnNumber > 0 && e.EndColumnNumber != e.ColumnNumber)
                        _writer.WriteAttributeString("EndColumn", XmlConvert.ToString(e.EndColumnNumber));
                    if (e.Code != null)
                        _writer.WriteAttributeString("Code", e.Code);
                    if (e.Subcategory != null)
                        _writer.WriteAttributeString("Subcategory", e.Subcategory);
                    if (e.File != null)
                        _writer.WriteAttributeString("File", e.File);
                    if (e.ProjectFile != null)
                        _writer.WriteAttributeString("ProjectFile", e.ProjectFile);
                    WriteEventArgsProperties(e);
                }
                finally { _writer.WriteEndElement(); }
            }
            finally { Monitor.Exit(_syncRoot); }
        }
        private void OnCustomEventRaised(object sender, CustomBuildEventArgs e)
        {
            Monitor.Enter(_syncRoot);
            try
            {
                _writer.WriteStartElement("CustomEvent");
                try
                {
                    WriteEventArgsProperties(e);
                }
                finally { _writer.WriteEndElement(); }
            }
            finally { Monitor.Exit(_syncRoot); }
        }
    }
}
