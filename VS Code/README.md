# Code Mind Map VS Code Extension

An extension for creating mind maps with nodes linked to code. Easily add code snippets as nodes and navigate between the mind map and your code files.

## 1. Open the Mind Map Window
- Run the `Open Code Mind Map Panel` command from the Command Palette (Ctrl+Shift+P)

## 2. Add Code as a Node
- Select any code in the editor (or place the caret on a line)
- Press `Ctrl+2, Ctrl+2` to add it as a new node in the mind map, linked to that code

## 3. Navigate to Linked Code
`Ctrl+Click` any node in the mind map to jump directly to the linked code.

## 4. Organize & Connect Nodes
- Drag and drop nodes to rearrange
- Right-click to edit, delete, or add connections

That's it! Start visualizing and navigating your code faster with **Code Mind Map**.

> **Tip:** Use the toolbar buttons for quick actions like saving/loading maps.

## Screenshots

See screenshots at [CodeMindMap.com](https://codemindmap.com/)

## Versions

- **v1.20**
  - Persist view type selection (left/right/flower) via autosave.
  - Fix: direction not restored on load (mind.refresh skips direction).
  - Feature: Reopen the diagram if it was open before the last reload.
  - Feature: pressing ESC while editing a node exits edit and selects the node.
  - Fix: focus map element on node select so keyboard shortcuts work on first click.
  - Feature: save diagrams as .json with pretty printing, open accepts .json and .txt.
  - Feature: reopen diagram automatically after window reload.
  - Node completion status: press `c` on any node to cycle through In Progress (⟳) and Completed (✓) states. Status is saved with the diagram and can also be set via right-click context menu.

- **v1.19**
  - "Code node added" status bar notification.
  - Improved navigation with searching code with the "moving window" approach.

- **v1.18**
  - Fixed a bug with creating a new mind map.

- **v1.17**
  - The MindElixir mind mapping engine has been updated from version 4.0.0 to version 4.6.2.

- **v1.16**
  - The Ctrl+Scroll keyboard shortcut has been replaced with Alt+Scroll for zooming in/out to avoid conflicts with the web browser's zoom feature.
  - Fixed CTRL+Click opens new tab instead of using open one.

- **v1.15**
  - Added a toolbar with buttons: Zoom In/Out, To Center, Tree View: Left, Right, Side.
  - Now works offline.
  
- **v1.14**
  - Storing relative paths in mind maps.
  - Storing a relative path to the auto-save file in the workspace settings.

- **v1.13**
  - "Toggle color scheme: Light/Dark" button added.
  - Light color scheme is default.
  - The color scheme is saved to the auto-save file.
  
- **v1.12**
  - First release of the extension.