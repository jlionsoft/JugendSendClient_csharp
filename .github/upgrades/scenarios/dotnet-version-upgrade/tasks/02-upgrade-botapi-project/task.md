# 02-upgrade-botapi-project: Upgrade BotApi project to net10.0

Ziel: BotApi\BotApi.csproj auf net10.0 umstellen und Paketkompatibilität prüfen.

Gefundene betroffene Dateien und Befunde (erste Analyse):

- Projektdatei: BotApi\BotApi.csproj
  - Aktuelles TargetFramework: net9.0 (wurde auf net10.0 gesetzt)
  - Properties: Nullable=enable, ImplicitUsings=enable
- PackageReference Einträge:
  - Microsoft.AspNetCore.OpenApi Version="9.0.10"
  - Microsoft.VisualStudio.Azure.Containers.Tools.Targets Version="1.23.0"
  - Newtonsoft.Json Version="13.0.4"
  - Swashbuckle.AspNetCore Version="10.2.3"

Empfohlene nächste Schritte:
1. dotnet restore/ build ausführen und Kompilationsfehler/NU1202-Fehler prüfen.
2. Falls Pakete inkompatibel sind: Versionen aktualisieren (prüfen, ob Directory.Packages.props oder CPM verwendet wird).
3. Codefixes für API-Verhaltensänderungen anwenden (falls Build-Errors auftreten).

Done when: Projektfile targets net10.0; Projekt baut ohne Fehler; relevante Tests laufen.
