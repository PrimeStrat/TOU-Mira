# Auto-update script for TOU-Mira
# Fetches and merges latest changes from AU-Avengers upstream
# Preserves local TownOfUsPlayerColors.cs

param(
    [switch]$Push = $false,
    [switch]$Verbose = $false
)

$repoPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoPath

Write-Host "TOU-Mira Auto-Update Script" -ForegroundColor Cyan
Write-Host "Repository: $repoPath" -ForegroundColor Gray

# Check if we're in a git repository
if (-not (Test-Path ".git")) {
    Write-Host "ERROR: Not a git repository. Exiting." -ForegroundColor Red
    exit 1
}

# Check for uncommitted changes
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Host "WARNING: You have uncommitted changes. Please commit or stash them first." -ForegroundColor Yellow
    Write-Host $gitStatus
    exit 1
}

# Fetch latest from both remotes (including tags)
Write-Host "`nFetching latest changes..." -ForegroundColor Yellow
git fetch upstream main --tags
git fetch origin main

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Fetch failed" -ForegroundColor Red
    exit 1
}

# Find latest stable release tag from upstream (excludes pre-release tags with hyphens)
$latestTag = git tag --list --sort=-version:refname | Where-Object { $_ -match '^v?\d+\.\d+(\.\d+)*$' } | Select-Object -First 1
if (-not $latestTag) {
    Write-Host "ERROR: No stable release tags found. Aborting to avoid pulling unreleased upstream commits." -ForegroundColor Red
    exit 1
}
$upstreamTarget = $latestTag
Write-Host "Latest stable release: $latestTag" -ForegroundColor Cyan

# Get commit counts
$upstreamAhead = @(git rev-list "HEAD..$upstreamTarget").Count
$originAhead = @(git rev-list "HEAD..origin/main").Count

Write-Host "Fetched successfully" -ForegroundColor Green
Write-Host "  - ${upstreamTarget}: $upstreamAhead commits ahead" -ForegroundColor Gray
Write-Host "  - origin/main: $originAhead commits ahead" -ForegroundColor Gray

# Merge from upstream if there are new commits
if ($upstreamAhead -gt 0) {
    Write-Host "`nMerging $upstreamTarget..." -ForegroundColor Yellow
    git merge $upstreamTarget --no-edit

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Merge conflict detected!" -ForegroundColor Red
        Write-Host "Please resolve conflicts manually:" -ForegroundColor Yellow
        git status
        exit 1
    }

    Write-Host "Merge completed successfully" -ForegroundColor Green

    # Verify player colors were preserved
    $hasPlayerColors = git diff HEAD~1..HEAD -- TownOfUs/TownOfUsPlayerColors.cs
    if (-not $hasPlayerColors) {
        Write-Host "Local player colors preserved" -ForegroundColor Green
    }
} else {
    Write-Host "Already up to date with upstream" -ForegroundColor Green
}

# Push to origin if requested
if ($Push) {
    Write-Host "`nPushing to origin..." -ForegroundColor Yellow
    git push origin main

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Pushed successfully" -ForegroundColor Green
    } else {
        Write-Host "ERROR: Push failed" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`nUpdate complete!" -ForegroundColor Green

if ($Verbose) {
    Write-Host "`nRecent commits:" -ForegroundColor Cyan
    git log --oneline -5
}
