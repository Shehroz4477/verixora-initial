# Run the local web and Android flow

This guide runs the real local API. It does not use mock authentication. Development OTPs are written only to the API console; never use this mode outside a local machine.

## 1. Start backend infrastructure

In the backend repository:

```powershell
cd infrastructure
docker compose up -d
cd ..
.\scripts\Initialize-LocalDatabases.ps1
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://0.0.0.0:5166'
dotnet run --project .\api-host\ApiHost\ApiHost.csproj --no-launch-profile
```

Keep the API terminal open. The API is available on this PC at `http://localhost:5166` and from the same Wi-Fi at `http://192.168.0.102:5166`. Local SMS/email OTPs appear in this terminal only. If Windows asks for firewall permission, allow **Private networks**. Do not expose this debug HTTP API to a public network.

## 2. Start the web portal

In the frontend repository:

```powershell
npm run start:web
```

Open `http://localhost:4200`. The local API allows this origin through the local-only CORS configuration.

## 3. Install the Android application on a physical phone

The current debug build targets this PC's current Wi-Fi IP: `192.168.0.102`. Both the PC and phone must be on the same trusted Wi-Fi. It deliberately permits debug-only cleartext traffic only to the emulator, localhost, and this one LAN address; release builds remain HTTPS-only.

In the frontend repository run:

```powershell
npx cap sync android
cd android
.\gradlew.bat assembleDebug
```

Copy and install [app-debug.apk](C:/Users/Shehroz/source/repos/Shehroz4477/verixora-frontemd-monoprepo-initial/android/app/build/outputs/apk/debug/app-debug.apk) on the Android phone, then launch **Verixora** (`com.verixora.mobile`). Do not use the mobile browser preview for registration: the real flow requires the Android device-binding key.

If the PC Wi-Fi IP changes, update both `androidPhysicalDeviceApiUrl` in `projects/mobile-app/environments/environment.ts` and the LAN `<domain>` in `android/app/src/debug/res/xml/debug_network_security_config.xml`, then repeat the build. To test through an Android emulator later, set `androidDebugTarget` to `'emulator'` instead.

## 4. Test the user flow

1. In Android, register with a unique valid E.164 phone number (for example `+923001234567`) and a strong password.
2. Copy the registration OTP from the backend API terminal into the Android app.
3. Log in from the same Android emulator/device. A login OTP is again shown in the API terminal.
4. In the Android **Profile/Settings** screen, enter an email address, request email verification, then enter the email OTP from the API terminal.
5. Open the web portal at `http://localhost:4200`, enter that verified email and password, request the web OTP, and enter the OTP from the API terminal.

The web portal intentionally remains unavailable until email verification is completed on the trusted Android application. Door/controller flows can be tested next from the Android app after a home and a securely provisioned controller are registered.
