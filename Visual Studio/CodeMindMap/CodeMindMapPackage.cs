using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace CodeMindMap
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(CodeMindMapPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(MindMapToolWindow), Style = VsDockStyle.Tabbed, DockedWidth = 300, Window = "DocumentWell", Orientation = ToolWindowOrientation.Bottom)]
    public sealed class CodeMindMapPackage : AsyncPackage
    {
        /// <summary>
        /// CodeMindMapPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "c0307968-224e-4754-9d45-78088d021aa2";

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Debug.WriteLine("CodeMindMapPackage -> InitializeAsync");

            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            CreateLocalApplicationDataFolder();

            await MindMapToolWindowCommand.InitializeAsync(this);
            await AddToMindMapCommand.InitializeAsync(this);

            bool isSolutionLoaded = await IsSolutionLoadedAsync();
            if (isSolutionLoaded)
            {
                await HandleSolutionOpened();
            }

            SolutionEvents.OnAfterOpenSolution += SolutionEvents_OnAfterOpenSolution;
            SolutionEvents.OnAfterCloseSolution += SolutionEvents_OnAfterCloseSolution;
        }

        private void CreateLocalApplicationDataFolder()
        {
            if (Directory.Exists(AppDataFolderPath))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(AppDataFolderPath);
            }
            catch (Exception)
            {

            }
        }

        private string AppDataFolderPath
        {
            get
            {
                if (string.IsNullOrEmpty(_appDataFolderPath))
                {
                    _appDataFolderPath = CodeMindMapHtml.GetLocalApplicationDataDirPath();
                }
                return _appDataFolderPath;
            }
        }

        private string _appDataFolderPath;

        public CodeMindMapHtml MindMapHtmlFile
        {
            get
            {
                if (_mindMapHtml == null)
                {
                    _mindMapHtml = new CodeMindMapHtml(AppDataFolderPath, "Web");
                }
                return _mindMapHtml;
            }
        }

        private CodeMindMapHtml _mindMapHtml;

        public SolutionMindMapData CurrentSolutionMindMapData => _currentSolutionMindMapData;

        private SolutionMindMapData _currentSolutionMindMapData = new SolutionMindMapData();

        public void SetMindMapDataFilePath(string filePath)
        {
            _currentSolutionMindMapData.MindMapDataFilePath = filePath;
        }

        private async void SolutionEvents_OnAfterOpenSolution(object sender, OpenSolutionEventArgs eventArgs)
        {
            await HandleSolutionOpened();
        }

        private async Task HandleSolutionOpened()
        {
            LoadSolutionMindMapDataFromSettings();

            if (!_currentSolutionMindMapData.IsEmpty)
            {
                await LoadMindMapData();
                return;
            }

            SetDefaultSolutionMindMapData();

            SaveSolutionMindMapDataToSettings();

            await ReloadMindMapBrowser();
        }

        public void SetDefaultSolutionMindMapData()
        {
            var solutionId = GetSolutionId();
            if (string.IsNullOrEmpty(solutionId))
            {
                return;
            }

            var solutionFilePath = GetSolutionPath();
            if (string.IsNullOrEmpty(solutionFilePath))
            {
                return;
            }

            var solutionMindMapData = new SolutionMindMapData(solutionId, solutionFilePath, AppDataFolderPath);
            if (string.IsNullOrEmpty(solutionMindMapData.MindMapDataFilePath))
            {
                return;
            }

            _currentSolutionMindMapData = solutionMindMapData;
        }

        private async void SolutionEvents_OnAfterCloseSolution(object sender, EventArgs eventArgs)
        {
            SetEmptySolutionMindMapData();

            await ReloadMindMapBrowser();
        }

        public void SetEmptySolutionMindMapData()
        {
            _currentSolutionMindMapData = new SolutionMindMapData();
        }

        private async Task ReloadMindMapBrowser()
        {
            try
            {
                var window = await FindToolWindowAsync(typeof(MindMapToolWindow), 0, false, DisposalToken) as MindMapToolWindow;
                if (window == null)
                {
                    return;
                }

                var control = window.Content as MindMapToolWindowControl;
                if (control == null)
                {
                    return;
                }

                control.ReloadMindMapBrowser();
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Failed to ReloadMindMapBrowser: {exception.Message}");
            }
        }

        private async Task LoadMindMapData()
        {
            try
            {
                var window = await FindToolWindowAsync(typeof(MindMapToolWindow), 0, false, DisposalToken) as MindMapToolWindow;
                if (window == null)
                {
                    return;
                }

                var control = window.Content as MindMapToolWindowControl;
                if (control == null)
                {
                    return;
                }

                await control.LoadMindMapData(CurrentSolutionMindMapData.MindMapDataFilePath);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Failed to LoadMindMapData: {exception.Message}");
            }
        }

        private async Task<bool> IsSolutionLoadedAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            var solService = await GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            if (solService == null)
            {
                return false;
            }

            try
            {
                ErrorHandler.ThrowOnFailure(solService.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen, out object value));

                return value is bool isSolOpen && isSolOpen;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Fail to get VSPROPID_IsSolutionOpen: {exception.Message}");
                return false;
            }

        }

        private void LoadSolutionMindMapDataFromSettings()
        {
            var solutionId = GetSolutionId();
            if (string.IsNullOrEmpty(solutionId))
            {
                return;
            }

            var solutionFilePath = GetSolutionPath();
            if (string.IsNullOrEmpty(solutionFilePath))
            {
                return;
            }

            var settingsManager = new MindMapSettingsManager(this);

            var mindMapData = settingsManager.LoadSolutionMindMapData();

            var solutionMindMapData = mindMapData.FirstOrDefault(mmd => solutionId.Equals(mmd.Id) && solutionFilePath.Equals(mmd.SolutionFilePath));

            if (solutionMindMapData == null || solutionMindMapData.IsEmpty)
            {
                return;
            }

            _currentSolutionMindMapData = solutionMindMapData;
        }

        public void SaveSolutionMindMapDataToSettings()
        {
            if (_currentSolutionMindMapData.IsEmpty)
            {
                return;
            }

            var solutionId = GetSolutionId();
            if (string.IsNullOrEmpty(solutionId))
            {
                return;
            }

            var solutionFilePath = GetSolutionPath();
            if (string.IsNullOrEmpty(solutionFilePath))
            {
                return;
            }

            var settingsManager = new MindMapSettingsManager(this);

            var mindMapData = settingsManager.LoadSolutionMindMapData();

            bool settingsFound = false;
            foreach (var mmd in mindMapData)
            {
                if (solutionId.Equals(mmd.Id) && solutionFilePath.Equals(mmd.SolutionFilePath))
                {
                    mmd.MindMapDataFilePath = _currentSolutionMindMapData.MindMapDataFilePath;
                    settingsFound = true;
                    break;
                }
            }

            if (!settingsFound)
            {
                mindMapData.Add(new SolutionMindMapData()
                {
                    Id = solutionId,
                    SolutionFilePath = solutionFilePath,
                    MindMapDataFilePath = _currentSolutionMindMapData.MindMapDataFilePath
                });
            }

            settingsManager.SaveSolutionMindMapData(mindMapData);

        }

        private string GetSolutionId()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var dte = GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                if (dte == null)
                {
                    return string.Empty;
                }

                return Path.GetFileNameWithoutExtension(dte.Solution.FullName).ToLower();
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Fail to get Solution Id: {exception.Message}");

                return string.Empty;
            }
        }

        private string GetSolutionPath()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var dte = GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                if (dte == null)
                {
                    return string.Empty;
                }

                return dte.Solution.FullName;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Fail to get Solution Path: {exception.Message}");

                return string.Empty;
            }
        }

        #endregion
    }
}
