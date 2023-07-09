#region [ Using ]
using System;
using System.IO;
using System.IO.Compression;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using System.Net;
using System.Net.Mail;
using System.Messaging;
using Microsoft.Win32;
using System.Runtime.InteropServices;
#endregion 

#region [ * NOTES * ]
/*
 *  <BRANDON description="Batch Runner & Diagnostics for Operating/Networking">
 *      <Batches  ie="Portlets">
 *          <Batch id="">
 *              ...
 *          </Batch>
 *      </Batches>
 *      
 *      <Run id="..." test="" xpath=""><!-- Run the batch (if xpath, then the batch at that xpath, otherwise, the batch contained herein -->
 *          <command id="..." test="" xpath="">        <!-- xpath = if given then look here for the command node (from transform), otherwise, this is the command node -->
 *              <params useShell="true" workingDirectory="C:\" command="notepad.exe" arguments="C:\boot.ini" style="min|max|hidden|normal"/>
 *          </command>
 *          <run-batch id="..." test="" xpath="">      <!-- xpath = if given, look here for the batch to run, as versus whatever batch is contained in the run-batch node -->
 *              { any set of the commands available within a Run batch }
 *          </run-batch>
 *          <transform id="..." test="" xpath="">
 *              <params  filepath=""/>   <!-- filepath = the path to the transform needed to be used -->
 *          </transform>
 *          <load-file id="..." test="" xpath="">
 *              <params filepath="" file-type="text|xml|base64"/>    <!-- filepath = the complete file/directory path of the file to be loaded (text will be CDATA, xml will be node and base64 will be CDATA with binary converted to Base-64 -->
 *          </load-file>
 *          <stop-if id="..." test="" xpath="">         <!-- method determines what type of code should be returned by this program run -->
 *              <params method="stop|error|..."/>
 *          </stop-if>
 *          <skip-to id="..." test="" xpath="">         <!-- method determines what type of code should be returned by this program run -->
 *              <params nextCommandType="" nextCommandId="" end=""/>
 *          </skip-to>
 *          <dump-log test="" xpath="">             <!-- filepath = the path to be used to dump the current run, if filepath="", then it will dump to Console -->
 *              <params filepath=""/>
 *          </dump-log>
 *          <pause id="..." test="" xpath="">
 *              <params method="console|msgbox|timer" timeout="" caption="" text="" buttons=""/> <!-- method="console" requires keyboard input, method="timer" requires timeout attribute to be number of milliseconds to wait, method="msgbox" will show a MessageBox with appropriate text, caption and buttons -->
 *          </pause>
 *          <echo id="..." test="" xpath="">                     <!-- Contents will be echoed to Console -->
 *              { any text to be output }
 *          </echo>
 *      </Run>
 * </BRANDON>
 *          
 */
#endregion

#region [ BRANDON ]
namespace BRANDON
{
    #region [ BDOG ]
    class BDOG
    {
        public string appLocation = String.Empty;
        public string xmlLocation = String.Empty;
        public XmlDocument appXml = new XmlDocument();
        public XmlDocument runXml = new XmlDocument();
        public string rootElementName = "X2Shell";     // was "BDOG"
        public Stream outputStream = null;
        public StreamWriter outputStreamWriter = null;
        public XmlNode contextNode = null;
        public bool writeToConsole = true;
        public bool writeToFile = false;
        public bool debug = false;
        public bool showUsage = false;
        public bool useConsoleForXml = false;
        public bool repeat = false;
        public bool alwaysRepeat = false;
        public bool stopped = false;
        public string stopMethod = String.Empty;
        public string errorHandlerId = String.Empty;
        public bool ignoreErrors = true;
        public bool skipping = false;
        public int exitCode = 0;
        public string skipMethod = String.Empty;
        public string skipValue = String.Empty;
        public DateTime systemTime;
        public byte[] bytes = null;
        public string powered = "Powered by X2Shell(TM) (www.x2a2.com) - © 2010\nReliance Software Systems Inc. (www.relware.com)";
        public string legalese = "All rights reserved.  Use of this application is permitted by anyone,\nhowever neither the compiled binaries nor source code may be altered.";
    }
    #endregion

    #region [ Program ]
    class Program
    {

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(SetConsoleCtrlEventHandler handler, bool add);

        private delegate bool SetConsoleCtrlEventHandler(CtrlType sig);

        private enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }


        #region [ Local Variables ]
        static BDOG m_bdog = new BDOG();
        #endregion

        #region [ Main ]
        [STAThread]  // Lets main know that multiple threads are involved.
        static void Main(string[] args)
        {
            try
            {
                SetConsoleCtrlHandler(ControlHandler, true);

                // INIT
                m_bdog.appLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                m_bdog.systemTime = System.DateTime.UtcNow.ToLocalTime();

                if (m_bdog.debug)
                    Console.WriteLine("Application: {0}", m_bdog.appLocation);

                m_bdog.runXml.AppendChild(m_bdog.runXml.CreateElement(m_bdog.rootElementName));

                SetXmlAttribute(m_bdog.runXml.DocumentElement, "powered-by", m_bdog.powered);
                SetXmlAttribute(m_bdog.runXml.DocumentElement, "legal-notice", m_bdog.legalese);

                LoadRunTimeVariables(m_bdog.runXml.DocumentElement);

                if (!m_bdog.showUsage)
                {
                    // LOAD
                    if (m_bdog.useConsoleForXml)
                    {
                        m_bdog.appXml.LoadXml(System.Console.In.ReadToEnd());
                    }
                    else
                    {
                        if (m_bdog.xmlLocation.Length == 0)
                        {
                            m_bdog.xmlLocation = m_bdog.appLocation.Substring(0, m_bdog.appLocation.Length - 4) + ".xml";
                        }

                        try
                        {
                            m_bdog.appXml.Load(m_bdog.xmlLocation);
                        }
                        catch(Exception)
                        {
                            if (m_bdog.xmlLocation == ( m_bdog.appLocation.Substring(0, m_bdog.appLocation.Length - 4) + ".xml"))
                            {
                                ShowUsage();
                                return;
                            }
                            else
                            {
                                Console.WriteLine(String.Format("ERROR: Could not locate specified configuration file: {0}", m_bdog.xmlLocation));
                                return;
                            }
                        }
                    }


                    if (m_bdog.debug)
                    {
                        Console.WriteLine("Configuration: {0}", m_bdog.xmlLocation);
                        Console.WriteLine("System Time: {0}", m_bdog.systemTime.ToString());
                    }

                    m_bdog.ignoreErrors = (m_bdog.appXml.DocumentElement.SelectNodes("/*/Error-Handler").Count == 0);
                    if (m_bdog.debug)
                    {
                        Console.WriteLine("ignore errors? {0}", (m_bdog.ignoreErrors)?"true":"false");

                    }

                    // START PROCESSING
                    do
                    {
                        m_bdog.repeat = m_bdog.alwaysRepeat;

                        XmlNode root = m_bdog.appXml.DocumentElement;
                        XmlNodeList nlBatches = root.SelectNodes("Data|Run");

                        XmlElement rootEl = (XmlElement)root;
                        string newRootNodeName = rootEl.GetAttribute("root-node");

                        if (rootEl.GetAttribute("use-root").Length != 0)
                        {
                            if ((rootEl.GetAttribute("use-root").ToLower() == "true")||(rootEl.GetAttribute("use-root").ToLower() == "yes"))
                            {
                                newRootNodeName = rootEl.Name;
                            }
                        }

                        if (newRootNodeName.Length != 0)
                        {
                            Console.WriteLine(String.Format("Setting Root Node: {0}", newRootNodeName));

                            m_bdog.rootElementName = newRootNodeName;

                            // RENAME ROOT NODE
                            XmlDocument docNew = new XmlDocument();
                            XmlElement newRoot = docNew.CreateElement(m_bdog.rootElementName);
                            docNew.AppendChild(newRoot);
                            newRoot.InnerXml = m_bdog.runXml.DocumentElement.InnerXml;
                            m_bdog.runXml = docNew;

                        }


                        if (m_bdog.debug)
                            Console.WriteLine("Number of Batches: {0}", nlBatches.Count);

                        foreach (XmlNode nodeBatch in nlBatches)
                        {

                            if (m_bdog.debug)
                            {
                                Console.WriteLine("Current Batch Type: {0}", nodeBatch.Name);
                                Console.WriteLine("Current Batch Id: {0}", GetXmlAttribute(nodeBatch, "id", String.Empty));
                            }

                            // see if there is a test and if so, validate it.
                            bool resultBatchTest = PerformTest(nodeBatch);
                            if ((resultBatchTest) && (!m_bdog.stopped))
                            {
                                switch (nodeBatch.Name)
                                {
                                    case "Run":
                                        ProcessBatch(nodeBatch);
                                        break;

                                    case "Data":
                                        GetDataBlock(nodeBatch);
                                        break;

                                    default:
                                        /* ignore anything other than the above types */
                                        break;

                                }


                                if (m_bdog.skipping)
                                {// clean up
                                    m_bdog.skipMethod = String.Empty;
                                    m_bdog.skipValue = String.Empty;
                                    m_bdog.skipping = false;
                                }

                            }

                        }

                        ProcessFinally();

                        Terminate();

                    } while (m_bdog.repeat == true);

                }
                else
                {
                    ShowUsage();
                }
            }
            catch (System.Exception pException)
            {
                m_bdog.exitCode = 9;
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));

                XmlElement terminate = m_bdog.runXml.CreateElement("terminate");
                m_bdog.runXml.DocumentElement.AppendChild(terminate);
                SetXmlAttribute(terminate, "cause", "ERROR");
                SetXmlAttribute(terminate, "message", pException.Message);
                SetXmlAttribute(terminate, "timestamp", System.DateTime.UtcNow.ToLocalTime().ToString());

                ProcessFinally();
                
                Console.WriteLine(m_bdog.runXml.OuterXml.ToString());
            }

            Environment.Exit(m_bdog.exitCode);
        }
        #endregion

        #region [ SECONDARY FUNCTIONS ]


        #region [ ControlHandler ]
        private static bool ControlHandler(CtrlType signal)
        {
            string reason = String.Empty;

            switch (signal)
            {
                case CtrlType.CTRL_BREAK_EVENT:
                    reason = "CTRL_BREAK_EVENT";
                    break;
                case CtrlType.CTRL_C_EVENT:
                    reason = "CTRL_C_EVENT";
                    break;
                case CtrlType.CTRL_CLOSE_EVENT:
                    reason = "CTRL_CLOSE_EVENT";
                    break;
                case CtrlType.CTRL_LOGOFF_EVENT:
                    reason = "CTRL_LOGOFF_EVENT";
                    break;
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                    reason = "CTRL_SHUTDOWN_EVENT";
                    break;

                default:
                    return false;
            }

            // if we got here, there is a reason
            if ((m_bdog != null) && (m_bdog.appXml != null))
            {
                try
                {
                    XmlElement terminate = m_bdog.runXml.CreateElement("terminate");
                    m_bdog.runXml.DocumentElement.AppendChild(terminate);
                    SetXmlAttribute(terminate, "cause", reason);
                    SetXmlAttribute(terminate, "timestamp", System.DateTime.UtcNow.ToLocalTime().ToString());

                    ProcessFinally();
                }
                catch(Exception)
                {

                }
            }
            else
            {
                Console.WriteLine("**** ERROR: Unable to access m_bdog ****");
            }

            m_bdog.stopped = true;
            return false;            
            
        }

        #endregion

        #region [ PerformTest (Base) ]
        private static bool PerformTest(XmlNode pNode, string pTestAttribute, bool pDefaultResult)
        {
            try
            {
                // see if there is a test and if so, validate it.
                string test = GetXmlAttribute(pNode, pTestAttribute, String.Empty);
                bool resultTest = pDefaultResult;
                if (test.Length != 0)
                {
                    XmlNode nodeTest = m_bdog.runXml.DocumentElement.SelectSingleNode("/" + m_bdog.rootElementName + "[" + test + "]");
                    resultTest = (nodeTest != null);
                }

                if (m_bdog.debug)
                {
                    Console.WriteLine("Test: {0}", test);
                    Console.WriteLine("Test Path: {0}", "/" + m_bdog.rootElementName + "[" + test + "]");
                    Console.WriteLine("Test Result: {0}", resultTest.ToString().ToLower());
                }

                return resultTest;
            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
                return false;
            }
        }
        #endregion

        #region [ PerformTest (Defaulted) ]
        private static bool PerformTest(XmlNode pNode)
        {
            try
            {
                // call the base PerformTest, defaulting to the attribute named "test" (if no test exists, default to true)
                return PerformTest(pNode, "test", true);
            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
                return false;
            }
        }
        #endregion

        #region [ GetRealNode ]
        private static XmlNode GetRealNode(XmlNode pNode)
        {
            try
            {
                XmlNode realNode = pNode;
                string xpath = GetXmlAttribute(realNode, "xpath", String.Empty);

                if (xpath.Length != 0)
                {
                    realNode = m_bdog.runXml.SelectSingleNode(xpath);
                }

                return realNode;
            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
                return null;
            }
        }
        #endregion

        #region [ GetParamsNode ]
        private static XmlNode GetParamsNode(XmlNode pNode)
        {
            try
            {
                return pNode.SelectSingleNode("params");
            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
                return null;
            }

        }
        #endregion

        #region [ GetParamValue ]
        private static string GetParamValue(XmlNode pNode, string pAttribute)
        {
            try
            {
                return GetParamValue(pNode, pAttribute, String.Empty);
            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
                return null;
            }

        }
        #endregion

        #region [ GetParamValue ]
        private static string GetParamValue(XmlNode pNode, string pAttribute, string pDefault)
        {
            try
            {
                string attributeType = GetXmlAttribute(pNode, pAttribute + "_type", "literal");

                switch(attributeType.ToLower())
                {
                    case "literal":
                        return GetXmlAttribute(pNode, pAttribute, pDefault);

                    case "xpath":
                        string attribute = GetXmlAttribute(pNode, pAttribute, String.Empty);
                        if (attribute.Length == 0)
                            throw new ApplicationException("No data was provided for this parameter value (" + pAttribute + ").\nWhen specifying a type of 'xpath' a value must be provided.\n");

                        //string attributeSource = GetXmlAttribute(pNode, pAttribute + "_src", String.Empty);

                        //XmlNode valueNode = m_bdog.contextNode.SelectSingleNode(attribute);
                        XmlNode valueNode = GetValueUsingXPathNav(m_bdog.contextNode, attribute);
                        
                        if (valueNode != null)
                        {
                            string attributeFormat = GetXmlAttribute(pNode, pAttribute + "_format", "text");
                            switch (attributeFormat)
                            {
                                case "text":
                                    return valueNode.InnerText;

                                case "xml":
                                    return valueNode.InnerXml;

                                case "xml-outer":
                                    return valueNode.OuterXml;

                                default:
                                    throw new ApplicationException("The specified value format (" + attributeFormat + ") is not valid.\nAcceptable options are: text, xml, xml-outer.\n");
                            }
                        }
                        else
                            return pDefault;
                    
                    default:
                        throw new ApplicationException("The specified value type (" + attributeType + ") is not valid.\nAcceptable options are: literal, xpath.\n");
                }
            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
                return null;
            }

        }
        #endregion

        #region [ GetParamValueFromNode ]
        private static string GetParamValueFromNode(XmlNode pNode, string pDefault)
        {
            try
            {
                string type = GetXmlAttribute(pNode, "type", "literal");

                switch (type.ToLower())
                {
                    case "literal":
                        return (pNode.InnerText.Length!=0) ? pNode.InnerText : pDefault;

                    case "xpath":
                        string xpath = pNode.InnerText;
                        if (xpath.Length == 0)
                            throw new ApplicationException("No data was provided for this parameter value.\nWhen specifying a type of 'xpath' a value must be provided.\n");

                        //XmlNode valueNode = m_bdog.contextNode.SelectSingleNode(xpath);
                        XmlNode valueNode = GetValueUsingXPathNav(m_bdog.contextNode, xpath);

                        if (valueNode != null)
                        {
                            string format = GetXmlAttribute(pNode, "format", "text");
                            switch (format)
                            {
                                case "text":
                                    return valueNode.InnerText;

                                case "xml":
                                    return valueNode.InnerXml;

                                case "xml-outer":
                                    return valueNode.OuterXml;

                                default:
                                    throw new ApplicationException("The specified value format (" + format + ") is not valid.\nAcceptable options are: text, xml, xml-outer.\n");
                            }
                        }
                        else
                            return pDefault;

                    default:
                        throw new ApplicationException("The specified value type (" + type + ") is not valid.\nAcceptable options are: literal, xpath.\n");
                }
            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
                return null;
            }

        }
        #endregion


        #region [ GetValueUsingXPathNav ]
        private static XmlNode GetValueUsingXPathNav(XmlNode pNode, string pPath)
        {
            XmlNode resultNode = null;
            try
            {
                XPathNavigator navigator = m_bdog.contextNode.CreateNavigator();
                XsltArgumentList varList = new XsltArgumentList();
                /*
                foreach (string key in pVariables.Keys)
                {
                    varList.AddParam(key, string.Empty, pVariables[key]);
                }
                CustomContext customContext = new CustomContext(new NameTable(), varList);
                */

                XPathExpression xpath = XPathExpression.Compile(pPath);
                /*
                xpath.SetContext(customContext);
                */
                
                object commandOutput = navigator.Evaluate(xpath);
                if (commandOutput is XPathNodeIterator)
                {
                    foreach (XPathNavigator commandOutputNode in (XPathNodeIterator)commandOutput)
                    {
                        resultNode = (XmlNode)commandOutputNode.UnderlyingObject;
                        // only grab the first node found
                        break;
                    }
                }
                else
                {
                    resultNode = m_bdog.contextNode.OwnerDocument.CreateElement("node");
                    XmlText requestedText = m_bdog.contextNode.OwnerDocument.CreateTextNode(commandOutput.ToString());
                    resultNode.AppendChild(requestedText);
                }

            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
            }

            return resultNode;

        }
        #endregion

        #region [ GetErrorHandler ]
        private static XmlNode GetErrorHandler(string pErrorHandlerId)
        {
            try
            {
                XmlNode errorHandler = null;

                if (m_bdog.debug)
                    Console.WriteLine("Locating error handler '" + m_bdog.errorHandlerId + "'.... (ignore? " + m_bdog.ignoreErrors + ")");

                if (!m_bdog.ignoreErrors)
                {
                    if (pErrorHandlerId.Length > 0)
                        errorHandler = m_bdog.appXml.DocumentElement.SelectSingleNode("/*/Error-Handler[@id='" + pErrorHandlerId + "']");
                    
                    if (errorHandler == null)
                        errorHandler = m_bdog.appXml.DocumentElement.SelectSingleNode("/*/Error-Handler[(count(@id)=0) or (string-length(@id)=0)]");

                    if (m_bdog.debug)
                        Console.WriteLine(".... found? " + (errorHandler != null).ToString());

                }

                return errorHandler;
            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
                return null;
            }

        }
        #endregion

        #region [ TriggerError ]
        private static void TriggerError(XmlNode pErrorBatch)
        {
            try
            {
                if (!m_bdog.ignoreErrors)
                {
                    m_bdog.stopped = true;
                    m_bdog.stopMethod = "error";
                }
            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
            }

        }
        #endregion
        
        #region [ Terminate ]
        private static void Terminate()
        {
            try
            {
                if (m_bdog.debug)
                    Console.WriteLine("terminating...");

                CloseOutputStream();


                // SHOULD ADD SOMETHING HERE TO SAY...
                if (m_bdog.stopped)
                {
                    if (m_bdog.stopMethod == "error")
                    {
                        if (!m_bdog.ignoreErrors)
                        {
                            // Attempt to locate the current error handler
                            XmlNode errorHandler = GetErrorHandler(m_bdog.errorHandlerId);
                            if (errorHandler != null)
                            {
                                // we need to clear our stopped flag to allow further processing
                                m_bdog.stopped = false;

                                // If this does not reference another Error-Handler, then turn off error handling from here on out, but process this batch accordingly.
                                m_bdog.ignoreErrors = (GetXmlAttribute(errorHandler, "error-handler-id", String.Empty).Length == 0);
                                ProcessBatch(errorHandler);

                                // Try again to end (but allow for nested handling)
                                Terminate();
                            }
                        }
                    }
                    
                    // Now clear this for any potential repeat.
                    m_bdog.stopMethod = String.Empty;
                    m_bdog.stopped = false;
                }
            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
            }

        }
        #endregion

        #region [ LoadRunTimeVariables ]
        private static void LoadRunTimeVariables(XmlNode pRunRoot)
        {

            try
            {

                XmlNode nodeRunTime = pRunRoot.OwnerDocument.CreateElement("run-time");
                pRunRoot.OwnerDocument.DocumentElement.AppendChild(nodeRunTime);

                LoadCommandLine(nodeRunTime);

                SetXmlAttribute(nodeRunTime, "app-location", m_bdog.appLocation);
                SetXmlAttribute(nodeRunTime, "xml-location", m_bdog.xmlLocation);

                SetXmlAttribute(nodeRunTime, "app-path", Application.StartupPath);
                
                SetXmlAttribute(nodeRunTime, "systemtime", m_bdog.systemTime.ToString());

                /* Additional Date/Time Data */
                SetXmlAttribute(nodeRunTime, "local24", m_bdog.systemTime.ToString("MM/dd/yyyy HH:mm:ss"));
                SetXmlAttribute(nodeRunTime, "datetime", m_bdog.systemTime.ToString("yyyyMMddHHmmss"));

                SetXmlAttribute(nodeRunTime, "month", m_bdog.systemTime.Month.ToString());
                SetXmlAttribute(nodeRunTime, "monthName", m_bdog.systemTime.ToString("MMMM"));
                SetXmlAttribute(nodeRunTime, "monthAbbrev", m_bdog.systemTime.ToString("MMM"));
                SetXmlAttribute(nodeRunTime, "day", m_bdog.systemTime.Day.ToString());
                SetXmlAttribute(nodeRunTime, "dayName", m_bdog.systemTime.ToString("dddd"));
                SetXmlAttribute(nodeRunTime, "year", m_bdog.systemTime.ToString("yyyy"));
                SetXmlAttribute(nodeRunTime, "hour24", m_bdog.systemTime.ToString("hh"));
                SetXmlAttribute(nodeRunTime, "hour12", (m_bdog.systemTime.Hour == 0 ? 12 : (m_bdog.systemTime.Hour > 12 ? (m_bdog.systemTime.Hour - 12) : m_bdog.systemTime.Hour)).ToString());
                SetXmlAttribute(nodeRunTime, "minutes", m_bdog.systemTime.Minute.ToString());
                SetXmlAttribute(nodeRunTime, "seconds", m_bdog.systemTime.Second.ToString());
                SetXmlAttribute(nodeRunTime, "ampm", m_bdog.systemTime.ToString("tt").ToLower());


                LoadSystemInfo(nodeRunTime);

                LoadEnvironmentVariables(nodeRunTime);

            }
            catch (System.Exception pException)
            {
                pRunRoot.OwnerDocument.DocumentElement.AppendChild(HandleException(pRunRoot.OwnerDocument, pException));
            }
        }
        #endregion

        #region [ LoadCommandLine ]
        private static void LoadCommandLine(XmlNode pParent)
        {
            try
            {
                XmlNode nodeCommandLine = CreateElementNode(pParent.OwnerDocument, "command-line");
                pParent.AppendChild(nodeCommandLine);

                //NOTE: This made the XML unreadable - moved to an attribute below
                //SetXmlTextValue(nodeCommandLine, System.Environment.CommandLine);

                SetXmlAttribute(nodeCommandLine, "cwd", System.Environment.CurrentDirectory);
                SetXmlAttribute(nodeCommandLine, "text", System.Environment.CommandLine);

                XmlNode nodeArguments = pParent.OwnerDocument.CreateElement("arguments");
                nodeCommandLine.AppendChild(nodeArguments);

                bool nextArgIsFilename = false;

                string[] args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    XmlNode nodeArgument = pParent.OwnerDocument.CreateElement("arg");
                    SetXmlAttribute(nodeArgument, "index", i.ToString());
                    SetXmlTextValue(nodeArgument, args[i]);
                    nodeArguments.AppendChild(nodeArgument);

                    m_bdog.debug = m_bdog.debug | (args[i].ToLower() == "/debug");
                    m_bdog.showUsage = m_bdog.showUsage | ((args[i].ToLower() == "/help") | (args[i].ToLower() == "/?"));
                    m_bdog.useConsoleForXml = m_bdog.useConsoleForXml | (args[i].ToLower() == "/console");

                    if (nextArgIsFilename)
                    {
                        m_bdog.xmlLocation = args[i];
                    }
                    else
                        nextArgIsFilename = (args[i].ToLower() == "/xml");
                }

                if (m_bdog.useConsoleForXml)
                    m_bdog.xmlLocation = "(console)";
            }
            catch (System.Exception pException)
            {
                pParent.OwnerDocument.DocumentElement.AppendChild(HandleException(pParent.OwnerDocument, pException));
            }
        }
        #endregion

        #region [ LoadSystemInfo ]
        private static void LoadSystemInfo(XmlNode pParent)
        {
            try
            {
                XmlNode nodeSystem = CreateElementNode(pParent.OwnerDocument, "system");
                pParent.AppendChild(nodeSystem);

                SetXmlAttribute(nodeSystem, "machine-name", System.Environment.MachineName);
                SetXmlAttribute(nodeSystem, "os-version", System.Environment.OSVersion.VersionString);

                SetXmlAttribute(nodeSystem, "processor-count", System.Environment.ProcessorCount.ToString());
                SetXmlAttribute(nodeSystem, "username", System.Environment.UserName);
                SetXmlAttribute(nodeSystem, "domain", System.Environment.UserDomainName);
                SetXmlAttribute(nodeSystem, "is-interactive", System.Environment.UserInteractive.ToString().ToLower());
            }
            catch (System.Exception pException)
            {
                pParent.OwnerDocument.DocumentElement.AppendChild(HandleException(pParent.OwnerDocument, pException));
            }
        }
        #endregion

        #region [ LoadEnvironmentVariables ]
        private static void LoadEnvironmentVariables(XmlNode pParent)
        {   
            try
            {
                XmlNode nodeEnvironment = CreateElementNode(pParent.OwnerDocument, "environment");
                pParent.AppendChild(nodeEnvironment);

                foreach (string key in System.Environment.GetEnvironmentVariables().Keys)
                {
                    XmlNode variable = nodeEnvironment.OwnerDocument.CreateElement("env-var");
                    nodeEnvironment.AppendChild(variable);
                    SetXmlAttribute(variable, "name", key);
                    SetXmlTextValue(variable, System.Environment.GetEnvironmentVariable(key));
                }
            }
            catch (System.Exception pException)
            {
                pParent.OwnerDocument.DocumentElement.AppendChild(HandleException(pParent.OwnerDocument, pException));
            }

        }
        #endregion

        #region [ LoadStreamData ]
        private static void LoadStreamData(XmlNode pParent, string pNodeName, StreamReader pStream)
        {
            try
            {
                if (m_bdog.debug)
                    Console.WriteLine("...loading " + pNodeName + " data from stream...");

                string streamData = pStream.ReadToEnd();

                if (streamData.Length != 0)
                {
                    if (m_bdog.debug)
                        Console.WriteLine(streamData);

                    if (m_bdog.debug)
                        Console.WriteLine("...appending data to datacloud...");

                    XmlNode nodeStreamData = pParent.OwnerDocument.CreateElement(pNodeName);
                    pParent.AppendChild(nodeStreamData);

                    AppendRawText(nodeStreamData, streamData);
                }
                    
                if (m_bdog.debug)
                    Console.WriteLine("...completed stream data handling...");

            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
            }
        }
        #endregion


        #region [ GetDataBlock ]
        /*
        */
        private static void GetDataBlock(XmlNode pBatch)
        {
            XmlElement elBatch = (XmlElement)pBatch;

            if (elBatch.HasAttribute("src"))
            {
                XmlElement newData = m_bdog.runXml.CreateElement(elBatch.Name);
                m_bdog.runXml.DocumentElement.AppendChild(newData);

                foreach(XmlAttribute att in elBatch.Attributes)
                {
                    newData.SetAttribute(att.Name, att.Value);
                }

                string src = elBatch.Attributes["src"].Value;
                bool keepRoot = ((elBatch.HasAttribute("keep-root")) && ((elBatch.Attributes["keep-root"].Value.ToLower() == "true") || (elBatch.Attributes["keep-root"].Value.ToLower() == "yes")));

                try
                {
                    XmlDocument data = new XmlDocument();
                    data.Load(src);
                    CopyNodes(newData, data.DocumentElement, !(keepRoot));
                }
                catch(Exception e)
                {
                    throw new Exception(String.Format("Error loading Data block from src: {0}", src), e);
                }
            }
            else
            {
                CopyNodes(m_bdog.runXml.DocumentElement, pBatch, false);
            }
        }
        #endregion

        #region [ ProcessBatch ]
        private static void ProcessBatch(XmlNode pBatch)
        {
            try
            {
                m_bdog.errorHandlerId = GetXmlAttribute(pBatch, "error-handler-id", String.Empty);
                if (m_bdog.debug)
                    Console.WriteLine("Using error handler: '" + m_bdog.errorHandlerId + "'");

                bool repeatBatch = false;
                do
                {
                    XmlNodeList nlContextLoop;

                    string forEach = GetXmlAttribute(pBatch, "for-each", String.Empty);
                    if (forEach.Length != 0)
                    {
                        nlContextLoop = m_bdog.runXml.DocumentElement.SelectNodes(forEach);
                    }
                    else
                    {
                        nlContextLoop = m_bdog.runXml.DocumentElement.SelectNodes("/*");    // Will grab root (single node loop)
                    }

                    foreach (XmlNode nodeContext in nlContextLoop)
                    {
                        m_bdog.contextNode = nodeContext;

                        XmlNodeList nlCommands = pBatch.SelectNodes("*");

                        if (m_bdog.debug)
                            Console.WriteLine("Number of Commands: {0}", nlCommands.Count);

                        foreach (XmlNode nodeCommand in nlCommands)
                        {
                            // if we are stopped, then we don't run any more
                            if (!m_bdog.stopped)
                            {   // if we are currently skipping, see if we should continue to do so.
                                if (m_bdog.skipping)
                                {
                                    switch (m_bdog.skipMethod)
                                    {
                                        case "type": if (nodeCommand.Name == m_bdog.skipValue) m_bdog.skipping = false;
                                            break;
                                        case "id": if (GetXmlAttribute(nodeCommand, "id", String.Empty) == m_bdog.skipValue) m_bdog.skipping = false;
                                            break;
                                        default:
                                            /* continue skipping to end */
                                            break;
                                    }
                                }

                                // if we are still skipping, then skip away
                                if (!m_bdog.skipping)
                                {
                                    bool resultCommandTest = PerformTest(nodeCommand);
                                    if (resultCommandTest)
                                    {
                                        if (m_bdog.debug)
                                        {
                                            Console.WriteLine("Current Command: {0}", nodeCommand.Name);
                                            Console.WriteLine("Command Id: {0}", GetXmlAttribute(nodeCommand, "id", String.Empty));
                                        }

                                        XmlNode nodeRun = CreateElementNode(m_bdog.runXml, nodeCommand.Name);
                                        m_bdog.runXml.DocumentElement.AppendChild(nodeRun);

                                        SetXmlAttribute(nodeRun, "id", GetXmlAttribute(nodeCommand, "id", String.Empty));
                                        SetXmlAttribute(nodeRun, "timestamp", System.DateTime.UtcNow.ToLocalTime().ToString());

                                        switch (nodeCommand.Name)
                                        {
                                            case "command":
                                                RunCommand(nodeCommand, nodeRun);
                                                break;

                                            case "set-bytes":
                                                RunSetBytes(nodeCommand, nodeRun);
                                                break;

                                            case "clear-bytes":
                                                RunClearBytes(nodeCommand, nodeRun);
                                                break;

                                            case "compress-bytes":
                                                RunCompressBytes(nodeCommand, nodeRun);
                                                break;

                                            case "decompress-bytes":
                                                RunDecompressBytes(nodeCommand, nodeRun);
                                                break;

                                            case "run-batch":
                                                RunRunBatch(nodeCommand, nodeRun);
                                                break;

                                            case "transform":
                                                RunTransform(nodeCommand, nodeRun);
                                                break;

                                            case "load-file":
                                                RunLoadFile(nodeCommand, nodeRun);
                                                break;

                                            case "save-file":
                                                RunSaveFile(nodeCommand, nodeRun);
                                                break;

                                            case "stop-if":
                                                RunStopIf(nodeCommand, nodeRun);
                                                break;

                                            case "skip-to":
                                                RunSkipTo(nodeCommand, nodeRun);
                                                break;

                                            case "sql":
                                                RunSQL(nodeCommand, nodeRun);
                                                break;

                                            case "dump-log":
                                                RunDumpLog(nodeCommand, nodeRun);
                                                break;

                                            case "load-config":
                                                RunLoadConfig(nodeCommand, nodeRun);
                                                break;

                                            case "new-id":
                                            case "guid":
                                                RunNewGUID(nodeCommand, nodeRun);
                                                break;

                                            case "pause":
                                                RunPause(nodeCommand, nodeRun);
                                                break;

                                            case "echo":
                                                RunEcho(nodeCommand, nodeRun);
                                                break;

                                            case "output":
                                                RunOutput(nodeCommand, nodeRun);
                                                break;

                                            case "path":
                                                RunPath(nodeCommand, nodeRun);
                                                break;


                                            case "ftp":
                                            case "ftps":
                                            case "sftp":
                                                RunFTP(nodeCommand, nodeRun);
                                                break;
                                            
                                            
                                            case "http":
                                                RunHTTP(nodeCommand, nodeRun);
                                                break;

                                            case "mail-to":
                                                RunMailTo(nodeCommand, nodeRun);
                                                break;

                                            case "msmq":
                                                RunMSMQ(nodeCommand, nodeRun);
                                                break;

                                            case "registry":
                                                RunRegistry(nodeCommand, nodeRun);
                                                break;

                                            case "reset":
                                                RunReset(nodeCommand, nodeRun);
                                                break;

                                            case "powershell":
                                                RunPowershell(nodeCommand, nodeRun);
                                                break;

                                            case "form":
                                                RunForm(nodeCommand, nodeRun);
                                                break;

                                            case "compress":
                                                RunCompress(nodeCommand, nodeRun);
                                                break;


                                            case "unzip":
                                            case "unzip-file":
                                            case "unzip-files":
                                            case "list-zip":
                                            case "list-zip-file":
                                            case "read-zip":
                                            case "read-zip-file":
                                                bool zipReadOnly = ((nodeCommand.Name.ToLower().Substring(0,5) == "list-")||(nodeCommand.Name.ToLower().Substring(0,5) == "read-"));
                                                RunUnzipFiles(nodeCommand, nodeRun, zipReadOnly);
                                                break;

                                            case "zip":
                                            case "zip-file":
                                            case "zip-files":
                                                RunZipFiles(nodeCommand, nodeRun);
                                                break;



                                            case "x2tl":
                                                RunX2TL(nodeCommand, nodeRun);
                                                break;

                                            default:
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    repeatBatch = PerformTest(pBatch, "do-while", false);
                } 
                while (repeatBatch);

            }
            catch (System.Exception pException)
            {
                TriggerError(pBatch.AppendChild(HandleException(pBatch.OwnerDocument, pException)));
            }
        }
        #endregion

        #region [ ProcessFinally ]
        private static void ProcessFinally()
        {
            foreach (XmlNode final in m_bdog.appXml.DocumentElement.SelectNodes("Finally"))
            {
                ProcessBatch(final);
            }
		    m_bdog.stopped = true;
        }
        #endregion

        #region [ RecursiveCopy ]
        private static void RecursiveCopy(string pSourcefolder, string pDestinationfolder, bool pRecurse, bool pOverwrite)
        {
            try
            {


                FileAttributes attr = File.GetAttributes(pSourcefolder);

                //detect whether its a directory or file
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    foreach (string filename in Directory.GetFiles(pSourcefolder))
                    {

                        File.Copy(filename, pDestinationfolder + "\\" + GetFileName(filename), pOverwrite);
                    }

                    foreach (string foldername in Directory.GetDirectories(pSourcefolder))
                    {
                        Directory.CreateDirectory(pDestinationfolder + "\\" + GetFileName(foldername));
                        if (pRecurse)
                            RecursiveCopy(pSourcefolder + "\\" + GetFileName(foldername), pDestinationfolder + "\\" + GetFileName(foldername), pRecurse, pOverwrite);
                    }
                }
                else
                {

                    try
                    {
                        FileAttributes destattr = File.GetAttributes(pDestinationfolder);

                        if ((destattr & FileAttributes.Directory) == FileAttributes.Directory)
                        {
                            File.Copy(pSourcefolder, pDestinationfolder + "\\" + GetFileName(pSourcefolder), pOverwrite);
                        }
                        else
                        {
                            File.Copy(pSourcefolder, pDestinationfolder, pOverwrite);
                        }
                    }
                    catch(Exception)
                    {// error is almost always File-not-Found, so we are probably good to copy
                        File.Copy(pSourcefolder, pDestinationfolder, pOverwrite);
                    }
                }
            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
            }
        }
        #endregion

        #region [ GetFileName ]
        private static string GetFileName(string pPath)
        {
            int lastOccrance = 0;
            try
            {
                for (int i = 0; i < pPath.Length - 1; i++)
                {
                    if (pPath.Substring(i, 1) == "\\")
                        lastOccrance = i;

                }

            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
            }
            return pPath.Substring(lastOccrance + 1);
        }
        #endregion

        #region [ AddFileDetails ]
        private static void AddFileDetails(string fileName, XmlNode nodeFile, string attPrefix = "")
        {
            try
            {
                FileInfo oFileInfo = new FileInfo(fileName);
                DateTime dtCreationTime = oFileInfo.CreationTime;

                SetXmlAttribute(nodeFile, String.Format("{0}name",attPrefix), oFileInfo.Name);
                SetXmlAttribute(nodeFile, String.Format("{0}ext",attPrefix), oFileInfo.Extension);
                if (oFileInfo.IsReadOnly)
                {
                    SetXmlAttribute(nodeFile, "READ-ONLY", "true");
                }
                SetXmlAttribute(nodeFile, String.Format("{0}size",attPrefix), oFileInfo.Length.ToString());
                SetXmlAttribute(nodeFile, String.Format("{0}attributes",attPrefix), oFileInfo.Attributes.ToString());
                SetXmlAttribute(nodeFile, String.Format("{0}created",attPrefix), oFileInfo.CreationTime.ToString());
                SetXmlAttribute(nodeFile, String.Format("{0}updated", attPrefix), oFileInfo.LastWriteTime.ToString());
            }
            catch (Exception detailsExc)
            {
                throw new Exception(String.Format("Error obtaining details for file: {0}", fileName), detailsExc);
            }
        }
        #endregion


        #region [ BuildForm ]
        private static Form BuildForm(XmlNode pGUI)
        {
            try
            {

                Form guiForm = new Form();

                if (GetParamValue(pGUI, "background-color", String.Empty).Length != 0)
                    guiForm.BackColor = Color.FromName(GetParamValue(pGUI, "background-color"));

                guiForm.Text = GetParamValue(pGUI, "caption");
                guiForm.ControlBox = (GetParamValue(pGUI, "control-box", "true") == "true");
                guiForm.MaximizeBox = (GetParamValue(pGUI, "maximize-box", "true") == "true");
                guiForm.MinimizeBox = (GetParamValue(pGUI, "minimize-box", "true") == "true");
                guiForm.AutoScroll = (GetParamValue(pGUI, "auto-scroll", "true") == "true");
                guiForm.ShowIcon = (GetParamValue(pGUI, "show-icon", "true") == "true");
                guiForm.ShowInTaskbar = (GetParamValue(pGUI, "show-in-taskbar", "true") == "true");

                if (GetParamValue(pGUI, "icon", String.Empty).Length != 0)
                    guiForm.Icon = new Icon(GetParamValue(pGUI, "icon", String.Empty));


                if (GetParamValue(pGUI, "background-image", String.Empty).Length != 0)
                {
                    StreamReader bmpReader = new StreamReader(GetParamValue(pGUI, "background-image", String.Empty));
                    Bitmap bmp = new Bitmap(bmpReader.BaseStream);
                    guiForm.BackgroundImage = bmp;
                    //guiForm.BackgroundImageLayout
                }

                int xSize = System.Convert.ToInt32(GetParamValue(pGUI, "width", "300"));
                int ySize = System.Convert.ToInt32(GetParamValue(pGUI, "height", "200"));
                guiForm.ClientSize = new Size(xSize, ySize); //Size except the Title Bar-CaptionHeight

                int xPos;
                int yPos;

                //guiForm.Visible
                //guiForm.WindowState = FormWindowState.

                guiForm.StartPosition = FormStartPosition.CenterScreen;
                guiForm.AutoScaleBaseSize = new Size(5, 13);
                //runForm.MinTrackSize = new Size(300, (200 + SystemInformation.CaptionHeight));
                Button formAcceptButton = null;
                Button formCancelButton = null;

                foreach (XmlNode layout in pGUI.SelectNodes("layout"))
                {

                    if (m_bdog.debug)
                        Console.WriteLine("Form: " + layout.SelectNodes("*").Count.ToString() + " controls");

                    int layoutTop = 0, layoutLeft = 0;
                    int offsetTop = 0, offsetLeft = 0;

                    if (GetParamValue(layout, "left").Length != 0) 
                        layoutLeft = System.Convert.ToInt32(GetParamValue(layout, "left", "0"));

                    if (GetParamValue(layout, "top").Length != 0)
                        layoutTop = System.Convert.ToInt32(GetParamValue(layout, "top", "0"));


                    foreach (XmlNode control in layout.SelectNodes("*"))
                    {
                        string controlType = control.Name;
                        string defaultBackcolor = String.Empty, defaultColor = String.Empty;
                        bool supportsTransparentBackground = true;
                        xPos = 0; yPos = 0; xSize = 0; ySize = 0;
                        XmlNodeList items = null;
                        Control formControl = null;

                        switch (controlType)
                        {
                            case "label":
                                Label formLabel = new Label();
                                formControl = (Control)formLabel;

                                formLabel.UseMnemonic = (GetParamValue(control, "use-mnemonic", "false") == "true");
                                formLabel.BorderStyle = String2BorderStyle(GetParamValue(control, "border-style", "none"));

                                guiForm.Controls.Add(formLabel);
                                break;


                            case "textbox":
                                TextBox formTextbox = new TextBox();
                                formControl = (Control)formTextbox;

                                supportsTransparentBackground = false;
                                defaultBackcolor = "white";

                                formTextbox.BorderStyle = String2BorderStyle(GetParamValue(control, "border-style", "none"));
                                formTextbox.ScrollBars = String2ScrollBars(GetParamValue(control, "scroll-bars", "both"));
                                formTextbox.Multiline = (GetParamValue(control, "multi-line", "true") == "true");
                                formTextbox.UseSystemPasswordChar = ((GetParamValue(control, "password", "false") == "true") && (GetParamValue(control, "password-char", String.Empty).Length == 0));
                                if (GetParamValue(control, "password-char", String.Empty).Length != 0)
                                    formTextbox.PasswordChar = GetParamValue(control, "password-char", String.Empty).ToCharArray()[0];

                                formTextbox.ReadOnly = (GetParamValue(control, "read-only", "false") == "true");
                                /*
                                // after text has been set
                                if (GetParamValue(control, "select-all", "true") == "true")
                                {
                                    formTextbox.SelectionStart = 0;
                                    formTextbox.SelectionLength = formTextbox.Text.Length;
                                }
                                else
                                {
                                    formTextbox.SelectionLength = 0;
                                }
                                */

                                guiForm.Controls.Add(formTextbox);
                                break;



                            case "button":
                                Button formButton = new Button();
                                formControl = (Control)formButton;

                                // Note: This actually DOES support transparent backgrounds, but it is a little disconcerting to have them default.
                                supportsTransparentBackground = false;

                                formButton.UseMnemonic = (GetParamValue(control, "use-mnemonic", "false") == "true");
                                formButton.FlatStyle = String2FlatStyle(GetParamValue(control, "style", "flat"));
                                formButton.UseVisualStyleBackColor = (GetParamValue(control, "use-style-backcolor", "true") == "true");
                                formButton.AutoEllipsis = (GetParamValue(control, "auto-ellipsis", "true") == "true");

                                formButton.DialogResult = String2DialogResult(GetParamValue(control, "type", "none"));

                                if (GetParamValue(control, "form-accept", "false") == "true")
                                    formAcceptButton = formButton;

                                if (GetParamValue(control, "form-cancel", "false") == "true")
                                    formCancelButton = formButton;

                                guiForm.Controls.Add(formButton);
                                break;



                            case "image":
                                PictureBox formImage = new PictureBox();
                                formControl = (Control)formImage;

                                formImage.BorderStyle = String2BorderStyle(GetParamValue(control, "border-style", "none"));
                                formImage.ImageLocation = GetParamValue(control, "path");

                                guiForm.Controls.Add(formImage);
                                break;
                        
                            case "checkbox":
                                CheckBox formCheckbox = new CheckBox();
                                formControl = (Control)formCheckbox;

                                formCheckbox.AutoCheck = (GetParamValue(control, "auto-check", "true") == "true");
                                formCheckbox.Appearance = String2Appearance(GetParamValue(control, "appearance", "normal"));
                                formCheckbox.AutoEllipsis = (GetParamValue(control, "auto-ellipsis", "true") == "true");
                                formCheckbox.Checked = (GetParamValue(control, "checked", "false") == "true");
                                formCheckbox.CheckState = String2CheckState(GetParamValue(control, "check-state", (formCheckbox.Checked)?"checked":"unchecked"));

                                guiForm.Controls.Add(formCheckbox);
                                break;


                            case "radio":
                                RadioButton formRadio = new RadioButton();
                                formControl = (Control)formRadio;

                                formRadio.AutoCheck = (GetParamValue(control, "auto-check", "true") == "true");
                                formRadio.Appearance = String2Appearance(GetParamValue(control, "appearance", "normal"));
                                formRadio.AutoEllipsis = (GetParamValue(control, "auto-ellipsis", "true") == "true");
                                formRadio.Checked = (GetParamValue(control, "checked", "false") == "true");

                                guiForm.Controls.Add(formRadio);
                                break;


                            case "combobox":
                                ComboBox formCombo = new ComboBox();
                                formControl = (Control)formCombo;

                                supportsTransparentBackground = false;

                                if (GetParamValue(control, "drop-down-width", String.Empty).Length != 0)
                                    formCombo.DropDownWidth = System.Convert.ToInt32(GetParamValue(control, "drop-down-width", "0"));

                                items = control.SelectNodes("item");
                                object[] formComboItems = new object[items.Count];
                                for(int i=0; i< items.Count; i++)
                                {
                                    formComboItems[i] = GetParamValue(items[i], "caption", String.Empty);

                                }
                                
                                formCombo.Items.AddRange(formComboItems);

                                guiForm.Controls.Add(formCombo);
                                break;

                            case "listbox":
                                ListBox formListBox = new ListBox();
                                formControl = (Control)formListBox;

                                supportsTransparentBackground = false;

                                formListBox.SelectionMode = (GetParamValue(control, "allow-multiple", "false") == "true") ? SelectionMode.MultiExtended : SelectionMode.One;

                                items = control.SelectNodes("item");
                                object[] formListboxItems = new object[items.Count];
                                for (int i = 0; i < items.Count; i++)
                                {
                                    formListboxItems[i] = GetParamValue(items[i], "caption", String.Empty);
                                }

                                formListBox.Items.AddRange(formListboxItems);

                                guiForm.Controls.Add(formListBox);
                                break;

                            
                            
                            case "checked-listbox":
                                CheckedListBox formCheckedListBox = new CheckedListBox();
                                formControl = (Control)formCheckedListBox;

                                supportsTransparentBackground = false;

                                //formCheckedListBox.SelectionMode = (GetParamValue(control, "allow-multiple", "false") == "true") ? SelectionMode.MultiExtended : SelectionMode.One;
                                formCheckedListBox.CheckOnClick = true;

                                items = control.SelectNodes("item");
                                object[] formChkListboxItems = new object[items.Count];
                                for (int i = 0; i < items.Count; i++)
                                {
                                    formChkListboxItems[i] = GetParamValue(items[i], "caption", String.Empty);
                                }

                                formCheckedListBox.Items.AddRange(formChkListboxItems);

                                guiForm.Controls.Add(formCheckedListBox);
                                break;
                        

                            case "numeric":  // numeric up/down
                                NumericUpDown formNumeric = new NumericUpDown();
                                formControl = (Control)formNumeric;

                                supportsTransparentBackground = false;

                                guiForm.Controls.Add(formNumeric);
                                break;

                            /* this is really just the datetime picker stripped down
                            case "monthcalendar":  // month calendar
                                MonthCalendar formMonthCalendar = new MonthCalendar();
                                formControl = (Control)formMonthCalendar;

                                supportsTransparentBackground = false;

                                guiForm.Controls.Add(formMonthCalendar);
                                break;
                            */

                            case "date":  // date time picker
                                DateTimePicker formDTPicker = new DateTimePicker();
                                formControl = (Control)formDTPicker;

                                supportsTransparentBackground = false;

                                if (GetParamValue(control, "max-date", String.Empty).Length != 0)
                                {
                                    try
                                    {

                                        DateTime dtMax = System.Convert.ToDateTime(GetParamValue(control, "max-date", String.Empty));
                                        formDTPicker.MaxDate = dtMax;
                                    }
                                    catch(Exception)
                                    {
                                        throw new ApplicationException("Invalid max-date.\nThe date value that you provided (" + GetParamValue(control, "max-date", String.Empty) + ") for the max-date was invalid.");
                                    }
                                }

                                if (GetParamValue(control, "min-date", String.Empty).Length != 0)
                                {
                                    try
                                    {

                                        DateTime dtMin = System.Convert.ToDateTime(GetParamValue(control, "min-date", String.Empty));
                                        formDTPicker.MinDate = dtMin;
                                    }
                                    catch (Exception)
                                    {
                                        throw new ApplicationException("Invalid min-date.\nThe date value that you provided (" + GetParamValue(control, "min-date", String.Empty) + ") for the min-date was invalid.");
                                    }
                                }

                                formDTPicker.ShowUpDown = (GetParamValue(control, "show-up-down", "false") == "true");
                                formDTPicker.Format = DateTimePickerFormat.Short;


                                guiForm.Controls.Add(formDTPicker);
                                break;
                        
                        }



                        if (formControl != null) 
                        {
                            // Set the id (name) of the control
                            if (GetParamValue(control, "id").Length != 0)
                                formControl.Name = GetParamValue(control, "id");

                            // Set more common properties
                            formControl.Enabled = (GetParamValue(control, "enabled", "true") == "true");


                            // NOTE: Some flexing on whether or not to attempt transparent background by default or not
                            if (GetParamValue(control, "color").Length != 0)
                                formControl.ForeColor = Color.FromName(GetParamValue(control, "color"));

                            if ((supportsTransparentBackground) || (GetParamValue(control, "background-color", String.Empty).Length != 0))
                                formControl.BackColor = Color.FromName(GetParamValue(control, "background-color", defaultBackcolor));

                            // NOTE: Some flexing on mnemonic and text
                            formControl.Text = GetParamValue(control, "caption");

                            // NOTE: MUST do Text and Font BEFORE we do Size (and preferably Location)
                            if (GetParamValue(control, "font", String.Empty).Length != 0)
                            {
                                string fontFamily = GetParamValue(control, "font", "Arial");
                                FontStyle fontStyle = String2FontStyle(GetParamValue(control, "font-style"));
                                float fontSize = (float)System.Convert.ToDouble(GetParamValue(control, "font-size", "8"));
                                formControl.Font = new Font(fontFamily, fontSize, fontStyle);
                            }

                            bool absolutePositioning = (GetParamValue(control, "position", "relative") == "absolute");
                            
                            if (GetParamValue(control, "left").Length != 0)
                                xPos = System.Convert.ToInt32(GetParamValue(control, "left", "0"));
                            else
                                xPos = offsetLeft;

                            if (GetParamValue(control, "top").Length != 0)
                                yPos = System.Convert.ToInt32(GetParamValue(control, "top", "0"));
                            else
                                yPos = offsetTop;

                            //xPos = System.Convert.ToInt32(GetParamValue(control, "left", "0"));
                            //yPos = System.Convert.ToInt32(GetParamValue(control, "top", "0"));

                            if (!absolutePositioning)
                            {
                                xPos += layoutLeft;
                                yPos += layoutTop;
                            }

                            formControl.Location = new Point(xPos, yPos);

                            if ((GetParamValue(control, "width").Length != 0) || (GetParamValue(control, "height").Length != 0))
                            {
                                xSize = System.Convert.ToInt32(GetParamValue(control, "width", formControl.PreferredSize.Width.ToString()));
                                ySize = System.Convert.ToInt32(GetParamValue(control, "height", formControl.PreferredSize.Height.ToString()));
                                formControl.Size = new Size(xSize, ySize);
                            }
                            else
                            {
                                formControl.Size = formControl.PreferredSize;
                            }

                            // Until we add "stack-type" proprties in the layout, offsetLeft does not change
                            if (!absolutePositioning)
                            {
                                offsetLeft += 0;
                                offsetTop += formControl.Size.Height;
                            }

                        }


                    }

                }

                if (formAcceptButton != null)
                    guiForm.AcceptButton = formAcceptButton;
                if (formCancelButton != null)
                    guiForm.CancelButton = formCancelButton;

                return guiForm;
            }
            catch (System.Exception pException)
            {
                m_bdog.runXml.DocumentElement.AppendChild(HandleException(m_bdog.runXml, pException));
                return new Form();
            }
        }
        #endregion

        #region [ ShowUsage ]
        private static void ShowUsage()
        {

            Console.WriteLine("==============================================================================\r\nX2Shell - XML/XSLT Shell/Command Line Interface Tool \r\n==============================================================================\r\nCode named \"Batch Runner and Diagnostics for Operating/Networking\" (BRANDON), \r\nthis application uses an XML file for its configuration, and instructions.\r\nBy default, it looks for an XML file by the same name and path as the .EXE.\r\n(See command line switches at end of this document for other options.)\r\n\r\nThe configuration (XML) file should consist of any valid root node, which then\r\ncontains one or more <Data> or <Run> nodes, which themselves may contain a\r\n'test' attribute.  The test should be in the style of an xpath to a valid node\r\nwhere a hit is a pass and a miss is a fail.  If no test exists, it is a pass.\r\nEach <Data> node that passes will be copied as is into the data cloud.\r\nEach <Run> can contain any combination of the following execution types:\r\n\r\nNOTE: Unless otherwise configured, the resultant data cloud will have a root\r\nelement named \"X2Shell\".  To change this, include an attribute named\r\n\"root-node\" with a desired value in the root node of your configuration XML,\r\nor include the attribute \"use-root\" with either \"yes\" or \"true\" to\r\nuse the name of your configuration XML root node as the data cloud root name.\r\n\r\n==============================================================================\r\n\"command\" - Launch a new process / executable or shell extension\r\n------------------------------------------------------------------------------\r\nParams: \tcommand=\"some.exe\" \r\n\t\tworking-directory=\"c:\\\"\r\n\t\targuments=\"/arg1 /arg2\"  (*NOTE: see below)\r\n\t\tuse-shell=\"true|false\"\r\n\t\tstyle=\"min|max|hidden|normal\"\r\n\t\twait-for-exit=\"true|false\"\r\n\t\tcapture-stdin=\"true|false\" (**NOTE: see below)\r\n\t\tcapture-stdout=\"true|false\"\r\n\t\tcapture-stderr=\"true|false\"\r\n* NOTE: Additional arguments may be given as <arg> nodes within the Param.\r\nThese arg nodes should have the inner text reflecting the value to be used.\r\nFurther, arg nodes may have \"type\" and \"format\" attributes, which will act\r\nexactly as the \"_type\" and \"_format\" suffixed attributes explained below.\r\nFor more flexibility, the <arg> nodes also support two additional attributes:\r\n\"no-preceding-whitespace\" and \"separator-character\".  Giving the first of these\r\na \"true\" value will simply instruct the argument builder to not add a space\r\nbetween it and the previous arg node.  You may instead give your own separator\r\nvalue to be used instead of a space (\" \"). (i.e. separator-character=\"+\" )\r\n\r\n\r\n**NOTE: The option to capture standard input is only used if use-shell = true.\r\nFurther, an additional node given as <stdin> must be provided within the Param\r\nparent node.  Like the <arg> nodes above, it supports type and format.\r\n\r\n==============================================================================\r\n\"clear-bytes\" - Used to empty out the binary data byte array (bytes)\r\n------------------------------------------------------------------------------\r\nClears out the \"bytes\" byte array when the contents are no longer needed.\r\n\r\n==============================================================================\r\n\"compress-bytes\" - Used to compress the binary data byte array (bytes)\r\n------------------------------------------------------------------------------\r\nPerforms ZIP compression on the \"bytes\" byte array, compressing the contents\r\n\r\n==============================================================================\r\n\"decompress-bytes\" - Used to empty out the binary data byte array (bytes)\r\n------------------------------------------------------------------------------\r\nPerforms ZIP decompression on the \"bytes\" byte array, exploding the contents\r\n\r\n==============================================================================\r\n\"compress\" - Used to ZIP a file\r\n------------------------------------------------------------------------------\r\nParams: \tfilepath=\"compressed.zip\"\r\n\t\t\tmode=\"create|update\"\r\n\t\t\tsource-filepath=\"some-file-to-compress\"\r\n\t\t\tsaveas-filepath=\"directory/path/file_name.ext\"\r\n\t\t\tdelete-onsuccess=\"false|true\"\r\nEither create a new or update an existing ZIP file by adding the source file\r\nand saving it in the ZIP as the specified filepath.  If delete-onsuccess,\r\nthen the source file will be deleted after compression.\r\n\r\n==============================================================================\r\n\"dump-log\" - Used to dump the current datacloud out to a file or console\r\n------------------------------------------------------------------------------\r\nParams: \tfilepath=\"output.xml\" (or empty for console)\r\n\t\t\trootnode-xpath=\"xpath-to-element\" (default = \"/\")\r\nWrite out all or part of the XML of the datacloud\r\n\r\n==============================================================================\r\n\"echo\" - Write some text out to the console, or file (see output)\r\n------------------------------------------------------------------------------\r\n\t\t{ any text to be output }\r\nBase Attributes:  \r\n\t\tnew-line==\"true|false\"\r\n\r\nNote: unlike others, the \"echo\" execution does not have a <params> node,\r\nbut instead allows complex strings to be created in the same fashion as \r\ncomplex arguments are creaed in the above \"command\" process.  Instead of using\r\n<arg/> nodes, you use either <string/> or <line/> nodes with same attributes.\r\n\r\n\r\n==============================================================================\r\n\"form\" - Build a GUI Dialog Box\r\n------------------------------------------------------------------------------\r\ngui:\tdefinition\r\ngui Attributes:\r\n\t\t\tbackground-color=\"\"\r\n\t\t\tcontrol-box=\"true|false\"\r\n\t\t\tminimize-box=\"true|false\"\r\n\t\t\tmaximize-box=\"true|false\"\r\n\t\t\tauto-scroll=\"true|false\"\r\n\t\t\tshow-icon=\"true|false\"\r\n\t\t\tshow-in-taskbar=\"true|false\"\r\n\t\t\ticon=\"\"\r\n\t\t\tbackground-image=\"\"\r\n\t\t\twidth=\"300\"\r\n\t\t\theight=\"200\"\r\n\r\nNote: unlike others, the \"form\" execution does not have a <params> node,\r\nbut instead allows for a complete UI form to be defined in the XML as a child\r\nelement called \"gui\".\r\n\r\nA \"gui\" element can have one or more \"layout\" elements with attributes:\r\nlayout Attributes:\r\n\t\t\tleft=\"0\"\r\n\t\t\ttop=\"0\"\r\n\r\nA \"layout\" can then have any of the following elements with attributes:\r\n\r\nlabel:\r\nlabel Attributes:\r\n\t\t\tuse-mnemonic=\"false\"\r\n\t\t\tborder-style=\"none\"\r\n\r\ntextbox:\r\ntextbox Attributes:\r\n\t\t\tborder-style=\"none\"\r\n\t\t\tscroll-bars=\"both\"\r\n\t\t\tmulti-line=\"true\"\r\n\t\t\tpassword=\"false\"\r\n\t\t\tpassword-char=\"\"\r\n\t\t\tread-only=\"false\"\r\n\r\nbutton:\r\nbutton Attributes:\r\n\t\t\tuse-mnemonic=\"false\"\r\n\t\t\tstyle=\"flat\"\r\n\t\t\tuse-style-backcolor=\"true\"\r\n\t\t\ttype=\"none|yes|no|abort|retry|cancel|ignore\"\r\n\t\t\tform-accept=\"false\"\r\n\t\t\tform-cancel=\"false\"\r\n\r\nimage:\r\nimage Attributes:\r\n\t\t\tpath=\"\"\r\n\t\t\tborder-style=\"none\"\r\n\r\n\r\ncheckbox:\r\ncheckbox Attributes:\r\n\t\t\tauto-check=\"true|false\"\r\n\t\t\tappearance\"normal\"\r\n\t\t\tauto-ellipsis=\"true|false\"\r\n\t\t\tchecked=\"false|true\"\r\n\t\t\tcheck-state=\"checked|unchecked\"\r\n\r\n\r\nradio:\r\nradio Attributes:\r\n\t\t\tauto-check=\"true|false\"\r\n\t\t\tappearance\"normal\"\r\n\t\t\tauto-ellipsis=\"true|false\"\r\n\t\t\tchecked=\"false|true\"\r\n\r\n\r\ncombobox:\r\ncombobox Attributes:\r\n\t\t\tdrop-down-width=\"\"\r\nA \"combobox\" should contain one or more \"item\" elements\r\nitem Attributes:\r\n\t\t\tcaption=\"\"\r\n\r\nlistbox:\r\nlistbox Attributes:\r\n\t\t\tallow-multiple=\"false|true\"\r\nA \"listbox\" should contain one or more \"item\" elements (see above)\r\n\r\n                            \r\nchecked-listbox:\r\nA \"checked-listbox\" should contain one or more \"item\" elements (see above)\r\n                        \r\n\r\nnumeric:\r\nnumeric Attributes:\r\n(N/A)\r\n\r\ndate:\r\ndate Attributes:\r\n\t\t\tmin-date=\"\"\r\n\t\t\tmax-date=\"\"\r\n\t\t\tshow-up-down=\"false|true\"\r\n\r\n\r\n==============================================================================\r\n\"http\" - Used to get or post contents via HTTP web request\r\n------------------------------------------------------------------------------\r\nParams: \tmethod=\"get|post\"\r\n\t\turi=\"http://....\"\r\n\t\tpost-rootnode-xpath=\"/\" (if method is \"post\", what is sent)\r\n\t\tpost-format=\"text|xml\"  (and what format is it sent in)\r\n\r\n==============================================================================\r\n\"load-config\" - Used to load the contents of the active configuration\r\n------------------------------------------------------------------------------\r\nParams: \t(N/A)\r\n\r\n==============================================================================\r\n\"load-file\" - Used to load the contents of a file into the datacloud\r\n------------------------------------------------------------------------------\r\nParams: \tfilepath=\"somefilename\"\r\n\t\tfile-type=\"text|xml|base64\"\r\n\r\n==============================================================================\r\n\"mail-to\" - Used to send results to one or more e-mail recipients.\r\n------------------------------------------------------------------------------\r\nParams: \tsmtp-host=\"\"   (either IP address or DNS name of SMTP server)\r\n\t\ttcpip-port=\"25\" (optional - can change for custom port)\r\n\t\tfrom=\"\"         (email address of sender)\r\n\t\tto=\"\"           (one or more email addresses of recipients)\r\n\t\tsubject=\"\"\r\n\t\tcredentials=\"network|none\" (if server requires authorization)\r\n\t\turi=\"http://....\"\r\n\t\trootnode-xpath=\"/\"      (path to what is being sent)\r\n\t\tformat=\"text|xml\"       (and what format is it sent in)\r\n\r\n==============================================================================\r\n\"msmq\" - Used to send, receive or list contents of a Microsoft Message Queue.\r\n------------------------------------------------------------------------------\r\nParams: \tqueue-name=\".\\Private$\\MyQueue\"   (path/name of MSMQ)\r\n\t\tmethod=\"send|receive|list\"  (note: \"list\" is non-destructive)\r\n\t\trootnode-xpath=\"/\"     (if \"send\", path to what is being sent)\r\n\t\tformat=\"text|xml\"      (and what format is it sent in)\r\n\t\tpriority=\"lowest|very-low|low|normal|above-normal|\r\n\t\t\thigh|very-high|highest\"  (default is \"normal\")\r\n\r\n==============================================================================\r\n\"new-id\" - (aka guid) Used to generate one or more GUIDs\r\n------------------------------------------------------------------------------\r\nParams: \tcount=\"1\"\r\n\r\n==============================================================================\r\n\"output\" - Specify where echo content should be sent\r\n------------------------------------------------------------------------------\r\nParams: \tmethod=\"console|file|both|close\"\r\n\t\tfilepath=\"somefilepath\"\r\n\t\tfilemethod=\"create|append\"\r\nThe output command with the file (or both) method, forces all echo statements\r\nfrom that point forward to be sent to the filepath specified. \r\nIf the filemethod is set to append, then the output file will be opened for\r\nand the echo statements will be written at the end, otherwise, any existing\r\ncontent before the output command was issued will be overwritten.\r\nThe overriding of the echo command will continue until either a new output\r\ncommand is given, or the program terminates.\r\n\r\n==============================================================================\r\n\"path\" - Used to get or set the working dir, or list the contents of a path\r\n------------------------------------------------------------------------------\r\nParams: \tcommand=\"get-cwd|set-cwd|list|exists|file-exists|...\r\n\t\t... copy|move[-file]|move-dir[ectory]|delete|make-directory|make-dir|mkdir\"\r\n\t\tfilepath=\"somefilepath\" (optional - \"cwd\" is default)\r\n\t\tdestpath=\"somefilepath\" (only used for \"copy\" or \"move\")\r\nThis is used to manipulate a local drive or network drive path, or context.\r\n\r\n\r\n==============================================================================\r\n\"pause\" - Hold processing for some intervention\r\n------------------------------------------------------------------------------\r\nParams: \tmethod=\"console|msgbox|timer\"\r\n\t\ttimeout=\"number-in-milliseconds\" (for timer only)\r\n\t\tcaption=\"\"\r\n\t\ttext=\"\"\r\n\t\tbuttons=\"ok|ok-cancel|yes-no|yes-no-cancel|retry-cancel|\r\n\t\t\tabort-retry-cancel\"\r\n\r\n==============================================================================\r\n\"powershell\" - Run a PowerShell script or applet\r\n------------------------------------------------------------------------------\r\nParams: \tworking-directory=\"c:\\\"\r\n\t\tcapture-stdout=\"true|false\"\r\nNOTE: See definition for the \"command\" action for argument usage\r\n\r\n==============================================================================\r\n\"registry\" - Manage the machine registry (get, set, create, list)\r\n** NOTICE: USE WITH CAUTION!! **\r\n------------------------------------------------------------------------------\r\nParams: \thkey=\"HKLM|HKCU|HKCR|HKU|HKCC|HKEY_LOCAL_MACHINE|HKEY_CURRENT_USER|\r\n\t\t\tHKEY_CLASSES_ROOT|HKEY_USERS|HKEY_CURRENT_CONFIG\"\r\n\t\tsubkey=\"\"\r\n\t\tmethod=\"get(-value)|set(-value)|create(-key)|list\"\r\n\t\tvalue-name=\"\"\r\n\t\tvalue-type=\"REG_SZ|REG_DWORD\"\r\n\t\tvalue-data=\"\"\r\n\t\tformat=\"text|xml\"\r\nEither get, set, create or list a subkey within a branch of the registry.\r\n\r\n==============================================================================\r\n\"reset\" - Resets the DataCloud to remove all prior execution information\r\n------------------------------------------------------------------------------\r\nParams: \tmethod=\"soft|hard\"\r\nBoth resets will clear the DataCloud, but use a different manner.  The soft\r\nreset will remove all prior exeuction nodes.  The hard reset will rebuild the\r\nDataCloud from scratch.  A hard reset will also update the systemtime.\r\n\r\n==============================================================================\r\n\"run-batch\" - Process another batch of commands (typically from transform)\r\n------------------------------------------------------------------------------\r\nParams: \t{ List of commands to be run }\r\n\r\n==============================================================================\r\n\"save-file\" - Save the contents of part or all of the datacloud to file\r\n------------------------------------------------------------------------------\r\nParams: \tfilepath=\"\"             (path/name of file to be created)\r\n\t\trootnode-xpath=\"/\"            (path to what is being saved)\r\n\t\tformat=\"text|xml\"             (and what format it is saved in)\r\n\t\tappend-contents=\"true|FALSE\"  (default = override contents)\r\n\r\n==============================================================================\r\n\"skip-to\" - Skip processing if this command is run\r\n------------------------------------------------------------------------------\r\nParams: \tnextCommandType=\"{ any valid command type }\" (i.e. \"pause\")\r\n\t\tnextCommandId=\"{ id of an upcoming command }\"\r\n\t\tend=\"true\" (actual value is ignored, must be non-empty text)\r\n\r\n==============================================================================\r\n\"stop-if\" - Stop processing if this command is run\r\n------------------------------------------------------------------------------\r\nParams: \tmethod=\"this-run|all\"\r\n\r\n==============================================================================\r\n\"sql\" - Perform a Microsoft SQL Server Execution\r\n------------------------------------------------------------------------------\r\nParams: \tmethod=\"recordset|xml|bytes|execute-only\"\r\n\t\tserver=\"sqlserver\\instance\"\r\n\t\tconnect=\"DataSource=Connection_String\"\r\n\t\tquery=\"exec my_query @param1, @param2\"\r\n\t\trow-label=\"mydataitem\" (default: row)\r\nFor standard recordsets, the row-label (default is \"row\") will be used for\r\neach entry in the resulting recordset, with the field names as attributes.\r\nNOTE: When using recordset (default) method, do NOT use spaces or special\r\ncharacters for the names of the columns, or you will generate an error.\r\n\r\nNOTE: Use of the bytes return type will store the resulting single image \r\nresponse in the general byte array \"bytes\".\r\n\r\nIf the result will be XML (for instance using the FOR XML option of SQL)\r\nuse the xml method, and the row-label to indicate the wrapper element.\r\n\r\nNOTE: If your query or stored procedure has parameters, include them as \r\n\"param\" child elements of the \"params\" element.  Give them a \"name\" \r\nattribute, a \"value\" attribute, and optionally a \"test\" attribute.\r\nNo value attribute will indicate a NULL. A value format of \"bytes\" to\r\nuse the general byte array.\r\n\r\nExample:\r\n  <sql id=\"...\" test=\"\" xpath=\"\">\r\n    <params server=\"\" connect=\"\" query=\"exec my_sproc @p1, @p2, @pImage\">\r\n      <param name=\"@p1\" value=\"1\"/>\r\n      <param name=\"@p2\" />\r\n      <param name=\"@pImage\" value_type=\"bytes\"/>\r\n    </params>\r\n  </sql>\r\n\r\n==============================================================================\r\n\"transform\" - Used to apply an external XSL file to a portion of the datacloud\r\n------------------------------------------------------------------------------\r\nParams: \tfilepath=\"somefile.xsl\"\r\n\t\tsource-filepath=\"somedocument.xml\"\r\n\t\t-OR-\r\n\t\trootnode-xpath=\"/datacloud/xpath\"\r\nThe XSL will be applied to the datacloud, optionally at the rootnode-xpath.\r\n-OR- By specifying a source-filepath, an external document will be used.\r\nNOTICE: An XSLT may be embedded inline as well by NOT specifying a filepath,\r\n\t\tand instead including an <inline> element with a full XSLT contained\r\nExample:\r\n\t\t<transform>\r\n\t\t\t<params rootnode-xpath=\"/datacloud/xpath\"\r\n\t\t\t<inline>\r\n\t\t\t\t<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org...\">\r\n\t\t\t\t...\r\n\t\t\t\t</xsl:stylesheet>\r\n\t\t\t</inline>\r\n\t\t</transform>\r\n\r\n==============================================================================\r\n\"zip\" - (aka zip-files) Creates or Updates a ZIP file from one or more files\r\n------------------------------------------------------------------------------\r\nParams: \tfilepath=\"some-files-to-zip*.*\"\r\n\t\tdestpath=\"my-zip-file.zip\"\r\n\t\taction=\"ADD | move\"\r\n\t\toverwrite=\"true | FALSE\"\r\n\r\nIf the action is set to \"move\", the source file will be deleted once added\r\nIf overwrite is set to \"true\" then any pre-existing zip will be overwritten\r\n\r\nExample:\r\n  <zip id=\"...\" test=\"\" xpath=\"\">\r\n    <params filepath=\"\" destpath=\"\" action=\"\" overwrite=\"\" />\r\n  </zip>\r\n\r\n==============================================================================\r\n\"unzip\" - (aka unzip-files) Expands a ZIP file to a specified directory\r\n------------------------------------------------------------------------------\r\nParams: \tfilepath=\"source-file-name.zip\"\r\n\t\tdestpath=\"file-path-to-extract-into\"\r\n\r\nExample:\r\n  <unzip id=\"...\" test=\"\" xpath=\"\">\r\n    <params filepath=\"\" destpath=\"\" />\r\n  </unzip>\r\n\r\n==============================================================================\r\n\"list-zip\" - (aka read-zip) Lists the contents of a ZIP file\r\n------------------------------------------------------------------------------\r\nParams: \tfilepath=\"source-file-name.zip\"\r\n\r\nExample:\r\n  <list-zip id=\"...\" test=\"\" xpath=\"\">\r\n    <params filepath=\"\" />\r\n  </list-zip>\r\n\r\n==============================================================================\r\n\"x2tl\" - Apply an {{ X2 Templating Language }} Template against the datacloud\r\n------------------------------------------------------------------------------\r\nParams: \texecute-results=\"true|FALSE\"\r\nTemplate: (The first element in the commend that is NOT the params)\r\n\r\nExample:\r\n  <x2tl id=\"...\" test=\"\" xpath=\"\">\r\n    <params execute-results=\"\" />\r\n    <results>{{ Template }}</results>\r\n  </x2tl>\r\n\r\n==============================================================================\r\n\r\n\r\n\r\n\r\nADDITIONAL TECH NOTES\r\n\r\nEach Run node may provide a \"for-each\" attribute that specifies an\r\nXPath to a subset of nodes to be run against.  The commands in the Run node\r\nwill then loop for each node within the for-each context.  \r\nNOTICE: When using for-each looping, all \"xpath\" parameter types will refer\r\nto the current node context, whereas traditionally, they are relative from\r\nthe root node.\r\n\r\nEach execution may have 'id', 'test' or 'xpath' attributes.\r\n'id' will be carried forward to the run-time datacloud.\r\n'test' will use an xpath expression to determine if the command should run.\r\n'xpath' can point to a different path for the <params> node of the command.\r\n(NOTE: In the case of the <echo> command, the xpath points to the echo text.)\r\n\r\nEach command has its own parameters, which are held in the params node.\r\nThe params shown above are given as attributes of the same name.\r\nEach attribute may have a paired attribute suffixed with \"_type\".\r\nA \"_type\" attribute may have the values: \"literal\" or \"xpath\" which indicate\r\nhow the attribute in question should be valued.\r\nFurther, if a \"xpath\" is specified, then an additional \"_format\" attribute\r\nmay be provided as well.\r\nA \"_format\" attribute may have the values: \"text\", \"xml\", or \"xml-outer\" to\r\nindicate the format of the value that should be represented.\r\n\r\n\r\nThis program recognizes the following command-line parameters:\r\n\r\n\t/?\t - Show this help screen\r\n\t/help\t - (ditto)\r\n\t/debug\t - Show step-by-step debug statements during processing\r\n\t/xml\t - The next parameter will be the path to the config XML.\r\n\t/console - Use the console stdin for the config XML.\r\n\r\n");
        
        }
        #endregion

        #endregion

        #region [ RUNNERS ]

        #region [ RunCommand ]
        /*
             *          <command id="..." test="" xpath="">        <!-- xpath = if given then look here for the command node (from transform), otherwise, this is the command node -->
             *              <params wait-for-exit="true" use-shell="true" working-directory="C:\" command="notepad.exe" arguments="C:\boot.ini" style="min|max|hidden|normal"/>
             *          </command>
         */
        private static XmlNode RunCommand(XmlNode pCommand, XmlNode pRun)
        {
            try
            {
                XmlNode nodeCommand = GetRealNode(pCommand);
                XmlNode nodeParams = GetParamsNode(nodeCommand);

                bool waitForExit = (GetParamValue(nodeParams, "wait-for-exit", "true").ToLower() == "true");
                bool useShell = (GetParamValue(nodeParams, "use-shell", "true").ToLower() == "true");
                string workingDirectory = GetParamValue(nodeParams, "working-directory", String.Empty);
                string command = GetParamValue(nodeParams, "command", String.Empty);
                string style = GetParamValue(nodeParams, "style", String.Empty);
                bool captureStdIn = (GetParamValue(nodeParams, "capture-stdin", "false").ToLower() == "true");
                bool captureStdOut = (GetParamValue(nodeParams, "capture-stdout", "false").ToLower() == "true");
                bool captureStdErr = (GetParamValue(nodeParams, "capture-stderr", "false").ToLower() == "true");

                // get baseline arguments as well as any arguments found in the param node
                string arguments = GetParamValue(nodeParams, "arguments", String.Empty);

                XmlNodeList additionalArguments = nodeParams.SelectNodes("arg");
                foreach (XmlNode additionalArgument in additionalArguments)
                {
                    if (PerformTest(additionalArgument))
                    {

                        string thisArgument = GetParamValueFromNode(additionalArgument, String.Empty);
                        if (thisArgument.Length != 0)
                            arguments += (((arguments.Length != 0) && (GetXmlAttribute(additionalArgument, "no-preceding-whitespace", "false").ToLower() == "false")) ? (GetXmlAttribute(additionalArgument, "separator-character", " ")) : String.Empty) + thisArgument;
                    }
                }

                System.Diagnostics.Process proc; // Declare New Process
                System.Diagnostics.ProcessStartInfo procInfo = new System.Diagnostics.ProcessStartInfo(); // Declare New Process Starting Information

                procInfo.UseShellExecute = useShell;  //If this is false, only .exe's can be run.
                procInfo.WorkingDirectory = workingDirectory; //execute notepad from the C: Drive
                procInfo.FileName = command; // Program or Command to Execute.
                procInfo.Arguments = arguments; //Command line arguments.
                switch(style)
                {
                    case "min": procInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized; break;
                    case "max": procInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Maximized; break;
                    case "hidden": procInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden; break;
                    default: procInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal; break;
                }

                string stdin = String.Empty;
                if (captureStdIn)
                {
                    XmlNode nodeStdIn = nodeParams.SelectSingleNode("stdin");

                    if (nodeStdIn != null)
                    {
                        stdin = GetParamValueFromNode(nodeStdIn, String.Empty);

                    }
                    else
                    {
                        throw new ApplicationException("Standard Input not defined.\nWhen capturing standard input, you must specify a valid source with a \"std\" node.\n");
                    }

                }

                SetXmlAttribute(pRun, "use-shell", useShell.ToString().ToLower());
                SetXmlAttribute(pRun, "working-directory", workingDirectory);
                SetXmlAttribute(pRun, "command", command);
                SetXmlAttribute(pRun, "arguments", arguments);
                SetXmlAttribute(pRun, "style", style);
                SetXmlAttribute(pRun, "capture-stdout", captureStdOut.ToString().ToLower());
                SetXmlAttribute(pRun, "capture-stderr", captureStdErr.ToString().ToLower());

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                procInfo.RedirectStandardInput = captureStdIn;
                procInfo.RedirectStandardOutput = captureStdOut;
                procInfo.RedirectStandardError = captureStdErr;

                if (m_bdog.debug)
                    Console.WriteLine("...starting new process...");

                proc = System.Diagnostics.Process.Start(procInfo);


                if (useShell && captureStdIn)
                {
                    StreamWriter streamStdIn = proc.StandardInput;
                    streamStdIn.AutoFlush = true;
                    streamStdIn.Write(stdin);
                    streamStdIn.Close();

                    if (m_bdog.debug)
                        Console.WriteLine("...completed sending standard input...");

                }

                if (captureStdOut)
                {
                    string captureStdOutFormat = GetParamValue(nodeParams, "capture-stdout-format", "text");
                    SetXmlAttribute(pRun, "capture-stdout-format", captureStdOutFormat);

                    if (captureStdOutFormat.ToLower() == "xml")
                    {
                        try
                        {
                            XmlDocument stdoutXml = new XmlDocument();
                            stdoutXml.Load(proc.StandardOutput);
                            XmlNode stdoutNode = pRun.OwnerDocument.CreateElement("stdout");
                            pRun.AppendChild(stdoutNode);
                            CopyNodes(stdoutNode, stdoutXml.DocumentElement);
                        }
                        catch(Exception xmlE)
                        {
                            throw new Exception("Error attempting to capture STDOUT as XML",xmlE);
                        }
                    }
                    else // text
                    {
                        LoadStreamData(pRun, "stdout", proc.StandardOutput);
                    }
                }

                if (m_bdog.debug)
                    Console.WriteLine("...completed capturing standard output...");

                if (captureStdErr)
                    LoadStreamData(pRun, "stderr", proc.StandardError);

                if (m_bdog.debug)
                    Console.WriteLine("...completed capturing standard error...");

                if (waitForExit)
                    proc.WaitForExit(); // Waits for the process to end. (ie. when user closes it down)

                if (m_bdog.debug)
                    Console.WriteLine("...completed waiting for process...");

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                SetXmlAttribute(pRun, "process-id", proc.Id.ToString());
                try { SetXmlAttribute(pRun, "process-exited", proc.HasExited.ToString().ToLower()); }
                catch (System.Exception ) { }
                try { SetXmlAttribute(pRun, "exit-code", proc.ExitCode.ToString()); }
                catch (System.Exception ) { }

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        
        }
        #endregion



        #region [ RunSetBytes ]
        /*
         *          <set-bytes id="..." test="" xpath=""/>
         */
        private static XmlNode RunSetBytes(XmlNode pBytes, XmlNode pRun)
        {
            try
            {
                XmlNode nodeBytes = GetRealNode(pBytes);

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                if (pBytes.Attributes["format"] != null)
                {
                    string format = pBytes.Attributes["format"].Value.ToLower();
                    SetXmlAttribute(pRun, "format", format);

                    string newValue = (format == "text") ? nodeBytes.InnerText : nodeBytes.InnerXml;

                    if (newValue.Trim().Length != 0)
                    {
                        m_bdog.bytes = Encoding.ASCII.GetBytes(newValue);
                    }
                    else
                    {
                        m_bdog.bytes = new byte[0];
                    }
                }

                SetXmlAttribute(pRun, "length", m_bdog.bytes.Length.ToString());

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion


        #region [ RunUnzipFiles ]
        /*
         *          <unzip-files id="..." test="" xpath="">
         *             <params filepath="" destpath="" />
         *           </unzip>
         */
        private static XmlNode RunUnzipFiles(XmlNode pUnzip, XmlNode pRun, bool pReadOnly)
        {
            try
            {
                XmlNode nodeUnzip= GetRealNode(pUnzip);
                XmlNode nodeParams = GetParamsNode(nodeUnzip);

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());
                SetXmlAttribute(pRun, "read-only", pReadOnly ? "true" : "false");

                string filepath = GetParamValue(nodeParams, "filepath", String.Empty);
                SetXmlAttribute(pRun, "filepath", filepath);

                if (filepath.Trim().Length != 0)
                {
                    string destpath = GetParamValue(nodeParams, "destpath", Directory.GetCurrentDirectory());

                    if (!pReadOnly)
                    {
                        SetXmlAttribute(pRun, "destpath", destpath);
                    }

                    ZipArchive zip = ZipFile.OpenRead(filepath);
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string relPath = (entry.FullName!= entry.Name)?entry.FullName.Substring(0,entry.FullName.Length-entry.Name.Length):String.Empty;
                        if (!Directory.Exists(String.Format("{0}\\{1}", destpath, relPath)))
                        {
                            Directory.CreateDirectory(String.Format("{0}\\{1}", destpath, relPath));
                        }

                        XmlElement fileEl = pRun.OwnerDocument.CreateElement("entry");

                        fileEl.SetAttribute("name", entry.Name);
                        fileEl.SetAttribute("full-name", entry.FullName);
                        fileEl.SetAttribute("size", entry.Length.ToString());

                        pRun.AppendChild(fileEl);

                        // This method is shared for list/read and unzip.
                        // If ReadOnly, then do NOT extract file.  Otherwise, continue on
                        if (!pReadOnly)
                        {
                            string destFullPath = String.Format("{0}\\{1}", destpath, entry.FullName);

                            try
                            {
                                entry.ExtractToFile(destFullPath,true);
                            }
                            catch (Exception extE)
                            {
                                throw new Exception(String.Format("Unable to Extract file: {0}", destFullPath), extE);
                            }
                        }
                    }
                    zip.Dispose();

                }
                else
                {
                    throw new Exception("No source filepath provided.");
                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }

        #endregion

        #region [ RunZipFiles ]
        /*
         *          <zip-files id="..." test="" xpath="">
         *             <params filepath="" destpath="" overwrite="" action="" />
         *           </zip>
         */
        private static XmlNode RunZipFiles(XmlNode pZip, XmlNode pRun)
        {
            try
            {
                XmlNode nodeZip = GetRealNode(pZip);
                XmlNode nodeParams = GetParamsNode(nodeZip);

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                string filepath = GetParamValue(nodeParams, "filepath", String.Empty);
                SetXmlAttribute(pRun, "filepath", filepath);

                string action = GetParamValue(nodeParams, "action", "add").ToLower();
                SetXmlAttribute(pRun, "action", action);
                bool deleteAfterZip = (action == "move");

                string overwrite = GetParamValue(nodeParams, "overwrite", "false").ToLower();
                SetXmlAttribute(pRun, "overwrite", overwrite);
                bool overwriteExisting = (overwrite == "true");

                if (filepath.Trim().Length != 0)
                {
                    string destpath = GetParamValue(nodeParams, "destpath", String.Format(@"{0}\Archive.zip", Directory.GetCurrentDirectory()));
                    SetXmlAttribute(pRun, "destpath", destpath);

                    ZipArchive zip;

                    if (File.Exists(destpath))
                    {
                        if (overwriteExisting)
                        {
                            File.Delete(destpath);
                            zip = ZipFile.Open(destpath, ZipArchiveMode.Create); 
                        }
                        else
                        {
                            zip = ZipFile.Open(destpath, ZipArchiveMode.Update); 
                        }
                    }
                    else
                    {
                        zip = ZipFile.Open(destpath, ZipArchiveMode.Create); 
                    }


                    if (File.Exists(filepath))
                    {
                        // This path is a file
                        string baseDir = Directory.GetParent(filepath).FullName;
                        ZipHelperCompressFile(pRun, zip, filepath, baseDir, deleteAfterZip);
                    }
                    else if (Directory.Exists(filepath))
                    {
                        // This path is a directory
                        string baseDir = filepath;
                        ZipHelperCompressDirectory(pRun, zip, filepath, baseDir, deleteAfterZip);
                    }
                    else
                    {
                        throw new Exception(String.Format("ERROR: {0} is not a valid file or directory.", filepath));
                    }

                    zip.Dispose();
                }
                else
                {
                    throw new Exception("No source filepath provided.");
                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }

        #endregion

        #region [ ZipHelperCompressFile ]
        private static void ZipHelperCompressFile(XmlNode pRun, ZipArchive pZip, string pFilepath, string pBaseDirectory, bool pDeleteAfterZip)
        {
            try
            {
                string relPath = pFilepath.Substring(pBaseDirectory.Length + 1);

                pZip.CreateEntryFromFile(pFilepath, relPath);
                
                XmlElement xFile = pRun.OwnerDocument.CreateElement("File");
                xFile.SetAttribute("filename", pFilepath);

                xFile.SetAttribute("entry", relPath);


                if (pDeleteAfterZip)
                {
                    File.Delete(pFilepath);
                    xFile.SetAttribute("action", "moved");
                }
                else
                {
                    xFile.SetAttribute("action", "added");
                }


                pRun.AppendChild(xFile);
            }
            catch (System.Exception pException)
            {
                throw new Exception(String.Format("Error processing Zip of {0}", pFilepath), pException);
            }

        }
        #endregion

        #region [ ZipHelperCompressDirectory ]
        private static void ZipHelperCompressDirectory(XmlNode pRun, ZipArchive pZip, string pFilepath, string pBaseDirectory, bool pDeleteAfterZip)
        {
            try
            {
                // Process the list of files found in the directory.
                string[] fileEntries = Directory.GetFiles(pFilepath);
                foreach (string fileName in fileEntries)
                    ZipHelperCompressFile(pRun, pZip, fileName, pBaseDirectory, pDeleteAfterZip);

                // Recurse into subdirectories of this directory.
                string[] subdirectoryEntries = Directory.GetDirectories(pFilepath);
                foreach (string subdirectory in subdirectoryEntries)
                    ZipHelperCompressDirectory(pRun, pZip, subdirectory, pBaseDirectory, pDeleteAfterZip);
            }
            catch (System.Exception pException)
            {
                throw new Exception(String.Format("Error processing Zip of {0}", pFilepath), pException);
            }

        }


        #endregion



        #region [ RunX2TL ]
        /*
         *          <x2tl id="..." test="" xpath="">
         *             <params template="" root="" execute-results="" />
         *           </x2tl>
         */
        private static XmlNode RunX2TL(XmlNode pX2TL, XmlNode pRun)
        {
            try
            {
                XmlNode nodeX2TL = GetRealNode(pX2TL);
                XmlNode nodeParams = GetParamsNode(nodeX2TL);
                XmlNode nodeSource = pRun.OwnerDocument.DocumentElement;
                XmlNode nodeTemplate = nodeX2TL.SelectSingleNode("*[not(self::params)][1]");
                XmlNode nodeSupporting = null;

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                bool executeResults = (GetParamValue(nodeParams, "execute-results", "false") == "true");


                if (nodeTemplate != null)
                {
                    /*
                    string filepath = GetParamValue(nodeParams, "filepath", String.Empty);
                    SetXmlAttribute(pRun, "filepath", filepath);

                    string action = GetParamValue(nodeParams, "action", "add").ToLower();
                    SetXmlAttribute(pRun, "action", action);
                    */

                    RelWare.X2TL x2 = new RelWare.X2TL();
                    string result = x2.TransformNode(nodeSource, nodeTemplate, nodeSupporting);
                    StringBuilder sb = new StringBuilder("<result>");
                    sb.Append(result);
                    sb.Append("</result>");

                    XmlDocument resultDoc = new XmlDocument();
                    resultDoc.LoadXml(sb.ToString());
                    XmlElement resultFrag = resultDoc.DocumentElement;

                    CopyNodes(pRun, resultFrag);

                    SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());


                    if (executeResults)
                    {
                        ProcessBatch((XmlNode)resultFrag);
                    }

                    return pRun;
                }
                else
                {
                    throw new Exception("No template found");
                }
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }

        #endregion


        #region [ RunClearBytes ]
        /*
         *          <clear-bytes id="..." test="" xpath=""/>
         */
        private static XmlNode RunClearBytes(XmlNode pBytes, XmlNode pRun)
        {
            try
            {
                XmlNode nodeBytes = GetRealNode(pBytes);
                XmlNode nodeParams = GetParamsNode(nodeBytes);

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                m_bdog.bytes = new byte[0];

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion

        #region [ RunCompressBytes ]
        /*
         *          <compress-bytes id="..." test="" xpath=""/>
         */
        private static XmlNode RunCompressBytes(XmlNode pBytes, XmlNode pRun)
        {
            try
            {
                XmlNode nodeBytes = GetRealNode(pBytes);

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                if (pBytes.Attributes["format"] != null)
                {
                    string format = pBytes.Attributes["format"].Value.ToLower();
                    SetXmlAttribute(pRun, "format", format);

                    string newValue = (format == "text") ? nodeBytes.InnerText : nodeBytes.InnerXml;

                    if (newValue.Trim().Length != 0)
                    {
                        m_bdog.bytes = Encoding.ASCII.GetBytes(newValue);
                    }
                }

                long origLength = m_bdog.bytes.Length;
                SetXmlAttribute(pRun, "original-length", origLength.ToString());

                try
                {
                    MemoryStream output = new MemoryStream();
                    using (DeflateStream dstream = new DeflateStream(output, CompressionLevel.Optimal))
                    {
                        dstream.Write(m_bdog.bytes, 0, m_bdog.bytes.Length);
                    }
                    m_bdog.bytes = output.ToArray();

                    SetXmlAttribute(pRun, "compressed-length", m_bdog.bytes.Length.ToString());
                    SetXmlAttribute(pRun, "compression-ratio", ((Math.Floor(((double)m_bdog.bytes.Length / (double)origLength) * 10000.0)) / 10000.0).ToString());
                }
                catch(Exception compExp)
                {
                    throw new Exception("Error compressing bytes array", compExp);
                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion

        #region [ RunDecompressBytes ]
        /*
         *          <decompress-bytes id="..." test="" xpath=""/>
         */
        private static XmlNode RunDecompressBytes(XmlNode pBytes, XmlNode pRun)
        {
            try
            {
                XmlNode nodeBytes = GetRealNode(pBytes);
                XmlNode nodeParams = GetParamsNode(nodeBytes);

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                if (pBytes.Attributes["format"] != null)
                {
                    string format = pBytes.Attributes["format"].Value.ToLower();
                    SetXmlAttribute(pRun, "format", format);

                    string newValue = (format == "text") ? nodeBytes.InnerText : nodeBytes.InnerXml;

                    if (newValue.Trim().Length != 0)
                    {
                        m_bdog.bytes = Encoding.ASCII.GetBytes(newValue);
                    }
                }
                
                SetXmlAttribute(pRun, "original-length", m_bdog.bytes.Length.ToString());

                try
                {
                    MemoryStream input = new MemoryStream(m_bdog.bytes);
                    MemoryStream output = new MemoryStream();
                    using (DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress))
                    {
                        dstream.CopyTo(output);
                    }
                    m_bdog.bytes = output.ToArray();

                    SetXmlAttribute(pRun, "decompressed-length", m_bdog.bytes.Length.ToString());
                }
                catch (Exception decompExp)
                {
                    throw new Exception("Error decompressing bytes array", decompExp);
                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion

        #region [ RunPause ]
        /*
         *          <pause id="..." test="" xpath="">
         *              <params method="console|msgbox|timer" timeout="" caption="" text="" buttons=""/> <!-- method="console" requires keyboard input, method="timer" requires timeout attribute to be number of milliseconds to wait, method="msgbox" will show a MessageBox with appropriate text, caption and buttons -->
         *          </pause>
         */
        private static XmlNode RunPause(XmlNode pPause, XmlNode pRun)
        {
            try
            {
                XmlNode nodePause = GetRealNode(pPause);
                XmlNode nodeParams = GetParamsNode(nodePause);

                string method = GetParamValue(nodeParams, "method", "console").ToLower();
                string caption = GetParamValue(nodeParams, "caption", String.Empty);
                string text = GetParamValue(nodeParams, "text", String.Empty);
                string buttons = GetParamValue(nodeParams, "buttons", String.Empty);
                string icon = GetParamValue(nodeParams, "icon", String.Empty);

                SetXmlAttribute(pRun, "method", method);
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                DialogResult dResult;

                switch (method)
                {
                    case "console":
                        string consoletype = GetParamValue(nodeParams, "console-type", "line").ToLower();
                        SetXmlAttribute(pRun, "console-type", consoletype);
                        string consoleInput = String.Empty;

                        switch (consoletype)
                        {
                            case "file":
                                consoleInput = System.Console.In.ReadToEnd();
                                break;

                            //case "line":
                            default:
                                consoleInput = Console.ReadLine();
                                break;
                        }

                        //XmlCDataSection cdata = m_bdog.runXml.CreateCDataSection(consoleInput);
                        //pRun.AppendChild(cdata);
                        AppendRawText(pRun, consoleInput);
                        break;

                    case "msgbox":
                        string dr = DialogResult2String(System.Windows.Forms.MessageBox.Show(text,caption, String2MessageBoxButtons(buttons),String2MessageBoxIcon(icon)));
                        SetXmlAttribute(pRun, "dialog-result", dr);

                        break;

                    case "file-dialog":

                        OpenFileDialog openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
                        openFileDialog1.InitialDirectory = Directory.GetCurrentDirectory();
                        openFileDialog1.FileName = null;
  
                        // Display the openFile dialog.
                        dResult = openFileDialog1.ShowDialog();

                        SetXmlAttribute(pRun, "dialog-result", DialogResult2String(dResult));

                        // OK button was pressed.
                        if (dResult == DialogResult.OK)
                        {
                            SetXmlAttribute(pRun, "dialog-filename", openFileDialog1.FileName);
                        }
                        break;

                    case "folder-dialog":

                        FolderBrowserDialog folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();

                        // Display the folder browser dialog.
                        dResult = folderBrowserDialog1.ShowDialog();

                        SetXmlAttribute(pRun, "dialog-result", DialogResult2String(dResult));

                        // OK button was pressed.
                        if (dResult == DialogResult.OK)
                        {
                            SetXmlAttribute(pRun, "dialog-path", folderBrowserDialog1.SelectedPath);
                        }
                        break;

                    case "timer":
                        int timeout = int.Parse(GetParamValue(nodeParams, "timeout", "0"));
                        SetXmlAttribute(pRun, "timeout", timeout.ToString());
                        Thread.Sleep(timeout);
                        break;
                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }
        

        #endregion

        #region [ RunDumpLog ]
        /*
         *          <dump-log test="" xpath="">             <!-- filepath = the path to be used to dump the current run, if filepath="", then it will dump to Console -->
         *              <params filepath=""/>
         *          </dump-log>
         */
        private static XmlNode RunDumpLog(XmlNode pDumpLog, XmlNode pRun)
        {
            try
            {
                XmlNode nodeDumpLog = GetRealNode(pDumpLog);
                XmlNode nodeParams = GetParamsNode(nodeDumpLog);

                string filepath = String.Empty;
                bool includeExternalData = false;
                string rootPath = "/";
                
                if (nodeParams != null)
                {
                    filepath = GetParamValue(nodeParams, "filepath", String.Empty);
                    includeExternalData = (GetParamValue(nodeParams, "include-external-data", "false").ToLower() == "true");
                    rootPath = GetParamValue(nodeParams, "rootnode-xpath", "/");
                }

                SetXmlAttribute(pRun, "filepath", filepath);
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());
                SetXmlAttribute(pRun, "include-external-data", (includeExternalData ? "true" : "false"));
                SetXmlAttribute(pRun, "rootnode-xpath", rootPath);

                XmlDocument dumpDoc = new XmlDocument();
                dumpDoc.LoadXml(m_bdog.runXml.SelectSingleNode(rootPath).OuterXml);

                if ( ! includeExternalData)
                {
                    XmlElement root = dumpDoc.DocumentElement;
                    XmlNodeList nlExternalData = root.SelectNodes("/*/Data[@src]");
                    foreach (XmlNode n in nlExternalData)
                    {
                        foreach (XmlNode delMe in n.SelectNodes("*|text()|comment()"))
                        {
                            n.RemoveChild(delMe);
                        }
                    }
                }


                if (filepath.Length != 0)
                {
                    dumpDoc.Save(filepath);
                }
                else
                {
                    Console.WriteLine(dumpDoc.OuterXml.ToString());
                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }
        #endregion


        #region [ RunLoadConfig ]
        /*
         *          <load-config test=""/>
         */
        private static XmlNode RunLoadConfig(XmlNode pLoadConfig, XmlNode pRun)
        {
            try
            {
                XmlNode nodeLoadConfig = GetRealNode(pLoadConfig);
                XmlElement elRun = (XmlElement)pRun;

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                //DeepCopy(pLoadConfig.OwnerDocument.DocumentElement, ref elRun);
                CopyNodes(elRun, pLoadConfig.OwnerDocument.DocumentElement, false);

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }
        #endregion

        #region [ RunCompress ]
        /*
         *          <compress test="">
         *             <params ....
         *           </compress>
         */
        private static XmlNode RunCompress(XmlNode pCompress, XmlNode pRun)
        {
            try
            {
                XmlNode nodeCompress = GetRealNode(pCompress);
                XmlNode nodeParams = GetParamsNode(nodeCompress);
                XmlElement elRun = (XmlElement)pRun;
                string mode = GetParamValue(nodeParams, "mode", "create");
                string filepath = GetParamValue(nodeParams, "filepath", String.Empty);
                string sourcefilepath = GetParamValue(nodeParams, "source-filepath", String.Empty);
                string saveasfilename = GetParamValue(nodeParams, "saveas-filepath", String.Empty);
                bool deleteAfterCompress = (GetParamValue(nodeParams, "delete-onsuccess", "false").ToLower() == "true");
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                SetXmlAttribute(pRun, "mode", mode);
                SetXmlAttribute(pRun, "zip-filepath", filepath);
                SetXmlAttribute(pRun, "source-filepath", sourcefilepath);
                SetXmlAttribute(pRun, "saveas-filepath", saveasfilename);
                SetXmlAttribute(pRun, "delete-onsuccess", (deleteAfterCompress?"true":"false"));

                using (ZipArchive zip = ZipFile.Open(filepath, ((mode.ToLower()=="update")?ZipArchiveMode.Update:ZipArchiveMode.Create)))
                {
                    zip.CreateEntryFromFile(sourcefilepath, saveasfilename);
                }

                if (deleteAfterCompress)
                {
                    Directory.Delete(sourcefilepath);
                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }
        #endregion

        #region [ RunNewGUID ]
        /*
         *          <new-id test=""/>
         *          <guid test=""/>
         */
        private static XmlNode RunNewGUID(XmlNode pNewGUID, XmlNode pRun)
        {
            try
            {
                XmlNode nodeNewGUID = GetRealNode(pNewGUID);
                XmlElement elRun = (XmlElement)pRun;
                XmlNode nodeParams = GetParamsNode(nodeNewGUID);

                int numIDs = Int32.Parse( GetParamValue(nodeParams,"count","1"));

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                for (int i=0;i<numIDs;i++)
                {
                    XmlElement elGUID = pRun.OwnerDocument.CreateElement("GUID");
                    elGUID.SetAttribute("position", (i + 1).ToString());
                    elGUID.InnerText = System.Guid.NewGuid().ToString();
                    pRun.AppendChild(elGUID);
                }
                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }
        #endregion


        #region [ RunEcho ]
        /*
             *          <echo id="..." test="" xpath="..">                     <!-- Contents will be echoed to Console -->
             *              { any text to be output }
             *          </echo>
         */
        private static XmlNode RunEcho(XmlNode pEcho, XmlNode pRun)
        {
            try
            {
                XmlNode nodeEcho = GetRealNode(pEcho);
                //XmlNode nodeParams = GetParamsNode(nodeEcho);

                string echoText = String.Empty;

                bool newLine = (GetParamValue(nodeEcho, "new-line", "true").ToLower() == "true");
                SetXmlAttribute(pRun, "new-line", newLine.ToString().ToLower());

                //XmlNodeList nlEchoTexts = nodeEcho.SelectNodes("text()");
                //foreach (XmlNode nodeEchoText in nlEchoTexts)
                //    echoText += nodeEchoText.InnerText;

                bool waitingNewLine = false;

                XmlNodeList nodeEchoFragments = nodeEcho.SelectNodes("string|line|text()");
                foreach (XmlNode nodeEchoFragment in nodeEchoFragments)
                {
                    if (nodeEchoFragment.NodeType == XmlNodeType.Text)
                    {
                        echoText += nodeEchoFragment.InnerText;
                        waitingNewLine = true;
                    }
                    else
                    {
                        string fragmentText = GetParamValueFromNode(nodeEchoFragment, String.Empty);
                        switch (nodeEchoFragment.Name)
                        {
                            case "line":
                                if (waitingNewLine)
                                {
                                    echoText += "\n";
                                    waitingNewLine = false;
                                }

                                if (fragmentText.Length != 0)
                                {
                                    echoText += fragmentText;
                                }

                                echoText += "\n";
                                break;

                            default:
                                if (fragmentText.Length != 0)
                                    echoText += (((echoText.Length != 0) && (waitingNewLine) && (GetXmlAttribute(nodeEchoFragment, "no-preceding-whitespace", "false").ToLower() == "false")) ? (GetXmlAttribute(nodeEchoFragment, "separator-character", " ")) : String.Empty) + fragmentText;
                                waitingNewLine = true;
                                break;
                        }
                    }
                }

                SetXmlAttribute(pRun, "text", echoText);
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                SetXmlAttribute(pRun, "write-to-console", (m_bdog.writeToConsole?"true":"false"));
                SetXmlAttribute(pRun, "write-to-file", (m_bdog.writeToFile ? "true" : "false"));

                if (m_bdog.writeToFile)
                {

                    if ((m_bdog.outputStream != null) && (m_bdog.outputStreamWriter != null))
                    {
                        m_bdog.outputStreamWriter.Write(echoText);
                        if (newLine)
                        {
                            m_bdog.outputStreamWriter.WriteLine(String.Empty);
                        }
                    }
                    else
                    {
                        throw new Exception("Error attempting to output to file with no open stream");
                    }
                    
                }
                if (m_bdog.writeToConsole)
                {
                    Console.Write(echoText);

                    if (newLine)
                    {
                        Console.WriteLine(String.Empty);
                    }
                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }
        #endregion


        #region [ RunOutput ]
        /*
             *          <output id="..." test="" xpath="..">
             *              <params method="console | file | close" filepath="someoutputfile.txt" />
             *          </output>
         */
        private static XmlNode RunOutput(XmlNode pOutput, XmlNode pRun)
        {
            try
            {
                XmlNode nodeOutput = GetRealNode(pOutput);
                XmlNode nodeParams = GetParamsNode(nodeOutput);

                string method = GetParamValue(nodeParams, "method", "console");
                string filepath = GetParamValue(nodeParams, "filepath", String.Empty);
                string filemethod = GetParamValue(nodeParams, "filemethod", "create");

                SetXmlAttribute(pRun, "method", method);
                if (filepath!=String.Empty)
                {
                    SetXmlAttribute(pRun, "filepath", filepath);
                }
                if (filemethod != String.Empty)
                {
                    SetXmlAttribute(pRun, "filemethod", filemethod);
                }

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                // in all cases, console, file, or close, we will be closing any active file stream, but for readability, and future flexibility, we have it switch out                

                switch (method.ToLower())
                {
                    case "file":
                    case "both":
                        CloseOutputStream();    
                        OpenOutputStream(filepath, filemethod);

                        m_bdog.writeToFile = true;
                        m_bdog.writeToConsole = (method.ToLower() == "both");
                        break;

                    case "close":
                    case "console":
                    default:
                        CloseOutputStream();    

                        m_bdog.writeToFile = false;
                        m_bdog.writeToConsole = true;
                        break;
                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }
        #endregion

        private static void OpenOutputStream(string filepath, string filemethod)
        {
            m_bdog.outputStream = File.Open(filepath, (filemethod.ToLower() == "append" ? FileMode.Append : FileMode.Create));
            m_bdog.outputStreamWriter = new StreamWriter(m_bdog.outputStream, Encoding.ASCII, 32768, true);
        }


        private static void CloseOutputStream()
        {
            if (m_bdog.outputStreamWriter != null)
            {
                m_bdog.outputStreamWriter.Flush();
                m_bdog.outputStreamWriter.Close();
                m_bdog.outputStreamWriter.Dispose();
                m_bdog.outputStreamWriter = null;
            }
            if (m_bdog.outputStream != null)
            {
                m_bdog.outputStream.Flush();
                m_bdog.outputStream.Close();
                m_bdog.outputStream.Dispose();
                m_bdog.outputStream = null;
            }

        }


        #region [ RunStopIf ]
        /*
         *          <stop-if id="..." test="" xpath="">         <!-- method determines what type of code should be returned by this program run -->
         *              <params method="this-run|all"/>
         *          </stop-if>
         */
        private static XmlNode RunStopIf(XmlNode pStopIf, XmlNode pRun)
        {
            try
            {
                XmlNode nodeStopIf = GetRealNode(pStopIf);
                XmlNode nodeParams = GetParamsNode(nodeStopIf);

                string method = GetParamValue(nodeParams, "method", "all");
                string exitCode = GetParamValue(nodeParams, "exit-code", "0");

                SetXmlAttribute(pRun, "method", method);
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                if (method == "this-run")
                {
                    m_bdog.skipping = true;
                    m_bdog.skipMethod = "end";
                }
                else
                {
                    SetXmlAttribute(pRun, "exit-code", exitCode);

                    m_bdog.stopped = true;
                    m_bdog.stopMethod = method;
                    m_bdog.exitCode = System.Convert.ToInt32(exitCode);
                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion

        #region [ RunSkipTo ]
        /*
         *          <skip-to id="..." test="" xpath="">         <!-- method determines what type of code should be returned by this program run -->
         *              <params method="(execution-)type|id|end" value=""/> <!-- (deprecated) nextCommandType="" nextCommandId="" end=""-->
         *          </skip-to>
         */
        private static XmlNode RunSkipTo(XmlNode pSkipTo, XmlNode pRun)
        {
            try
            {
                XmlNode nodeSkipTo = GetRealNode(pSkipTo);
                XmlNode nodeParams = GetParamsNode(nodeSkipTo);

                string method = GetParamValue(nodeParams, "method", String.Empty);
                string skipValue = GetParamValue(nodeParams, "value", String.Empty);

                // the following three attributes are deprecated and should no longer be used
                string nextCommandType = GetParamValue(nodeParams, "nextCommandType", String.Empty);
                string nextCommandId = GetParamValue(nodeParams, "nextCommandId", String.Empty);
                string endString = GetParamValue(nodeParams, "end", String.Empty);

                if (nextCommandType.Length != 0) 
                    SetXmlAttribute(pRun, "nextCommandType", nextCommandType);
                if (nextCommandId.Length != 0) 
                    SetXmlAttribute(pRun, "nextCommandId", nextCommandId);
                if (endString.Length != 0) 
                    SetXmlAttribute(pRun, "end", endString);
                
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                if (nextCommandType.Length != 0)
                {
                    m_bdog.skipMethod = "type";
                    m_bdog.skipValue = nextCommandType;
                }
                else if (nextCommandId.Length != 0)
                {
                    m_bdog.skipMethod = "id";
                    m_bdog.skipValue = nextCommandId;
                }
                else if (endString.Length != 0)
                {
                    m_bdog.skipMethod = "end";
                    m_bdog.skipValue = endString;
                }
                else if (method.Length != 0)
                {
                    m_bdog.skipMethod = method;
                    m_bdog.skipValue = skipValue;
                }

                m_bdog.skipping = (m_bdog.skipMethod.Length != 0);

                SetXmlAttribute(pRun, "skipping", (m_bdog.skipping) ? "true" : "false");
                SetXmlAttribute(pRun, "method", m_bdog.skipMethod);
                SetXmlAttribute(pRun, "value", m_bdog.skipValue);

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion


        #region [ RunSQL ]
        /*
         *          <sql id="..." test="" xpath="">
         *              <params server="" connect="" query="exec my_sproc @p1, @p2" method="recordset|xml|execute-only" row-label="row">
         *                <param name="@p1" value="1"/>
         *                <param name="@p2" value="2"/>
         *              </params>
         *          </sql>
         */
        private static XmlNode RunSQL(XmlNode pSQL, XmlNode pRun)
        {
            try
            {
                XmlNode nodeSQL = GetRealNode(pSQL);
                XmlNode nodeParams = GetParamsNode(nodeSQL);

                string server = GetParamValue(nodeParams, "server", String.Empty);
                string connect = GetParamValue(nodeParams, "connect", String.Empty);
                string query = GetParamValue(nodeParams, "query", String.Empty);
                string method = GetParamValue(nodeParams, "method", "recordset");
                string rowlabel = GetParamValue(nodeParams, "row-label", "row");
                string timeoutValue = GetParamValue(nodeParams, "timeout", "0");

                int timeout = 0;

                try
                {
                    timeout = Int32.Parse(timeoutValue);
                }
                catch(Exception)
                {
                    /* don't care */
                }

                SetXmlAttribute(pRun, "server", server);
                SetXmlAttribute(pRun, "connect", connect);
                SetXmlAttribute(pRun, "query", query);
                SetXmlAttribute(pRun, "method", method);
                SetXmlAttribute(pRun, "timeout", timeoutValue);

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                using (SqlConnection sqlConn = new SqlConnection())
                {
                    sqlConn.ConnectionString = String.Format("Data source={0};{1}", server, connect);
                    sqlConn.Open();

                    SqlCommand cmd = new SqlCommand(query, sqlConn);

                    cmd.CommandTimeout = timeout;

                    foreach(XmlNode nodeParam in nodeParams.SelectNodes("param"))
                    {
                        if (PerformTest(nodeParam))
                        {
                            XmlElement elParam = (XmlElement)nodeParam;
                            string paramName = GetParamValue(nodeParam, "name", String.Empty);
                            string paramFormat = GetParamValue(nodeParam, "format", "text");
                            if (paramFormat.ToLower() == "bytes")
                            {
                                cmd.Parameters.Add(new SqlParameter(paramName, m_bdog.bytes));
                            }
                            else
                            {
                                string paramValue = GetParamValue(nodeParam, "value", String.Empty);
                                if (paramValue != String.Empty)
                                {
                                    cmd.Parameters.Add(new SqlParameter(paramName, paramValue));
                                }
                                else
                                {
                                    cmd.Parameters.Add(new SqlParameter(paramName, DBNull.Value));
                                }
                            }
                        }
                    }


                    switch(method.ToLower())
                    {
                        case "execute-only":
                        case "exec-only":
                            cmd.ExecuteNonQuery();
                            break;

                        case "bytes":
                            m_bdog.bytes = (cmd.ExecuteScalar() as byte[]);
                            break;

                        case "xml":
                            using (XmlReader xmlReader = cmd.ExecuteXmlReader())
                            {
                                try
                                {
                                    XmlDocument d = new XmlDocument();
                                    
                                    StringBuilder sb = new StringBuilder();
                                    sb.Append(String.Format("<{0}>", rowlabel));

                                    string outxml = null;

                                    while (xmlReader.Read())
                                    {
                                        XmlReader readOut = xmlReader.ReadSubtree();
                                        readOut.Read();
                                        outxml = readOut.ReadOuterXml();
                                        sb.Append(outxml);
                                    }

                                    sb.Append(String.Format("</{0}>", rowlabel));

                                    d.LoadXml(sb.ToString());

                                    if (d.DocumentElement != null)
                                    {
                                        CopyNodes(pRun, d.DocumentElement);
                                    }
                                }
                                catch(Exception xmlE)
                                {
                                    throw new Exception("Error attempting to load sql results as XML", xmlE);
                                }
                            }
                            break;

                        case "recordset":
                        default:
                            using (System.Data.SqlClient.SqlDataReader sqlReader = cmd.ExecuteReader())
                            {
                                // while there is another record present
                                while (sqlReader.Read())
                                {
                                    XmlElement elRow = pRun.OwnerDocument.CreateElement(rowlabel);
                                    pRun.AppendChild(elRow);

                                    for(int f=0;f<sqlReader.FieldCount;f++)
                                    {
                                        if ((sqlReader[f]!=null)&&(sqlReader[f]!=DBNull.Value))
                                        {
                                            elRow.SetAttribute((sqlReader.GetName(f)), (sqlReader[f].ToString()));
                                        }
                                    }
                                }

                            }
                            break;

                    }

                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion


        
        #region [ RunLoadFile ]
        /*
         *          <load-file id="..." test="" xpath="">
         *              <params filepath="" file-type="text|xml|base64"/>    <!-- filepath = the complete file/directory path of the file to be loaded (text will be CDATA, xml will be node and base64 will be CDATA with binary converted to Base-64 -->
         *          </load-file>
         */
        private static XmlNode RunLoadFile(XmlNode pLoadFile, XmlNode pRun)
        {
            try
            {
                XmlNode nodeLoadFile = GetRealNode(pLoadFile);
                XmlNode nodeParams = GetParamsNode(nodeLoadFile);

                string type = GetParamValue(nodeParams, "file-type", String.Empty).ToLower();
                string filepath = GetParamValue(nodeParams, "filepath", String.Empty);

                SetXmlAttribute(pRun, "filepath", filepath);
                SetXmlAttribute(pRun, "file-type", type);
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                switch (type)
                {
                    case "text":
                        //XmlCDataSection cdata = m_bdog.runXml.CreateCDataSection(String.Empty);
                        //pRun.AppendChild(cdata);

                        StreamReader objReader = new StreamReader(filepath);
                        string sLine = String.Empty;
                        string inText = String.Empty;
                        ArrayList arrText = new ArrayList();

                        bool addCRLF = false;
                        while (sLine != null)
                        {
                            if (addCRLF)
                                inText += '\n';
                            sLine = objReader.ReadLine();
                            if (sLine != null)
                                inText += sLine;
                            addCRLF = true;
                        }
                        objReader.Close();

                        AppendRawText(pRun, inText);

                        break;

                    case "xml":
                    case "xml-fragment":
                        XmlDocument loadFile = new XmlDocument();
                        try
                        {
                            loadFile.Load(filepath);

                            if (loadFile.DocumentElement != null)
                            {
                                CopyNodes(pRun, loadFile.DocumentElement);
                            }
                            else
                            {
                                throw new ApplicationException("The specified file did not contain valid XML.\n" + filepath);
                            }
                        
                        }
                        catch (System.Exception)
                        {// Possibly an XML Fragment.  Try wrappering it.

                            StringBuilder wrapperedXml = new StringBuilder("<WRAPPER>");
                            using (StreamReader fragReader = new StreamReader(filepath))
                            {
                                wrapperedXml.Append(fragReader.ReadToEnd());
                                wrapperedXml.Append("</WRAPPER>");
                            }
                            loadFile.LoadXml(wrapperedXml.ToString());

                            if ((loadFile.DocumentElement != null)&&(loadFile.DocumentElement.SelectNodes("*").Count != 0))
                            {
                                foreach (XmlNode n in loadFile.DocumentElement.SelectNodes("*"))
                                {
                                    CopyNodes(pRun, (XmlElement)n);
                                }
                            }
                            else
                            {
                                throw new ApplicationException("The specified file did not contain valid XML.\n" + filepath);
                            }

                        }


                        break;

                    case "base64":
                        //System.IO.BinaryReader br = new System.IO.BinaryReader(System.IO.File.Open(sPath, System.IO.FileMode.Open), Encoding.UTF8); 
                        // Encode and output as Base-64
                        SetXmlTextValue(pRun, "file-type of 'base64' is not yet implemented");
                        break;
                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion

        #region [ RunSaveFile ]
        /*
         *          <save-file id="..." test="" xpath="">
         *              <params filepath="" format="text|xml" rootnode-xpath=""/>    <!-- filepath = the complete file/directory path of the file to be loaded (text will be CDATA, xml will be node and base64 will be CDATA with binary converted to Base-64 -->
         *          </save-file>
         */
        private static XmlNode RunSaveFile(XmlNode pSaveFile, XmlNode pRun)
        {
            try
            {
                XmlNode nodeSaveFile = GetRealNode(pSaveFile);
                XmlNode nodeParams = GetParamsNode(nodeSaveFile);

                string format = GetParamValue(nodeParams, "format", "text").ToLower();
                string filepath = GetParamValue(nodeParams, "filepath", String.Empty);
                string rootPath = GetParamValue(nodeParams, "rootnode-xpath", "/");
                bool appendContents = (GetParamValue(nodeParams, "append-contents", "false").ToLower() == "true");

                string fileContents = String.Empty;

                SetXmlAttribute(pRun, "filepath", filepath);
                SetXmlAttribute(pRun, "format", format);
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());
                SetXmlAttribute(pRun, "rootnode-xpath", rootPath);
                SetXmlAttribute(pRun, "append-contents", (appendContents?"true":"false"));

                XmlDocument sourceXml = new XmlDocument();

                XmlNode runRootSource = m_bdog.runXml.DocumentElement.SelectSingleNode(rootPath);
                if (runRootSource != null)
                {
                    sourceXml.LoadXml(runRootSource.OuterXml);
                }
                else
                {
                    throw new ApplicationException("The requested rootnode-xpath was not valid within this run:\n" + rootPath);
                }

                switch (format)
                {
                    case "text":
                        fileContents = sourceXml.InnerText;
                        break;

                    case "xml":
                        fileContents = sourceXml.InnerXml;
                        break;

                    case "xml-outer":
                        fileContents = sourceXml.OuterXml;
                        break;
                }

                AppendRawText(pRun, fileContents);

                StreamWriter objWriter = new StreamWriter(filepath, appendContents);
                objWriter.Write(fileContents);
                objWriter.Close();

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion

        #region [ RunTransform ]
        /*
         *          <transform id="..." test="" xpath="">
         *              <params  filepath="" rootnode-xpath=""/>   <!-- filepath = the path to the transform needed to be used, rootnode-xpath = (optional) subset of runXml document -->
         *          </transform>
         */
        private static XmlNode RunTransform(XmlNode pTransform, XmlNode pRun)
        {
            try
            {
                XmlNode nodeTransform = GetRealNode(pTransform);
                XmlNode nodeParams = GetParamsNode(nodeTransform);

                string filepath = GetParamValue(nodeParams, "filepath", String.Empty);
                string sourceFilepath = GetParamValue(nodeParams, "source-filepath", String.Empty);
                string rootPath = GetParamValue(nodeParams, "rootnode-xpath", "/");

                if (rootPath.Length != 0)
                {
                    SetXmlAttribute(pRun, "rootnode-xpath", rootPath);
                }
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                XmlDocument sourceXml = new XmlDocument();
                XslTransform xformXsl = new XslTransform();

                if (filepath.Length != 0)
                {
                    SetXmlAttribute(pRun, "filepath", filepath);
                    XmlUrlResolver urlResolver = new XmlUrlResolver();
                    urlResolver.Credentials = System.Net.CredentialCache.DefaultCredentials;
                    xformXsl.Load(filepath, urlResolver);
                }
                else
                {
                    SetXmlAttribute(pRun, "filepath", "<inline>");
                    XmlNode inline = nodeTransform.SelectSingleNode("inline");
                    if (inline != null)
                    {
                        XmlNode sheet = inline.FirstChild;
                        xformXsl.Load(sheet);
                    }
                    else
                    {
                        throw new ApplicationException("No transform document was provided.\nYou must either provide a valid filepath or an inline child node.");
                    }
                }

                if (sourceFilepath.Length != 0)
                {
                    SetXmlAttribute(pRun, "source-filepath", sourceFilepath);
                    sourceXml.Load(sourceFilepath);
                }
                else
                {
                    XmlNode runRootSource = m_bdog.runXml.DocumentElement.SelectSingleNode(rootPath);
                    if (runRootSource != null)
                    {
                        sourceXml.LoadXml(runRootSource.OuterXml);

                    }
                    else
                    {
                        throw new ApplicationException("The requested rootnode-xpath was not valid within this run:\n" + rootPath);
                    }
                }

                XsltArgumentList args = new XsltArgumentList();
                XmlDocument resultXml = new XmlDocument();
                resultXml.Load(xformXsl.Transform(sourceXml,args));

                if (resultXml.DocumentElement != null)
                {
                    CopyNodes(pRun, resultXml.DocumentElement);
                }
                else
                {
                    SetXmlAttribute(pRun, "empty-resultset", "true");
                }
                
                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion

        #region [ RunRunBatch ]
        /*
         *          <run-batch id="..." test="" xpath="">      <!-- xpath = if given, look here for the batch to run, as versus whatever batch is contained in the run-batch node -->
         *              { any set of the commands available within a Run batch }
         *          </run-batch>
         */
        private static XmlNode RunRunBatch(XmlNode pRunBatch, XmlNode pRun)
        {
            try
            {
                XmlNode nodeRunBatch = GetRealNode(pRunBatch);
                //XmlNode nodeParams = GetParamsNode(nodeLoadFile);

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                // the errror handler may be overridden by the run-batch.
                string previousErrorHandlerId = m_bdog.errorHandlerId;

                ProcessBatch(nodeRunBatch);

                // re-instate the parent handler once the run-batch completes.
                m_bdog.errorHandlerId = previousErrorHandlerId;

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion

        #region [ RunPath ]
        /*
         *          <path id="..." test="" xpath="">
         *              <params command="" filepath="" />    <!-- command directs one of several actions; filepath = the complete file/directory path -->
         *          </path>
         */
        private static XmlNode RunPath(XmlNode pPath, XmlNode pRun)
        {
            try
            {
                XmlNode nodePath = GetRealNode(pPath);
                XmlNode nodeParams = GetParamsNode(nodePath);

                string command = GetParamValue(nodeParams, "command", String.Empty).ToLower();
                string filepath = GetParamValue(nodeParams, "filepath", String.Empty);
                string destpath = GetParamValue(nodeParams, "destpath", String.Empty);
                string filemask = GetParamValue(nodeParams, "filemask", String.Empty);
                bool deep = GetParamValue(nodeParams, "deep", "false") == "true";
                bool noDetails = GetParamValue(nodeParams, "no-details", "false") == "true";

                string targetPath = (filepath.Length != 0)? filepath : Directory.GetCurrentDirectory();

                SetXmlAttribute(pRun, "command", command);
                SetXmlAttribute(pRun, "filepath", filepath);
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                SetXmlAttribute(pRun, "cwd", Directory.GetCurrentDirectory());

                switch (command)
                {
                    case "set-cwd":
                        Directory.SetCurrentDirectory(filepath);
                        SetXmlAttribute(pRun, "new-cwd", Directory.GetCurrentDirectory());
                        break;


                    case "get-cwd":
                        // already handled as a base attribute above
                        break;

                    case "exists":
                        bool fileExists = File.Exists(filepath);
                        bool dirExists = Directory.Exists(filepath);

                        SetXmlAttribute(pRun, "file-exists", (fileExists?"true":"false"));
                        SetXmlAttribute(pRun, "directory-exists", (dirExists ? "true" : "false"));
                        break;

                    case "list":
                        SetXmlAttribute(pRun, "path", targetPath);
                        SetXmlAttribute(pRun, "filemask", filemask);
                        SetXmlAttribute(pRun, "no-details", (noDetails ? "true" : "false"));

                        // Process the subdirectories of this directory.
                        string[] subdirectoryEntries = Directory.GetDirectories(targetPath,filemask,SearchOption.TopDirectoryOnly);
                        foreach (string subdirectory in subdirectoryEntries)
                        {
                            XmlNode nodeFile = pRun.OwnerDocument.CreateElement("dir");
                            pRun.AppendChild(nodeFile);
                            nodeFile.InnerText = subdirectory;
                        }
                        
                        // Process the list of files found in the directory.
                        string[] fileEntries = Directory.GetFiles(targetPath,filemask,SearchOption.TopDirectoryOnly);

                        foreach (string fileName in fileEntries)
                        {
                            XmlNode nodeFile = pRun.OwnerDocument.CreateElement("file");
                            pRun.AppendChild(nodeFile);
                            nodeFile.InnerText = fileName;

                            if (!noDetails)
                            {
                                AddFileDetails(fileName, nodeFile);
                            }
                        }
                        break;

                    case "make-directory":
                    case "make-dir":
                    case "mkdir":
                        if (File.Exists(filepath))
                            throw new ApplicationException("The requested path already exists as a file:\n" + filepath);

                        if (!Directory.Exists(filepath))
                        {
                            Directory.CreateDirectory(filepath);
                            SetXmlTextValue(pRun, "directory created");
                        }
                        else
                        {
                            SetXmlTextValue(pRun, "directory already exists");
                        }
                        break;

                    case "copy":
                        string overwriteCopy = GetParamValue(nodeParams, "overwrite", String.Empty).ToLower();
                        SetXmlAttribute(pRun, "overwrite", overwriteCopy);

                        RecursiveCopy(filepath, destpath, deep, ((overwriteCopy == "true") || (overwriteCopy == "yes")));
                        SetXmlTextValue(pRun, "filepath copied");

                        if (!noDetails)
                        {
                            AddFileDetails(destpath, pRun, "file-");
                        }
                        break;

                    case "move":
                    case "move-file":
                    case "move-files":
                    case "move-dir":
                    case "move-directory":
                        SetXmlAttribute(pRun, "destpath", destpath);

                        string overwrite = GetParamValue(nodeParams, "overwrite", String.Empty).ToLower();
                        SetXmlAttribute(pRun, "overwrite", overwrite);
                        
                        if ((overwrite == "true") || (overwrite == "yes"))
                        {
                            if (File.Exists(destpath))
                            {
                                File.Delete(destpath);
                            }
                        }

                        if (command.IndexOf("-dir") != -1)
                        {
                            Directory.Move(filepath, destpath);
                        }
                        else
                        {
                            File.Move(filepath, destpath);
                        }

                        SetXmlTextValue(pRun, "Filepath Moved");

                        if (!noDetails)
                        {
                            AddFileDetails(destpath, pRun, "file-");
                        }
                        
                        break;

                    case "delete":
                        if (File.Exists(filepath))
                        {
                            File.Delete(filepath);
                            SetXmlTextValue(pRun, "1 file deleted");
                        }
                        else if (Directory.Exists(filepath))
                        {
                            SetXmlAttribute(pRun, "deep", (deep?"true":"false"));
                            Directory.Delete(filepath, deep);
                            SetXmlTextValue(pRun, "1 directory deleted");
                        }
                        break;
                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion


        #region [ RunFTP ]
        /*
         *          <FTP id="..." test="" xpath="">
         *              <params method="get|post" format="text|xml" uri="" post-format="text|xml" post-rootnode-xpath="" />    <!-- command directs one of several actions; filepath = the complete file/directory path -->
         *          </FTP>
         */
        private static XmlNode RunFTP(XmlNode pPath, XmlNode pRun)
        {
            try
            {

                XmlNode nodePath = GetRealNode(pPath);
                XmlNode nodeParams = GetParamsNode(nodePath);

                string ftpType = nodePath.Name.ToLower();

                string host = GetParamValue(nodeParams, "host", String.Empty);
                string port = GetParamValue(nodeParams, "port", String.Empty);
                string method = GetParamValue(nodeParams, "method", String.Empty);
                string format = GetParamValue(nodeParams, "format", "ascii");

                string path = GetParamValue(nodeParams, "path", String.Empty);
                string localpath = GetParamValue(nodeParams, "local-path", String.Empty);
                string filename = GetParamValue(nodeParams, "filename", String.Empty);
                string destfilename = GetParamValue(nodeParams, "dest-filename", String.Empty);

                SetXmlAttribute(pRun, "host", host);
                SetXmlAttribute(pRun, "port", port);
                SetXmlAttribute(pRun, "method", method);
                // note: we will set format attribute later

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());


                switch(ftpType)
                {
                    case "ftp":
                        throw new Exception("FTP not yet implemented.");
                        break;


                    case "sftp":




                        break;



                    case "ftps":
                        throw new Exception("FTPS not yet implemented.");
                        break;

                }
                
                
                
                
                
                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion



        #region [ RunHTTP ]
        /*
         *          <http id="..." test="" xpath="">
         *              <params method="get|post" format="text|xml" uri="" post-format="text|xml" post-rootnode-xpath="" />    <!-- command directs one of several actions; filepath = the complete file/directory path -->
         *          </http>
         */
        private static XmlNode RunHTTP(XmlNode pPath, XmlNode pRun)
        {
            try
            {

                XmlNode nodePath = GetRealNode(pPath);
                XmlNode nodeParams = GetParamsNode(nodePath);

                string uri = GetParamValue(nodeParams, "uri", String.Empty);
                string method = GetParamValue(nodeParams, "method", "get");
                string format = GetParamValue(nodeParams, "format", "text");

                SetXmlAttribute(pRun, "uri", uri);
                SetXmlAttribute(pRun, "method", method);
                // note: we will set format attribute later
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                const int bufSizeMax = 65536; // max read buffer size conserves memory    
                const int bufSizeMin = 8192;  // min size prevents numerous small reads    
                StringBuilder sb;    // A WebException is thrown if HTTP request fails    
                
                //try     // s'alright.  pass it on to the error handler
                //{        
                    // Create an HttpWebRequest using WebRequest.Create (see .NET docs)!        
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);

                    if (method == "post")
                    {
                        string postFormat = GetParamValue(nodeParams, "post-format", "text");
                        string rootPath = GetParamValue(nodeParams, "post-rootnode-xpath", "/");
                        string postContents = String.Empty;

                        XmlDocument sourceXml = new XmlDocument();

                        XmlNode runRootSource = m_bdog.runXml.DocumentElement.SelectSingleNode(rootPath);
                        if (runRootSource != null)
                        {
                            sourceXml.LoadXml(runRootSource.OuterXml);
                        }
                        else
                        {
                            throw new ApplicationException("The requested rootnode-xpath was not valid within this run:\n" + rootPath);
                        }

                        switch (postFormat)
                        {
                            case "text":
                                postContents = sourceXml.InnerText;
                                request.ContentType = "application/x-www-form-urlencoded";
                                break;

                            case "xml":
                                postContents = sourceXml.InnerXml;
                                request.ContentType = "text/xml";
                                break;

                            case "xml-outer":
                                postContents = sourceXml.OuterXml;
                                request.ContentType = "text/xml";
                                break;
                        }

                        //XmlCDataSection cdata = m_bdog.runXml.CreateCDataSection(postContents);
                        //pRun.AppendChild(cdata);
                        XmlNode runPost = pRun.OwnerDocument.CreateElement("post");
                        pRun.AppendChild(runPost);
                        AppendRawText(runPost, postContents);

                        System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
                        byte[] postBytes = encoding.GetBytes(postContents);
                         
                        request.Method = "POST";
                        request.ContentLength = postContents.Length;
                        Stream dataStream = request.GetRequestStream();
                        dataStream.Write(postBytes, 0, postContents.Length);
                        dataStream.Close();
                    }


                    // Execute the request and obtain the response stream        
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();        
                    
                    Stream responseStream = response.GetResponseStream();        
                    // Content-Length header is not trustable, but makes a good hint.        
                    
                    // Responses longer than int size will throw an exception here!        
                    int length = (int)response.ContentLength;        
                    
                    // Use Content-Length if between bufSizeMax and bufSizeMin        
                    int bufSize = bufSizeMin;        
                    
                    if (length > bufSize)            
                        bufSize = length > bufSizeMax ? bufSizeMax : length;        
                    
                    // Allocate buffer and StringBuilder for reading response        
                    byte[] buf = new byte[bufSize];        
                    
                    sb = new StringBuilder(bufSize);        
                    // Read response stream until end        
                    
                    while ((length = responseStream.Read(buf, 0, buf.Length)) != 0)            
                        sb.Append(Encoding.UTF8.GetString(buf, 0, length));    
                
                //}    
                //catch (Exception ex)    
                //{        
                //    sb = new StringBuilder(ex.Message);
                //    format = "text";
                //}

                // NOTE: Since format could get modified by an Error response occurring, moved set to here.
                SetXmlAttribute(pRun, "format", format);

                switch (format)
                {
                    case "text":
                        //XmlCDataSection textResponse = pRun.OwnerDocument.CreateCDataSection(sb.ToString());
                        //pRun.AppendChild(textResponse);
                        AppendRawText(pRun, sb.ToString());
                        break;

                    case "xml":
                        XmlDocument xmlResponse = new XmlDocument();
                        xmlResponse.LoadXml(sb.ToString());
                        CopyNodes(pRun, xmlResponse.DocumentElement);
                        break;

                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion

        #region [ RunMailTo ]
        /*
         *          <mail-to id="..." test="" xpath="">
         *              <params smtp-host="" tcpip-port="" from="" to="" subject="" credentials="network|default|none" rootnode-xpath="/" format="text|xml"/>
         *          </mail-to>
         */
        private static XmlNode RunMailTo(XmlNode pMailTo, XmlNode pRun)
        {
            try
            {
                XmlNode nodeMailTo = GetRealNode(pMailTo);
                XmlNode nodeParams = GetParamsNode(nodeMailTo);

                string host = GetParamValue(nodeParams, "smtp-host", String.Empty);
                string port = GetParamValue(nodeParams, "tcpip-port", "25");
                string toAddr = GetParamValue(nodeParams, "to", String.Empty);
                string fromAddr = GetParamValue(nodeParams, "from", String.Empty);
                string subject = GetParamValue(nodeParams, "subject", String.Empty);
                string credentials = GetParamValue(nodeParams, "credentials", "none");

                string format = GetParamValue(nodeParams, "format", "/");
                string rootPath = GetParamValue(nodeParams, "rootnode-xpath", "/");
                string fileContents = String.Empty;

                SetXmlAttribute(pRun, "smtp-host", host);
                SetXmlAttribute(pRun, "tcpip-port", port);
                SetXmlAttribute(pRun, "to", toAddr);
                SetXmlAttribute(pRun, "from", fromAddr);
                SetXmlAttribute(pRun, "subject", subject);
                SetXmlAttribute(pRun, "credentials", credentials);

                SetXmlAttribute(pRun, "format", format);
                SetXmlAttribute(pRun, "rootnode-xpath", rootPath);
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                // Command line argument must the the SMTP host.
                SmtpClient client = new SmtpClient(host, System.Convert.ToInt32(port));

                XmlDocument sourceXml = new XmlDocument();

                XmlNode runRootSource = m_bdog.runXml.DocumentElement.SelectSingleNode(rootPath);
                if (runRootSource != null)
                {
                    sourceXml.LoadXml(runRootSource.OuterXml);
                }
                else
                {
                    throw new ApplicationException("The requested rootnode-xpath was not valid within this run:\n" + rootPath);
                }

                switch (format)
                {
                    case "text":
                        fileContents = sourceXml.InnerText;
                        break;

                    case "xml":
                        fileContents = sourceXml.InnerXml;
                        break;

                    case "xml-outer":
                        fileContents = sourceXml.OuterXml;
                        break;
                }

                //XmlCDataSection cdata = m_bdog.runXml.CreateCDataSection(fileContents);
                //pRun.AppendChild(cdata);
                AppendRawText(pRun, fileContents);

                System.Net.Mail.MailMessage message = new MailMessage(fromAddr, toAddr, subject, fileContents);

                switch(credentials)
                {
                    case "default":
                        client.Credentials = (NetworkCredential)System.Net.CredentialCache.DefaultCredentials;
                        break;
                    case "network":
                        client.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials;
                        break;
                    case "none":
                    case "":
                        break;

                }
                client.Send(message);

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion

        #region [ RunMSMQ ]
        /*
         *          <msmq id="..." test="" xpath="">
         *              <params queue-name="" method="send|receive|list" rootnode-xpath="/" format="text|xml" priority="lowest|very-low|low|normal|above-normal|high|very-high|highest"/>
         *          </mail-to>
         */
        private static XmlNode RunMSMQ(XmlNode pMSMQ, XmlNode pRun)
        {
            try
            {
                XmlNode nodeMSMQ = GetRealNode(pMSMQ);
                XmlNode nodeParams = GetParamsNode(nodeMSMQ);

                if (m_bdog.debug)
                    Console.WriteLine("Running msmq...");

                string queue = GetParamValue(nodeParams, "queue-name", String.Empty);
                string method = GetParamValue(nodeParams, "method", String.Empty);

                if (m_bdog.debug)
                    Console.WriteLine("..." + method + " - " + queue);

                SetXmlAttribute(pRun, "queue-name", queue);
                SetXmlAttribute(pRun, "method", method);
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                MessageQueue msmq = new MessageQueue(queue);
                //XmlCDataSection cdata;
                XmlNode queueMessage;
                XmlDocument bodyXML;
                XmlNode Error;
                System.Messaging.Message msg;
                string format = String.Empty;

                if (m_bdog.debug)
                    Console.WriteLine("...queue opened...");

                switch(method)
                {
                    case "send":
                        string priority = GetParamValue(nodeParams, "priority", "normal");
                        format = GetParamValue(nodeParams, "format", "text");
                        string rootPath = GetParamValue(nodeParams, "rootnode-xpath", "/");
                        string fileContents = String.Empty;

                        if (m_bdog.debug)
                            Console.WriteLine("...Pri: " + priority + "\tFormat: " + format + "...\n...XPath: " + rootPath);

                        SetXmlAttribute(pRun, "priority", priority);
                        SetXmlAttribute(pRun, "format", format);
                        SetXmlAttribute(pRun, "rootnode-xpath", rootPath);

                        XmlDocument sourceXml = new XmlDocument();

                        XmlNode runRootSource = m_bdog.runXml.DocumentElement.SelectSingleNode(rootPath);
                        if (runRootSource != null)
                        {
                            sourceXml.LoadXml(runRootSource.OuterXml);
                        }
                        else
                        {
                            throw new ApplicationException("The requested rootnode-xpath was not valid within this run:\n" + rootPath);
                        }

                        switch (format)
                        {
                            case "text":
                                fileContents = sourceXml.InnerText;
                                break;

                            case "xml":
                                fileContents = sourceXml.InnerXml;
                                break;

                            case "xml-outer":
                                fileContents = sourceXml.OuterXml;
                                break;
                        }

                        if (m_bdog.debug)
                            Console.WriteLine("...contents obtained (~" + fileContents.Length.ToString() + " bytes)...");

                      
                        //cdata = pRun.OwnerDocument.CreateCDataSection(fileContents);
                        //pRun.AppendChild(cdata);
                        AppendRawText(pRun, fileContents);

                        msg = new System.Messaging.Message(fileContents);

                        if (m_bdog.debug)
                            Console.WriteLine("...message created...");

                    switch (priority)
                        {
                            case "above-normal":
                                msg.Priority = MessagePriority.AboveNormal;
                                break;
                            case "high":
                                msg.Priority = MessagePriority.High;
                                break;
                            case "highest":
                                msg.Priority = MessagePriority.Highest;
                                break;
                            case "low":
                                msg.Priority = MessagePriority.Low;
                                break;
                            case "lowest":
                                msg.Priority = MessagePriority.Lowest;
                                break;
                            case "very-high":
                                msg.Priority = MessagePriority.VeryHigh;
                                break;
                            case "very-low":
                                msg.Priority = MessagePriority.VeryLow;
                                break;
                            case "normal":
                            case "":
                                msg.Priority = MessagePriority.Normal;
                                break;
                        
                        }

                        msmq.Send(msg);

                        if (m_bdog.debug)
                            Console.WriteLine("...message sent...");
                        break;

                    case "receive":
                        queueMessage = pRun.OwnerDocument.CreateElement("message");
                        pRun.AppendChild(queueMessage);

                        format = GetParamValue(nodeParams, "format", "text");
                        SetXmlAttribute(pRun, "format", format);

                        if (m_bdog.debug)
                            Console.WriteLine("...preparing to receive message...");

                        if (format == "xml")
                            msmq.Formatter = new XmlMessageFormatter(new string[] { "System.String" });
                        else
                            msmq.Formatter = new ActiveXMessageFormatter();


                        if (m_bdog.debug)
                            Console.WriteLine("...receiving message...");

                        msg = msmq.Receive();

                        if (m_bdog.debug)
                            Console.WriteLine("...message received (~" + msg.Body.ToString().Length.ToString() + " bytes)...");


                        try
                        {
                            if (format == "text")
                            {
                                object msgBody = (object)msg.Body;
                                string msgText = msgBody.ToString();
                                AppendRawText(queueMessage, msgText);
                            }
                            else  // "xml"
                            {
                                bodyXML = new XmlDocument();
                                bodyXML.LoadXml(msg.Body.ToString());
                                CopyNodes(queueMessage, bodyXML.DocumentElement);
                            }
                        }
                        catch (Exception e)
                        {
                            Error = pRun.OwnerDocument.CreateElement("Error");
                            pRun.AppendChild(Error);
                            Error.Value = e.Message;
                        }


                        if (m_bdog.debug)
                            Console.WriteLine("...obtaining additional information...");

                        //SetXmlAttribute(queueMessage, "msg-priority", msg.Priority.ToString());
                        SetXmlAttribute(queueMessage, "msg-label", msg.Label);
                        SetXmlAttribute(queueMessage, "msg-type", msg.MessageType.ToString());
                        //SetXmlAttribute(queueMessage, "msg-sent", msg.SentTime.ToLocalTime().ToString());
                        //SetXmlAttribute(queueMessage, "msg-source", msg.SourceMachine);
                        //SetXmlAttribute(queueMessage, "msg-transaction-id", msg.TransactionId);
                        SetXmlAttribute(queueMessage, "msg-id", msg.Id);

                        if (m_bdog.debug)
                            Console.WriteLine("...completed...");
                        break;

                    case "list":
                        bool includeBody = true;

                        if (m_bdog.debug)
                            Console.WriteLine("...preparing to review (" + msmq.GetAllMessages().Length.ToString() + ") messages...");

                        format = GetParamValue(nodeParams, "format", "text");
                        SetXmlAttribute(pRun, "format", format);

                        if (format == "xml")
                            msmq.Formatter = new XmlMessageFormatter(new string[] { "System.String" });
                        else
                            msmq.Formatter = new ActiveXMessageFormatter();

                        foreach (System.Messaging.Message qMsg in msmq)
                        {
                            if (m_bdog.debug)
                                Console.WriteLine("...reviewing message...");

                            queueMessage = pRun.OwnerDocument.CreateElement("message");
                            pRun.AppendChild(queueMessage);

                            if (includeBody)
                            {
                                try
                                {
                                    if (format == "text")
                                    {
                                        object msgBody = (object)qMsg.Body;
                                        string msgText = msgBody.ToString();
                                        AppendRawText(queueMessage, msgText);

                                    }
                                    else  // "xml"
                                    {
                                        bodyXML = new XmlDocument();
                                        bodyXML.LoadXml(qMsg.Body.ToString());
                                        CopyNodes(queueMessage, bodyXML.DocumentElement);
                                    }
                                }
                                catch (Exception e)
                                {
                                    Error = pRun.OwnerDocument.CreateElement("Error");
                                    queueMessage.AppendChild(Error);
                                    Error.InnerText = e.Message;
                                }
                            }

                            if (m_bdog.debug)
                                Console.WriteLine("...message reviewed (~" + qMsg.Body.ToString().Length.ToString() + " bytes)...");

                            if (m_bdog.debug)
                                Console.WriteLine("...obtaining additional information...");

                            //SetXmlAttribute(queueMessage, "msg-priority", qMsg.Priority.ToString());
                            SetXmlAttribute(queueMessage, "msg-label", qMsg.Label);
                            SetXmlAttribute(queueMessage, "msg-type", qMsg.MessageType.ToString());
                            //SetXmlAttribute(queueMessage, "msg-sent", qMsg.SentTime.ToLocalTime().ToString());
                            //SetXmlAttribute(queueMessage, "msg-source", qMsg.SourceMachine);
                            //SetXmlAttribute(queueMessage, "msg-transaction-id", qMsg.TransactionId);
                            SetXmlAttribute(queueMessage, "msg-id", qMsg.Id);
                        }

                        if (m_bdog.debug)
                            Console.WriteLine("...completed...");

                        break;
                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion

        #region [ RunRegistry ]
        /*
         *          <registry id="..." test="" xpath="">
         *              <params hkey="HKLM|HKCU|HKCR|HKU|HKCC|HKEY_LOCAL_MACHINE|HKEY_CURRENT_USER|HKEY_CLASSES_ROOT|HKEY_USERS|HKEY_CURRENT_CONFIG" subkey="" method="get(-value)|set(-value)|create(-key)|list" value-name="" value-type="REG_SZ|REG_DWORD (FUTURE: REG_BINARY, REG_QWORD, REG_MULTI_SZ, REG_EXPAND_SZ)" value-data="" format="text|xml"/>
         *          </registry>
         */
        private static XmlNode RunRegistry(XmlNode pCommand, XmlNode pRun)
        {
            try
            {
                XmlNode nodeCommand = GetRealNode(pCommand);
                XmlNode nodeParams = GetParamsNode(nodeCommand);

                if (m_bdog.debug)
                    Console.WriteLine("Running registry...");

                string hkey = GetParamValue(nodeParams, "hkey", String.Empty);
                string subkey = GetParamValue(nodeParams, "subkey", String.Empty);
                string method = GetParamValue(nodeParams, "method", String.Empty);

                string valueName = GetParamValue(nodeParams, "value-name", String.Empty);
                string valueType = GetParamValue(nodeParams, "value-type", "REG_SZ");
                string valueData = GetParamValue(nodeParams, "value-data", String.Empty);
                string newKeyName = GetParamValue(nodeParams, "key-name", String.Empty);

                if (m_bdog.debug)
                    Console.WriteLine("..." + method + " - " + hkey + "\\" + subkey);

                SetXmlAttribute(pRun, "hkey", hkey);
                SetXmlAttribute(pRun, "subkey", subkey);
                SetXmlAttribute(pRun, "method", method);

                // Note: value-name, -type and -data, and key-name are only used in certain methods.
                // To avoid confusion, we will only set them in the methods in which they are used.
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                if (m_bdog.debug)
                    Console.WriteLine("...obtaining root key...");

                RegistryKey regKey;
                RegistryValueKind valueKind;

                switch(hkey)
                {
                    case "HKLM":
                    case "HKEY_LOCAL_MACHINE":
                        regKey = Registry.LocalMachine;
                        break;

                    case "HKCU":
                    case "HKEY_CURRENT_USER":
                        regKey = Registry.CurrentUser;
                        break;

                    case "HKCR":
                    case "HKEY_CLASSES_ROOT":
                        regKey = Registry.ClassesRoot;
                        break;

                    case "HKU":
                    case "HKEY_USERS":
                        regKey = Registry.Users;
                        break;

                    case "HKCC":
                    case "HKEY_CURRENT_CONFIG":
                        regKey = Registry.CurrentConfig;
                        break;

                    default:
                        throw new ApplicationException("The hkey given (" + hkey + ") was invalid.\nAcceptable values are: HKLM|HKCU|HKCR|HKU|HKCC|HKEY_LOCAL_MACHINE|HKEY_CURRENT_USER|HKEY_CLASSES_ROOT|HKEY_USERS|HKEY_CURRENT_CONFIG");
                }


                bool openWritable = ((method == "set") || (method == "set-value"));

                if (m_bdog.debug)
                    Console.WriteLine("...obtaining sub key (" + subkey + ") as [ R/" + (openWritable?"W":"O") + " ]...");

                RegistryKey regSubkey = regKey.OpenSubKey(subkey, openWritable);

                switch (method)
                {

                    case "get":
                    case "get-value":
                        SetXmlAttribute(pRun, "value-name", valueName);
                        SetXmlTextValue(pRun, regSubkey.GetValue(valueName, String.Empty).ToString());
                        //SetXmlAttribute(pRun, "value-type", regSubkey.GetValueKind(valueName).ToString());
                        valueKind = regSubkey.GetValueKind(valueName);
                        switch(valueKind)
                        {
                            case RegistryValueKind.Binary:
                                valueType = "REG_BINARY";
                                break;
                            case RegistryValueKind.DWord:
                                valueType = "REG_DWORD";
                                break;
                            case RegistryValueKind.QWord:
                                valueType = "REG_QWORD";
                                break;
                            case RegistryValueKind.String:
                                valueType = "REG_SZ";
                                break;
                            case RegistryValueKind.MultiString:
                                valueType = "REG_MULTI_SZ";
                                break;
                            case RegistryValueKind.ExpandString:
                                valueType = "REG_EXPAND_SZ";
                                break;
                        }
                        SetXmlAttribute(pRun, "value-type", valueType);
                        break;

                    case "set":
                    case "set-value":
                        SetXmlAttribute(pRun, "value-name", valueName);
                        SetXmlAttribute(pRun, "value-data", valueData);
                        SetXmlAttribute(pRun, "value-type", valueData);
                        object value;
                        switch(valueType)
                        {
                            case "REG_SZ":
                                valueKind = RegistryValueKind.String;
                                value = (object)valueData;
                                break;

                            case "REG_DWORD":
                                valueKind = RegistryValueKind.DWord;
                                value = (object)System.Convert.ToInt32(valueData);
                                break;

                            case "REG_QWORD":
                                valueKind = RegistryValueKind.DWord;
                                value = (object)System.Convert.ToInt64(valueData);
                                break;

                            case "REG_BINARY":
                            case "REG_MULTI_SZ":
                            case "REG_EXPAND_SZ":
                            default:
                                throw new ApplicationException("The value-type provided (" + valueType + ") is not supported at this time.\nAcceptable values are: REG_SZ|REG_DWORD");
                        }
                        regSubkey.SetValue(valueName,value, valueKind);
                        SetXmlTextValue(pRun, "Value Set");
                        break;

                    case "create":
                    case "create-key":
                        SetXmlAttribute(pRun, "key-name", newKeyName);
                        RegistryKey newKey = regSubkey.CreateSubKey(newKeyName);
                        SetXmlTextValue(pRun, "Key Created");
                        break;

                    case "list":
                        string[] valueNames = regSubkey.GetValueNames();
                        foreach (String curValueName in valueNames)
                        {
                            XmlNode curValue = pRun.OwnerDocument.CreateElement("value");
                            pRun.AppendChild(curValue);
                            SetXmlAttribute(curValue, "name", curValueName);
                            RegistryValueKind curValueKind = regSubkey.GetValueKind(curValueName);
                            string curValueType;
                            switch (curValueKind)
                            {
                                case RegistryValueKind.Binary:
                                    curValueType = "REG_BINARY";
                                    break;
                                case RegistryValueKind.DWord:
                                    curValueType = "REG_DWORD";
                                    break;
                                case RegistryValueKind.QWord:
                                    curValueType = "REG_QWORD";
                                    break;
                                case RegistryValueKind.String:
                                    curValueType = "REG_SZ";
                                    break;
                                case RegistryValueKind.MultiString:
                                    curValueType = "REG_MULTI_SZ";
                                    break;
                                case RegistryValueKind.ExpandString:
                                    curValueType = "REG_EXPAND_SZ";
                                    break;
                                default:
                                    curValueType = "Unknown";
                                    break;
                            }
                            SetXmlAttribute(curValue, "value-type", curValueType);
                            SetXmlTextValue(curValue, regSubkey.GetValue(curValueName, String.Empty).ToString());
                        }
                        string[] subKeyNames = regSubkey.GetSubKeyNames();
                        foreach (String curSubKeyName in subKeyNames)
                        {
                            XmlNode curSubKey = pRun.OwnerDocument.CreateElement("key");
                            pRun.AppendChild(curSubKey);
                            SetXmlAttribute(curSubKey, "name", curSubKeyName);
                        }

                        SetXmlAttribute(pRun, "value-count", valueNames.Length.ToString());
                        SetXmlAttribute(pRun, "key-count", regSubkey.SubKeyCount.ToString());
                        break;

                }

                regSubkey.Close();

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion

        #region [ RunReset ]
        /*
         *          <reset id="..." test="" xpath="">
         *              <params method="soft|hard|restart"/>
         *          </reset>
         */
        private static XmlNode RunReset(XmlNode pPath, XmlNode pRun)
        {
            try
            {
                XmlNode nodePath = GetRealNode(pPath);
                XmlNode nodeParams = GetParamsNode(nodePath);

                string method = GetParamValue(nodeParams, "method", "soft").ToLower();

                if (m_bdog.debug)
                    Console.WriteLine("Reset [" + method + "]...");

                SetXmlAttribute(pRun, "method", method);
                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                XmlNodeList removeNodes = m_bdog.runXml.DocumentElement.SelectNodes("/" + m_bdog.rootElementName + "/*[name() != 'run-time'][position() != last()]");
                SetXmlAttribute(pRun, "previous-executions", removeNodes.Count.ToString());

                switch (method)
                {
                    case "soft":
                        try
                        {
                            if (m_bdog.debug)
                                Console.WriteLine("... Removing (" + removeNodes.Count.ToString() + ") previous executions...");

                            foreach (XmlNode removeNode in removeNodes)
                            {   // remove all but the last node, which is our current run node
                                XmlNode parentNode = removeNode.ParentNode;
                                parentNode.RemoveChild(removeNode);
                            }
                        }
                        catch (SystemException e)
                        {
                            pRun.AppendChild(HandleException(pRun.OwnerDocument, e));
                        }

                        break;


                        // Note: Both "hard" and "restart" should perform a hard reset, but restart should take it further.
                    case "hard":
                    case "restart":
                        try
                        {
                            XmlElement oldRoot = m_bdog.runXml.DocumentElement;
                            
                            int runCount = ((oldRoot.HasAttribute("run-count")) ? (int.Parse(oldRoot.Attributes["run-count"].Value)) : 0) + 1;
                            
                            string originalStart = (runCount == 1) ? oldRoot.SelectSingleNode(String.Format("/{0}/run-time/@systemtime", m_bdog.rootElementName)).Value : oldRoot.Attributes["original-starttime"].Value;

                            
                            // for a hard init, we will reset the system time as well
                            m_bdog.systemTime = System.DateTime.UtcNow.ToLocalTime();

                            // re-create our Run DataCloud
                            XmlDocument newRun = new XmlDocument();
                            newRun.AppendChild(newRun.CreateElement(m_bdog.rootElementName));

                            if (m_bdog.debug)
                                Console.WriteLine("... Creating new document ...");

                            SetXmlAttribute(newRun.DocumentElement, "run-count", runCount.ToString());
                            SetXmlAttribute(newRun.DocumentElement, "original-starttime", originalStart);
                            SetXmlAttribute(newRun.DocumentElement, "powered-by", m_bdog.powered);
                            SetXmlAttribute(newRun.DocumentElement, "legal-notice", m_bdog.legalese);

                            if (m_bdog.debug)
                                Console.WriteLine("... Re-loading run-time variables ...");

                            LoadRunTimeVariables(newRun.DocumentElement);

                            if (m_bdog.debug)
                                Console.WriteLine("... Copying forward current run node ...");

                            XmlNode newRunNode = newRun.CreateElement(pRun.Name);
                            foreach (XmlAttribute copyAttribute in pRun.Attributes)
                            {
                                SetXmlAttribute(newRunNode, copyAttribute.Name, copyAttribute.Value);
                            }
                            newRun.DocumentElement.AppendChild(newRunNode);

                            if (m_bdog.debug)
                                Console.WriteLine("... Setting pointers to new run document ...");

                            m_bdog.runXml.LoadXml(newRun.DocumentElement.OuterXml);
                            pRun = m_bdog.runXml.DocumentElement.SelectSingleNode("/" + m_bdog.rootElementName + "/reset");

                            if (method == "restart")
                            {
                                m_bdog.stopped = true;
                                m_bdog.repeat = true;
                            }
                        }
                        catch (SystemException e)
                        {
                            pRun.AppendChild(HandleException(pRun.OwnerDocument, e));
                        }

                        break;

                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }


        #endregion

        #region [ RunPowershell ]
        /*
             *          <powershell id="..." test="" xpath="">        <!-- xpath = if given then look here for the command node (from transform), otherwise, this is the command node -->
             *              <params working-directory="C:\" format="text|xml" capture-output="true|false">
         *                      <script/>
         *                  </params>
             *          </powershell>
         */
        private static XmlNode RunPowershell(XmlNode pCommand, XmlNode pRun)
        {
            try
            {
                XmlNode nodeCommand = GetRealNode(pCommand);
                XmlNode nodeParams = GetParamsNode(nodeCommand);
                
                bool waitForExit = true;    //(GetParamValue(nodeParams, "wait-for-exit", "true").ToLower() == "true");
                bool useShell = false;      //(GetParamValue(nodeParams, "use-shell", "true").ToLower() == "true");
                string workingDirectory = GetParamValue(nodeParams, "working-directory", String.Empty);
                string command = "powershell.exe"; //GetParamValue(nodeParams, "command", String.Empty);
                string style = String.Empty;  //GetParamValue(nodeParams, "style", String.Empty);
                bool captureStdIn = true;   //(GetParamValue(nodeParams, "capture-stdin", "false").ToLower() == "true");
                bool captureStdOut = (GetParamValue(nodeParams, "capture-output", "false").ToLower() == "true");
                bool captureStdErr = true;  //(GetParamValue(nodeParams, "capture-stderr", "false").ToLower() == "true");
                string format = GetParamValue(nodeParams, "format", "text");
                

                // get baseline arguments as well as any arguments found in the param node
                string arguments = /* -NonInteractive */ "-OutputFormat " + (format == "text" ? "Text" : "XML") + " -Command -";   //GetParamValue(nodeParams, "arguments", String.Empty);


                System.Diagnostics.Process proc; // Declare New Process
                System.Diagnostics.ProcessStartInfo procInfo = new System.Diagnostics.ProcessStartInfo(); // Declare New Process Starting Information

                procInfo.UseShellExecute = useShell;  //If this is false, only .exe's can be run.
                procInfo.WorkingDirectory = workingDirectory; //execute notepad from the C: Drive
                procInfo.FileName = command; // Program or Command to Execute.
                procInfo.Arguments = arguments; //Command line arguments.
                switch (style)
                {
                    case "min": procInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized; break;
                    case "max": procInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Maximized; break;
                    case "hidden": procInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden; break;
                    default: procInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal; break;
                }

                string stdin = String.Empty;
                if (captureStdIn)
                {
                    if (m_bdog.debug)
                        Console.WriteLine("... loading powershell script ...");

                    XmlNode nodeStdIn = nodeParams.SelectSingleNode("script");

                    if (nodeStdIn != null)
                    {
                        stdin = GetParamValueFromNode(nodeStdIn, String.Empty);

                    }
                    else
                    {
                        throw new ApplicationException("Script not defined.\nWhen capturing standard input, you must specify a valid source with a \"std\" node.\n");
                    }

                }

                //SetXmlAttribute(pRun, "use-shell", useShell.ToString().ToLower());
                SetXmlAttribute(pRun, "working-directory", workingDirectory);
                //SetXmlAttribute(pRun, "command", command);
                //SetXmlAttribute(pRun, "arguments", arguments);
                //SetXmlAttribute(pRun, "style", style);
                SetXmlAttribute(pRun, "capture-output", captureStdOut.ToString().ToLower());
                //SetXmlAttribute(pRun, "capture-stderr", captureStdErr.ToString().ToLower());

                SetXmlAttribute(pRun, "start-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                procInfo.RedirectStandardInput = captureStdIn;
                procInfo.RedirectStandardError = captureStdErr;
                procInfo.RedirectStandardOutput = captureStdOut;

                //proc = System.Diagnostics.Process.Start(procInfo);
                proc = new System.Diagnostics.Process();
                proc.StartInfo = procInfo;
                proc.Start();

                if (m_bdog.debug)
                    Console.WriteLine("...Powershell command...\n" + stdin + "\n... End of Powershell command\n");

                if (captureStdIn)  // was (useShell && ...)
                {
                    proc.StandardInput.AutoFlush = true;
                    proc.StandardInput.Write(stdin);
                    proc.StandardInput.Close();
                }

                if (m_bdog.debug)
                    Console.WriteLine("...Completed sending powershell command...\n");

                if (captureStdOut)
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    XmlNode output;

                    if (stdout.Length != 0)
                    {
                        if (format == "text")
                        {
                            output = pRun.OwnerDocument.CreateElement("output");
                            pRun.AppendChild(output);

                            AppendRawText(output, stdout);
                        }
                        else
                        {
                            string stdxml;
                            if (stdout.Substring(0, 1) == "#")
                                stdxml = stdout.Substring(stdout.IndexOf("\n"));
                            else
                                stdxml = stdout;

                            XmlDocument powershellOutputXml = new XmlDocument();
                            powershellOutputXml.LoadXml("<output>" + stdxml + "</output>");
                            CopyNodes(pRun, powershellOutputXml.DocumentElement);
                        }
                    }

                    if (m_bdog.debug)
                        Console.WriteLine("...Completed reading standard output (if captured) ...\n");
                }

                if (captureStdErr)
                    LoadStreamData(pRun, "error", proc.StandardError);

                if (m_bdog.debug)
                    Console.WriteLine("...Completed reading standard error (if captured) ...\n");

                if (waitForExit)
                    proc.WaitForExit(); // Waits for the process to end. (ie. when user closes it down)

                if (m_bdog.debug)
                    Console.WriteLine("...Powershell process has completed ...\n");

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                SetXmlAttribute(pRun, "process-id", proc.Id.ToString());
                try { SetXmlAttribute(pRun, "process-exited", proc.HasExited.ToString().ToLower()); }
                catch (System.Exception) { }
                try { SetXmlAttribute(pRun, "exit-code", proc.ExitCode.ToString()); }
                catch (System.Exception) { }

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }

        }
        #endregion

        #region [ RunForm ]
        /*
         *          <form test="" xpath="">
         *              <params filepath=""/>
         *              <gui>
         *                   <control/>
         *              </gui>
         *          </form>
         */
        private static XmlNode RunForm(XmlNode pExecution, XmlNode pRun)
        {
            try
            {
                XmlNode nodeExecution = GetRealNode(pExecution);
                XmlNode nodeParams = GetParamsNode(nodeExecution);
                XmlNode nodeGUI = nodeExecution.SelectSingleNode("gui");

                if (m_bdog.debug)
                    Console.WriteLine("Running form...");

                if (nodeGUI == null)
                    throw new ApplicationException("The form operation was not provided a GUI layout");


                Form runForm = BuildForm(nodeGUI);

                if (m_bdog.debug)
                    Console.WriteLine("Form GUI: " + runForm.ToString());

                DialogResult result = runForm.ShowDialog();
                
                if (m_bdog.debug)
                    Console.WriteLine("Result: " + result);

                SetXmlAttribute(pRun, "dialog-result", DialogResult2String(result));

                int numControls = runForm.Controls.Count;
                for (int i =0; i < numControls; i++)
                {
                    Control control = (Control)runForm.Controls[i];
                    string controlName = ControlType2String(control);

                    XmlNode controlResult = pRun.OwnerDocument.CreateElement(controlName);
                    pRun.AppendChild(controlResult);
                    
                    SetXmlAttribute(controlResult, "id", control.Name);

                    switch (controlName)
                    {
                        case "label":
                            break;

                        case "textbox":
                            break;

                        case "image":
                            break;

                        case "button":
                            Button controlButton = (Button)runForm.Controls[i];
                            SetXmlAttribute(controlResult, "type", DialogResult2String(controlButton.DialogResult));
                            break;

                        case "checkbox":
                            CheckBox controlCheckbox = (CheckBox)runForm.Controls[i];
                            SetXmlAttribute(controlResult, "checked", (controlCheckbox.Checked ? "true" : "false"));
                            break;

                        case "radio":
                            RadioButton controlRadio = (RadioButton)runForm.Controls[i];
                            SetXmlAttribute(controlResult, "checked", (controlRadio.Checked ? "true" : "false"));
                            break;

                        case "combo":
                            ComboBox controlCombo = (ComboBox)runForm.Controls[i];
                            if (controlCombo.SelectedItem != null)
                            {
                                object selectedItem = controlCombo.SelectedItem;
                                SetXmlAttribute(controlResult, "selection", selectedItem.ToString());
                            }
                            break;

                        case "listbox":
                            ListBox controlList = (ListBox)runForm.Controls[i];
                            if (controlList.SelectedItems.Count > 1)
                            {
                                SetXmlAttribute(controlResult, "selection-count", controlList.SelectedItems.Count.ToString());

                                foreach (object selectedItem in controlList.SelectedItems)
                                {
                                    XmlNode selection = CreateElementNode(controlResult.OwnerDocument, "selection");
                                    controlResult.AppendChild(selection);
                                    SetXmlTextValue(selection, selectedItem.ToString());
                                }

                            }
                            else if (controlList.SelectedItem != null)
                            {
                                object selectedItem = controlList.SelectedItem;
                                SetXmlAttribute(controlResult, "selection", selectedItem.ToString());
                            }
                            break;

                        case "checked-listbox":
                            CheckedListBox controlCheckedList = (CheckedListBox)runForm.Controls[i];

                            if (controlCheckedList.CheckedItems.Count > 0)  // NOTE: As this is different than SelectedItems, use it if ANY items were checked
                            {
                                SetXmlAttribute(controlResult, "selection-count", controlCheckedList.CheckedItems.Count.ToString());

                                foreach (object selectedItem in controlCheckedList.CheckedItems)
                                {
                                    XmlNode selection = CreateElementNode(controlResult.OwnerDocument, "selection");
                                    controlResult.AppendChild(selection);
                                    SetXmlTextValue(selection, selectedItem.ToString());
                                }

                            }
                            else if (controlCheckedList.SelectedItem != null)
                            {
                                object selectedItem = controlCheckedList.SelectedItem;
                                SetXmlAttribute(controlResult, "selection", selectedItem.ToString());
                            }

                            break;

                        case "numeric":
                            NumericUpDown controlNumeric = (NumericUpDown)runForm.Controls[i];
                            SetXmlAttribute(controlResult, "value", controlNumeric.Value.ToString());
                            break;

                        case "date":
                            DateTimePicker controlDTPicker = (DateTimePicker)runForm.Controls[i];
                            SetXmlAttribute(controlResult, "value", controlDTPicker.Value.ToString());
                            break;



                        default:
                            break;
                    }
                    
                
                
                }

                SetXmlAttribute(pRun, "end-time", System.DateTime.UtcNow.ToLocalTime().ToString());

                return pRun;
            }
            catch (System.Exception pException)
            {
                TriggerError(pRun.AppendChild(HandleException(pRun.OwnerDocument, pException)));
                return pRun;
            }
        }
        #endregion
        
        #endregion

        #region [ COMMON XML HELPERS ]

        #region string GetXmlAttribute(XmlNode pNode, string pName)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pNode"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        private static string GetXmlAttribute(XmlNode pNode, string pName)
        {
            return GetXmlAttribute(pNode, pName, String.Empty);
        }
        #endregion

        #region string GetXmlAttribute(XmlNode pNode, string pName, string pDefault)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pNode"></param>
        /// <param name="pName"></param>
        /// <param name="pDefault"></param>
        /// <returns></returns>
        private static string GetXmlAttribute(XmlNode pNode, string pName, string pDefault)
        {
            if (pNode != null)
            {
                XmlNode xmlAtt = pNode.Attributes.GetNamedItem(pName);
                if (xmlAtt != null)
                {
                    return xmlAtt.Value;
                }
                else
                {
                    return pDefault;
                }
            }
            else
            {
                return pDefault;    //null
            }

        }
        #endregion

        #region void SetXmlAttribute(XmlNode pNode, string pName, string pValue)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pNode"></param>
        /// <param name="pName"></param>
        /// <param name="pValue"></param>
        private static void SetXmlAttribute(XmlNode pNode, string pName, string pValue)
        {
            XmlNode pAttribute = pNode.OwnerDocument.CreateNode(System.Xml.XmlNodeType.Attribute, pName, String.Empty);
            pAttribute.Value = pValue;
            pNode.Attributes.SetNamedItem(pAttribute);
        }
        #endregion

        #region void SetXmlTextValue(XmlNode pNode, string pText)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pNode"></param>
        /// <param name="pText"></param>
        private static void SetXmlTextValue(XmlNode pNode, string pText)
        {
            XmlText text = pNode.OwnerDocument.CreateTextNode(pText);
            pNode.AppendChild(text);
        }
        #endregion

        #region XmlNode CreateElementNode(XmlDocument pOwnerDoc, string pNodeName)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pOwnerDoc"></param>
        /// <param name="pNodeName"></param>
        /// <returns></returns>
        private static XmlNode CreateElementNode(XmlDocument pOwnerDoc, string pNodeName)
        {
            return pOwnerDoc.CreateNode(System.Xml.XmlNodeType.Element, pNodeName, String.Empty);
        }
        #endregion

        #region void AppendRawText(XmlDocument pOwnerDoc, string pNodeName)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pOwnerDoc"></param>
        /// <param name="pNodeName"></param>
        /// <returns></returns>
        private static void AppendRawText(XmlNode pDest, string pText)
        {
            XmlCDataSection cdata;

            if (pText.IndexOf("]]>") >= 0)
            {
                string preText = pText.Substring(0, pText.IndexOf("]]>"));
                cdata = pDest.OwnerDocument.CreateCDataSection(preText);
                pDest.AppendChild(cdata);
                XmlText sepText = pDest.OwnerDocument.CreateTextNode("]]&gt;");
                AppendRawText(pDest, pText.Substring(pText.IndexOf("]]>") + 3));
            }
            else
            {
                cdata = pDest.OwnerDocument.CreateCDataSection(pText);
                pDest.AppendChild(cdata);
            }
        }
        #endregion

        #region XmlNode CopyNodes(XmlNode pDest, XmlNode pSource)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pDest"></param>
        /// <param name="pSource"></param>
        /// <returns></returns>
        private static XmlNode CopyNodes(XmlNode pDest, XmlNode pSource)
        {
            try
            {
                return CopyNodes(pDest, pSource, false);
            }
            catch (System.Exception)
            {
                return null;
            }
        }
        #endregion

        #region XmlNode CopyNodes(XmlNode pDest, XmlNode pSource, bool pRipRoot)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pDest"></param>
        /// <param name="pSource"></param>
        /// <param name="pRipRoot"></param>
        /// <returns></returns>
        private static XmlNode CopyNodes(XmlNode pDest, XmlNode pSource, bool pRipRoot)
        {
            try
            {
                XmlDocument docOwner = pDest.OwnerDocument;
                XmlNode tempNode;

                if (pRipRoot)
                {
                    tempNode = pDest;
                }
                else
                {
                    tempNode = docOwner.CreateNode(pSource.NodeType, pSource.Name, pSource.NamespaceURI);
                    pDest.AppendChild(tempNode);

                    foreach (XmlAttribute srcAttrib in pSource.Attributes)
                    {
                        XmlAttribute tempAttrib = docOwner.CreateAttribute(srcAttrib.Name);
                        tempAttrib.Value = srcAttrib.Value;
                        tempNode.Attributes.Append(tempAttrib);
                    }

                }

                XmlNodeList nl = pSource.SelectNodes("node()");
                foreach (XmlNode childNode in nl)
                {

                    switch (childNode.NodeType)
                    {
                        case XmlNodeType.Element:
                            CopyNodes(tempNode, childNode);
                            break;

                        case XmlNodeType.Comment:
                            XmlComment tempComment = docOwner.CreateComment(childNode.Value);
                            tempNode.AppendChild(tempComment);
                            break;

                        case XmlNodeType.Text:
                            XmlText tempText = docOwner.CreateTextNode(childNode.Value);
                            tempNode.AppendChild(tempText);
                            break;

                        case XmlNodeType.CDATA:
                            XmlCDataSection tempCDATA = docOwner.CreateCDataSection(childNode.Value);
                            tempNode.AppendChild(tempCDATA);
                            break;

                    }
                }

                return tempNode;

            }
            catch (System.Exception)
            {
                return null;
            }
        }
        #endregion

        #region [ HandleException ]
        private static XmlNode HandleException(XmlDocument pOwnerDoc, System.Exception pException)
        {
            try
            {
                XmlNode returnNode = CreateElementNode(pOwnerDoc, "Error");

                try
                {
                    SetXmlAttribute(returnNode, "type", pException.GetType().ToString());
                }
                catch (System.Exception)
                { // don't care
                }

                try
                {
                    SetXmlAttribute(returnNode, "source", pException.Source);
                }
                catch (System.Exception)
                { // don't care
                }

                try
                {
                    SetXmlAttribute(returnNode, "targetSite", pException.TargetSite.Name);
                }
                catch (System.Exception)
                { // don't care
                }

                try
                {
                    XmlNode stackEl = CreateElementNode(pOwnerDoc, "ErrorStackTrace");
                    SetXmlTextValue(stackEl, pException.StackTrace);
                    returnNode.AppendChild(stackEl);
                }
                catch (System.Exception)
                { // don't care
                }

                try
                {
                    SetXmlTextValue(returnNode, pException.Message);
                }
                catch (System.Exception)
                { // don't care
                }

                try
                {
                    if (pException.InnerException != null)
                        returnNode.AppendChild(HandleException(pOwnerDoc, pException.InnerException));
                }
                catch (System.Exception)
                { // don't care
                }

                return returnNode;
            }
            catch (System.Exception)
            {
                XmlNode errEl = CreateElementNode(pOwnerDoc, "Error");
                SetXmlAttribute(errEl, "type", "UnhandledError");
                return errEl;
            }

        }
        #endregion


        #endregion

        #region [ TYPE CONVERTERS ]

        #region [DialogResult2String]
        private static string DialogResult2String(DialogResult pDR)
        {
            switch (pDR)
            {
                case DialogResult.Abort: return "abort"; 
                case DialogResult.Cancel: return "cancel";
                case DialogResult.Ignore: return "ignore";
                case DialogResult.No: return "no"; 
                case DialogResult.None: return "none"; 
                case DialogResult.OK: return "ok"; 
                case DialogResult.Retry: return "retry"; 
                case DialogResult.Yes: return "yes"; 
                default: return "unknown"; 
            }
        }
        #endregion

        #region [String2DialogResult]
        private static DialogResult String2DialogResult(string pString)
        {
            switch (pString)
            {
                case "abort": return DialogResult.Abort; 
                case "cancel": return DialogResult.Cancel; 
                case "ignore": return DialogResult.Ignore; 
                case "no": return DialogResult.No; 
                case "none": return DialogResult.None; 
                case "ok": return DialogResult.OK; 
                case "retry": return DialogResult.Retry; 
                case "yes": return DialogResult.Yes;
                default: return DialogResult.Cancel;
            }
        }
        #endregion

        #region [String2MessageBoxButtons]
        private static MessageBoxButtons String2MessageBoxButtons(string pString)
        {
            switch(pString.ToLower())
            {
                case "abort-retry-ignore": return MessageBoxButtons.AbortRetryIgnore; 
                case "ok": return MessageBoxButtons.OK; 
                case "ok-cancel": return MessageBoxButtons.OKCancel; 
                case "retry-cancel": return MessageBoxButtons.RetryCancel; 
                case "yes-no": return MessageBoxButtons.YesNo; 
                case "yes-no-cancel": return MessageBoxButtons.YesNoCancel;
                default: return MessageBoxButtons.OK;
            }
        }
        #endregion

        #region [MessageBoxButtons2String]
        private static string MessageBoxButtons2String(MessageBoxButtons pButtons)
        {
            switch(pButtons)
            {
                case MessageBoxButtons.AbortRetryIgnore: return "abort-retry-ignore"; 
                case MessageBoxButtons.OK: return "ok"; 
                case MessageBoxButtons.OKCancel: return "ok-cancel"; 
                case MessageBoxButtons.RetryCancel: return "retry-cancel"; 
                case MessageBoxButtons.YesNo: return "yes-no"; 
                case MessageBoxButtons.YesNoCancel: return "yes-no-cancel";
                default: return "unknown";
            }
        }
        #endregion

        #region [String2MessageBoxIcon]
        private static MessageBoxIcon String2MessageBoxIcon(string pString)
        {
            switch (pString.ToLower())
            {
                case "asterisk": return MessageBoxIcon.Asterisk; 
                case "error": return MessageBoxIcon.Error; 
                case "exclamation": return MessageBoxIcon.Exclamation; 
                case "hand": return MessageBoxIcon.Hand; 
                case "information": return MessageBoxIcon.Information; 
                case "none": return MessageBoxIcon.None; 
                case "question": return MessageBoxIcon.Question; 
                case "stop": return MessageBoxIcon.Stop; 
                case "warning": return MessageBoxIcon.Warning;
                default: return MessageBoxIcon.None;
            }
        }
        #endregion

        #region [MessageBoxIcon2String]
        private static string MessageBoxIcon2String(MessageBoxIcon pIcon)
        {
            switch (pIcon)
            {
                case MessageBoxIcon.Asterisk: return "asterisk"; 
                case MessageBoxIcon.Error: return "error"; 
                case MessageBoxIcon.Exclamation: return "exclamation"; 
                //case MessageBoxIcon.Hand: return "hand"; 
                //case MessageBoxIcon.Information: return "information"; 
                case MessageBoxIcon.None: return "none"; 
                case MessageBoxIcon.Question: return "question"; 
                //case MessageBoxIcon.Stop: return "stop"; 
                //case MessageBoxIcon.Warning: return "warning";
                default: return "unknown";
            }
        }
        #endregion

        #region [ControlType2String]
        private static string ControlType2String(Control pControl)
        {
            if (pControl != null)
            {
                switch (pControl.GetType().ToString())
                {
                    case "System.Windows.Forms.Label": return "label";
                    case "System.Windows.Forms.TextBox": return "textbox";
                    case "System.Windows.Forms.PictureBox": return "image";
                    case "System.Windows.Forms.Button": return "button";
                    case "System.Windows.Forms.CheckBox": return "checkbox";
                    case "System.Windows.Forms.RadioButton": return "radio";
                    case "System.Windows.Forms.ComboBox": return "combo";
                    case "System.Windows.Forms.ListBox": return "listbox";
                    case "System.Windows.Forms.CheckedListBox": return "checked-listbox";
                    case "System.Windows.Forms.NumericUpDown": return "numeric";
                    case "System.Windows.Forms.DateTimePicker": return "date";
                    default: return "unknown";
                }
            }
            else
                return "null";
        }
        #endregion

        #region [String2FontStyle]
        private static FontStyle String2FontStyle(string pString)
        {
            switch (pString.ToLower())
            {
                case "bold": return FontStyle.Bold;
                case "italic": return FontStyle.Italic;
                case "underline": return FontStyle.Underline;
                case "strikeout": return FontStyle.Strikeout;

                case "bold-italic": return (FontStyle.Bold | FontStyle.Italic);
                case "bold-underline": return (FontStyle.Bold | FontStyle.Underline);
                case "bold-strikeout": return (FontStyle.Bold | FontStyle.Strikeout);

                case "italic-underline": return (FontStyle.Italic | FontStyle.Underline);
                case "italic-strikeout": return (FontStyle.Italic | FontStyle.Strikeout);

                case "underline-strikeout": return (FontStyle.Underline | FontStyle.Strikeout);

                case "bold-italic-underline": return (FontStyle.Bold | FontStyle.Italic | FontStyle.Underline);
                case "bold-italic-strikeout": return (FontStyle.Bold | FontStyle.Italic | FontStyle.Strikeout);

                case "italic-underline-strikeout": return (FontStyle.Italic | FontStyle.Underline | FontStyle.Strikeout);

                case "bold-italic-underline-strikeout": return (FontStyle.Bold | FontStyle.Italic | FontStyle.Underline | FontStyle.Strikeout);

                default: return FontStyle.Regular;
            }
        }
        #endregion

        #region [String2ScrollBars]
        private static ScrollBars String2ScrollBars(string pString)
        {
            switch (pString.ToLower())
            {
                case "both": return ScrollBars.Both;

                case "h":
                case "hor":
                case "horiz":
                case "horizontal": 
                    return ScrollBars.Horizontal;

                case "v":
                case "ver":
                case "vert":
                case "vertical":
                    return ScrollBars.Vertical;

                default: return ScrollBars.None;
            }
        }
        #endregion

        #region [String2FlatStyle]
        private static FlatStyle String2FlatStyle(string pString)
        {
            switch (pString.ToLower())
            {
                case "flat": return FlatStyle.Flat;
                case "popup": return FlatStyle.Popup;
                case "system": return FlatStyle.System;
                
                case "standard":
                default: 
                    return FlatStyle.Standard;
            }
        }
        #endregion

        #region [String2BorderStyle]
        private static BorderStyle String2BorderStyle(string pString)
        {
            switch (pString.ToLower())
            {
                case "3d": return BorderStyle.Fixed3D;
                case "single": return BorderStyle.FixedSingle;
                
                case "none":
                default: 
                    return BorderStyle.None;
            }
        }
        #endregion

        #region [String2Appearance]
        private static Appearance String2Appearance(string pString)
        {
            switch (pString.ToLower())
            {
                case "button": return Appearance.Button;
                default: return Appearance.Normal;
            }
        }
        #endregion

        #region [String2CheckState]
        private static CheckState String2CheckState(string pString)
        {
            switch (pString.ToLower())
            {
                case "checked": return CheckState.Checked;
                case "indeterminate": return CheckState.Indeterminate;
                case "unchecked":
                default: return CheckState.Unchecked;
            }
        }
        #endregion



        #endregion
    }
    #endregion

}
#endregion

