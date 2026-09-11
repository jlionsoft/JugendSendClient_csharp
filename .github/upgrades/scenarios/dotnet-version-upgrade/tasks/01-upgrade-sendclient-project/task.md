# 01-upgrade-sendclient-project: Upgrade SendClient project to net10.0

Ziel: SendClient\SendClient.csproj auf net10.0 umstellen und Paketkompatibilität prüfen.

Gefundene betroffene Dateien und Befunde (erste Analyse):

- Projektdatei: SendClient\SendClient.csproj
  - Aktuelles TargetFramework: net9.0
  - Properties: Nullable=enable, ImplicitUsings=enable
  - Blazor-Option gesetzt: BlazorDisableThrowNavigationException=true
- PackageReference Einträge:
  - Microsoft.AspNetCore.Authentication.OpenIdConnect Version="9.0.10"
  - Microsoft.IdentityModel.Protocols.OpenIdConnect Version="8.19.2"

Empfohlene nächste Schritte:
1. TargetFramework in SendClient.csproj auf net10.0 setzen (projekt-spezifisch).
2. dotnet restore/ build ausführen und Kompilationsfehler/NU1202-Fehler prüfen.
3. Falls Pakete inkompatibel sind: Versionen aktualisieren (prüfen, ob Directory.Packages.props oder CPM verwendet wird).
4. Codefixes für API-Verhaltensänderungen anwenden (falls Build-Errors auftreten).

Done when: Project file targets net10.0; Projekt baut ohne Fehler; relevante Tests laufen.
