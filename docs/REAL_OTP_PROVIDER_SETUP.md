# Real OTP delivery setup

Verixora deliberately keeps message-provider credentials out of source control.
Development uses terminal-only delivery by default; this is safe for local testing
but it does not send a real SMS or email.

To send real messages from the local API, create accounts with the providers,
obtain their credentials, then store the values in .NET user secrets. Never put
these values in `appsettings*.json`, a Git commit, an APK, or a web build.

## 1. Enable real delivery locally

Run once from the backend repository:

```powershell
dotnet user-secrets set "Messaging:UseRealProvidersInDevelopment" "true" --project .\api-host\ApiHost\ApiHost.csproj
```

Restart the API after changing secrets. If this flag is false, the API prints OTPs
only to the local API console and deliberately makes no provider network request.

## 2. SMS through Twilio

Configure an SMS-capable Twilio sender and store the values below:

```powershell
dotnet user-secrets set "Messaging:Sms:Provider" "Twilio" --project .\api-host\ApiHost\ApiHost.csproj
dotnet user-secrets set "Messaging:Sms:Twilio:AccountSid" "YOUR_ACCOUNT_SID" --project .\api-host\ApiHost\ApiHost.csproj
dotnet user-secrets set "Messaging:Sms:Twilio:AuthToken" "YOUR_AUTH_TOKEN" --project .\api-host\ApiHost\ApiHost.csproj
dotnet user-secrets set "Messaging:Sms:Twilio:FromNumber" "+15551234567" --project .\api-host\ApiHost\ApiHost.csproj
```

Use an E.164 sender number and register message templates/sender identity required
by the countries where Verixora will operate. A trial account can be useful for
testing but is not a production SMS service: it has trial limits and may require
recipient verification.

## 3. Email through SMTP or SendGrid

For a company SMTP mailbox, use an app password or provider token (not a personal
mailbox password):

```powershell
dotnet user-secrets set "Messaging:Email:Provider" "Smtp" --project .\api-host\ApiHost\ApiHost.csproj
dotnet user-secrets set "Messaging:Email:Smtp:Host" "smtp.example.com" --project .\api-host\ApiHost\ApiHost.csproj
dotnet user-secrets set "Messaging:Email:Smtp:Port" "587" --project .\api-host\ApiHost\ApiHost.csproj
dotnet user-secrets set "Messaging:Email:Smtp:UserName" "security@example.com" --project .\api-host\ApiHost\ApiHost.csproj
dotnet user-secrets set "Messaging:Email:Smtp:Password" "YOUR_APP_PASSWORD" --project .\api-host\ApiHost\ApiHost.csproj
dotnet user-secrets set "Messaging:Email:Smtp:FromEmail" "security@example.com" --project .\api-host\ApiHost\ApiHost.csproj
dotnet user-secrets set "Messaging:Email:Smtp:FromName" "Verixora Security" --project .\api-host\ApiHost\ApiHost.csproj
```

Alternatively select `SendGrid` and set `Messaging:Email:SendGrid:ApiKey`,
`Messaging:Email:SendGrid:FromEmail`, and `Messaging:Email:SendGrid:FromName`.

## 4. Test safely

Use one phone number and one email address that you control. Request an OTP, enter
the code within five minutes, and confirm that a resend is blocked for sixty
seconds. If provider delivery fails, Verixora deletes the generated Redis OTP and
returns a generic delivery error; it never leaves a code active when no message was
sent.

There is no legitimate unlimited free SMS provider suitable for a physical-access
system. Use trial credit only for controlled testing, then move to a paid,
compliant provider before customer onboarding.
