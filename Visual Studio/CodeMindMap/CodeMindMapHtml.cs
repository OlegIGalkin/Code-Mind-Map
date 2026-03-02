using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace CodeMindMap
{
    public class CodeMindMapHtml
    {
        public const string DefaultFileName = "CodeMindMap.html";
        public const string TempDirName = "CodeMindMap_Temp";

        public static readonly string DefaultHtmlContent =
            @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Code Mind Map</title>
    <style>
        #map {
            width: 100%;
            height: 100vh;
        }
        body {
            margin: 0;
            padding: 0;
        }
        /* Root node: match the same padding-to-font ratio as level-1 nodes.
           Level-1 uses 8px/25px padding with 14px font (ratios 0.57v / 1.79h).
           Root has 25px font, so target: 14px vertical / 45px horizontal. */
        .map-container me-root me-tpc {
            padding: 14px 45px !important;
        }

        /* Status indicator styles */
        /* Root (45px horizontal padding) and level-1 (25px padding) already have enough room for the icon */
        .map-container me-tpc {
            position: relative;
        }
        /* Level-2+ nodes: always reserve icon space so text never shifts when status changes. */
        .map-container me-children me-parent me-tpc {
            padding-left: 20px;
            box-sizing: border-box;
        }
        .map-container me-tpc[data-status=""completed""] {
            text-decoration: line-through;
        }
        .map-container me-tpc[data-status=""completed""] > .text {
            text-decoration: line-through !important;
        }
        .map-container me-tpc[data-status]::before {
            position: absolute;
            left: 2px; /* level-2+: within the 20px padding we add */
            top: 50%;
            transform: translateY(-50%);
            width: 14px;
            text-align: center;
            font-weight: bold;
        }
        /* Center icon in the existing left padding for root (45px) and level-1 (25px) */
        .map-container me-root me-tpc[data-status]::before {
            left: 16px; /* (45px - 14px) / 2 */
        }
        .map-container me-main > me-wrapper > me-parent > me-tpc[data-status]::before {
            left: 6px; /* (25px - 14px) / 2 */
        }
        .map-container me-tpc[data-status=""in-progress""]::before {
            content: '⟳';
            color: #ff9800;
        }
        .map-container me-tpc[data-status=""completed""]::before {
            content: '✓';
            color: #4caf50;
        }
    </style>
</head>
<body>
    <div id=""map""></div>

    <script type=""module"">
        import MindElixir from ""http://codemindmap.vsext/MindElixir.js"";

        let mind, themeManager;
        let linkDivDebounceTimer = null;
        let scheduleRafHandle = null;
        let scheduleTimerHandle = null;

        function initMindMap() {
            const options = {
                el: '#map',
                allowUndo: true,
                toolBar: true,
                contextMenu: {
                    extend: [
                        {
                            name: '⟳ In Progress',
                            onclick: () => {
                                const node = mind.currentNode?.nodeObj;
                                if (!node) return;
                                node.data = node.data || {};
                                node.data.status = 'in-progress';
                                updateNodeStatus(node);
                                window.chrome.webview.postMessage({ action: 'mindMapOperation', operationName: 'updateNodeStatus' });
                                const cm = document.querySelector('.map-container > .context-menu'); if (cm) cm.hidden = true;
                            }
                        },
                        {
                            name: '✓ Completed',
                            onclick: () => {
                                const node = mind.currentNode?.nodeObj;
                                if (!node) return;
                                node.data = node.data || {};
                                node.data.status = 'completed';
                                updateNodeStatus(node);
                                window.chrome.webview.postMessage({ action: 'mindMapOperation', operationName: 'updateNodeStatus' });
                                const cm = document.querySelector('.map-container > .context-menu'); if (cm) cm.hidden = true;
                            }
                        },
                        {
                            name: '✕ Clear Status',
                            onclick: () => {
                                const node = mind.currentNode?.nodeObj;
                                if (!node) return;
                                node.data = node.data || {};
                                delete node.data.status;
                                updateNodeStatus(node);
                                window.chrome.webview.postMessage({ action: 'mindMapOperation', operationName: 'updateNodeStatus' });
                                const cm = document.querySelector('.map-container > .context-menu'); if (cm) cm.hidden = true;
                            }
                        },
                    ]
                },
                view: {
                    beforeSelect(el, node) {
                        mind.currentNode = node;
                        return true;
                    }
                }
            };

        const LIGHT_THEME = {
                                name: 'Light',
                                // Updated color palette with more vibrant colors
                                palette: ['#dd7878', '#ea76cb', '#8839ef', '#e64553', '#fe640b', '#df8e1d', '#40a02b', '#209fb5', '#1e66f5', '#7287fd'],
                                // Enhanced CSS variables for better styling control
                                cssVar: {
                                    '--node-gap-x': '30px',
                                    '--node-gap-y': '10px',
                                    '--main-gap-x': '32px',
                                    '--main-gap-y': '12px',
                                    '--root-radius': '30px',
                                    '--main-radius': '20px',
                                    '--root-color': '#ffffff',
                                    '--root-bgcolor': '#4c4f69',
                                    '--root-border-color': 'rgba(0, 0, 0, 0)',
                                    '--main-color': '#444446',
                                    '--main-bgcolor': '#ffffff',
                                    '--topic-padding': '3px',
                                    '--color': '#333333',
                                    '--bgcolor': '#f6f6f6',
                                    '--selected': '#4dc4ff',
                                    '--panel-color': '#444446',
                                    '--panel-bgcolor': '#ffffff',
                                    '--panel-border-color': '#eaeaea',
                                    '--map-padding': '50px 80px',
                                    'font-size': '14px',
                                    'font-family':'Verdana',
                                },
                              };

        const DARK_THEME = {
                                name: 'Dark',
                                // Updated color palette with more vibrant colors
                                palette: ['#848FA0', '#748BE9', '#D2F9FE', '#4145A5', '#789AFA', '#706CF4', '#EF987F', '#775DD5', '#FCEECF', '#DA7FBC'],
                                // Enhanced CSS variables for better styling control
                                cssVar: {
                                    '--node-gap-x': '30px',
                                    '--node-gap-y': '10px',
                                    '--main-gap-x': '32px',
                                    '--main-gap-y': '12px',
                                    '--root-radius': '30px',
                                    '--main-radius': '20px',
                                    '--root-color': '#ffffff',
                                    '--root-bgcolor': '#4c4f69',
                                    '--root-border-color': 'rgba(0, 0, 0, 0)',
                                    '--main-color': '#ffffff',
                                    '--main-bgcolor': '#4c4f69',
                                    '--topic-padding': '3px',
                                    '--color': '#E0E0E0',
                                    '--bgcolor': '#252526',
                                    '--selected': '#4dc4ff',
                                    '--panel-color': '#ffffff',
                                    '--panel-bgcolor': '#2d3748',
                                    '--panel-border-color': '#696969',
                                    '--map-padding': '50px 80px',
                                    'font-size': '14px',
                                    'font-family':'Verdana',
                                },
                              };

        themeManager = {
            themes: {
                Light: LIGHT_THEME,
                Dark: DARK_THEME,
            },
    
            getTheme(themeName) {
                return this.themes[themeName];
            },

            contains(themeName) {
                    return this.themes.hasOwnProperty(themeName);
                },

            getAvailableThemes() {
                    return Object.keys(this.themes);
                },
        };
            
            const data = {
                nodeData: {
                    id: 'me-root',
                    topic: 'Code Mind Map',
                    children: [
                        {
                            topic: 'Code',
                            id: 'bd1f03fee1f63bc6',
                            direction: 0,
                            expanded: true,
                            children: [
                                {
                                    topic: 'Ctrl+2, Ctrl+2 — add the selected code (line under the caret) as a node linked to the code.',
                                    id: 'bd1f07c598e729dc',
                                },
                                {
                                    topic: 'Ctrl+click on a node - jump to the code linked to the node.',
                                    id: 'bd1bb4b14d6697c3',
                                },
                            ],
                        },
                        {
                            topic: 'Mind Map',
                            id: 'bd1b66c4b56754d9',
                            direction: 1,
                            expanded: true,
                            children: [
                                {
                                    topic: 'Alt+Scroll - Zoom in/out',
                                    id: 'bd1c1cb51e6745d3',
                                },
                                {
                                    topic: 'Shift+Scroll - Scroll horizontally',
                                    id: '16710cea5cfb50712c8832676b87d2cc',
                                },
                                {
                                    topic: 'Right Click+Drag - Move the mind map',
                                    id: 'bd1c1e12fd603ff6',
                                },
                                {
                                    topic: 'tab - Create a child node',
                                    id: 'bd1b6892bcab126a',
                                },
                                {
                                    topic: 'enter - Create a sibling node',
                                    id: 'bd1b6b632a434b27',
                                },
                                {
                                    topic: 'del - Remove a node',
                                    id: 'bd1b983085187c0a',
                                },
                                {
                                    topic: 'space - Expand/collapse nodes',
                                    id: 'bd1bb2ac4bbab458',
                                },
                                {
                                    topic: 'c - Cycle node status: (none) → In Progress → Completed → (none)',
                                    id: 'bd1bb2ac4bbab460',
                                    children: [
                                        {
                                            topic: 'In Progress nodes show ⟳ in orange',
                                            id: 'bd1bb2ac4bbab461',
                                        },
                                        {
                                            topic: 'Completed nodes show ✓ with strikethrough text',
                                            id: 'bd1bb2ac4bbab462',
                                        },
                                        {
                                            topic: 'Right-click a node to set status directly from the context menu',
                                            id: 'bd1bb2ac4bbab463',
                                        },
                                        {
                                            topic: 'Status is saved automatically with the diagram',
                                            id: 'bd1bb2ac4bbab464',
                                        },
                                    ],
                                },
                            ],
                        },
                    ],
                    expanded: true,
                },
                theme: themeManager.getTheme(LIGHT_THEME.name),
                direction: 2
            };

            mind = new MindElixir(options);
            mind.init(data);

            // Intercept direction-change methods so autosave is triggered when the
            // user switches between left tree, right tree, and flower (side) views.
            ['initLeft', 'initRight', 'initSide'].forEach(method => {
                const original = mind[method].bind(mind);
                mind[method] = function() {
                    original();
                    window.chrome.webview.postMessage({ action: 'mindMapOperation', operationName: 'changeDirection' });
                };
            });
            
            scheduleApplyAllStatuses();
            // Helper function to update node visual status
            function updateNodeStatus(nodeObj) {
                if (!nodeObj || !nodeObj.id) return;
                const nodeElement = MindElixir.E(nodeObj.id);
                if (!nodeElement) return;

                const status = nodeObj.data?.status || null;
                const domEl = nodeElement.getEl?.() || nodeElement;
                if (!domEl) return;

                const topicEl = (() => {
                    if (domEl.tagName === 'ME-TPC') return domEl;
                    const byQuery = domEl.querySelector?.('me-tpc');
                    if (byQuery) return byQuery;
                    const byTag = domEl.getElementsByTagName?.('me-tpc')?.[0];
                    if (byTag) return byTag;
                    return domEl;
                })();

                if (!topicEl) return;

                if (!status) {
                    topicEl.removeAttribute('data-status');
                    return;
                }

                topicEl.setAttribute('data-status', status);
            }

            function applyAllStatuses() {
                const root = mind?.nodeData;
                if (!root) return;
                const stack = [root];
                while (stack.length > 0) {
                    const node = stack.pop();
                    if (!node) continue;
                    updateNodeStatus(node);
                    if (Array.isArray(node.children)) {
                        for (const child of node.children) {
                            stack.push(child);
                        }
                    }
                }
            }

            function scheduleApplyAllStatuses() {
                if (!mind) return;
                if (scheduleRafHandle !== null) cancelAnimationFrame(scheduleRafHandle);
                if (scheduleTimerHandle !== null) clearTimeout(scheduleTimerHandle);
                scheduleRafHandle = requestAnimationFrame(() => { applyAllStatuses(); scheduleRafHandle = null; });
                scheduleTimerHandle = setTimeout(() => { applyAllStatuses(); scheduleTimerHandle = null; }, 50);
            }
            
            mind.bus.addListener('selectNode', node => {
                window.chrome.webview.postMessage({
                    action: 'nodeSelected',
                    nodeId: node.id,
                    nodeTopic: node.topic,
                    nodeData: node.data,
                });
                scheduleApplyAllStatuses();
            });

            // Debounced linkDiv listener: MindElixir fires linkDiv after every layout pass.
            // Wait for 50ms of silence before applying statuses so we run after the final DOM state.
            mind.bus.addListener('linkDiv', () => {
                clearTimeout(linkDivDebounceTimer);
                linkDivDebounceTimer = setTimeout(applyAllStatuses, 50);
            });

            mind.bus.addListener('operation', operation => {
                window.chrome.webview.postMessage({
                    action: 'mindMapOperation',
                    operationName: operation.name,
                });
                scheduleApplyAllStatuses();
            });

            document.addEventListener('click', (e) => {
                if (e.ctrlKey && e.target.tagName === 'ME-TPC') {
                    const currentNodeObj = mind.currentNode?.nodeObj
                    if (currentNodeObj) {
                        window.chrome.webview.postMessage({
                            action: 'nodeNavigate',
                            nodeId: currentNodeObj.id,
                            nodeTopic: currentNodeObj.topic,
                            nodeData: currentNodeObj.data,
                        });
                    }
                }
            });
            
            document.addEventListener('keydown', function(e) {
                if (e.key === ' ' || e.key === 'Spacebar') {
                    e.preventDefault();
                    const targetNodeId = mind.currentNode?.nodeObj?.id
                    if (!targetNodeId) return;
                    const targetNode = MindElixir.E(targetNodeId);
                    if (targetNode) mind.expandNode(targetNode);
                }
                else if ((e.ctrlKey || e.metaKey) && e.key === 'c') {
                    const currentNodeObj = mind.currentNode?.nodeObj
                    if (currentNodeObj) {
                        window.chrome.webview.postMessage({
                            action: 'nodeCopy',
                            nodeId: currentNodeObj.id,
                            nodeTopic: currentNodeObj.topic,
                            nodeData: currentNodeObj.data,
                        });
                    }
                }
                else if ((e.key === 'c' || e.key === 'C') && !e.ctrlKey && !e.metaKey && !e.altKey && !e.shiftKey) {
                    // Skip if MindElixir's inline editor is open
                    if (document.getElementById('input-box')) return;
                    e.preventDefault();
                    const currentNode = mind.currentNode?.nodeObj;
                    if (!currentNode) return;
                    currentNode.data = currentNode.data || {};

                    // Cycle: (none) -> in-progress -> completed -> (none)
                    const statuses = [null, 'in-progress', 'completed'];
                    const currentStatus = currentNode.data.status || null;
                    const currentIndex = statuses.indexOf(currentStatus);
                    const next = statuses[(currentIndex + 1) % statuses.length];
                    if (next === null) {
                        delete currentNode.data.status;
                    } else {
                        currentNode.data.status = next;
                    }

                    updateNodeStatus(currentNode);
                    window.chrome.webview.postMessage({ action: 'mindMapOperation', operationName: 'updateNodeStatus' });
                }
            });

            document.addEventListener('wheel', function(e) {
                if (e.altKey) {
                    e.preventDefault();
                    const delta = e.deltaY;
                    if (delta > 0) {
                            // Handle scroll down
                            if (mind.scaleVal < 0.6) return
                            mind.scale((mind.scaleVal -= 0.2))
                        } else if (delta < 0) {
                            // Handle scroll up
                            if (mind.scaleVal > 1.6) return
                            mind.scale((mind.scaleVal += 0.2))
                        }
                }
            }, { passive: false });        

        }

        window.addChildNode = function(topic = 'New Child Node', codeInfoObject) {
            if (!mind) {
                console.error(""Mind map not initialized"");
                return { success: false, error: ""Mind map not initialized"" };
            }
            
            if (topic.startsWith('""') && topic.endsWith('""')) {
                topic = topic.substring(1, topic.length - 1);
            }
            
            const { fileName, filePath, topLine } = codeInfoObject;
            const codeInfo = { fileName, filePath, topLine };

            const childData = {
                id: 'child_' + Date.now(),
                topic: topic,
                children: [],
                data: codeInfo, 
                tags: [codeInfo.fileName]
            };

            try {
                const targetNodeId = mind.currentNode?.nodeObj?.id || 'me-root';
                const targetNode = MindElixir.E(targetNodeId);
                if (!targetNode) return { success: false, error: ""Target node not found"" };
                mind.addChild(targetNode, childData);
                mind.selectNode(MindElixir.E(childData.id));
                return { 
                    success: true, 
                    nodeId: childData.id,
                    parentId: targetNodeId
                };
            } catch (e) {
                console.error(""Error adding child node:"", e);
                return { success: false, error: e.message };
            }
        };

        window.exportData = function() {
            if (!mind) return { success: false, error: ""Mind map not initialized"" };
            try {
                return JSON.stringify(JSON.parse(mind.getDataString()), null, 2);
            } catch (e) {
                console.error(""Error exporting data:"", e);
                return { success: false, error: e.message };
            }
        };

        function getThemeName(mindElixirData) {
            try {
                // First, check if the input is valid
                if (!mindElixirData || typeof mindElixirData !== 'object') {
                    return '';
                }
        
                // Safely navigate through the nested structure
                const theme = mindElixirData.theme || 
                             (mindElixirData.nodeData && mindElixirData.nodeData.theme);
        
                // Check if theme exists and has a name property
                if (theme && typeof theme === 'object' && theme.name) {
                    return String(theme.name);
                }
        
                return '';
            } catch (error) {
                // Catch any unexpected errors and return empty string
                return '';
            }
        }
        
        window.importData = function(mindDataString = '') {
            if (!mind) return { success: false, error: ""Mind map not initialized"" };
            if (mindDataString == '') return { success: false, error: ""No mind map data"" };
            
            try {

                if (mindDataString.startsWith('""') && mindDataString.endsWith('""')) {
                    mindDataString = mindDataString.substring(1, mindDataString.length - 1);
                }

                const mindData = JSON.parse(mindDataString);

                // mind.refresh() does not restore direction (unlike mind.init()),
                // so apply it manually before calling refresh so layout() picks it up.
                if (typeof mindData.direction === 'number') {
                    mind.direction = mindData.direction;
                }

                mind.refresh(mindData);

                const dataThemeName = getThemeName(mindData);
                if (dataThemeName != '' && themeManager.contains(dataThemeName) && dataThemeName != mind.theme?.name) {
                    mind.changeTheme(themeManager.getTheme(dataThemeName));
                }
                // Statuses are applied via the debounced linkDiv bus listener
                // which fires after MindElixir's layout settles.
                
                return { success: true, error: """" };
            } catch (e) {
                console.error(""Error importing data:"", e);
                return { success: false, error: e.message };
            }
        };

        window.toggleColorScheme = function() {

            if (!mind) return { success: false, error: ""Mind map not initialized"" };

            try {
                const availableThemeKeys = themeManager.getAvailableThemes();
                const currentThemeName = mind.theme?.name;
                const themeKeyToApply = availableThemeKeys.find(key => 
                    themeManager.getTheme(key)?.name !== currentThemeName
                );
                if (themeKeyToApply) {
                    const themeToApply = themeManager.getTheme(themeKeyToApply);
                    mind.changeTheme(themeToApply);
                    return { success: true, error: '' };
                }
                return { success: false, error: 'No alternative theme found' };
            } catch (e) {
                console.error(""Error toggling the color scheme:"", e);
                return { success: false, error: e.message };
            }

        };

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', initMindMap);
        } else {
            initMindMap();
        }
    </script>
</body>
</html>";

        public string DirectoryPath { get; }
        public string FilePath { get; }
        public string DirectoryName { get; }

        public CodeMindMapHtml(string vsTempPath)
            : this(vsTempPath, null)
        {
            _vsTempPath = vsTempPath;
        }

        private readonly string _vsTempPath;

        public CodeMindMapHtml(string vsTempPath, string directoryName)
        {
            _vsTempPath = vsTempPath;

            DirectoryName = directoryName ?? Path.GetRandomFileName();
            DirectoryPath = Path.Combine(_vsTempPath, DirectoryName);
            FilePath = Path.Combine(DirectoryPath, DefaultFileName);

            CreateMindMapFile(DirectoryPath, FilePath);
            CopyMindElixir(DirectoryPath);
        }

        private static void CreateMindMapFile(string directoryPath, string filePath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                File.WriteAllText(filePath, DefaultHtmlContent);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Error creating a mind map file: {exception.Message}");
            }
        }

        private static void CopyMindElixir(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string extensionAssemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string extensionDirectory = Path.GetDirectoryName(extensionAssemblyLocation);

                string mindElixirDir = Path.Combine(extensionDirectory, "MindElixir");

                string[] files = Directory.GetFiles(mindElixirDir);

                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(directoryPath, fileName);

                    File.Copy(file, destFile, true);
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Error copying mind elixir files: {exception.Message}");
            }
        }

        public static string GetVsTempRootPath(IServiceProvider serviceProvider)
        {

            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var shell = serviceProvider.GetService(typeof(SVsShell)) as IVsShell;
                if (shell == null)
                {
                    return string.Empty;
                }

                if (shell.GetProperty((int)__VSSPROPID.VSSPROPID_VirtualRegistryRoot, out object registryRoot) != VSConstants.S_OK)
                {
                    return GetLocalApplicationDataDirPath();
                }

                string versionFolder = Path.GetFileName(registryRoot.ToString());

                string vsTempPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft",
                    "VisualStudio",
                    versionFolder,
                    TempDirName);

                return vsTempPath;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Error getting VSSPROPID_VirtualRegistryRoot: {exception.Message}");

                return GetLocalApplicationDataDirPath();
            }
        }

        public static string GetLocalApplicationDataDirPath()
        {
            const string LocalAppDataDirName = "CodeMindMap";

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LocalAppDataDirName, TempDirName);
        }
    }
}
