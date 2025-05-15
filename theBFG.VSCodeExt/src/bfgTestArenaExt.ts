// src/extension.ts
import * as vscode from 'vscode';
import * as childProcess from 'child_process';
import * as net from 'net';

let bfgProcess: childProcess.ChildProcess | null = null;
let webviewPanel: vscode.WebviewPanel | null = null;
let currentUrl: string = 'http://localhost:888';

export function activate(context: vscode.ExtensionContext) {
    context.subscriptions.push(
        vscode.commands.registerCommand('bfg.connect', async () => {
            
            if (webviewPanel) {
                webviewPanel.dispose();
            }

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

            // Set up webview content
            webviewPanel.webview.html = getWebviewContent();

            // Handle panel disposal
            webviewPanel.onDidDispose(() => {
                if (bfgProcess) {
                    bfgProcess.kill();
                    bfgProcess = null;
                }
            });

            // Handle messages from webview
            webviewPanel.webview.onDidReceiveMessage(
                message => handleMessage(message),
                undefined,
                context.subscriptions
            );
        })
    );
}

function getWebviewContent(): string {
    return `
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8">
            <title>BFG Test Arena</title>
            <style>
           .banner {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    width: 100vw;  /* Added to ensure full width */
    background: #1e1e1e;
    padding: 10px;
    display: flex;
    justify-content: center;  /* Added to center content */
    align-items: center;
    gap: 10px;
    z-index: 1000;
    box-shadow: 0 2px 5px rgba(0,0,0,0.2);
}

.banner .nav-container,
.banner .url-container {
    display: flex;
    align-items: center;
    gap: 5px;
    margin: 0 10px;  /* Added for spacing between groups */
}

.banner button {
    padding: 5px 10px;
    background: #007acc;
    color: white;
    border: none;
    border-radius: 3px;
    cursor: pointer;
}

.banner input {
    padding: 5px;
    border: 1px solid #ccc;
    border-radius: 3px;
    width: 200px;  /* Added to make input field wider */
}
                .iframe-container {
                    position: absolute;
                    top: 42px; /* Banner height */
                    left: 0;
                    right: 0;
                    bottom: 0;
                }
                iframe {
                    width: 100%;
                    height: 100%;
                    border: none;
                }
            </style>
        </head>
        <body>

            <script>
            
                // Add this message handler
                window.addEventListener('message', event => {
                    
                    const message = event.data;
    
                    console.log(JSON.stringify(message));
                    switch (message.command) {
                        case 'updateInstallButton':
                            const button = document.getElementById('installBtn');
                            if (button) {
                                button.textContent = "Run Local";
                            }
                            break;
            
                        case 'updateUrl':
                            const urlInput = document.getElementById('urlInput');
                            if (urlInput) {
                                urlInput.value = message.url;
                            }
                            break;
                    }
                });

               const vscode = acquireVsCodeApi();

                function handleNavigation(direction) {
                    if(direction=='back') {                        
                        history.back();
                    }
                    else {
                        history.forward();
                    }
                }

                function handleRefresh() {
                    document.getElementById('mainFrame').src = document.getElementById('mainFrame').src;
                }

                function handleInstall() {
                    const isInstalled = document.getElementById('installBtn').textContent === 'Run Local';
                    if (isInstalled) {
                        vscode.postMessage({
                            command: 'runBfg'
                        });
                    } else {
                        vscode.postMessage({
                            command: 'installBfg'
                        });
                    }
                }

                function handleConnect() {
                    const newUrl = document.getElementById('urlInput').value;
                    document.getElementById('mainFrame').src = newUrl;
                    vscode.postMessage({
                        command: 'updateUrl',
                        url: newUrl
                    });
                }

                // Initialize install button state
                vscode.postMessage({
                    command: 'checkInstallation'
                });
            </script>


            <div class="banner">
                <div class="nav-container">
                    <button onclick="handleNavigation('back')">← Back</button>
                    <button onclick="handleNavigation('forward')">Forward →</button>
                </div>
                <button onclick="handleRefresh()">Refresh</button>
                <button id="installBtn" onclick="handleInstall()">Install</button>
                <div class="url-container">
                    <span>URL:</span>
                    <input type="text" id="urlInput" value="${currentUrl}">
                    <button onclick="handleConnect()">Connect</button>
                </div>
            </div>
            <div class="iframe-container">
                <iframe id="mainFrame" src="${currentUrl}"></iframe>
            </div>
        </body>
    </html>
    `;
}
function handleMessage(message: any) {

    console.info('saw bfg cmd', message);

    switch (message.command) {
        case 'checkInstallation':
            checkInstallation();
            break;
        case 'installBfg':
            installBfg();
            break;
        case 'runBfg':
            runBfg();
            break;
        case 'updateUrl':
            currentUrl = message.url;
            break;
        case 'updateInstallButton':
            console.info("Got install status: " + message.installed);
            const button = document.getElementById('installBtn');
            if (button) {
                button.textContent = message.installed ? "Run local" : "Install";                
            }
            break;
    }
}

async function checkInstallation() {
    console.info('checking install');
    try {
        const result = await execShellCommand('dotnet', ['tool', 'list', '-g']);
        const isInstalled = result.stdout.indexOf('thebfg') > -1;
        console.info(isInstalled ? "Found theBFG!" : "Didnt find theBfg :(");
        console.info(webviewPanel ? "Webview found" : "Not found webview");
        webviewPanel?.webview.postMessage({
            command: 'updateInstallButton',
            installed: isInstalled
        });
    } catch (error) {
        console.error('Error checking installation:', error);
        webviewPanel?.webview.postMessage({
            command: 'updateInstallButton',
            installed: false
        });
    }
}

async function installBfg() {
    try {
        await execShellCommand('dotnet', ['tool', 'install', '-g', 'thebfg']);
        checkInstallation();
    } catch (error) {
        vscode.window.showErrorMessage(`Failed to install BFG: ${error}`);
    }
}

async function runBfg() {
    if (bfgProcess) {
        vscode.window.showWarningMessage('BFG is already running!');
        return;
    }

    try {
        bfgProcess = childProcess.spawn('thebfg', []);

        bfgProcess.stdout?.on('data', (data) => {
            console.log(`[BFG] ${data}`);
        });

        bfgProcess.stderr?.on('data', (data) => {
            console.error(`[BFG][ERROR] ${data}`);
        });

        bfgProcess.on('close', (code) => {
            console.log(`BFG process exited with code ${code}`);
            bfgProcess = null;
            if (webviewPanel) {
                webviewPanel.dispose();
            }
        });
    } catch (error) {
        vscode.window.showErrorMessage(`Failed to start BFG: ${error}`);
    }
}

function execShellCommand(command: string, args: string[]): Promise<{ stdout: string; stderr: string }> {
    return new Promise((resolve, reject) => {
        const child = childProcess.spawn(command, args);
        let stdout = '';
        let stderr = '';
        child.stdout?.on('data', (data) => {
            console.info(command, data.toString());
            stdout += data.toString();
        });

        child.stderr?.on('data', (data) => {
            console.error(command, data.toString());
            stderr += data.toString();

        });

        child.on('close', (code) => {
            if (code === 0) {
                resolve({ stdout, stderr });
            } else {
                reject(new Error(`Command failed with code ${code}`));
            }
        });

        child.on('error', (err) => {
            reject(err);
        });
    });
}