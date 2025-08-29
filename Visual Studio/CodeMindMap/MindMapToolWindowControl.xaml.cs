using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.Web.WebView2.Core;
using TextSelection = EnvDTE.TextSelection;
using Task = System.Threading.Tasks.Task;
using System.Text;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Linq;
using CodeMindMap.MindMap;
using System.Threading.Tasks;

namespace CodeMindMap
{
    public partial class MindMapToolWindowControl : UserControl
    {
        public MindMapToolWindowControl(MindMapToolWindow toolWindow)
        {
            _toolWindow = toolWindow;

            InitializeComponent();
        }

        private readonly MindMapToolWindow _toolWindow;

        private CodeMindMapPackage MindMapPackage { get { return _toolWindow.Package as CodeMindMapPackage; } }

        private bool _loaded = false;

        private async void UserControl_Loaded(object sender, RoutedEventArgs eventArgs)
        {
            Debug.WriteLine("UserControl_Loaded");

            if (_loaded)
            {
                return;
            }

            _loaded = true;

#if DEBUG
            OpenDevToolsButton.Visibility = Visibility.Visible;
#else
            OpenDevToolsButton.Visibility = Visibility.Collapsed;
#endif

            await InitializeWebView2Async();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs eventArgs)
        {
            Debug.WriteLine("UserControl_Unloaded");
        }

        private async void MindMapBrowser_Loaded(object sender, RoutedEventArgs eventArgs)
        {
            Debug.WriteLine("MindMapBrowser_Loaded");

            await LoadMindMapData();
        }

        private void MindMapBrowser_Unloaded(object sender, RoutedEventArgs eventArgs)
        {
            Debug.WriteLine("MindMapBrowser_Unloaded");
        }

        private async Task InitializeWebView2Async()
        {
            try
            {
                //additionalBrowserArguments: "--disable-web-security --allow-running-insecure-content"
                var coreEnvOptions = new CoreWebView2EnvironmentOptions(additionalBrowserArguments: default, language: default, targetCompatibleBrowserVersion: default, allowSingleSignOnUsingOSPrimaryAccount: default, customSchemeRegistrations: default);

                coreEnvOptions.AreBrowserExtensionsEnabled = false;
                coreEnvOptions.IsCustomCrashReportingEnabled = true;
                coreEnvOptions.EnableTrackingPrevention = false;

                // Create the environment required for the WebView2 control 
                var environment = await CoreWebView2Environment.CreateAsync(null, MindMapPackage?.MindMapHtmlFile.DirectoryPath, coreEnvOptions);

                // Initialize the WebView2 control 
                await MindMapBrowser.EnsureCoreWebView2Async(environment);

                MindMapBrowser.CoreWebView2.SetVirtualHostNameToFolderMapping("codemindmap.vsext", MindMapPackage?.MindMapHtmlFile.DirectoryPath, CoreWebView2HostResourceAccessKind.Allow);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
            }
        }

        public bool IsBrowserReady => (MindMapBrowser?.CoreWebView2) != null && MindMapBrowser.IsInitialized;

        private void MindMapBrowser_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs eventArgs)
        {
            Debug.WriteLine("MindMapBrowser_CoreWebView2InitializationCompleted");

            if (!eventArgs.IsSuccess)
            {
                return;
            }

            MindMapBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

#if DEBUG
            MindMapBrowser.CoreWebView2.Settings.AreDevToolsEnabled = true;
#else
            MindMapBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
#endif

            MindMapBrowser.CoreWebView2.Settings.IsWebMessageEnabled = true;

            MindMapBrowser.CoreWebView2.Navigate("https://codemindmap.vsext/" + CodeMindMapHtml.DefaultFileName);
        }

        private async void MindMapBrowser_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs eventArgs)
        {
            Debug.WriteLine("MindMapBrowser_NavigationCompleted Source: " + MindMapBrowser.Source.ToString());

            await LoadMindMapData();
        }

        private SelectedText GetSelectedText()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                DTE2 dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
                if (dte == null || dte.ActiveDocument == null)
                {
                    return new SelectedText();
                }

                var selection = dte.ActiveDocument.Selection as TextSelection;
                if (selection == null)
                {
                    return new SelectedText();
                }

                var selectionText = selection.Text;

                // Check if no text is selected (just a caret)
                if (string.IsNullOrEmpty(selectionText))
                {
                    // Get the line where the caret is (1-based)
                    EditPoint caretPoint = selection.TopPoint.CreateEditPoint();
                    selectionText = caretPoint.GetLines(caretPoint.Line, caretPoint.Line + 1).Trim();
                }

                return new SelectedText
                {
                    Text = selectionText,
                    TopLine = selection.TopLine,
                    DocumentPath = dte.ActiveDocument.FullName
                };
            }
            catch (Exception)
            {
                return new SelectedText();
            }
        }

        private async void AddChildNodeClick(object sender, RoutedEventArgs eventArgs)
        {
            await AddChildNodeCodeAsync();
        }

        public async Task AddChildNodeCodeAsync()
        {
            if (!IsBrowserReady)
            {
                return;
            }

            var selectedCode = GetSelectedText();
            if (string.IsNullOrEmpty(selectedCode.ToString()))
            {
                return;
            }

            var nodeData = new { fileName = Path.GetFileName(selectedCode.DocumentPath), filePath = selectedCode.DocumentPath, topLine = selectedCode.TopLine };

            string nodeDataObject = JsonConvert.SerializeObject(nodeData);

            var nodeText = selectedCode.Text;

            try
            {
                await MindMapBrowser.ExecuteScriptAsync($"addChildNode('{EncodeJsString(nodeText)}', {nodeDataObject});");
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Executing addChildNode script failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Encodes a string to be represented as a string literal. The format
        /// is essentially a JSON string.
        /// 
        /// The string returned includes outer quotes 
        /// Example Output: "Hello \"Rick\"!\r\nRock on"
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public string EncodeJsString(string str)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\"");
            foreach (char c in str)
            {
                switch (c)
                {
                    case '\"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        int i = (int)c;
                        if (i < 32 || i > 127)
                        {
                            sb.AppendFormat("\\u{0:X04}", i);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append("\"");

            return sb.ToString();
        }

        private void NewCodeMindMapClick(object sender, RoutedEventArgs eventArgs)
        {
            var dialogResult = MessageBox.Show("Create a new code mind map? Your current progress will be lost.", "Create a new code mind map?", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation);

            if (dialogResult == MessageBoxResult.OK)
            {
                ReloadMindMapBrowser();
            }
        }

        public void ReloadMindMapBrowser()
        {
            if (IsBrowserReady)
            {
                MindMapBrowser.Reload();
            }
        }

        private void OpenDevToolsWindowClick(object sender, RoutedEventArgs eventArgs)
        {
            MindMapBrowser.CoreWebView2.OpenDevToolsWindow();
        }

        private async void MindMapBrowser_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs eventArgs)
        {
            string messageJson = eventArgs.WebMessageAsJson;
            if (string.IsNullOrEmpty(messageJson))
            {
                return;
            }

            await OnMindMapOperationAsync(messageJson);

            OnNodeSelected(messageJson);

            OnNodeNavigateMindMapAction(messageJson);
        }

        private async Task OnMindMapOperationAsync(string messageJson)
        {
            MindMapOperation mindMapOperation;

            try
            {
                mindMapOperation = JsonConvert.DeserializeObject<MindMapOperation>(messageJson);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"MindMapOperation parsing failed: {exception.Message}");

                return;
            }

            if (mindMapOperation == null)
            {
                return;
            }

            if (string.Compare(mindMapOperation.Action, "mindMapOperation", StringComparison.OrdinalIgnoreCase) != 0)
            {
                return;
            }

            Debug.WriteLine("Mind Map Operation Name: " + mindMapOperation.OperationName);

            await AutoSaveMindMapData();
        }

        private async Task AutoSaveMindMapData()
        {
            var autoSaveFilePath = MindMapPackage?.CurrentSolutionMindMapData.MindMapDataFilePath;
            if (string.IsNullOrEmpty(autoSaveFilePath))
            {
                return;
            }

            var saveResult = await SaveMindMapData(autoSaveFilePath);
            if (!saveResult)
            {
                MessageBox.Show("Error occurred when saving mind map to the file:\r\n" + autoSaveFilePath, "Error saving mind map", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnNodeSelected(string messageJson)
        {
            MindMapAction mindMapAction;

            try
            {
                mindMapAction = JsonConvert.DeserializeObject<MindMapAction>(messageJson);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"MindMapAction parsing failed: {exception.Message}");

                return;
            }

            if (mindMapAction == null)
            {
                return;
            }

            if (string.Compare(mindMapAction.Action, "nodeSelected", StringComparison.OrdinalIgnoreCase) != 0)
            {
                return;
            }

            NodeSelectedAction = mindMapAction;
        }

        private MindMapAction NodeSelectedAction
        {
            get => _nodeSelectedAction;
            set
            {
                _nodeSelectedAction = value;

                GoToCodeButton.IsEnabled = value != null && value.NodeData != null;
            }
        }

        private MindMapAction _nodeSelectedAction;

        private void OnNodeNavigateMindMapAction(string messageJson)
        {
            MindMapAction mindMapAction;

            try
            {
                mindMapAction = JsonConvert.DeserializeObject<MindMapAction>(messageJson);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"MindMapAction parsing failed: {exception.Message}");

                return;
            }

            if (mindMapAction == null)
            {
                return;
            }

            if (string.Compare(mindMapAction.Action, "nodeNavigate", StringComparison.OrdinalIgnoreCase) != 0)
            {
                return;
            }

            if (mindMapAction.NodeData == null)
            {
                return;
            }

            NavigateToNodeCode(mindMapAction);
        }

        private void NavigateToNodeCode(MindMapAction mindMapAction)
        {
            if (mindMapAction == null || mindMapAction.NodeData == null)
            {
                return;
            }

            var filePath = mindMapAction.NodeData.FilePath;
            var lineNumber = mindMapAction.NodeData.TopLine;

            if (lineNumber == 0)
            {
                return;
            }

            var code = mindMapAction.NodeTopic;

            try
            {
                var contentLineNumber = GetExpectedContentLineNumber(filePath, code);
                if (contentLineNumber > 0)
                {
                    lineNumber = contentLineNumber;
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"GetExpectedContentLineNumber failed: {exception.Message}");

                return;
            }

            NavigateToTextWithChecks(filePath, lineNumber);
        }

        public int GetExpectedContentLineNumber(string filePath, string expectedContent, bool exactMatch = false, bool ignoreCase = false)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;

            Document doc = null;

            // Check if document is already open
            foreach (Document document in dte.Documents)
            {
                if (document.FullName.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                {
                    doc = document;
                    break;
                }
            }

            if (doc == null)
            {
                try
                {
                    doc = dte.ItemOperations.OpenFile(filePath, EnvDTE.Constants.vsViewKindTextView).Document;
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"Navigation failed: {exception.Message}");

                    return 0;
                }
            }

            // Normalize the search pattern
            var searchLines = expectedContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                                           .Select(l => l.Trim())
                                           .Where(l => !string.IsNullOrEmpty(l))
                                           .ToArray();

            if (searchLines.Length == 0)
            {
                return 0;
            }

            try
            {
                var textDoc = doc.Object("TextDocument") as TextDocument;
                var editPoint = textDoc.StartPoint.CreateEditPoint();
                int totalLines = textDoc.EndPoint.Line;

                var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                for (int line = 1; line <= totalLines - searchLines.Length + 1; line++)
                {
                    bool allLinesMatch = true;

                    for (int i = 0; i < searchLines.Length; i++)
                    {
                        string currentLine = editPoint.GetLines(line + i, line + i + 1).Trim();
                        bool lineMatches;

                        if (exactMatch)
                        {
                            lineMatches = currentLine.Equals(searchLines[i], comparison);
                        }
                        else
                        {
                            lineMatches = currentLine.IndexOf(searchLines[i], comparison) >= 0;
                        }

                        if (!lineMatches)
                        {
                            allLinesMatch = false;
                            break;
                        }
                    }

                    if (allLinesMatch)
                    {
                        return line;
                    }
                }

                return 0;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Navigation failed: {exception.Message}");
            }

            return 0;
        }

        public void NavigateToTextWithChecks(string filePath, int lineNumber)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (!File.Exists(filePath))
                {
                    return;
                }

                var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;

                foreach (Document doc in dte.Documents)
                {
                    if (doc.FullName.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        doc.Activate();
                        ((TextSelection)doc.Selection).GotoLine(lineNumber, false);
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Navigation failed: {exception.Message}");

                return;
            }

            NavigateToText(filePath, lineNumber);
        }

        public void NavigateToText(string filePath, int lineNumber)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;

                // Open file and get window reference
                EnvDTE.Window window = dte.ItemOperations.OpenFile(filePath, EnvDTE.Constants.vsViewKindTextView);

                // Verify the window/document opened successfully
                if (window?.Document == null)
                {
                    throw new FileNotFoundException("Failed to open document");
                }

                // Access through either window.Document or dte.ActiveDocument
                Document doc = window.Document; // More explicit than ActiveDocument

                TextSelection selection = (TextSelection)doc.Selection;
                selection.GotoLine(lineNumber, Select: false);

                // Optional: Bring the window to foreground
                //window.Activate();
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Navigation failed: {exception.Message}");
            }
        }

        private async void SaveDataClick(object sender, RoutedEventArgs eventArgs)
        {
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = SolutionMindMapData.DefaultDataFileName,
                DefaultExt = ".txt",
                Filter = "Text documents (.txt)|*.txt",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Title = SaveDataButton.ToolTip.ToString()
            };

            var autoSaveFilePath = MindMapPackage?.CurrentSolutionMindMapData.MindMapDataFilePath;
            if (!string.IsNullOrEmpty(autoSaveFilePath))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(autoSaveFilePath);
            }

            bool? result = dialog.ShowDialog();

            if (!result.Value)
            {
                return;
            }

            var filePath = dialog.FileName;

            var saveResult = await SaveMindMapData(filePath);

            if (saveResult)
            {
                MindMapPackage?.SetMindMapDataFilePath(filePath);
            }
        }

        private async Task<bool> SaveMindMapData(string filePath)
        {
            var exportResult = string.Empty;

            try
            {
                exportResult = await MindMapBrowser.ExecuteScriptAsync("exportData();");
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"ExecuteScriptAsync failed: {exception.Message}");

                return false;
            }

            if (string.IsNullOrWhiteSpace(exportResult))
            {
                return false;
            }

            // Remove surrounding quotes if present
            exportResult = exportResult.Trim('"');

            //Debug.WriteLine("Trim Quotes Export Result: " + exportResult);

            if (string.IsNullOrWhiteSpace(exportResult))
            {
                return false;
            }

            try
            {
                string unescapedExportResult = System.Text.RegularExpressions.Regex.Unescape(exportResult);

                var dir = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(filePath, unescapedExportResult);

                return true;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"File write all text failed: {exception.Message}");

                return false;
            }
        }

        private async void LoadDataClick(object sender, RoutedEventArgs eventArgs)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                FileName = SolutionMindMapData.DefaultDataFileName,
                DefaultExt = ".txt",
                Filter = "Text documents (.txt)|*.txt",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Title = LoadDataButton.ToolTip.ToString()
            };

            var autoSaveFilePath = MindMapPackage?.CurrentSolutionMindMapData.MindMapDataFilePath;
            if (!string.IsNullOrEmpty(autoSaveFilePath))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(autoSaveFilePath);
            }

            bool? result = dialog.ShowDialog();

            if (result != true)
            {
                return;
            }

            string filePath = dialog.FileName;

            var loadResult = await LoadMindMapData(filePath);

            if (loadResult)
            {
                MindMapPackage?.SetMindMapDataFilePath(filePath);
            }
        }

        private async Task<bool> LoadMindMapData()
        {
            var autoSaveFilePath = MindMapPackage?.CurrentSolutionMindMapData.MindMapDataFilePath;
            if (string.IsNullOrEmpty(autoSaveFilePath))
            {
                return false;
            }

            return await LoadMindMapData(autoSaveFilePath);
        }

        public async Task<bool> LoadMindMapData(string filePath)
        {
            string dataRead = string.Empty;

            try
            {
                dataRead = File.ReadAllText(filePath);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"File read all text failed: {exception.Message}");

                return false;
            }

            var importResult = string.Empty;

            try
            {
                importResult = await MindMapBrowser.ExecuteScriptAsync("importData('" + EncodeJsString(dataRead) + "');");
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Executing import data script failed: {exception.Message}");

                return false;
            }

            if (string.IsNullOrEmpty(importResult) || importResult == "null")
            {
                return false;
            }

            MindMapPackage?.SetMindMapDataFilePath(filePath);

            return true;
        }

        private void GotoCodeClick(object sender, RoutedEventArgs eventArgs)
        {
            NavigateToNodeCode(NodeSelectedAction);
        }

        private async void ToggleColorSchemeClick(object sender, RoutedEventArgs eventArgs)
        {
            try
            {
                var result = await MindMapBrowser.ExecuteScriptAsync("toggleColorScheme();");

                Debug.WriteLine($"toggleColorScheme() result: {result}");
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Executing toggleColorScheme script failed: {exception.Message}");
                return;
            }

            await AutoSaveMindMapData();
        }
    }
}