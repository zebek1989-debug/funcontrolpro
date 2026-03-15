# Plan Etapowy Rozwoju FanControl Pro
## Roadmap od konceptu do wydania 1.0

---

## FAZA 0: PRZYGOTOWANIE I SETUP (Tydzień 1-2)

### Milestone 0.1: Środowisko deweloperskie
**Czas: 3-5 dni**

**Zadania:**
1. ✅ Instalacja Visual Studio 2025 Community
   - Workload: .NET Desktop Development
   - Workload: Windows 11 SDK
   
2. ✅ Instalacja dodatkowych narzędzi
   - Git + GitHub Desktop
   - WiX Toolset 4.0
   - .NET 8.0 SDK
   
3. ✅ Setup projektu
   - Utworzenie solution FanControlPro
   - Struktura folderów zgodna z Clean Architecture
   - Inicjalizacja Git repository
   
4. ✅ Konfiguracja CI/CD
   - GitHub Actions workflow (podstawowy)
   - Branch protection rules

**Kryteria akceptacji:**
- [ ] Projekt kompiluje się bez błędów
- [ ] Git repository jest skonfigurowane
- [ ] GitHub Actions wykonuje build

---

### Milestone 0.2: Architektura projektu
**Czas: 2-3 dni**

**Zadania:**
1. ✅ Utworzenie projektów w solution
   ```
   FanControlPro/
   ├── FanControlPro.Domain/          (Business logic)
   ├── FanControlPro.Infrastructure/  (Hardware, persistence)
   ├── FanControlPro.Application/     (Services, use cases)
   ├── FanControlPro.Presentation/    (WPF UI)
   └── FanControlPro.Tests/           (Unit tests)
   ```

2. ✅ Instalacja NuGet packages (podstawowe)
   - CommunityToolkit.Mvvm 8.3
   - Microsoft.Extensions.DependencyInjection 8.0
   - Serilog 4.0
   
3. ✅ Setup Dependency Injection
   - Konfiguracja Host Builder
   - Rejestracja podstawowych serwisów
   
4. ✅ Setup loggingu
   - Konfiguracja Serilog
   - Logi do pliku i konsoli

**Kryteria akceptacji:**
- [ ] Wszystkie projekty kompilują się
- [ ] DI działa poprawnie
- [ ] Logging zapisuje do pliku

**Deliverable:** Szkielet aplikacji gotowy do rozwoju

---

## FAZA 1: MONITORING HARDWARE (Tydzień 3-5)

### Milestone 1.1: Wykrywanie sprzętu
**Czas: 5-7 dni**
**Priorytet: KRYTYCZNY**
**FR: FR-HW-001, FR-HW-002**

**Zadania:**
1. ✅ Integracja LibreHardwareMonitor
   - Instalacja LibreHardwareMonitorLib 0.10.0
   - Wrapper dla bezpiecznego dostępu
   
2. ✅ Implementacja HardwareDetector
   ```csharp
   public interface IHardwareDetector
   {
       Task<DetectionResult> DetectHardwareAsync();
       Task<SupportLevel> ClassifyHardwareAsync(Hardware hw);
   }
   ```
   
3. ✅ Klasyfikacja wsparcia sprzętu
   - Full Control
   - Monitoring Only
   - Unsupported
   
4. ✅ Persystencja wykrytego sprzętu
   - Zapis do hardware.json
   - Cache dla szybszego startu

**Testy:**
- [ ] Unit testy dla klasyfikacji
- [ ] Manual testing na ASUS Z490-P
- [ ] Walidacja z HWiNFO64

**Kryteria akceptacji:**
- [ ] Aplikacja wykrywa CPU, GPU, MB, dyski
- [ ] Każdy komponent ma przypisany SupportLevel
- [ ] Wykryte dane są zgodne z HWiNFO (±5%)

---

### Milestone 1.2: Odczyt sensorów
**Czas: 3-5 dni**
**Priorytet: KRYTYCZNY**
**FR: FR-MON-001, FR-MON-002, FR-MON-003**

**Zadania:**
1. ✅ Implementacja SensorReader
   ```csharp
   public interface ISensorReader
   {
       Task<Temperature> ReadTemperatureAsync(string sensorId);
       Task<int> ReadFanSpeedAsync(string fanId);
       Task<SystemLoad> ReadSystemLoadAsync();
   }
   ```
   
2. ✅ Monitoring loop
   - Timer z konfigurowalnym interwałem (1-5s)
   - Async odczyt bez blokowania UI
   
3. ✅ Walidacja odczytów
   - Sanity checks (temp < 150°C, RPM < 10000)
   - Detekcja błędnych sensorów
   
4. ✅ State management dla odczytów
   - AppState z aktualnymi wartościami
   - Observable pattern dla UI

**Testy:**
- [ ] Unit testy dla walidacji
- [ ] Performance test (CPU usage < 2%)
- [ ] Stress test (24h continuous monitoring)

**Kryteria akceptacji:**
- [ ] Temperatury odświeżają się co 1-5s
- [ ] RPM są dokładne ±50 RPM
- [ ] CPU usage < 2% w tle

---

### Milestone 1.3: Podstawowy UI - Dashboard
**Czas: 4-6 dni**
**Priorytet: WYSOKI**
**FR: FR-UI-001**

**Zadania:**
1. ✅ Setup WPF + MaterialDesign
   - MaterialDesignThemes 5.1
   - Podstawowy theme (Dark/Light)
   
2. ✅ MainWindow z nawigacją
   - Menu boczne
   - Content area
   
3. ✅ Dashboard View
   ```xaml
   - Sekcja Temperatures (CPU, GPU, MB)
   - Sekcja Fan Speeds (lista wentylatorów)
   - Sekcja System Load (CPU%, GPU%, RAM)
   - Status bar (connection status)
   ```
   
4. ✅ Data binding
   - ViewModel → View
   - Auto-refresh co 1s
   
5. ✅ Ikony i layout
   - FontAwesome icons
   - Responsive grid

**Testy:**
- [ ] UI responsiveness test
- [ ] Różne rozdzielczości (1280x720 - 4K)

**Kryteria akceptacji:**
- [ ] Dashboard pokazuje wszystkie dane real-time
- [ ] UI nie laguje przy odświeżaniu
- [ ] Wygląd zgodny z Material Design 3

**Deliverable:** Działający monitoring w GUI

---

## FAZA 2: KONTROLA WENTYLATORÓW (Tydzień 6-9)

### Milestone 2.1: Architektura kontroli
**Czas: 3-4 dni**
**Priorytet: KRYTYCZNY**
**FR: FR-HW-003**

**Zadania:**
1. ✅ Interfejs IFanControllerV2
   ```csharp
   public interface IFanControllerV2
   {
       Task<bool> CanControlAsync(FanChannel channel);
       Task<FanControlResult> SetSpeedAsync(FanChannel channel, int percent);
       Task<int> GetCurrentSpeedAsync(FanChannel channel);
       Task<HealthStatus> GetHealthStatusAsync();
   }
   ```
   
2. ✅ Factory pattern dla vendor-specific controllers
   - AsusECControllerV2
   - GigabyteECControllerV2
   - MSIECControllerV2
   - MonitoringOnlyController (fallback)
   
3. ✅ Walidacja możliwości zapisu
   - Test write na bezpiecznym kanale
   - Rollback przy błędzie
   
4. ✅ Onboarding z ostrzeżeniem
   - Dialog z wyjaśnieniem ryzyka
   - Checkbox "Rozumiem konsekwencje"
   - Blokada do czasu potwierdzenia

**Testy:**
- [ ] Unit testy dla każdego controllera
- [ ] Mock hardware dla testów

**Kryteria akceptacji:**
- [ ] Użytkownik musi potwierdzić ryzyko przed kontrolą
- [ ] Walidacja zapisu działa poprawnie
- [ ] Fallback do Monitoring Only przy błędzie

---

### Milestone 2.2: Manualna kontrola PWM
**Czas: 4-5 dni**
**Priorytet: KRYTYCZNY**
**FR: FR-CTL-001, FR-CTL-002**

**Zadania:**
1. ✅ Implementacja AsusECControllerV2
   - P/Invoke do WinRing0
   - EC register access dla ASUS Z490-P
   - Bezpieczne minimum dla CPU_FAN (20%)
   
2. ✅ Manual Control View
   ```xaml
   - Slider dla każdego kanału (0-100%)
   - Numeric input
   - Current RPM display
   - Reset button
   - Full Speed button (emergency)
   ```
   
3. ✅ Grupowanie kanałów
   - UI do tworzenia grup
   - Synchronized control
   
4. ✅ Walidacja inputu
   - CPU_FAN >= 20%
   - Inne kanały >= 0%
   - Confirmation dla < 30% CPU_FAN

**Testy:**
- [ ] Manual testing na ASUS Z490-P
- [ ] Walidacja RPM response (<1s)
- [ ] Safety limits test

**Kryteria akceptacji:**
- [ ] Slider zmienia RPM w <1s
- [ ] CPU_FAN nie może zejść poniżej 20%
- [ ] Grupowanie działa poprawnie

---

### Milestone 2.3: Krzywe wentylatorów
**Czas: 6-8 dni**
**Priorytet: KRYTYCZNY**
**FR: FR-CUR-001, FR-CUR-002**

**Zadania:**
1. ✅ Curve Engine
   ```csharp
   public interface ICurveEngine
   {
       int CalculateSpeedForTemperature(FanCurve curve, double temp);
       bool ValidateCurve(FanCurve curve);
   }
   ```
   
2. ✅ FanCurve model
   - 4-8 punktów kontrolnych
   - Interpolacja liniowa
   - Histereza (1-10°C)
   - Smoothing
   
3. ✅ Curve Editor View
   - Wykres 2D (ScottPlot 5.0)
   - Drag & drop punktów
   - Numeric input dla precyzji
   - Live preview (aktualna temp zaznaczona)
   - Test mode
   
4. ✅ Walidacja krzywej
   - Punkty rosnące (temp i speed)
   - CPU_FAN >= 20% minimum
   - Brak punktów poza zakresem

**Testy:**
- [ ] Unit testy dla interpolacji
- [ ] Edge cases (0°C, 100°C, duplicate points)
- [ ] Performance test (calculation < 1ms)

**Kryteria akceptacji:**
- [ ] Krzywa działa zgodnie z wykresem
- [ ] Histereza zapobiega oscillation
- [ ] Walidacja blokuje niepoprawne krzywe

---

### Milestone 2.4: Profile
**Czas: 4-5 dni**
**Priorytet: WYSOKI**
**FR: FR-PROF-001**

**Zadania:**
1. ✅ Profile Service
   ```csharp
   public interface IProfileService
   {
       Task<Profile[]> GetProfilesAsync();
       Task ActivateProfileAsync(string name);
       Task SaveProfileAsync(Profile profile);
   }
   ```
   
2. ✅ Predefiniowane profile
   - **Silent:** Max 40% fan, temp do 75°C
   - **Balanced:** 20-80% fan, temp 60-70°C
   - **Performance:** 30-100% fan, temp <60°C
   - **Custom:** User-defined
   
3. ✅ Profile persistence
   - Zapis do profiles/*.json5
   - Auto-load ostatniego profilu
   
4. ✅ Profile Selector UI
   - Dropdown w dashboardzie
   - Hotkey switching (opcjonalne)
   - Visual indicator aktywnego profilu

**Testy:**
- [ ] Profile switching < 1s
- [ ] Persistence po restarcie

**Kryteria akceptacji:**
- [ ] Wszystkie 4 profile działają
- [ ] Przełączanie bez restartu
- [ ] Profil odtwarza się po restarcie

**Deliverable:** Pełna kontrola wentylatorów z profilami

---

## FAZA 3: BEZPIECZEŃSTWO I FAILSAFE (Tydzień 10-12)

### Milestone 3.1: Safety Monitor
**Czas: 5-7 dni**
**Priorytet: KRYTYCZNY**
**FR: FR-SAFE-001, FR-SAFE-002, FR-SAFE-003**

**Zadania:**
1. ✅ SafetyMonitorServiceV2
   ```csharp
   public class SafetyMonitorServiceV2
   {
       Task EnterSafeModeAsync(SafeModeReason reason);
       Task<bool> ValidateSensorHealthAsync();
       Task<HealthAttestation> GetHealthAttestationAsync();
   }
   ```
   
2. ✅ Watchdog timer
   - Sprawdzanie sensorów co 2s
   - Detekcja utraty sensora
   - Detekcja błędnych odczytów
   
3. ✅ Multi-level failsafe
   - **Normal:** Wszystko OK
   - **Caution:** Sensor suspicious
   - **Emergency:** 100% fan speed
   - **Shutdown:** Aplikacja się wyłącza, BIOS przejmuje
   
4. ✅ Temperature alerts
   - Konfigurowalne progi (CPU, GPU)
   - Toast notifications
   - Tray icon animation
   - One-click Full Speed

**Testy:**
- [ ] Symulacja utraty sensora
- [ ] Symulacja błędnych odczytów
- [ ] Recovery test

**Kryteria akceptacji:**
- [ ] Utrata sensora → Emergency w <5s
- [ ] Błędny zapis → Rollback w <2s
- [ ] Alert przy temp > threshold

---

### Milestone 3.2: Backup & Recovery
**Czas: 3-4 dni**
**Priorytet: WYSOKI**
**FR: FR-CONF-001**

**Zadania:**
1. ✅ BackupServiceV2
   - Atomic backup przed każdą zmianą
   - SHA256 checksums
   
2. ✅ RestoreManager
   ```csharp
   public interface IRestoreManager
   {
       Task<RestoreResult> RestoreLastKnownGoodAsync();
       Task<bool> ValidateBackupIntegrityAsync(string backupPath);
   }
   ```
   
3. ✅ Startup recovery flow
   - Detekcja uszkodzonej konfiguracji przy starcie
   - Automatyczny restore ostatniego poprawnego snapshotu
   - Fallback do Safe Defaults gdy restore się nie powiedzie
   
4. ✅ Versioning konfiguracji
   - Pole schemaVersion w configu
   - Migracje dla zmian formatu
   - Retencja 5 ostatnich backupów

**Testy:**
- [ ] Uszkodzony config nie blokuje startu aplikacji
- [ ] Checksum mismatch wykrywany poprawnie
- [ ] Restore po crashu w trakcie zapisu działa poprawnie

**Kryteria akceptacji:**
- [ ] Korupcja configu → aplikacja uruchamia się w trybie bezpiecznym
- [ ] Ostatnia poprawna konfiguracja odzyskuje się automatycznie
- [ ] Restore trwa < 3s dla typowej konfiguracji

---

### Milestone 3.3: Logi diagnostyczne
**Czas: 3-4 dni**
**Priorytet: WYSOKI**
**FR: FR-CONF-002**

**Zadania:**
1. ✅ Structured logging
   - Event IDs dla ważnych operacji
   - Enrichment: active profile, SupportLevel, SafeModeReason
   - Rotacja logów
   
2. ✅ Incident timeline
   - Zdarzenia: start, detekcja sprzętu, zmiana profilu, zapis kontroli, failsafe
   - Timestamp UTC + lokalny
   - Poziomy: Info, Warning, Error, Critical
   
3. ✅ Export pakietu diagnostycznego
   - Logi + config + hardware.json
   - ZIP do łatwego wysłania
   - Przygotowanie pod issue reporting
   
4. ✅ Diagnostics View
   - Podgląd ostatnich błędów
   - Przycisk "Export Support Bundle"
   - Redakcja danych wrażliwych

**Testy:**
- [ ] Log rotation działa poprawnie
- [ ] Support bundle generuje się bez błędów
- [ ] Logi nie zawierają danych osobowych poza diagnostyką systemową

**Kryteria akceptacji:**
- [ ] Wszystkie zdarzenia krytyczne są logowane
- [ ] Użytkownik może wyeksportować bundle w <30s
- [ ] Logi pomagają odtworzyć sekwencję wejścia w failsafe

**Deliverable:** Warstwa bezpieczeństwa i odzyskiwania gotowa do dalszych testów

---

## FAZA 4: UX I INTEGRACJA Z SYSTEMEM (Tydzień 13-14)

### Milestone 4.1: System Tray i autostart
**Czas: 3-4 dni**
**Priorytet: WYSOKI**
**FR: FR-UI-003**

**Zadania:**
1. ✅ TrayService
   ```csharp
   public interface ITrayService
   {
       void Show();
       void Hide();
       void UpdateStatus(TrayStatus status);
   }
   ```
   
2. ✅ Menu kontekstowe w tray
   - Show / Hide
   - Quick profile switch
   - Full Speed
   - Exit
   
3. ✅ Autostart integration
   - Task Scheduler zamiast Registry Run
   - Start minimized to tray
   - Obsługa opóźnionego startu
   
4. ✅ Powiadomienia systemowe
   - Alerty temperatur
   - Powiadomienie o wejściu w failsafe
   - Powiadomienie o konflikcie z vendor tool

**Testy:**
- [ ] Restore z tray działa zawsze
- [ ] Autostart po zalogowaniu uruchamia aplikację poprawnie
- [ ] Powiadomienia nie duplikują się i nie spamują

**Kryteria akceptacji:**
- [ ] Podstawowe akcje są dostępne bez otwierania głównego okna
- [ ] Aplikacja uruchamia się zminimalizowana, jeśli użytkownik tak ustawi
- [ ] Tray poprawnie odzwierciedla status aplikacji

---

### Milestone 4.2: Onboarding i klasyfikacja sprzętu
**Czas: 4-5 dni**
**Priorytet: WYSOKI**
**FR: FR-HW-003**

**Zadania:**
1. ✅ First Run Wizard
   - Ekran powitalny
   - Informacja o trybach Monitoring Only / Full Control
   - Wyjaśnienie wymogu uprawnień administratora
   
2. ✅ Screen klasyfikacji sprzętu
   - Lista kanałów i sensorów
   - Badge: Full Control / Monitoring Only / Unsupported
   - Link do dokumentacji kompatybilności
   
3. ✅ Consent flow dla trybu kontroli
   - Checkbox "Rozumiem konsekwencje"
   - Potwierdzenie przed pierwszym zapisem
   - Możliwość cofnięcia zgody i powrotu do Monitoring Only
   
4. ✅ Empty states i komunikaty
   - Brak wspieranego kontrolera
   - Konflikt z oprogramowaniem producenta
   - Brak uprawnień administratora

**Testy:**
- [ ] Onboarding działa dla sprzętu wspieranego i niewspieranego
- [ ] Brak zgody blokuje tryb kontroli
- [ ] Komunikaty są zrozumiałe dla nowego użytkownika

**Kryteria akceptacji:**
- [ ] Użytkownik rozumie status wsparcia sprzętu po pierwszym uruchomieniu
- [ ] Tryb kontroli nie może zostać włączony bez świadomej zgody
- [ ] Aplikacja nie zostawia użytkownika w martwym punkcie

---

### Milestone 4.3: Ustawienia aplikacji
**Czas: 3-4 dni**
**Priorytet: ŚREDNI**
**FR: FR-SAFE-002, FR-UI-003**

**Zadania:**
1. ✅ Settings View
   - Polling interval
   - Temperature thresholds
   - Dark / Light theme
   
2. ✅ Ustawienia bezpieczeństwa
   - CPU/GPU alert thresholds
   - Startup behavior
   - Domyślny profil po starcie
   
3. ✅ Persistencja ustawień
   - settings.json5
   - Walidacja zakresów
   - Reset do defaults
   
4. ✅ Live apply
   - Zmiana theme bez restartu
   - Zmiana polling interval bez restartu
   - Odświeżenie tray behavior

**Testy:**
- [ ] Zmiana ustawień zapisuje się po restarcie
- [ ] Niepoprawne wartości są blokowane
- [ ] Theme i interval stosują się bez restartu

**Kryteria akceptacji:**
- [ ] Użytkownik może skonfigurować zachowanie aplikacji bez edycji plików ręcznie
- [ ] Ustawienia nie psują istniejącej konfiguracji profili
- [ ] Reset do defaults działa poprawnie

**Deliverable:** Aplikacja gotowa do codziennego używania przez użytkownika końcowego

---

## FAZA 5: WALIDACJA, KOMPATYBILNOŚĆ I INSTALATOR (Tydzień 15-17)

### Milestone 5.1: Macierz kompatybilności sprzętu
**Czas: 5-7 dni**
**Priorytet: KRYTYCZNY**
**FR: FR-HW-001, FR-HW-002**

**Zadania:**
1. ✅ Test matrix
   - Minimum 3 konfiguracje Full Control
   - Minimum 10 konfiguracji Monitoring Only
   - Dokumentowanie wyników per motherboard / controller
   
2. ✅ Procedura walidacyjna
   - Cross-check z HWiNFO64
   - Weryfikacja RPM response
   - Weryfikacja statusów SupportLevel
   
3. ✅ Conflict testing
   - ASUS Armoury Crate
   - MSI Center
   - Gigabyte Control Center
   - Inne popularne vendor tools
   
4. ✅ Publiczna lista kompatybilności
   - supported-hardware.md
   - Lista ograniczeń
   - Znane problemy i workaroundy

**Testy:**
- [ ] Każda konfiguracja przechodzi pełną checklistę
- [ ] Brak false positive dla Full Control
- [ ] Lista kompatybilności zgadza się z wynikami testów

**Kryteria akceptacji:**
- [ ] Warunek PRD dla macierzy wsparcia jest spełniony
- [ ] Użytkownik widzi taki sam status wsparcia jak w wynikach walidacji
- [ ] Znane konflikty są opisane w dokumentacji

---

### Milestone 5.2: Performance i soak testing
**Czas: 4-5 dni**
**Priorytet: WYSOKI**
**FR: FR-MON-003, FR-SAFE-001, FR-CONF-001**

**Zadania:**
1. ✅ 24h monitoring soak test
   - Stabilność odczytów
   - Wyciek pamięci
   - Długotrwała responsywność UI
   
2. ✅ Stress test kontroli
   - Wielokrotne przełączanie profili
   - Zmiany manualne co kilka sekund
   - Symulacja zaniku sensora podczas obciążenia
   
3. ✅ Performance baselines
   - CPU usage w tle
   - RAM usage
   - Startup time
   
4. ✅ Crash diagnostics
   - Dump collection
   - Analiza deadlocków
   - Szybka klasyfikacja issue severity

**Testy:**
- [ ] 24h run bez krytycznych błędów
- [ ] CPU usage < 2% w tle
- [ ] RAM usage zgodne z budżetem

**Kryteria akceptacji:**
- [ ] Aplikacja spełnia budżety wydajności z PRD
- [ ] Brak niekontrolowanego zatrzymania wentylatorów
- [ ] Profil i konfiguracja pozostają spójne po stress teście

---

### Milestone 5.3: Instalator i dystrybucja
**Czas: 3-4 dni**
**Priorytet: WYSOKI**
**FR: NFR - dystrybucja desktopowa**

**Zadania:**
1. ✅ MSI installer
   - WiX Toolset 4.0
   - Install / Upgrade / Repair / Uninstall
   
2. ✅ Uprawnienia i skróty
   - Skróty Start Menu / Desktop
   - Obsługa uruchomienia jako administrator
   - Zachowanie danych użytkownika przy upgrade
   
3. ✅ Pipeline release artifacts
   - Build Release
   - ZIP portable
   - Checksums
   
4. ✅ Release packaging checklist
   - Licencja MIT
   - Third-party notices
   - Release notes draft

**Testy:**
- [ ] Fresh install działa poprawnie
- [ ] Upgrade zachowuje konfigurację
- [ ] Uninstall usuwa binaria i zostawia dane tylko jeśli użytkownik tak wybierze

**Kryteria akceptacji:**
- [ ] Instalacja trwa < 2 minuty
- [ ] Upgrade nie psuje configów i profili
- [ ] Artefakty release są generowane automatycznie

**Deliverable:** Kandydat do bety gotowy do dystrybucji

---

## FAZA 6: BETA I WYDANIE 1.0 (Tydzień 18-19)

### Milestone 6.1: Zamknięta beta
**Czas: 5-7 dni**
**Priorytet: KRYTYCZNY**
**FR: Walidacja release**

**Zadania:**
1. ✅ Rekrutacja testerów
   - Co najmniej 10 użytkowników
   - Mix sprzętu Full Control i Monitoring Only
   
2. ✅ Beta feedback loop
   - Template issue report
   - Wymagany support bundle
   - Triage severity P0/P1/P2
   
3. ✅ Stabilizacja
   - Naprawa bugów bezpieczeństwa
   - Naprawa regresji UX
   - Aktualizacja listy kompatybilności
   
4. ✅ Beta metrics review
   - Crash-free sessions
   - Failsafe rate
   - Najczęstsze problemy

**Testy:**
- [ ] Wszystkie bugi P0 i P1 są zamknięte lub mają zaakceptowany workaround
- [ ] Beta nie wykazuje krytycznych regresji temperatur
- [ ] Dane z bety wspierają decyzję release / no-release

**Kryteria akceptacji:**
- [ ] Crash-free sessions >= 99.5%
- [ ] Brak otwartych krytycznych problemów bezpieczeństwa
- [ ] Top 10 zgłoszeń użytkowników jest zaadresowane

---

### Milestone 6.2: Release Candidate
**Czas: 3-4 dni**
**Priorytet: WYSOKI**
**FR: Release readiness**

**Zadania:**
1. ✅ Feature freeze
   - Tylko bugfixy
   - Zamrożenie schematu configów
   
2. ✅ RC verification checklist
   - Fresh install
   - Upgrade from beta
   - Recovery from corrupted config
   - Failsafe verification
   
3. ✅ Dokumentacja wydania
   - Known issues
   - Hardware compatibility matrix
   - User guide
   
4. ✅ Wersjonowanie i changelog
   - SemVer 1.0.0-rcX
   - CHANGELOG.md
   - Tag release candidate

**Testy:**
- [ ] RC przechodzi pełną checklistę ręczną
- [ ] Brak regresji względem ostatniej bety
- [ ] Dokumentacja odpowiada realnemu stanowi produktu

**Kryteria akceptacji:**
- [ ] RC jest instalowalny i stabilny
- [ ] Lista kompatybilności i znanych ograniczeń jest gotowa do publikacji
- [ ] Zespół podejmuje świadomą decyzję Go / No-Go

---

### Milestone 6.3: Wydanie 1.0
**Czas: 1-2 dni**
**Priorytet: WYSOKI**
**FR: Public release**

**Zadania:**
1. ✅ Publikacja release
   - GitHub Release
   - MSI + ZIP + checksums
   - Finalny changelog
   
2. ✅ Publikacja dokumentacji
   - Getting Started
   - Compatibility list
   - Known limitations
   
3. ✅ Plan hotfixów
   - Kanał zgłoszeń
   - SLA dla krytycznych bugów
   - Procedura rollbacku release
   
4. ✅ Post-release monitoring
   - Pierwsze 72h obserwacji
   - Agregacja zgłoszeń
   - Decyzja o patchu 1.0.1

**Testy:**
- [ ] Publiczne artefakty są poprawne i kompletne
- [ ] Dokumentacja jest dostępna razem z release
- [ ] Procedura hotfix działa i jest opisana

**Kryteria akceptacji:**
- [ ] FanControl Pro 1.0 jest publicznie dostępny
- [ ] Użytkownik ma komplet informacji o wsparciu i ograniczeniach
- [ ] Zespół jest gotowy do szybkiej reakcji po wydaniu

**Deliverable:** FanControl Pro 1.0 wydany publicznie

---

## DODATKOWE ZADANIA - INTEGRATION TESTS (Etap 4.2 - Ukończony)

### Milestone 4.2: Integration Tests
**Czas: 3-4 dni**
**Status: ✅ UKOŃCZONY**
**Priorytet: WYSOKI**

**Zadania:**
1. ✅ Setup test framework
   - xUnit + coverlet dla code coverage
   - Microsoft.Extensions packages dla DI w testach
   - 58 testów ogółem (54 istniejące + 4 nowe integration)

2. ✅ End-to-end integration tests
   - **Cold start:** Hardware detection + default profile loading
   - **Profile switch:** Custom profile creation and activation
   - **Failsafe:** Temperature threshold simulation
   - **Recovery:** Normal operation restoration

3. ✅ Hardware simulation
   - FakeProbe dla mock ASUS hardware
   - SimulatableHardwareProbe dla dynamic scenarios
   - In-memory services dla izolacji testów

4. ✅ Test infrastructure
   - FakeManualFanControlService
   - InMemoryHardwareCacheStore
   - Proper DI container setup w testach

**Testy:**
- [x] Wszystkie 58 testów przechodzą (0 failed)
- [x] Integration tests walidują pełny application flow
- [x] Hardware simulation umożliwia automated testing
- [x] Code coverage: 51% (baseline dla dalszego rozwoju)

**Kryteria akceptacji:**
- [x] Integration tests pokrywają critical user journeys
- [x] Testy działają w CI/CD pipeline
- [x] Hardware simulation umożliwia automated testing
- [x] Wszystkie istniejące testy nadal przechodzą

**Deliverable:** Solid test foundation gotowy dla dalszego rozwoju i release

---

## PODSUMOWANIE HARMONOGRAMU

- Faza 0: Tydzień 1-2
- Faza 1: Tydzień 3-5
- Faza 2: Tydzień 6-9
- Faza 3: Tydzień 10-12
- Faza 4: Tydzień 13-14
- Faza 5: Tydzień 15-17
- Faza 6: Tydzień 18-19

**Szacowany czas całkowity:** 18-19 tygodni dla wersji 1.0

**Critical Path:**
0.1 → 0.2 → 1.1 → 1.2 → 2.1 → 2.2 → 2.3 → 3.1 → 5.1 → 5.2 → 6.1 → 6.2 → 6.3

**Najważniejsze warunki wydania 1.0:**

---

## AUDYT ZGODNOŚCI Z PLANEM (stan na 2026-03-15, aktualizacja lokalna)

Legenda statusów:
- `DONE` = wdrożone i potwierdzone lokalnym build/test lub działaniem runtime.
- `PARTIAL` = wdrożone, ale bez pełnej walidacji sprzętowej/E2E z kryteriów planu.
- `MISSING` = brak implementacji.

### Snapshot jakości

- `dotnet build` -> PASS (0 błędów, 0 ostrzeżeń)
- `dotnet test` -> PASS (**110/110**)
- UI uruchamia się i działa (onboarding + dashboard, poprawiony theme/kontrast/scroll)
- status projektu: **RC (~90-92%)**, gotowy do finalnego domknięcia hardware validation

### Faza 0

| Milestone | Status | Komentarz |
|-----------|--------|-----------|
| 0.1 Środowisko deweloperskie | DONE | Repo i workflow CI działają, projekt buduje się lokalnie. |
| 0.2 Architektura projektu | DONE | Clean Architecture, DI i logging są wdrożone i używane. |

### Faza 1

| Milestone | Status | Komentarz |
|-----------|--------|-----------|
| 1.1 Wykrywanie sprzętu | PARTIAL | Detekcja + klasyfikacja + cache działają; brak pełnego, udokumentowanego cross-checku z HWiNFO na macierzy docelowej. |
| 1.2 Odczyt sensorów | PARTIAL | Monitoring loop i sanity checks są; brak formalnych runów 24h i finalnych metryk wydajnościowych. |
| 1.3 Dashboard | PARTIAL | Dashboard działa stabilnie; są testy ViewModel (onboarding + dashboard), ale nadal brak formalnych testów UI dla różnych rozdzielczości/scenariuszy E2E. |

### Faza 2

| Milestone | Status | Komentarz |
|-----------|--------|-----------|
| 2.1 Architektura kontroli | DONE | `IFanControllerV2`, factory, consent flow i fallback monitoring-only działają. |
| 2.2 Manualna kontrola PWM | PARTIAL | Implementacja `WinRing0EcRegisterAccess` + `LibreHardwareMonitorSuperIoFanControlAccess` istnieje, ale produkcyjnie write-path jest nadal gated konfiguracją (`EnableHardwareAccess=false`). |
| 2.3 Krzywe wentylatorów | PARTIAL | Curve engine/editor/preview są; brak formalnego benchmarku i pełnej walidacji hardware-in-the-loop. |
| 2.4 Profile | DONE | Profile service, persistence i aktywacja profili działają i są przetestowane. |

### Faza 3

| Milestone | Status | Komentarz |
|-----------|--------|-----------|
| 3.1 Safety Monitor | PARTIAL | Watchdog/failsafe/alerting są, ale brak pełnej walidacji operacyjnej na docelowym sprzęcie i pełnego zestawu UX testów alarmów. |
| 3.2 Backup & Recovery | DONE | Backup/restore/checksum/startup recovery działają i są pokryte testami. |
| 3.3 Logi diagnostyczne | PARTIAL | Logi + support bundle + diagnostyka są; do domknięcia pełne E2E scenariusze incydentowe i finalna polityka redakcji danych. |

### Faza 4

| Milestone | Status | Komentarz |
|-----------|--------|-----------|
| 4.1 System Tray i autostart | DONE | Tray/autostart + parser startup argumentów są pokryte testami i checklistą (`docs/qa/phase4-ux-system-integration-checklist.md`) oraz skryptem walidacyjnym (`scripts/qa/Validate-Phase4Readiness.ps1`). |
| 4.2 Onboarding i klasyfikacja sprzętu | DONE | Wizard/consent/empty-states działają i mają rozszerzone testy ViewModel dla gatingu ryzyka, konfliktów vendor i scenariuszy Monitoring Only. |
| 4.3 Ustawienia aplikacji | DONE | Settings UI + persistence + live apply działają; testy obejmują zapis/walidację/reset oraz mapowanie motywu `System` do trybu systemowego. |

### Faza 5

| Milestone | Status | Komentarz |
|-----------|--------|-----------|
| 5.1 Macierz kompatybilności | PARTIAL | Checklisty i artefakty QA są; dodano automatyczny readiness script `scripts/qa/Validate-Phase5Readiness.ps1`, ale brak kompletu potwierdzonych runów `Validated` na docelowej macierzy hardware. |
| 5.2 Performance i soak testing | PARTIAL | Playbooki/skrypty/baselines są; readiness script obejmuje testy integration/stress i kontrolę baseline, ale brak finalnych runów 24h z kompletnym raportem metryk. |
| 5.3 Instalator i dystrybucja | DONE | WiX + pipeline release artifacts + walidacja lifecycle/upgrade MSI są wdrożone. |

### Faza 6

| Milestone | Status | Komentarz |
|-----------|--------|-----------|
| 6.1 Zamknięta beta | PARTIAL | Narzędzia i proces bety są; brak pełnego przebiegu z realnymi metrykami i triage zamykającym pętlę. |
| 6.2 Release Candidate | PARTIAL | Checklisty/polityki/release docs są; brakuje formalnego final Go/No-Go po walidacji sprzętowej P1. |
| 6.3 Wydanie 1.0 | DONE | Publiczny release + artefakty + dokumentacja istnieją; release jest konserwatywny (hardware write domyślnie wyłączony). |

### Dodatkowe zadania: Integration Tests

| Milestone | Status | Komentarz |
|-----------|--------|-----------|
| 4.2 Integration Tests (sekcja dodatkowa) | DONE | Suite testowa działa; obecnie **110 testów** przechodzi lokalnie. |

### Podsumowanie audytu

- Największa luka nie jest już „brak implementacji”, tylko **domknięcie walidacji produkcyjnej**.
- Krytyczny blocker do pełnego production-ready: hardware-in-the-loop na ASUS Z490-P + finalna decyzja dla `EnableHardwareAccess`.
- `AsusPwmRegisters` w konfiguracji nadal wymagają finalnej walidacji (placeholdery).
- Brak otwartych sygnałów o krytycznym regresie build/test.
- Aplikacja jest stabilna na poziomie RC i gotowa do ostatniej prostej przed pełnym domknięciem.
