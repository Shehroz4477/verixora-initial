# Run the local web and Android flow

This guide runs the real local API. It does not use mock authentication. Development OTPs are written only to the API console; never use this mode outside a local machine.

## 1. Start backend infrastructure

In the backend repository:

```powershell
cd infrastructure
docker compose up -d
cd ..
.\scripts\Initialize-LocalDatabases.ps1
dotnet run --project .\api-host\ApiHost\ApiHost.csproj --launch-profile http
```

Keep the API terminal open. The API is available on `http://localhost:5166` and local SMS/email OTPs appear in this terminal only.

## 2. Start the web portal

In the frontend repository:

```powershell
npm run start:web
```

Open `http://localhost:4200`. The local API allows this origin through the local-only CORS configuration.

## 3. Run the Android application

Create and boot an Android emulator from Android Studio's **Device Manager**. The debug Android build uses `10.0.2.2:5166`, which reaches the API running on the Windows host. It deliberately permits cleartext traffic only for that emulator address; release builds remain HTTPS-only.

With the emulator already running, in the frontend repository run:

```powershell
npx cap sync android
cd android
.\gradlew.bat installDebug
```

Launch **Verixora** (`com.verixora.mobile`) on the emulator. Do not use the mobile browser preview for registration: the real flow requires the Android device-binding key.

## 4. Test the user flow

1. In Android, register with a unique valid E.164 phone number (for example `+923001234567`) and a strong password.
2. Copy the registration OTP from the backend API terminal into the Android app.
3. Log in from the same Android emulator/device. A login OTP is again shown in the API terminal.
4. In the Android **Profile/Settings** screen, enter an email address, request email verification, then enter the email OTP from the API terminal.
5. Open the web portal at `http://localhost:4200`, enter that verified email and password, request the web OTP, and enter the OTP from the API terminal.

The web portal intentionally remains unavailable until email verification is completed on the trusted Android application. Door/controller flows can be tested next from the Android app after a home and a securely provisioned controller are registered.
