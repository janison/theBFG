# Deploy the VSCode extension
param (
    [string]$Version = "1.0.0",
    [string]$ExtensionPath,
    [string]$ApiKey,
    [string]$GalleryUrl = "https://marketplace.visualstudio.com/_apis/public/gallery/publishers/{publisher}/vsextensions/{extensionId}/{version}/publish",
    [string]$Publisher = "jonathan-captanis",
    [string]$ExtensionId = "thebfg-testarena"
)

function Get-VSCodeExecutable {
    
    $codeExe =  "$env:LOCALAPPDATA/Programs/Microsoft VS Code/_/Code.exe"
    Write-Host "Checking for VSCode at $codeExe" -ForegroundColor Green
    if (Test-Path $codeExe) {
        Write-Host "Found VSCode at $codeExe" -ForegroundColor Green
        return $codeExe
    }
    
    $codeExe = Join-Path $env:APPDATA "Code" "bin" "code.cmd"
    Write-Host "Checking for VSCode at $codeExe" -ForegroundColor Green

    if (Test-Path $codeExe) {
        return $codeExe
    }
    
    $codeExe = Join-Path ${env:ProgramFiles} "Microsoft VS Code" "bin" "code.cmd"
    Write-Host "Checking for VSCode at $codeExe" -ForegroundColor Green

    if (Test-Path $codeExe) {
        return $codeExe
    }
    
        $codeExe = Join-Path ${env:ProgramFiles(x86)} "Microsoft VS Code" "bin" "code.cmd"
    Write-Host "Checking for VSCode at $codeExe" -ForegroundColor Green

    if (Test-Path $codeExe) {
        return $codeExe
    }

    Write-Host "Didnt find VSCode" -ForegroundColor Red

    $codeExe = "code"


    return $codeExe;
}

function Execute-Command {
    param (
        [string]$Command,
        [string[]]$CommandArgs,
        [string]$Operation
    )
    
    Write-Host "$Command $CommandArgs"
    
    try {
        &"$Command" $CommandArgs
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to execute $Operation"
        }
        Write-Host "$Operation completed successfully" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "Failed to execute '$Operation' $_" -ForegroundColor Red
        return $false
    }false
    
}

# Step 1: Install dependencies
#if (-not (Execute-Command "npm" @("install") "npm install dependencies")) {
#    exit 1
#}

#Step 3: Package extension
if (-not (Execute-Command "node" @("node_modules/@vscode/vsce/vsce", "package") "extension packaging")) {
    exit 1
}
$vscode = Get-VSCodeExecutable


$uninstallArgs =  @("$env:USERPROFILE\.vscode\extensions\janison.bfg* /q");
if (-not (Execute-Command del $uninstallArgs "uninstall existing extension")) {
    Write-Host "Previous version not found, continuing with installation..." -ForegroundColor Yellow
}

# Step 4: Install locally
if (-not (Execute-Command $vscode @("--install-extension", "./bfg-portal-${Version}.vsix", "--log", "debug","--extensionDevelopmentPath=.") "local extension installation")) {
    exit 1
}

# Step 5: Publish to gallery if API key provided
if ($ApiKey) {
    $packagePath = "./dist/bfg-portal-${Version}.vsix"
    $headers = @{
        "Authorization" = "Bearer $ApiKey"
        "Content-Type" = "application/octet-stream"
    }
    
    try {
        $response = Invoke-WebRequest -Uri "https://marketplace.visualstudio.com/_apis/public/gallery/publishers/${Publisher}/vsextensions/${ExtensionId}/${Version}/publish" `
            -Method Post -Headers $headers -InFile $packagePath
        if ($response.StatusCode -eq 200) {
            Write-Host "Successfully published to VSCode Gallery" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "Failed to publish to VSCode Gallery: $_" -ForegroundColor Red
        exit 1
    }
}