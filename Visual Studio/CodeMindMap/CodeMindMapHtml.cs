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

        private static readonly string DefaultHtmlContent =
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
    </style>
</head>
<body>
    <div id=""map""></div>

    <script type=""module"">
        import MindElixir from ""https://cdn.jsdelivr.net/npm/mind-elixir@^4.0.0/dist/MindElixir.js"";

        let mind;

        function initMindMap() {
            const options = {
                el: '#map',
                allowUndo: true,
                toolBar: false,
                view: {
                    beforeSelect(el, node) {
                        mind.currentNode = node;
                        return true;
                    }
                }
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
                                    topic: 'Ctrl+Mouse Wheel Up/Down - Zoom in/out',
                                    id: 'bd1c1cb51e6745d3',
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
                            ],
                        },
                    ],
                    expanded: true,
                },
                theme: {
                    name: 'Dark',
                    type: 'dark',
                    palette: ['#848FA0', '#748BE9', '#D2F9FE', '#4145A5', '#789AFA', '#706CF4', '#EF987F', '#775DD5', '#FCEECF', '#DA7FBC'],
                    cssVar: {
                        '--main-color': '#ffffff',
                        '--main-bgcolor': '#4c4f69',
                        '--color': '#cccccc',
                        '--bgcolor': '#252526',
                        '--panel-color': '#ffffff',
                        '--panel-bgcolor': '#2d3748',
                        '--panel-border-color': '#696969',
                        'font-size': '10px',
                    },
                },
                direction: 2
            };

            mind = new MindElixir(options);
            mind.init(data);
            
            mind.bus.addListener('selectNode', node => {
                window.chrome.webview.postMessage({
                    action: 'nodeSelected',
                    nodeId: node.id,
                    nodeTopic: node.topic,
                    nodeData: node.data,
                });
            });

            mind.bus.addListener('operation', operation  => {
				window.chrome.webview.postMessage({
						action: 'mindMapOperation',
						operationName: operation.name,
					});
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
                    const nodeText = mind.currentNode?.nodeObj?.topic
                    if (!nodeText) return;
                    navigator.clipboard.writeText(nodeText)
                        .then(() => console.log('Node text copied to clipboard'))
                        .catch(err => console.error('Failed to copy text: ', err));
                    e.preventDefault();
                }
            });
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
                style: { color: '#FFFFFF' }, 
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
                return mind.getDataString();
            } catch (e) {
                console.error(""Error exporting data:"", e);
                return { success: false, error: e.message };
            }
        };
        
        window.importData = function(mindDataString = '') {
            if (!mind) return { success: false, error: ""Mind map not initialized"" };
            if (mindDataString == '') return { success: false, error: ""No mind map data"" };
            
            try {
                if (mindDataString.startsWith('""') && mindDataString.endsWith('""')) {
                    mindDataString = mindDataString.substring(1, mindDataString.length - 1);
                }
                const mindData = JSON.parse(mindDataString);
                mind.refresh(mindData);
                return { success: true, error: """" };
            } catch (e) {
                console.error(""Error importing data:"", e);
                return { success: false, error: e.message };
            }
        };

        document.addEventListener('DOMContentLoaded', initMindMap);
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

            try
            {
                if (!Directory.Exists(DirectoryPath))
                {
                    Directory.CreateDirectory(DirectoryPath);
                }

                File.WriteAllText(FilePath, DefaultHtmlContent);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Error creating a mind map file: {exception.Message}");
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
