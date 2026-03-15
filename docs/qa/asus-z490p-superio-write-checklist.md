# ASUS Z490-P Super I/O Write Checklist (P1)

## Cel

Bezpieczne uruchomienie rzeczywistej ścieżki zapisu Super I/O (Nuvoton NCT6798D) dla ASUS Z490-P bez ryzyka niekontrolowanego zatrzymania wentylatorów.

## Zasady bezpieczeństwa

- Najpierw walidacja mapy rejestrów, dopiero potem `EnableHardwareAccess=true`.
- Nie uruchamiaj równolegle narzędzi vendorowych (Armoury Crate, AI Suite, FanXpert).
- Test wykonuj z podłączonym monitoringiem temperatur i z widocznym przyciskiem `Emergency Full Speed`.

## Konfiguracja

Plik: `src/FanControlPro.Presentation/appsettings.json`  
Sekcja: `EcWriteSafety`

### Krok 1: tryb przygotowawczy (bez zapisu)

1. Zostaw `EnableHardwareAccess=false`.
2. Uzupełnij `AsusControlSensors` o tokeny mapowania kanałów na sensory kontrolne:
   - `cpu_fan`
   - `system_fan`
   - `rear_fan`
   Przykład dla Z490-P: `CPU`, `CHA1`, `CHA2`.
3. `AsusPwmRegisters` zostaw jako placeholder (`-1`) dopóki nie włączasz fallbacku EC.
4. Uruchom aplikację i sprawdź, że dashboard działa stabilnie.

### Krok 2: pierwszy bezpieczny write test

1. Ustaw:
   - `EnableHardwareAccess=true`
   - `MinimumWriteIntervalMs=350`
   - `VerifyReadBack=true`
   - `ReadBackTolerancePercent=4`
2. Ustaw ręcznie:
   - `cpu_fan` na 35%
   - `system_fan` na 45%
   - `rear_fan` na 45%
3. Po każdej zmianie odczekaj 10-15 sekund i potwierdź:
   - brak błędów `HardwareError`,
   - RPM reaguje i stabilizuje się,
   - brak nagłych skoków temperatury.

### Krok 3: test graniczny

1. Sprawdź `cpu_fan=30%` i `cpu_fan=25%` (z potwierdzeniem).
2. Wykonaj serię 5 zmian co 2-3 sekundy na `system_fan`.
3. Potwierdź, że cooldown ogranicza zbyt szybkie zapisy i nie ma utraty kontroli.

### Krok 4: rollback i fail-safe

1. Użyj `Reset` dla każdego kanału.
2. Uruchom `Emergency Full Speed`.
3. Zweryfikuj powrót RPM do wartości wysokich i brak zawieszenia aplikacji.

## Kryteria zaliczenia

- Każdy kanał odpowiada na zapis <1 s od operacji UI.
- Brak niekontrolowanego stopu wentylatorów.
- Brak trwałych `HardwareError` w normalnej pracy.
- Przejście przez `Reset` i `Emergency Full Speed` kończy się powodzeniem.

## Notatki walidacyjne

Po teście uzupełnij:
- `docs/qa/hardware-matrix.csv` (kolumny validation status + notatki),
- `supported-hardware.md` dla ASUS Z490-P (status z `Planned` -> `Validated` po pełnym runie).
