[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://localhost:5166',
    [int]$StartupTimeoutSeconds = 45
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# This test deliberately uses only disposable, randomly named local records. It never
# displays OTPs, passwords, access tokens, or private keys. It validates the security
# boundary that keeps a door locked while a controller is offline.
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$apiJob = $null

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
    $bash = Join-Path $env:ProgramFiles 'Git\bin\bash.exe'
    if (-not (Test-Path -LiteralPath $bash)) {
        throw 'Git Bash with OpenSSL is required for this local E2E test.'
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
    for ($attempt = 0; $attempt -lt ($StartupTimeoutSeconds * 2); $attempt++) {
        try {
            $health = Invoke-WebRequest -UseBasicParsing -Uri "$ApiBaseUrl/health/ready" -TimeoutSec 2
            if ($health.StatusCode -eq 200) { return }
        }
        catch { }
        Start-Sleep -Milliseconds 500
    }
    throw "The API did not become ready at $ApiBaseUrl. Confirm appsettings.Local.json and Docker infrastructure are configured."
}

try {
    $faceKeyBytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($faceKeyBytes) }
    finally { $rng.Dispose() }

    $faceKey = [Convert]::ToBase64String($faceKeyBytes)
    $apiJob = Start-Job -ScriptBlock {
        param($Root, $Url, $EphemeralFaceKey)
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $env:ASPNETCORE_URLS = $Url
        $env:FaceBiometrics__EncryptionKeyBase64 = $EphemeralFaceKey
        $env:ControllerProvisioning__AllowInsecureDevelopmentAttestation = 'true'
        Set-Location $Root
        dotnet run --project 'api-host\ApiHost\ApiHost.csproj' --no-launch-profile
    } -ArgumentList $projectRoot, $ApiBaseUrl, $faceKey
    Wait-ForApi

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
