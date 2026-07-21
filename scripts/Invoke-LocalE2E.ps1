[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://localhost:5166',
    [ValidateSet('PostgreSql', 'SqlServer')]
    [string]$DatabaseProvider = 'PostgreSql',
    [ValidateSet('DapperStoredProcedure', 'AdoNetStoredProcedure', 'EfCore')]
    [string]$DataAccessMode = 'DapperStoredProcedure',
    [int]$StartupTimeoutSeconds = 45
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# This test deliberately uses only disposable, randomly named local records. It never
# displays OTPs, passwords, access tokens, or private keys. It validates the security
# boundary that keeps a door locked while a controller is offline.
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$apiJob = $null

function Get-LocalDockerValue {
    param([Parameter(Mandatory)][string]$ContainerName, [Parameter(Mandatory)][string]$Name)

    $command = switch ($Name) {
        'MSSQL_SA_PASSWORD' { 'printf %s "$MSSQL_SA_PASSWORD"' }
        'REDIS_PASSWORD' { 'printf %s "$REDIS_PASSWORD"' }
        'POSTGRES_DB' { 'printf %s "$POSTGRES_DB"' }
        'POSTGRES_USER' { 'printf %s "$POSTGRES_USER"' }
        'POSTGRES_PASSWORD' { 'printf %s "$POSTGRES_PASSWORD"' }
        default { throw "Unsupported local Docker value '$Name'." }
    }
    $value = ((& docker exec $ContainerName /bin/sh -c $command 2>$null) | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($value)) {
        throw "The local Docker value '$Name' is unavailable. Start infrastructure/docker-compose.yml first."
    }
    return $value
}

function Assert-That {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

function Invoke-Api {
    param(
        [Parameter(Mandatory)][ValidateSet('GET', 'POST')][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [object]$Body,
        [string]$AccessToken,
        [switch]$Form
    )

    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($AccessToken)) {
        $headers['Authorization'] = "Bearer $AccessToken"
    }

    $request = @{
        Uri = "$ApiBaseUrl$Path"
        Method = $Method
        Headers = $headers
        UseBasicParsing = $true
    }
    if ($null -ne $Body) {
        if ($Form) {
            $request['Body'] = $Body
            $request['ContentType'] = 'application/x-www-form-urlencoded'
        }
        else {
            $request['Body'] = ($Body | ConvertTo-Json -Depth 12 -Compress)
            $request['ContentType'] = 'application/json'
        }
    }

    try {
        $response = Invoke-WebRequest @request
        return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Content = $response.Content }
    }
    catch {
        if ($null -eq $_.Exception.Response) { throw }
        $response = $_.Exception.Response
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        try { $content = $reader.ReadToEnd() }
        finally { $reader.Dispose() }
        return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Content = $content }
    }
}

function Convert-ResponseJson {
    param([Parameter(Mandatory)]$Response)
    Assert-That (-not [string]::IsNullOrWhiteSpace($Response.Content)) 'The API returned an empty response body.'
    return ($Response.Content | ConvertFrom-Json)
}

function Get-LocalOtp {
    param(
        [Parameter(Mandatory)]$Job,
        [Parameter(Mandatory)][string]$Recipient,
        [Parameter(Mandatory)][ValidateSet('SMS', 'EMAIL')][string]$Channel
    )

    $pattern = "LOCAL DEVELOPMENT $Channel to " + [regex]::Escape($Recipient) + ': OTP ([0-9]{6})'
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        $logs = (Receive-Job -Job $Job -Keep 2>&1 | Out-String)
        $matches = [regex]::Matches($logs, $pattern)
        if ($matches.Count -gt 0) { return $matches[$matches.Count - 1].Groups[1].Value }
        Start-Sleep -Milliseconds 250
    }

    throw "The local development $Channel OTP did not arrive in the API console."
}

function New-Base64UrlFingerprint {
    param([Parameter(Mandatory)][byte[]]$Bytes)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [Convert]::ToBase64String($sha.ComputeHash($Bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    }
    finally { $sha.Dispose() }
}

function New-P256KeyMaterial {
    $bash = if ($env:OS -eq 'Windows_NT') {
        Join-Path $env:ProgramFiles 'Git\bin\bash.exe'
    }
    else {
        (Get-Command bash -ErrorAction Stop).Source
    }
    if (-not (Test-Path -LiteralPath $bash)) {
        throw 'Bash with OpenSSL is required for this local E2E test.'
    }

    $spkiBase64 = ((& $bash -lc "openssl ecparam -name prime256v1 -genkey -noout | openssl ec -pubout -outform DER 2>/dev/null | openssl base64 -A") | Out-String).Trim()
    Assert-That (-not [string]::IsNullOrWhiteSpace($spkiBase64)) 'OpenSSL did not create a P-256 public key.'
    $pem = ((& $bash -lc "openssl ecparam -name prime256v1 -genkey -noout | openssl ec -pubout 2>/dev/null") | Out-String).Trim()
    Assert-That (-not [string]::IsNullOrWhiteSpace($pem)) 'OpenSSL did not create a controller P-256 public key.'

    $spki = [Convert]::FromBase64String($spkiBase64)
    return [pscustomobject]@{
        MobilePublicKeySpkiBase64 = $spkiBase64
        MobileFingerprint = New-Base64UrlFingerprint -Bytes $spki
        ControllerPublicKeyPem = $pem
    }
}

function Wait-ForApi {
    param($Job, [string[]]$SensitiveValues = @())

    for ($attempt = 0; $attempt -lt ($StartupTimeoutSeconds * 2); $attempt++) {
        try {
            $health = Invoke-WebRequest -UseBasicParsing -Uri "$ApiBaseUrl/health/ready" -TimeoutSec 2
            if ($health.StatusCode -eq 200) { return }
        }
        catch { }
        Start-Sleep -Milliseconds 500
    }

    $diagnostics = (Receive-Job -Job $Job -Keep 2>&1 | Out-String)
    foreach ($sensitiveValue in $SensitiveValues | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
        $diagnostics = $diagnostics.Replace($sensitiveValue, '<redacted>')
    }
    $diagnostics = (($diagnostics -split [Environment]::NewLine | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Last 12) -join [Environment]::NewLine)
    throw "The API did not become ready at $ApiBaseUrl. Diagnostics: $diagnostics"
}

try {
    # Apply idempotent schema scripts before creating test data. This makes the test
    # usable on a fresh local Docker volume and validates the selected SQL dialect.
    & (Join-Path $projectRoot 'scripts\Initialize-LocalDatabases.ps1') -Provider $DatabaseProvider | Out-Null

    $faceKeyBytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($faceKeyBytes) }
    finally { $rng.Dispose() }

    $faceKey = [Convert]::ToBase64String($faceKeyBytes)
    $securityKeyBytes = New-Object byte[] 32
    $securityRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $securityRng.GetBytes($securityKeyBytes) }
    finally { $securityRng.Dispose() }
    $otpHashKey = [Convert]::ToBase64String($securityKeyBytes)
    $jwtKey = [Convert]::ToBase64String($securityKeyBytes)

    $databaseConnection = $null
    $redisConfiguration = $null
    $sqlServerPassword = $null
    $postgreSqlPassword = $null
    $redisPassword = $null
    if ($DatabaseProvider -eq 'SqlServer') {
        $sqlServerPassword = Get-LocalDockerValue -ContainerName 'verixora-sqlserver' -Name 'MSSQL_SA_PASSWORD'
        $databaseConnection = "Server=127.0.0.1,14333;Database=verixora;User Id=sa;Password=$sqlServerPassword;Encrypt=True;TrustServerCertificate=True"
    }
    else {
        $postgreSqlPassword = Get-LocalDockerValue -ContainerName 'verixora-postgres' -Name 'POSTGRES_PASSWORD'
        $postgreSqlUser = Get-LocalDockerValue -ContainerName 'verixora-postgres' -Name 'POSTGRES_USER'
        $postgreSqlDatabase = Get-LocalDockerValue -ContainerName 'verixora-postgres' -Name 'POSTGRES_DB'
        $databaseConnection = "Host=127.0.0.1;Port=5433;Database=$postgreSqlDatabase;Username=$postgreSqlUser;Password=$postgreSqlPassword"
    }
    $redisPassword = Get-LocalDockerValue -ContainerName 'verixora-redis' -Name 'REDIS_PASSWORD'
    $redisConfiguration = "127.0.0.1:6379,password=$redisPassword,abortConnect=false"
    $apiArguments = @($projectRoot, $ApiBaseUrl, $faceKey, $DatabaseProvider, $databaseConnection, $redisConfiguration, $DataAccessMode, $otpHashKey, $jwtKey)
    $apiJob = Start-Job -ScriptBlock {
        param($Root, $Url, $EphemeralFaceKey, $Provider, $ConnectionString, $RedisConfiguration, $Mode, $OtpHashKey, $JwtKey)
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $env:ASPNETCORE_URLS = $Url
        $env:FaceBiometrics__EncryptionKeyBase64 = $EphemeralFaceKey
        $env:ControllerProvisioning__AllowInsecureDevelopmentAttestation = 'true'
        $env:DataAccess__Mode = $Mode
        $env:DatabaseProvider = $Provider
        $env:ConnectionStrings__DefaultConnection = $ConnectionString
        $env:Redis__Configuration = $RedisConfiguration
        $env:Otp__HashKey = $OtpHashKey
        $env:Jwt__Key = $JwtKey
        $env:Jwt__Issuer = 'Verixora'
        Set-Location $Root
        dotnet run --project 'api-host\ApiHost\ApiHost.csproj' --no-launch-profile
    } -ArgumentList $apiArguments
    Wait-ForApi -Job $apiJob -SensitiveValues @($databaseConnection, $redisConfiguration, $sqlServerPassword, $postgreSqlPassword, $redisPassword, $otpHashKey, $jwtKey)

    $suffix = (Get-Random -Minimum 1000000 -Maximum 9999999).ToString()
    $phone = '+92300' + $suffix
    $password = 'E2e!' + [Guid]::NewGuid().ToString('N') + 'Aa9'
    $deviceId = 'e2e-mobile-' + [Guid]::NewGuid().ToString('N')
    $hardwareId = 'e2e-esp32-' + [Guid]::NewGuid().ToString('N').Substring(0, 16)
    $keys = New-P256KeyMaterial

    $response = Invoke-Api -Method POST -Path '/api/v1/auth/send-otp' -Body @{ phoneNumber = $phone }
    Assert-That ($response.StatusCode -eq 200) 'Registration OTP request must succeed.'
    $registrationOtp = Get-LocalOtp -Job $apiJob -Recipient $phone -Channel SMS

    $response = Invoke-Api -Method POST -Path '/api/v1/auth/register' -Body @{
        phoneNumber = $phone; password = $password; confirmPassword = $password; otp = $registrationOtp
        deviceId = $deviceId; deviceFingerprint = $keys.MobileFingerprint; devicePublicKeySpkiBase64 = $keys.MobilePublicKeySpkiBase64
    }
    Assert-That ($response.StatusCode -eq 200) 'Trusted-device registration must succeed.'
    $registration = Convert-ResponseJson $response

    $response = Invoke-Api -Method POST -Path '/api/v1/auth/send-login-otp' -Body @{
        phoneNumber = $phone; password = $password; deviceId = $deviceId; deviceFingerprint = $keys.MobileFingerprint
    }
    Assert-That ($response.StatusCode -eq 200) 'Same-device login OTP request must succeed.'
    $loginOtp = Get-LocalOtp -Job $apiJob -Recipient $phone -Channel SMS
    $response = Invoke-Api -Method POST -Path '/api/v1/auth/login' -Body @{
        phoneNumber = $phone; password = $password; otp = $loginOtp; deviceId = $deviceId; deviceFingerprint = $keys.MobileFingerprint
        devicePublicKeySpkiBase64 = $keys.MobilePublicKeySpkiBase64
    }
    Assert-That ($response.StatusCode -eq 200) 'Same-device login must succeed.'
    $accessToken = (Convert-ResponseJson $response).token
    Assert-That (-not [string]::IsNullOrWhiteSpace($accessToken)) 'Login must return an access token.'

    $response = Invoke-Api -Method POST -Path '/api/v1/homes' -AccessToken $accessToken -Body @{ name = "E2E Home $suffix" }
    Assert-That ($response.StatusCode -eq 200) 'Home creation must succeed.'
    $homeId = (Convert-ResponseJson $response).id

    $response = Invoke-Api -Method POST -Path '/api/v1/devices' -AccessToken $accessToken -Body @{
        homeId = $homeId; name = 'E2E Controller'; hardwareId = $hardwareId
    }
    Assert-That ($response.StatusCode -eq 200) 'Controller registration must create a pending provisioning session.'
    $controller = Convert-ResponseJson $response
    Assert-That ($controller.status -eq 'Pending') 'A newly registered controller must be pending.'

    $response = Invoke-Api -Method POST -Path '/api/v1/locks' -AccessToken $accessToken -Body @{
        name = 'Rejected Pending Door'; deviceId = $controller.deviceId; homeId = $homeId; requiresFace = $false
    }
    Assert-That ($response.StatusCode -eq 400) 'A lock must not be registered before controller provisioning.'

    $response = Invoke-Api -Method POST -Path '/api/v1/devices/provisioning/complete' -Body @{
        deviceId = $controller.deviceId; provisioningToken = $controller.provisioningToken
        controllerPublicKeyPem = $keys.ControllerPublicKeyPem; hardwareAttestation = 'local-development-only'
    }
    Assert-That ($response.StatusCode -eq 200) 'Development controller provisioning must succeed with a P-256 key.'
    $provisioned = Convert-ResponseJson $response
    Assert-That ($provisioned.status -eq 'Active') 'Provisioned controller must become active.'

    $response = Invoke-Api -Method POST -Path '/api/v1/locks' -AccessToken $accessToken -Body @{
        name = 'E2E Door'; deviceId = $controller.deviceId; homeId = $homeId; requiresFace = $false
    }
    Assert-That ($response.StatusCode -eq 200) 'A lock may be registered only after provisioning.'
    $lock = Convert-ResponseJson $response

    $response = Invoke-Api -Method POST -Path "/api/v1/locks/$($lock.smartLockId)/unlock" -AccessToken $accessToken -Form -Body @{ idempotencyKey = [Guid]::NewGuid().ToString('N') }
    Assert-That ($response.StatusCode -eq 400) 'Remote unlock must fail closed while the controller is offline.'

    $response = Invoke-Api -Method GET -Path "/api/v1/auditlogs?homeId=$homeId" -AccessToken $accessToken
    Assert-That ($response.StatusCode -eq 200) 'The owner must be able to view the audit trail.'
    $audit = Convert-ResponseJson $response

    [pscustomobject]@{
        DatabaseProvider = $DatabaseProvider
        DataAccessMode = $DataAccessMode
        Registration = $registration.message
        SameDeviceLogin = $true
        PendingDoorRejected = $true
        ControllerProvisioned = $provisioned.status
        DoorRegistered = $lock.status
        OfflineUnlockRejected = $true
        AuditEvents = @($audit).Count
    } | ConvertTo-Json -Compress
}
finally {
    if ($null -ne $apiJob) {
        Stop-Job -Job $apiJob -ErrorAction SilentlyContinue
        Remove-Job -Job $apiJob -Force -ErrorAction SilentlyContinue
    }
}
