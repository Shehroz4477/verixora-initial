[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://localhost:5166'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Executes the same security flow through every supported provider/access-mode pair.
# Each child run creates isolated local data and outputs no credentials, OTPs, or tokens.
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$e2eScript = Join-Path $root 'scripts\Invoke-LocalE2E.ps1'
$results = [System.Collections.Generic.List[object]]::new()

foreach ($provider in @('PostgreSql', 'SqlServer')) {
    foreach ($mode in @('DapperStoredProcedure', 'AdoNetStoredProcedure', 'EfCore')) {
        $resultJson = & $e2eScript -ApiBaseUrl $ApiBaseUrl -DatabaseProvider $provider -DataAccessMode $mode
        $result = $resultJson | ConvertFrom-Json
        if (-not $result.SameDeviceLogin -or -not $result.PendingDoorRejected -or -not $result.OfflineUnlockRejected) {
            throw "Security E2E verification failed for $provider / $mode."
        }
        $results.Add($result)
    }
}

[pscustomobject]@{
    TotalCombinations = $results.Count
    PassedCombinations = $results.Count
    Results = $results
} | ConvertTo-Json -Depth 4
