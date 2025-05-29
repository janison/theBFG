# Publish the tool
param (
    [string]$Version = "1.0.0-beta",
    [string]$ToolPath,
    [string]$SourcePath = "./src/nupkg",
    [string]$ToolName = "theBfg",
    [string]$ApiKey,
    [string]$NuGetSource = "https://api.nuget.org/v3/index.json",
    [string]$ProjectPath ="./src/theBFG.csproj"
)

function Get-DefaultToolPath {
    if ($IsWindows) {
        return Join-Path $env:USERPROFILE ".dotnet\tools"
    } else {
        return Join-Path $HOME ".dotnet/tools"
    }
}

function Execute-DotnetCommand {
    param (
        [string[]]$CommandArgs,
        [string]$Operation
    )
    
    Write-Host "Executing $Operation..."
    try {
        $result = dotnet $CommandArgs 2>&1 | ForEach-Object {
            Write-Host $_ -ForegroundColor Cyan
        }
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to execute $Operation"
        }
        Write-Host "$Operation completed successfully" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "Failed to execute '$Operation' $_" -ForegroundColor Red
        return $false
    }
}

function Run {
    # Step 1: Build project (Release configuration)
    $buildArgs = @(
        "build",
        $ProjectPath,
        "--configuration", "Release",
        "--p:packageversion=$Version"
        #,"--verbosity", "detailed"
    )

    Write-Host "Building $ProjectPath with version $Version..."
    
    if (-not (Execute-DotnetCommand -CommandArgs $buildArgs -Operation "build")) {
        exit 1
    }
    
    # Step 2: Create the package
    $packArgs = @(
        "pack",
        "--no-build",
        $ProjectPath,
        "--output", $SourcePath,
        "--include-symbols",
        "--include-source",
        "--configuration", "Release"
        #,"--verbosity", "detailed"
    )
    
    if (-not (Execute-DotnetCommand -CommandArgs $packArgs -Operation "package creation")) {
        exit 1
    }

     if (-not $ToolPath) {
        $ToolPath = Get-DefaultToolPath
        Write-Host "Using default tool path: $ToolPath"
    }
    
    
    # Step 3: Install or update the tool
    Write-Host "Checking if tool is installed..."
    $toolListArgs = @(
        "tool", "list",
        "--tool-path", $ToolPath
    )
    
    $toolListOutput = dotnet $toolListArgs 2>&1 | ForEach-Object {
        Write-Host $_ -ForegroundColor Cyan
    }
    
    $isInstalled = $toolListOutput | Where-Object { $_ -like "*$ToolName*" }
    
   
    if ($isInstalled) {
        Write-Host "Tool is already installed, updating..."
        $updateArgs = @(
            "tool", "update",
            "--tool-path", $ToolPath,
            "--add-source", $SourcePath,
            $ToolName,
            "--version", $Version
            #,"--verbosity", "detailed"
        )
        
        if (-not (Execute-DotnetCommand -CommandArgs $updateArgs -Operation "tool update")) {
            exit 1
        }
    } else {
        Write-Host "Tool is not installed, installing..."
        $installArgs = @(
            "tool", "install",
            "--tool-path", $ToolPath,
            "--add-source", $SourcePath,
            $ToolName,
            "--version", $Version
            #,"--verbosity", "detailed"
        )
        
        if (-not (Execute-DotnetCommand -CommandArgs $installArgs -Operation "tool install")) {
            exit 1
        }
    }
    
    # Step 4: Publish to nuget.org if API key is provided
    if ($ApiKey) {
        $packagePath = Join-Path $SourcePath "$ToolName.$Version.nupkg"
        
        $pushArgs = @(
            "nuget", "push",
            $packagePath,
            "--api-key", $ApiKey,
            "--source", $NuGetSource
            #"--verbosity", "detailed"
        )
        
        if (-not (Execute-DotnetCommand -CommandArgs $pushArgs -Operation "nuget.org publish")) {
            exit 1
        }
    } else {
        Write-Host "No API key provided. Package created and updated locally only." -ForegroundColor Yellow
    }
}

# Run the process
Run

Write-Host "thebfg should now be availible as a command. If not, make sure '"%USERPROFILE%\.dotnet\tools"' is in your PATH."