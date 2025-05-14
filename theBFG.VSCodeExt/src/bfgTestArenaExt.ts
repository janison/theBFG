// src/extension.ts
import * as vscode from 'vscode';
import * as childProcess from 'child_process';
import * as net from 'net';

let bfgProcess: childProcess.ChildProcess | null = null;
let webviewPanel: vscode.WebviewPanel | null = null;

export function activate(context: vscode.ExtensionContext) {
    // Register command to start BFG
    context.subscriptions.push(
        vscode.commands.registerCommand('bfg.start', async () => {
            // Check if BFG is already running
            if (bfgProcess) {
                vscode.window.showWarningMessage('BFG is already running!');
                return;
            }

            // Start BFG process
            //bfgProcess = childProcess.spawn('thebfg');
            
            //// Handle process output
            //bfgProcess.stdout?.on('data', (data) => {
            //    console.log(`BFG stdout: ${data}`);
            //});
            
            //bfgProcess.stderr?.on('data', (data) => {
            //    console.error(`BFG stderr: ${data}`);
            //});
            
            //bfgProcess.on('close', (code) => {
            //    console.log(`BFG process exited with code ${code}`);
            //    bfgProcess = null;
            //    if (webviewPanel) {
            //        webviewPanel.webview.postMessage({ command: 'bfgClosed' });
            //        webviewPanel.dispose();
            //    }
            //});

            vscode.window.showInformationMessage('BFG Test Arena launhing!');

                        
            // Create webview panel
            webviewPanel = vscode.window.createWebviewPanel(
                'theBFGTestArena',
                'theBFG Test Arena',
                vscode.ViewColumn.One,
                {
                    enableScripts: true,
                    retainContextWhenHidden: true,
                    enableCommandUris: true,
                    
                }
            );
            // need to wait for the server to start up after running theBFG which createsit
            // Wait for the server to start


            // Set up webview content
            webviewPanel.webview.html = `
<!DOCTYPE html>
<html id="vscode-bfg-root">
<head>
    <meta charset="UTF-8">
    <title>BFG Portal</title>
    <style>
        /* Reset all existing styles */
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
            font-family: inherit;
            line-height: normal;
        }

        /* Unique root selector */
        #vscode-bfg-root {
            width: 100%;
            height: 100vh !important;
            overflow: hidden !important;
        }

        /* Body with unique ID */
        #vscode-bfg-body {
            display: flex;
            flex-direction: column;
            width: 100%;
            height: 100%;
        }

        /* Controls panel with unique ID */
        #vscode-bfg-controls {
            background: #1e1e1e;
            border-bottom: 1px solid #333;
            padding: 10px;
            min-height: auto !important;
            max-height: none !important;
        }

        /* Button with unique ID */
        #vscode-bfg-button {
            padding: 5px 10px;
            background: #00ff00;
            color: #000;
            border: none;
            border-radius: 3px;
            cursor: pointer;
        }

        #vscode-bfg-button:hover {
            background: #00cc00;
        }

        /* Frame container with unique ID */
        #vscode-bfg-frame {
            flex: 1 1 auto !important;
            overflow: hidden !important;
            position: relative;
            min-height: 0 !important;
        }

        /* Iframe with unique ID */
        #vscode-bfg-iframe {
            width: 100% !important;
            height: 100% !important;
            border: none !important;
            display: block !important;
            position: absolute !important;
            top: 0 !important;
            left: 0 !important;
            bottom: 0 !important;
            right: 0 !important;
        }

        /* Error message with unique ID */
        #vscode-bfg-error {
            position: absolute !important;
            top: 50% !important;
            left: 50% !important;
            transform: translate(-50%, -50%) !important;
            color: #ff0000 !important;
            font-family: monospace !important;
            z-index: 1000 !important;
        }
    </style>
</head>
<body id="vscode-bfg-body">
    <div id="vscode-bfg-controls">
        <button id="vscode-bfg-button" onclick="reloadPortal()">Reload Portal</button>
    </div>
    <div id="vscode-bfg-frame">
        <iframe id="vscode-bfg-iframe" src="http://localhost:888/#/" frameborder="0"></iframe>
        <div id="vscode-bfg-error"></div>
    </div>
    <script>
        function reloadPortal() {
            const iframe = document.getElementById('vscode-bfg-iframe');
            const errorDiv = document.getElementById('vscode-bfg-error');
            errorDiv.style.display = 'none';
            iframe.src = iframe.src;
        }
        
        document.getElementById('vscode-bfg-iframe').addEventListener('error', function() {
            const errorDiv = document.getElementById('vscode-bfg-error');
            errorDiv.textContent = 'Portal not available. Click reload to try again.';
            errorDiv.style.display = 'block';
        });
    </script>
</body>
</html>`;
            
            // Handle panel disposal
            webviewPanel.onDidDispose(() => {
                if (bfgProcess) {
                    bfgProcess.kill();
                    bfgProcess = null;
                }
            });
        })
    );
}

function getWebviewContent(): string {
    return `
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8">
            <title>theBFG Test Arena</title>
            <style>
                body {
                    margin: 0;
                    padding: 0;
                    width: 100%;
                    height: 100%;
                    display: flex;
                    justify-content: center;
                    align-items: center;
                }
                iframe {
                    width: 100%;
                    height: 100%;
                    border: none;
                }
            </style>
        </head>
        <body>
            <iframe src="http://localhost:888"></iframe>
        </body>
        </html>
    `;
}

export function deactivate() {
    if (bfgProcess) {
        bfgProcess.kill();
    }
    if (webviewPanel) {
        webviewPanel.dispose();
    }
}