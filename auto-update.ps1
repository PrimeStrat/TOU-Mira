# Auto-update script for TOU-Mira
# Fetches and merges latest changes from AU-Avengers upstream
# Preserves local TownOfUsPlayerColors.cs

param(
    [switch]$Push = $false,
    [switch]$Verbose = $false
)

$repoPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoPath

Write-Host "🔄 TOU-Mira Auto-Update Script" -ForegroundColor Cyan
Write-Host "Repository: $repoPath" -ForegroundColor Gray

# Check if we're in a git repository
if (-not (Test-Path ".git")) {
    Write-Host "❌ Not a git repository. Exiting." -ForegroundColor Red
    exit 1
}

# Check for uncommitted changes
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Host "⚠️  You have uncommitted changes. Please commit or stash them first." -ForegroundColor Yellow
    Write-Host $gitStatus
    exit 1
}

# Fetch latest from both remotes
Write-Host "`n📡 Fetching latest changes..." -ForegroundColor Yellow
git fetch upstream main
git fetch origin main

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Fetch failed" -ForegroundColor Red
    exit 1
}

# Get commit counts
$upstreamAhead = @(git rev-list "HEAD..upstream/main" 2>$null).Count
$originAhead = @(git rev-list "HEAD..origin/main" 2>$null).Count

Write-Host "✓ Fetched successfully" -ForegroundColor Green
Write-Host "  - upstream/main: $upstreamAhead commits ahead" -ForegroundColor Gray
Write-Host "  - origin/main: $originAhead commits ahead" -ForegroundColor Gray

# Merge from upstream if there are new commits
if ($upstreamAhead -gt 0) {
    Write-Host "`n🔀 Merging upstream/main..." -ForegroundColor Yellow
    git merge upstream/main --no-edit
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Merge conflict detected!" -ForegroundColor Red
        Write-Host "Please resolve conflicts manually:" -ForegroundColor Yellow
        git status
        exit 1
    }
    
    Write-Host "✓ Merge completed successfully" -ForegroundColor Green
    
    # Verify player colors were preserved
    $hasPlayerColors = git diff HEAD~1..HEAD -- TownOfUs/TownOfUsPlayerColors.cs
    if (-not $hasPlayerColors) {
        Write-Host "✓ Local player colors preserved" -ForegroundColor Green
    }
}
else {
    Write-Host "✓ Already up to date with upstream" -ForegroundColor Green
}

# Push to origin if requested
if ($Push) {
    Write-Host "`n📤 Pushing to origin..." -ForegroundColor Yellow
    git push origin main
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Pushed successfully" -ForegroundColor Green
    }
    else {
        Write-Host "❌ Push failed" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`n✅ Update complete!" -ForegroundColor Green

if ($Verbose) {
    Write-Host "`n📋 Recent commits:" -ForegroundColor Cyan
    git log --oneline -5
}
