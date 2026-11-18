param()

# Process guarantee for tools/math-visual/capture.ps1: a verification run may
# only ever terminate the MarkMello instance it started itself. Statically
# pins the rule down so a future edit cannot silently reintroduce the
# `Get-Process -Name 'MarkMello' | Stop-Process -Force` restart branch that
# could kill a user's instance with unsaved edits.

$ErrorActionPreference = 'Stop'

$capture = Join-Path $PSScriptRoot '../../tools/math-visual/capture.ps1'
$source = Get-Content -Path $capture -Raw
$code = $source -split "`n" | Where-Object { $_ -notmatch '^\s*#' } | Out-String

foreach ($forbidden in @('Stop-Process')) {
    if ($code -match [regex]::Escape($forbidden)) {
        throw "capture.ps1 must not call ${forbidden}: it may only close the process it started (CloseMainWindow)."
    }
}

if ($code -notmatch 'Start-Process.*-PassThru') {
    throw 'capture.ps1 must start MarkMello.exe itself with -PassThru and remember the PID.'
}

if ($code -notmatch 'CloseMainWindow') {
    throw 'capture.ps1 must close its own instance gracefully via CloseMainWindow.'
}

if ($code -notmatch 'finally') {
    throw 'capture.ps1 must clean up its own instance in a finally block.'
}

Write-Host 'PASS: capture.ps1 only manages the MarkMello instance it starts itself.'
