# 03-update-common-packages: Reconcile and update shared NuGet packages

Ziel: Gemeinsame NuGet-Pakete auf Versionen bringen, die .NET 10 unterstützen.

Gefundene Pakete (aus Projekten):
- Microsoft.AspNetCore.Authentication.OpenIdConnect (SendClient) — aktualisiert auf 10.0.12
- Microsoft.IdentityModel.Protocols.OpenIdConnect (SendClient) — aktualisiert auf 8.22.0
- Microsoft.AspNetCore.OpenApi (BotApi) — aktualisiert auf 10.0.12
- Microsoft.VisualStudio.Azure.Containers.Tools.Targets (BotApi) — 1.23.0
- Newtonsoft.Json (BotApi) — 13.0.4
- Swashbuckle.AspNetCore (BotApi) — 10.2.3

Aktionsplan:
1. Für identifizierte Updates habe ich die Paketversionen aktualisiert (siehe oben).
2. Führe `dotnet restore` + `dotnet build` aus, prüfe auf NU1202-Fehler oder Laufzeit-Inkompatibilitäten.
3. Wenn weitere inkompatible Pakete gefunden werden, recherchiere Alternativen oder bedingte PackageReferences.

**Eisenhower Matrix**:
Aktivitäten wurden nach Wichtigkeit und Dringlichkeit kategorisiert.
| Wichtigkeit \ Dringlichkeit | Nicht dringend | Dringend |
|-----------------------------|----------------|---------|
| Wichtig                      | 1. Recherchiere Alternativen für inkompatible Pakete (BotApi). <br> 2. Teste Anwendung mit aktualisierten Paketen. | - |
| Nicht wichtig                | - | - |

Done when: Alle relevanten Pakete sind auf kompatiblen Versionen aktualisiert und Lösung baut ohne Paketkonflikte.
