# Raport Zgodności (Stan Faktyczny) - FanControl Pro

**Data weryfikacji:** 15 marca 2026  
**Zakres:** stan repo, testów, uruchomienia UI, ścieżek hardware write

## 1) Status ogólny

**Aktualna gotowość projektu:** około **90-92%** (release-candidate, nie pełna produkcja).  
**Build:** PASS (0 błędów, 0 ostrzeżeń).  
**Testy:** PASS (**110/110**).  
**Tag:** `v1.0.0` istnieje.  

Projekt jest bardzo blisko produkcji, ale nie można jeszcze uczciwie uznać go za „w pełni domknięty v1.0 hardware-control” bez końcowej walidacji sprzętowej i domknięcia konfiguracji write-path.

## 2) Co jest realnie zaimplementowane

### 2.1 Kontrola wentylatorów - backend
- Jest implementacja `WinRing0EcRegisterAccess` (P/Invoke, porty EC 0x62/0x66).
- Jest implementacja `LibreHardwareMonitorSuperIoFanControlAccess` (ścieżka Super I/O).
- `AsusEcControllerV2` obsługuje:
  - preferencję Super I/O,
  - fallback EC,
  - cooldown gate,
  - read-back verification,
  - fallback do symulacji, gdy write path niedostępny.

### 2.2 UI / UX
- Działa dashboard z sekcjami Settings, Manual PWM i Curve Editor.
- Działa onboarding i przejście do dashboardu.
- Dodane poprawki uruchamiania pod WSL/Windows (skrypty run + lokalny staging exe + diagnostyka).
- Naprawiony temat/kontrast i przewijanie głównego widoku.
- Domknięte etapy 4.x na poziomie kodu/testów:
  - startup policy (`--start-minimized`, `--force-visible`) pokryta testami,
  - onboarding gating/conflict scenarios pokryte testami,
  - settings save/validation/reset + poprawne mapowanie `Theme=System`.
- Dodane artefakty walidacji 4.x:
  - `docs/qa/phase4-ux-system-integration-checklist.md`
  - `scripts/qa/Validate-Phase4Readiness.ps1`

### 2.3 Bezpieczeństwo i narzędzia wydaniowe
- Działa safety/cooldown/read-back logika w backendzie.
- Istnieją artefakty release i instalator WiX (`installer/wix/*`, workflow release).
- Dokumentacja QA/release jest obecna i rozbudowana.
- Dodano automatyczny walidator 5.x:
  - `scripts/qa/Validate-Phase5Readiness.ps1`
  - raport wynikowy: `artifacts/qa/phase5-readiness-report.md`

## 3) Co nadal blokuje pełne „production-ready”

### 3.1 Hardware write path jest domyślnie wyłączony
W `src/FanControlPro.Presentation/appsettings.json`:
- `EnableHardwareAccess = false`.

To oznacza, że standardowy start działa w trybie bezpiecznym/symulacyjnym, dopóki nie włączysz tego świadomie.

### 3.2 Rejestry ASUS EC są placeholderami
W `AsusPwmRegisters` wartości są nadal `-1` (placeholder), więc ścieżka EC nie jest finalnie zwalidowana mapą rejestrów produkcyjnych.

### 3.3 Brak potwierdzonego hardware-in-the-loop final pass
Brakuje zamkniętego, udokumentowanego runu walidacji na docelowym sprzęcie (ASUS Z490-P + NCT6798D) z checklistą P1 i raportem końcowym PASS.

### 3.4 Brak pełnych testów UI/E2E warstwy Presentation
Testy domeny/aplikacji/infrastruktury są mocne, coverage ViewModel Presentation został rozszerzony, ale nadal brakuje pełnej automatyzacji UI E2E (workflow okno/tray/interakcje użytkownika 1:1).

## 4) Aktualny, rzetelny wniosek

Najważniejsza różnica względem wcześniejszych raportów:
- To **nie** jest projekt „w 82% i z brakami podstawowymi”.
- To jest **zaawansowany RC**, z działającą architekturą, testami i ścieżkami hardware write w kodzie.

Jednocześnie:
- Bez finalnej walidacji hardware write i domknięcia konfiguracji produkcyjnej nie należy oznaczać stanu jako pełne „100% production-ready”.

## 5) Priorytety domknięcia (ostatnia prosta)

1. **P1:** Walidacja sprzętowa ASUS Z490-P checklistą QA i końcowy raport PASS.
2. **P1:** Uzupełnienie finalnych map rejestrów EC (jeśli EC fallback ma być aktywnie używany).
3. **P1:** Decyzja release: domyślnie `EnableHardwareAccess=false` (bezpiecznie) czy profil wdrożeniowy z `true` po walidacji.
4. **P2:** Dodać pełne testy UI/E2E (okno + tray + interakcje) dla kluczowych flow.
5. **P2:** Finalny commit/release clean-up i publikacja artefaktów po potwierdzeniu hardware run.

---

## 6) Potwierdzenie wykonania w tym audycie

- `dotnet build` -> PASS
- `dotnet test` -> PASS (110/110)
- ręczne uruchomienie aplikacji -> działa, widoczne UI
- zweryfikowane: ścieżki EC/Super I/O, konfiguracja bezpieczeństwa, stan repo
