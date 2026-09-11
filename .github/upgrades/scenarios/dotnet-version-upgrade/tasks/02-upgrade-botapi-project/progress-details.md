# Progress Details - 02-upgrade-botapi-project

Kurz: Zielframework auf net10.0 gesetzt, Restore/Build erfolgreich mit Warnings.

Änderungen:
- BotApi/BotApi.csproj: TargetFramework von net9.0 → net10.0 gesetzt
- tasks/02-upgrade-botapi-project/task.md: Recherchen und Befunde ergänzt

Validierung:
- `dotnet restore` für SendClient.slnx: erfolgreich
- `dotnet build` für SendClient.slnx: erfolgreich (alle Projekte bauen, viele Compiler-Warnungen bezüglich Non-Nullable und mögliche Nullreferenzen)

Offene Punkte / Hinweise:
- Viele Nullable-Warnungen (CS8618/CS8603/CS8602). Diese betreffen Felder/Eigenschaften, die als non-nullable deklariert sind, aber nicht im Konstruktor initialisiert werden. Empfehlung: gezielt dort initialisieren, `required`-Modifier verwenden, oder die Eigenschaft als nullable deklarieren, je nach Kontext.
- NuGet-Pakete wurden noch nicht geändert; falls Laufzeit-Inkompatibilitäten auftreten, aktualisiere ich die betroffenen Pakete im nächsten Task (03).

Done when: Projektfile targets net10.0 und Projekt baut fehlerfrei (aktuell: Warnings vorhanden, aber kein Fehler).
