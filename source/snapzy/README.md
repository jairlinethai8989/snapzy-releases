# SnapZy Source

SnapZy is a Windows screen capture and image editing application. This folder contains the SnapZy-only application source and the scripts used to build its Windows installer. It does not contain Neo Snap product configuration or internal company documents.

## Build

Requirements: Windows 10 version 2004 or newer, Windows 11, .NET 10 SDK (x64), Microsoft Edge WebView2 Runtime, and Microsoft Visual C++ 2015-2022 Redistributable (x64).

Build the framework-dependent application:

```powershell
dotnet publish src/SnapCraft/SnapCraft.csproj -c Release -p:Platform=x64 -p:SelfContained=false -p:RollForward=LatestPatch -p:AppHostDotNetSearch=Global -o ./artifacts/publish
```

Build the Windows installer with the included SnapZy-only setup scripts:

```powershell
./installer/build-installer.ps1 -OutputDirectory ./artifacts
```

The app uses .NET 10 Desktop Runtime separately; it is not bundled in the application publish output. The installer can request installation of the official runtime when it is missing. Building the installer also requires Windows IExpress and network access when a required Microsoft runtime package is not cached.

## License

The original application source code is licensed under MIT; see [LICENSE](LICENSE) and [license scope](LICENSE-SCOPE.md). Third-party components retain their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

This public source tree is SnapZy-only. It intentionally omits Neo Snap product settings, internal documentation, private release materials, and internal test fixtures.
