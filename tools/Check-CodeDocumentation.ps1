$ErrorActionPreference = "Stop"

# Lightweight guard for contributor-facing function documentation.
# It checks methods/constructors that start with an access modifier and ignores
# fields/properties whose initializer happens to contain parentheses.
$Root = Split-Path $PSScriptRoot -Parent
$Files = Get-ChildItem $Root -Recurse -Filter *.cs -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }

$Missing = New-Object System.Collections.Generic.List[string]
$MethodPattern = '^\s*(public|private|protected|internal)\s+(?:(?:static|async|override|virtual|sealed|new|partial|unsafe)\s+)*(?:[\w<>,?.\[\]():]+\s+)?([A-Za-z_]\w*)\s*\('

foreach ($File in $Files) {
    $Lines = Get-Content -LiteralPath $File.FullName
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $Line = $Lines[$i]
        $BeforeParen = if ($Line.Contains('(')) { $Line.Substring(0, $Line.IndexOf('(')) } else { $Line }
        if ($BeforeParen.Contains('=')) { continue }
        if ($Line -match '\b(class|record|interface|delegate)\b') { continue }
        if ($Line -notmatch $MethodPattern) { continue }

        $j = $i - 1
        while ($j -ge 0 -and [string]::IsNullOrWhiteSpace($Lines[$j])) { $j-- }
        if ($j -lt 0 -or -not $Lines[$j].TrimStart().StartsWith('///')) {
            $Relative = [IO.Path]::GetRelativePath($Root, $File.FullName)
            $Missing.Add("${Relative}:$($i + 1): $($Matches[2])")
        }
    }
}

if ($Missing.Count -gt 0) {
    Write-Host "Functions missing XML documentation:" -ForegroundColor Red
    $Missing | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "Code documentation check passed: all detected methods/constructors have XML summaries." -ForegroundColor Green
