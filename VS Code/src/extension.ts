import * as vscode from 'vscode';
import * as path from 'path';

// Utilities for path handling
function getWorkspaceRootForFile(fileFsPath: string): string | undefined {
	const wsFolder = vscode.workspace.getWorkspaceFolder(vscode.Uri.file(fileFsPath));
	if (wsFolder) return wsFolder.uri.fsPath;
	const folders = vscode.workspace.workspaceFolders;
	return folders && folders.length > 0 ? folders[0]?.uri.fsPath : undefined;
}

function toWorkspaceRelative(absPath: string): string {
	const root = getWorkspaceRootForFile(absPath);
	if (!root) return absPath; // fallback to absolute if no workspace
	const rel = path.relative(root, absPath);
	// If outside workspace, keep absolute
	return isPathRelative(rel) ? rel : absPath;
}

function toAbsoluteFromWorkspace(filePath: string, contextPath?: string): string {
	let relPath = filePath;
    if (path.isAbsolute(filePath)) {
        const rel = toWorkspaceRelative(filePath);
        if (isPathRelative(rel)) {
            relPath = rel;
        }
        else {
            return filePath
        }
    }
	const base = contextPath ? getWorkspaceRootForFile(contextPath) : (vscode.workspace.workspaceFolders?.[0]?.uri.fsPath);
	return base ? path.join(base, relPath) : relPath;
}

function isPathRelative(rel: string) {
    return rel && !rel.startsWith('..') && !path.isAbsolute(rel);
}

export function activate(context: vscode.ExtensionContext) {
    context.subscriptions.push(
        vscode.commands.registerCommand('codeMindMap.openPanel', () => {
            CodeMindMapPanel.createOrShow(context.extensionUri);
        })
    );

    // Register command for adding code to mind map node
    context.subscriptions.push(
        vscode.commands.registerCommand('codeMindMap.addCodeToNode', () => {

            const editor = vscode.window.activeTextEditor;
            if (!editor) {
                vscode.window.showInformationMessage('No active editor');
                return;
            }

            const selection = editor.selection;
            const text = editor.document.getText(selection);
            const line = editor.document.lineAt(selection.active.line);
            const lineText = line.text;

            // If there's no selection, use the current line
            const codeToAdd = text || lineText;

            // Create node data with file information
			const nodeData = {
				fileName: editor.document.fileName.split(/[\/\\]/).pop() || 'untitled',
				filePath: toWorkspaceRelative(editor.document.fileName),
				topLine: selection.active.line + 1
			};

            // If the panel is not open, show it first
            if (!CodeMindMapPanel.CurrentPanel) {
                CodeMindMapPanel.createOrShow(context.extensionUri);
                return;
            }

            // Send the code to the panel
            CodeMindMapPanel.CurrentPanel?.addCodeToNode(codeToAdd, nodeData);
        })
    );
}

export function deactivate() {}

export class CodeMindMapPanel {
    private static _lastSavePath: vscode.Uri | undefined;
    public static CurrentPanel: CodeMindMapPanel | undefined;
    private readonly _panel: vscode.WebviewPanel;
    private readonly _extensionUri: vscode.Uri;
    private _disposables: vscode.Disposable[] = [];
    private _lastSelection: { text: string; document: vscode.TextDocument; line: number } | undefined;
    private _lastSelectedNode: { id: string; topic: string; data: any } | undefined;
    private _pendingSaveUri: vscode.Uri | undefined;

    private requestExportMindMapData() {
        this._panel.webview.postMessage({ action: 'exportMindMapData' });
    }

    private exportIfPathKnown() {
        if (CodeMindMapPanel._lastSavePath) {
            console.debug('Sending exportMindMapData message to webview');
            this._pendingSaveUri = CodeMindMapPanel._lastSavePath;
            this.requestExportMindMapData();
        } else {
            console.debug('No last save path available');
        }
    }

    public static createOrShow(extensionUri: vscode.Uri) {
        const column = vscode.window.activeTextEditor
            ? vscode.window.activeTextEditor.viewColumn
            : undefined;

        // If we already have a panel, show it
        if (CodeMindMapPanel.CurrentPanel) {
            CodeMindMapPanel.CurrentPanel._panel.reveal(column);
            return;
        }

        // Otherwise, create a new panel
        const panel = vscode.window.createWebviewPanel(
            'codeMindMap',
            'Code Mind Map',
            column || vscode.ViewColumn.One,
            {
                enableScripts: true,
                retainContextWhenHidden: true,
                localResourceRoots: [
                    vscode.Uri.joinPath(extensionUri, 'out', 'MindElixir')
                ]
            }
        );

        CodeMindMapPanel.CurrentPanel = new CodeMindMapPanel(panel, extensionUri);

        // Try to load last save path from workspace settings
        const config = vscode.workspace.getConfiguration('codeMindMap');
        let lastSavePath = config.get<string>('autoSavePath');
        if (lastSavePath) {
            lastSavePath = toAbsoluteFromWorkspace(lastSavePath);
            CodeMindMapPanel._lastSavePath = vscode.Uri.file(lastSavePath);
            // Load the mind map from the file
            vscode.workspace.fs.readFile(CodeMindMapPanel._lastSavePath).then(
                fileContent => {
                    const data = new TextDecoder().decode(fileContent);
                    panel.webview.postMessage({
                        action: 'importMindMapData',
                        data: data
                    });
                },
                error => {
                    vscode.window.showErrorMessage('Failed to load mind map: ' + error);
                }
            );
        }

    }

    private constructor(panel: vscode.WebviewPanel, extensionUri: vscode.Uri) {
        this._panel = panel;
        this._extensionUri = extensionUri;
        this._panel.webview.html = this._getHtmlForWebview(this._panel.webview, extensionUri);
        
        // Open dev tools on startup
        //vscode.commands.executeCommand('workbench.action.webview.openDeveloperTools');
        
        // Initialize _lastSelection if there's an active selection
        const initialEditor = vscode.window.activeTextEditor;
        if (initialEditor) {
            const selection = initialEditor.selection;
            if (!selection.isEmpty) {
                const text = initialEditor.document.getText(selection);
                this._lastSelection = {
                    text,
                    document: initialEditor.document,
                    line: selection.active.line
                };
                // Enable the button when there's a selection
                this._panel.webview.postMessage({
                    action: 'enableAddCodeButton'
                });
            }
        }

        // Track selection changes in any editor
        vscode.window.onDidChangeTextEditorSelection(e => {
            const selection = e.selections[0];
            if (selection && !selection.isEmpty) {
                const text = e.textEditor.document.getText(selection);
                this._lastSelection = {
                    text,
                    document: e.textEditor.document,
                    line: selection.active.line
                };
                // Enable the button when there's a selection
                this._panel.webview.postMessage({
                    action: 'enableAddCodeButton'
                });
            } else {
                // Disable the button when there's no selection
                this._panel.webview.postMessage({
                    action: 'disableAddCodeButton'
                });
            }
        }, null, this._disposables);

        // Also check initial selection state
        const editor = vscode.window.activeTextEditor;
        if (editor) {
            const selection = editor.selection;
            if (!selection.isEmpty) {
                this._panel.webview.postMessage({
                    action: 'enableAddCodeButton'
                });
            } else {
                this._panel.webview.postMessage({
                    action: 'disableAddCodeButton'
                });
            }
        }

        this._panel.onDidDispose(() => this.dispose(), null, this._disposables);

        // Handle messages from the webview
        this._panel.webview.onDidReceiveMessage(
            async message => {
                switch (message.action) {
                    case 'openDevTools':
                        vscode.commands.executeCommand('workbench.action.webview.openDeveloperTools');
                        break;

                    case 'nodeSelected':
                        // Store the selected node data
                        this._lastSelectedNode = {
                            id: message.nodeId,
                            topic: message.nodeTopic,
                            data: message.nodeData
                        };
                        // Enable/disable buttons based on node selection
                        if (message.nodeData) {
                            this._panel.webview.postMessage({
                                action: 'enableGoToCode'
                            });
                        } else {
                            this._panel.webview.postMessage({
                                action: 'disableGoToCode'
                            });
                        }
                        break;

                    case 'addCodeToNode':
                        if (this._lastSelection) {
                            // Use the last selected text
                            const nodeData = {
								fileName: this._lastSelection.document.fileName.split(/[\/\\]/).pop(),
								filePath: toWorkspaceRelative(this._lastSelection.document.fileName),
								topLine: this._lastSelection.line + 1
							};

                            this._panel.webview.postMessage({
                                action: 'addChildNode',
                                code: this._lastSelection.text,
                                nodeData: nodeData
                            });
                        } else {
                            // Fallback to current editor if no last selection
                            const editor = vscode.window.activeTextEditor;
                            if (!editor) {
                                vscode.window.showInformationMessage('No active editor');
                                return;
                            }

                            const selection = editor.selection;
                            const text = editor.document.getText(selection);
                            const line = editor.document.lineAt(selection.active.line);
                            const lineText = line.text;

                            // If there's no selection, use the current line
                            const codeToAdd = text || lineText;

                            const nodeData = {
								fileName: editor.document.fileName.split(/[\/\\]/).pop() || 'untitled',
								filePath: toWorkspaceRelative(editor.document.fileName),
								topLine: selection.active.line + 1
							};

                            this._panel.webview.postMessage({
                                action: 'addChildNode',
                                code: codeToAdd,
                                nodeData: nodeData
                            });
                        }
                        break;

                    case 'goToCode':
                        if (this._lastSelectedNode?.data) {
                            await this.navigateToCode(this._lastSelectedNode.data);
                        }
                        break;

                    case 'saveMindMap':
                        const saveUri = await vscode.window.showSaveDialog({
                            filters: {
                                'Text files': ['txt']
                            },
                            title: 'Save Mind Map As',
                            defaultUri: CodeMindMapPanel._lastSavePath || vscode.Uri.file('CodeMindMap.txt')
                        });

                        if (saveUri) {
                            // Store the last save path
                            CodeMindMapPanel._lastSavePath = saveUri;
                            // Save to workspace settings if available
                            await this.SaveToWorkspaceSettings();
                            // Request mind map data from the webview
                            this.requestExportMindMapData();
                            // Store the save URI for later use
                            this._pendingSaveUri = saveUri;
                        }
                        break;

                    case 'exportedMindMapData':
                        if (this._pendingSaveUri && message.data) {
                            try {
                                await vscode.workspace.fs.writeFile(
                                    this._pendingSaveUri,
                                    new TextEncoder().encode(message.data)
                                );
                                //vscode.window.showInformationMessage('Mind map saved successfully');
                            } catch (error) {
                                vscode.window.showErrorMessage('Failed to save mind map: ' + error);
                            }
                            this._pendingSaveUri = undefined;
                        }
                        break;

                    case 'loadMindMap':
                        const openUri = await vscode.window.showOpenDialog({
                            filters: {
                                'Text files': ['txt']
                            },
                            title: 'Load Mind Map',
                            canSelectMany: false,
                            defaultUri: CodeMindMapPanel._lastSavePath || vscode.Uri.file('CodeMindMap.txt')
                        });

                        if (openUri && openUri[0]) {
                            try {
                                const fileContent = await vscode.workspace.fs.readFile(openUri[0]);

                                const data = new TextDecoder().decode(fileContent);

                                // Store the last path
                                CodeMindMapPanel._lastSavePath = openUri[0];
                                await this.SaveToWorkspaceSettings();

                                this._panel.webview.postMessage({
                                    action: 'importMindMapData',
                                    data: data
                                });

                            } catch (error) {
                                vscode.window.showErrorMessage('Failed to load mind map data: ' + error);
                            }
                        }
                        break;

                    case 'nodeNavigate':
                        if (message.nodeData) {
                            await this.navigateToCode(message.nodeData);
                        }
                        break;

                    case 'newMindMap':
                        // Show confirmation dialog before resetting
                        const result = await vscode.window.showWarningMessage(
                            'Create a new code mind map? Your current progress will be lost.',
                            { modal: true },
                            'OK'
                        );
                        
                        if (result === 'OK') {
                            // Reset the mind map to its initial state
                            this._panel.webview.postMessage({
                                action: 'resetMindMap'
                            });
                        }
                        break;

                    case 'mindMapOperation':
                        console.debug('mindMapOperation received: ' + message.operationName);
                        console.debug('Last save path:', CodeMindMapPanel._lastSavePath);
                        
                        this.exportIfPathKnown();
                        break;

                    case 'toggleColorScheme':
                        this._panel.webview.postMessage({
                            action: 'toggleColorScheme'
                        });
                        this.exportIfPathKnown();
                        break;
                }
            },
            null,
            this._disposables
        );

        // Call when a new workspace is created
        vscode.workspace.onDidChangeWorkspaceFolders(async (event) => {
            if (event.added.length > 0 && vscode.workspace.workspaceFile) {
                await this.SaveToWorkspaceSettings();
            }
        });
    }

    private async SaveToWorkspaceSettings() {
        if (!CodeMindMapPanel._lastSavePath) {
            return;
        }
        const workspaceFolders = vscode.workspace.workspaceFolders;
        if (workspaceFolders && workspaceFolders.length > 0) {
            const config = vscode.workspace.getConfiguration('codeMindMap');
            var relativePath = toWorkspaceRelative(CodeMindMapPanel._lastSavePath.fsPath);
            console.debug('Saving to workspace settings: ' + relativePath);
            await config.update('autoSavePath', relativePath, vscode.ConfigurationTarget.Workspace);
        }
    }

    private async navigateToCode(nodeData: { filePath: string; topLine: number }) {
		const { filePath, topLine } = nodeData;
		const absPath = toAbsoluteFromWorkspace(filePath, this._lastSelection?.document.fileName);
		const document = await vscode.workspace.openTextDocument(absPath);
		const editor = await vscode.window.showTextDocument(document);
		const line = document.lineAt(topLine - 1);
		const firstNonWhitespace = line.firstNonWhitespaceCharacterIndex;
		const position = new vscode.Position(topLine - 1, firstNonWhitespace);
		editor.selection = new vscode.Selection(position, position);
		editor.revealRange(
			new vscode.Range(position, position),
			vscode.TextEditorRevealType.InCenter
		);
	}

    public dispose() {
        CodeMindMapPanel.CurrentPanel = undefined;
        this._panel.dispose();
        while (this._disposables.length) {
            const x = this._disposables.pop();
            if (x) {
                x.dispose();
            }
        }
    }

    private _getHtmlForWebview(webview: vscode.Webview, extensionUri: vscode.Uri): string {
        // Get URI for local MindElixir library
        const mindElixirFileUri = vscode.Uri.joinPath(extensionUri, 'out', 'MindElixir', 'MindElixir.js');
        const mindElixirUri = webview.asWebviewUri(mindElixirFileUri);

        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Code Mind Map</title>
    <style>
        html, body { 
            height: 100%; 
            margin: 0; 
            padding: 0; 
            background: #1e1e1e; 
            color: #fff; 
        }
        #container { 
            height: 100vh; 
            display: flex; 
            flex-direction: column; 
        }
        #map {
            width: 100%;
            height: calc(100vh - 48px);
        }
        .mm-btn {
            background: #333;
            border: none;
            border-radius: 4px;
            margin: 2px;
            padding: 4px 6px;
            display: flex;
            align-items: center;
            cursor: pointer;
            transition: background 0.2s;
        }
        .mm-btn:disabled {
            opacity: 0.5;
            cursor: not-allowed;
        }
        .mm-btn:hover:not(:disabled) {
            background: #444;
        }
        .mm-icon {
            display: flex;
            align-items: center;
            justify-content: center;
            color: #b3b3b3;
            font-size: 18px;
        }
        .mm-label {
            display: none;
            margin-left: 6px;
            color: #fff;
            font-size: 13px;
        }
        .hidden {
            display: none !important;
        }
    </style>
</head>
<body>
    <div id="container">
        <div style="display: flex; gap: 4px; padding: 8px; background: #222; border-bottom: 1px solid #444;">
            <!-- Add Code Node -->
            <button class="mm-btn" id="addCodeNodeBtn" title="Add selected code as a mind map node" disabled>
                <span class="mm-icon" aria-label="Add">➕</span>
                <span class="mm-label">Add Code Linked Node</span>
            </button>
            <!-- Go To Linked Code -->
            <button class="mm-btn" id="goToCodeBtn" title="Jump to code linked to node" disabled>
                <span class="mm-icon" aria-label="Go to link">🔗</span>
                <span class="mm-label">Jump To Linked Code</span>
            </button>
            <!-- New Code Mind Map -->
            <button class="mm-btn" id="newMindMapBtn" title="Create a new code mind map">
                <span class="mm-icon" aria-label="New">🆕</span>
                <span class="mm-label">New Code Mind Map</span>
            </button>
            <!-- Open Dev Tools -->
            <button class="mm-btn hidden" id="openDevToolsBtn" title="Open Dev Tools">
                <span class="mm-icon" aria-label="Dev tools">🛠️</span>
                <span class="mm-label">Open Dev Tools</span>
            </button>
            <!-- Auto-Save As -->
            <button class="mm-btn" id="saveMindMapBtn" title="Automatically save mind map as...">
                <span class="mm-icon" aria-label="Save">💾</span>
                <span class="mm-label">Auto-Save As...</span>
            </button>
            <!-- Load and Link Mind Map Data -->
            <button class="mm-btn" id="loadMindMapBtn" title="Load and link your mind map data to...">
                <span class="mm-icon" aria-label="Load">📂</span>
                <span class="mm-label">Load and Link Mind Map Data</span>
            </button>
            <!-- Toggle Color Scheme -->
            <button class="mm-btn" id="toggleColorSchemeBtn" title="Toggle between Light and Dark color scheme">
                <span class="mm-icon" aria-label="Color palette">🎨</span>
                <span class="mm-label">Toggle Color Scheme</span>
            </button>
        </div>
        <div id="map"></div>
    </div>

    <script type="module">
        import MindElixir from '${mindElixirUri}';
        const vscode = acquireVsCodeApi();

        let mind, data, themeManager;

        function initMindMap() {
            const options = {
                el: '#map',
                allowUndo: true,
                toolBar: true,
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

            data = {
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
                                    expanded: true,
                                    children: [
                                        {
                                            topic: 'Only opens the extension panel if it is closed.',
                                            id: 'bd1bae68e0ab186e',
                                        },
                                    ],
                                },
                                {
                                    topic: 'Ctrl+click on a node - jump to the code linked to the node.',
                                    id: 'bd1bb4b14d6697c3',
                                },
                                {
                                    topic: 'Use the toolbar button to save the map to an autosave file so you can continue saving changes.',
                                    id: 'bd1babdd5c18a7a2',
                                },
                                {
                                    topic: 'The path to the autosave file is stored in the workspace settings (.code-workspace). Do "Save Workspace As..." to save the path.',
                                    id: 'bd1ba81d9bc95a7e',
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
                                    topic: 'Alt+Mouse Wheel Up/Down - Zoom in/out',
                                    id: 'bd1c1cb51e6745d3',
                                },
                                {
                                    topic: 'Shift+Mouse Scroll - Scroll horizontally',
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
                            ],
                        },
                    ],
                    expanded: true,
                },
                theme: themeManager.getTheme(DARK_THEME.name),
                direction: 2
            };

            mind = new MindElixir(options);
            mind.init(data);

            mind.bus.addListener('selectNode', node => {
                vscode.postMessage({
                    action: 'nodeSelected',
                    nodeId: node.id,
                    nodeTopic: node.topic,
                    nodeData: node.data,
                });
            });

            mind.bus.addListener('operation', operation => {
                vscode.postMessage({
                    action: 'mindMapOperation',
                    operationName: operation.name,
                });
            });

            document.addEventListener('click', (e) => {
                if (e.ctrlKey && e.target.tagName === 'ME-TPC') {
                    const currentNodeObj = mind.currentNode?.nodeObj
                    if (currentNodeObj) {
                        vscode.postMessage({
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
                console.error('Mind map not initialized');
                return { success: false, error: 'Mind map not initialized' };
            }
            if (topic.startsWith('"') && topic.endsWith('"')) {
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
                if (!targetNode) return { success: false, error: 'Target node not found' };
                mind.addChild(targetNode, childData);
                mind.selectNode(MindElixir.E(childData.id));
                return { 
                    success: true, 
                    nodeId: childData.id,
                    parentId: targetNodeId
                };
            } catch (e) {
                console.error('Error adding child node:', e);
                return { success: false, error: e.message };
            }
        };

        window.exportData = function() {
            if (!mind) return { success: false, error: 'Mind map not initialized' };
            try {
                return mind.getDataString();
            } catch (e) {
                console.error('Error exporting data:', e);
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
        };

        window.importData = function(mindDataString = '') {

            if (!mind) return { success: false, error: 'Mind map not initialized' };
            if (mindDataString == '') return { success: false, error: 'No mind map data' };

            try {

                if (mindDataString.startsWith('"') && mindDataString.endsWith('"')) {
                    mindDataString = mindDataString.substring(1, mindDataString.length - 1);
                }

                const mindData = JSON.parse(mindDataString);

                mind.refresh(mindData);

                const dataThemeName = getThemeName(mindData);

                if (dataThemeName != '' && themeManager.contains(dataThemeName) && dataThemeName != mind.theme?.name) {
                    mind.changeTheme(themeManager.getTheme(dataThemeName));
                }

                return { success: true, error: '' };

            } catch (e) {
                console.error('Error importing data:', e);
                return { success: false, error: e.message };
            }
        };

        window.toggleColorScheme = function() {

            if (!mind) return { success: false, error: 'Mind map not initialized' };

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
                console.error('Error toggling the color scheme:', e);
                return { success: false, error: e.message };
            }

        };

        // Add event listeners for all buttons
        window.addEventListener('DOMContentLoaded', () => {
            // Open Dev Tools button
            const devBtn = document.getElementById('openDevToolsBtn');
            if (devBtn) {
                devBtn.addEventListener('click', () => {
                    vscode.postMessage({ action: 'openDevTools' });
                });
            }

            // Add Code Node button
            const addCodeBtn = document.getElementById('addCodeNodeBtn');
            if (addCodeBtn) {
                addCodeBtn.addEventListener('click', () => {
                    vscode.postMessage({ action: 'addCodeToNode' });
                });
            }

            // Go To Code button
            const goToCodeBtn = document.getElementById('goToCodeBtn');
            if (goToCodeBtn) {
                goToCodeBtn.addEventListener('click', () => {
                    vscode.postMessage({ action: 'goToCode' });
                });
            }

            // New Mind Map button
            const newMindMapBtn = document.getElementById('newMindMapBtn');
            if (newMindMapBtn) {
                newMindMapBtn.addEventListener('click', () => {
                    vscode.postMessage({ action: 'newMindMap' });
                });
            }

            // Save Mind Map button
            const saveMindMapBtn = document.getElementById('saveMindMapBtn');
            if (saveMindMapBtn) {
                saveMindMapBtn.addEventListener('click', () => {
                    vscode.postMessage({ action: 'saveMindMap' });
                });
            }

            // Load Mind Map button
            const loadMindMapBtn = document.getElementById('loadMindMapBtn');
            if (loadMindMapBtn) {
                loadMindMapBtn.addEventListener('click', () => {
                    vscode.postMessage({ action: 'loadMindMap' });
                });
            }

            // Toggle Color Scheme button
            const toggleColorSchemeBtn = document.getElementById('toggleColorSchemeBtn');
            if (toggleColorSchemeBtn) {
                toggleColorSchemeBtn.addEventListener('click', () => {
                    vscode.postMessage({ action: 'toggleColorScheme' });
                });
            }

            initMindMap();
        });

        // Handle messages from the extension
        window.addEventListener('message', event => {
            const message = event.data;
            switch (message.action) {
                case 'addChildNode':
                    if (mind) {
                        window.addChildNode(message.code, message.nodeData);
                    }
                    break;
                case 'enableAddCodeButton':
                    const addCodeBtn = document.getElementById('addCodeNodeBtn');
                    if (addCodeBtn) {
                        addCodeBtn.disabled = false;
                    }
                    break;
                case 'disableAddCodeButton':
                    const addCodeBtn2 = document.getElementById('addCodeNodeBtn');
                    if (addCodeBtn2) {
                        addCodeBtn2.disabled = true;
                    }
                    break;
                case 'enableGoToCode':
                    const goToCodeBtn = document.getElementById('goToCodeBtn');
                    if (goToCodeBtn) {
                        goToCodeBtn.disabled = false;
                    }
                    break;
                case 'disableGoToCode':
                    const goToCodeBtn2 = document.getElementById('goToCodeBtn');
                    if (goToCodeBtn2) {
                        goToCodeBtn2.disabled = true;
                    }
                    break;
                case 'exportMindMapData':
                    if (mind) {
                        vscode.postMessage({
                            action: 'exportedMindMapData',
                            data: window.exportData()
                        });
                    }
                    break;
                case 'importMindMapData':
                    if (mind) {
                        window.importData(message.data);
                    }
                    break;
                case 'resetMindMap':
                    if (mind) {
                        mind.refresh(data);
                    }
                    break;
                case 'toggleColorScheme':
                    if (mind) {
                        window.toggleColorScheme();
                    }
                    break;
            }
        });
    </script>
</body>
</html>`;
    }

    public addCodeToNode(code: string, nodeData: { fileName: string; filePath: string; topLine: number }) {
        this._panel.webview.postMessage({ 
            action: 'addChildNode',
            code: code,
            nodeData: nodeData
        });
    }
} 