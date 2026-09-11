# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade solution SendClient.slnx from .NET 9 to net10.0 (.NET 10, LTS)
**Scope**: 2 projects identified by assessment with TFM changes required (BotApi, SendClient). Scope includes project file updates, NuGet package upgrades, and code fixes for API incompatibilities. Build and test validation included.

## Tasks

### 01-upgrade-sendclient-project: Upgrade SendClient project to net10.0

Aktualisiere SendClient\SendClient.csproj: set TargetFramework to net10.0, update package references that are incompatible with .NET 10, rebuild and fix compile errors caused by API changes.

Affects: SendClient\SendClient.csproj

**Done when**: Project file targets net10.0, solution builds without errors, unit/integration tests for this project pass (if present).

---

### 02-upgrade-botapi-project: Upgrade BotApi project to net10.0

Aktualisiere BotApi\BotApi.csproj: set TargetFramework to net10.0, update or replace incompatible NuGet packages, apply code fixes for API behavior changes noted in assessment.

Affects: BotApi\BotApi.csproj

**Done when**: Project file targets net10.0, project builds, and service-level tests pass (if present).

---

### 03-update-common-packages: Reconcile and update shared NuGet packages

Untersuche gemeinsame Pakete (Versionskonflikte) und aktualisiere sie zu Versionen, die .NET 10 unterstützen. Wo kein kompatibler Release existiert, dokumentieren und suchen Alternativen.

**Done when**: Alle projektrelevanten Pakete, die aktualisiert werden können, haben kompatible Versionen; keine unresolved package conflicts remain.

---

### 04-fix-runtime-api-incompatibilities: Fix code-level API incompatibilities

Behebe API-Verhaltensänderungen und Inkompatibilitäten, die in assessment.md genannt sind. Priorisiere fixes per-project as they fail to build or test. Document any non-trivial workaround used.

**Done when**: Code compiles and tests pass; behavior verified for known breaking-change areas.

---

### 05-build-and-validate-solution: Full build and test validation

Führe eine vollständige Lösungserstellung aus, fixiere verbleibende Warnungen (Treat warnings as errors per workflow), und führe Test-Suite (falls vorhanden).

**Done when**: Solution builds without errors or warnings; tests pass.

---
