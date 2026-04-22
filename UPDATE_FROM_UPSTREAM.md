# Automatic Updates from AU-Avengers/TOU-Mira

This repository is configured to automatically merge changes from the AU-Avengers upstream repository while preserving your local `TownOfUsPlayerColors.cs` file.

## How It Works

- **Upstream Remote**: `https://github.com/AU-Avengers/TOU-Mira.git`
- **Merge Strategy**: Custom merge driver keeps your local player colors on conflicts
- **Union Merging**: Common file types (`.cs`, `.xml`, `.json`) use union merge strategy to minimize conflicts

## Keeping Updated Manually

Run this command to fetch and merge the latest AU-Avengers changes:

```powershell
git fetch upstream main
git merge upstream/main
```

Or use the convenience command:

```powershell
git pull upstream main
```

## Automatic Updates (Recommended)

To keep your repository automatically synced, run this PowerShell script periodically (e.g., via Windows Task Scheduler):

```powershell
# auto-update.ps1
$repoPath = "c:\Users\mkate\OneDrive\Documents\GitHub\TOU-Mira"
Set-Location $repoPath

# Fetch latest from upstream
git fetch upstream main
git fetch origin main

# Merge upstream changes
git merge upstream/main --no-edit -q

# If merge succeeded, push to origin
if ($LASTEXITCODE -eq 0) {
    git push origin main
    Write-Host "Successfully updated from AU-Avengers and pushed to origin"
} else {
    Write-Host "Merge conflict detected. Please resolve manually."
}
```

### Schedule with Windows Task Scheduler

1. Save the script as `auto-update.ps1` in your repo
2. Open Task Scheduler
3. Create a new task:
   - Name: "TOU-Mira Auto Update"
   - Trigger: Daily (or as desired)
   - Action: `powershell.exe -File "C:\path\to\auto-update.ps1"`
   - Check "Run whether user is logged in or not"

## Configuration Summary

**Git Config:**
```
pull.rebase = false        # Always merge, never rebase
fetch.prune = true         # Clean up deleted remote branches
merge.ours.driver = true   # Custom driver for "ours" strategy
```

**Merge Rules (`.gitattributes`):**
- `TownOfUsPlayerColors.cs` → Always keep local version
- `*.cs`, `*.xml`, `*.json` → Use union merge strategy

## Remotes

```
origin:   https://github.com/PrimeStrat/TOU-Mira.git
upstream: https://github.com/AU-Avengers/TOU-Mira.git
```

## Notes

- Your local `TownOfUsPlayerColors.cs` will never be overwritten by merges
- All other AU-Avengers changes are automatically adopted
- Review merge commits if you need to track what changed
