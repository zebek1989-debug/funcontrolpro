# Product Requirements Document (PRD)
## FanControl Pro for Windows

## 1. Informacje o dokumencie

- Produkt: FanControl Pro
- Wersja dokumentu: 2.0
- Data: 15 marca 2026
- Status: Draft do walidacji
- Właściciel produktu: [Twoje imię / organizacja]
- Typ produktu: aplikacja desktopowa Windows
- Model dystrybucji: open source, MIT

## 2. Streszczenie

FanControl Pro ma umożliwić bezpieczne monitorowanie temperatur i sterowanie wentylatorami na komputerach z Windows, z naciskiem na przewidywalność działania i ochronę sprzętu. Główna wartość produktu to obniżenie hałasu przy niskim obciążeniu oraz utrzymanie bezpiecznych temperatur pod obciążeniem bez zmuszania użytkownika do pracy w BIOS-ie lub korzystania z wielu narzędzi vendorowych.

Kluczowe założenie produktu brzmi: aplikacja ma być użyteczna tylko wtedy, gdy pozostaje bezpieczna. Jeżeli sprzęt lub sensory nie są wystarczająco wiarygodne do sterowania, produkt ma przejść w tryb monitoringu zamiast podejmować ryzykowne działania.

## 3. Problem

Użytkownicy Windows PC mają dziś trzy główne problemy:

- domyślne profile wentylatorów są zbyt głośne albo zbyt agresywne,
- sterowanie w BIOS-ie jest niewygodne i słabo iterowalne,
- narzędzia producentów płyt głównych bywają ciężkie, niespójne lub ograniczone do jednego ekosystemu.

## 4. Cele produktu

- Umożliwić odczyt temperatur, RPM i obciążenia w jednym miejscu.
- Umożliwić ręczne sterowanie obsługiwanymi kanałami wentylatorów.
- Umożliwić tworzenie bezpiecznych krzywych wentylatorów dla obsługiwanego sprzętu.
- Zmniejszyć hałas komputera bez zwiększania ryzyka przegrzania.
- Jasno komunikować, jaki sprzęt jest wspierany w trybie monitoringu, a jaki w trybie pełnej kontroli.

## 5. Cele nieproduktowe

- Nie budujemy narzędzia do overclockingu CPU/GPU.
- Nie budujemy centrum zarządzania RGB.
- Nie obiecujemy uniwersalnej kontroli dla wszystkich płyt głównych w wersji 1.0.
- Nie zastępujemy BIOS-u ani firmware i nie zapisujemy ustawień bezpośrednio do firmware jako wymogu MVP.

## 6. Użytkownicy docelowi

### 6.1 Główni użytkownicy

- Entuzjaści PC i gracze, którzy chcą lepszego balansu hałas/temperatura.
- Użytkownicy domowi, którzy chcą cichszego komputera bez głębokiej wiedzy technicznej.

### 6.2 Użytkownicy wtórni

- Power userzy i testerzy sprzętu, którzy chcą szybko porównywać profile chłodzenia.
- Autorzy buildów i serwisanci, którzy potrzebują prostego narzędzia diagnostycznego.

## 7. Zakres wersji 1.0

### 7.1 MVP

Do MVP wchodzą tylko funkcje, które da się wdrożyć i zweryfikować bezpiecznie:

- wykrywanie sensorów i kanałów wentylatorów,
- monitoring temperatur, RPM i podstawowego obciążenia,
- ręczna kontrola obsługiwanych kanałów PWM/DC,
- krzywe wentylatorów oparte o jeden wybrany sensor na kanał lub grupę,
- profile: Silent, Balanced, Performance, Custom,
- zapis ustawień lokalnie,
- minimalizacja do tray,
- autostart po zalogowaniu,
- alarmy i mechanizmy failsafe,
- jasna macierz wsparcia sprzętu: Full Control, Monitoring Only, Unsupported.

### 7.2 Poza MVP, ale w backlogu

- automatyczne przełączanie profili na podstawie uruchomionej aplikacji,
- import i eksport profili,
- zaawansowane wykresy historyczne z długą retencją,
- udostępnianie profili społeczności,
- rozbudowany theme engine,
- wsparcie dla wielu producentów poza listą walidowaną na start.

## 8. Założenia i ograniczenia techniczne

- System operacyjny: Windows 10 i Windows 11 x64.
- Uprawnienia administratora są wymagane do trybu pełnej kontroli.
- Na niewspieranym lub częściowo wspieranym sprzęcie aplikacja działa w trybie monitoringu.
- Sterowanie kanałem jest możliwe wyłącznie po pozytywnej walidacji, że zapis nie zagraża stabilności platformy.
- Konflikt z narzędziami vendorowymi jest możliwy; aplikacja musi to komunikować użytkownikowi.
- W MVP wspieramy tylko sprzęt jawnie przetestowany i wpisany do listy kompatybilności.
- W przypadku utraty sensora, błędu odczytu lub błędu zapisu aplikacja musi przejść do bezpiecznego zachowania.

## 9. Wymagania funkcjonalne

### 9.1 Wykrywanie sprzętu i klasyfikacja wsparcia

#### FR-HW-001

System musi wykrywać dostępne sensory temperatur, kanały wentylatorów i ich typ.

Kryteria akceptacji:

- aplikacja pokazuje listę wykrytych sensorów i kanałów przy starcie,
- każdy kanał ma status: Full Control, Monitoring Only albo Unsupported,
- użytkownik widzi źródło danych lub powód braku wsparcia.

#### FR-HW-002

System musi rozróżniać kanały sterowalne od tylko monitorowanych.

Kryteria akceptacji:

- kanały bez walidacji zapisu nie pokazują aktywnego suwaka sterowania,
- UI wyjaśnia, dlaczego dany kanał nie może być sterowany,
- stan klasyfikacji jest zapisany w logach diagnostycznych.

#### FR-HW-003

Przed włączeniem trybu zapisu aplikacja musi uzyskać świadomą zgodę użytkownika.

Kryteria akceptacji:

- przy pierwszym wejściu w tryb kontroli pojawia się ekran ostrzegawczy,
- użytkownik musi potwierdzić zrozumienie ryzyka,
- do czasu potwierdzenia aplikacja pozostaje w trybie monitoringu.

### 9.2 Monitoring

#### FR-MON-001

System musi odczytywać temperatury CPU, GPU, płyty głównej i dysków, jeśli są dostępne.

Kryteria akceptacji:

- odświeżanie jest konfigurowalne w zakresie 1-5 sekund,
- wartości są spójne z zewnętrznymi narzędziami referencyjnymi w dopuszczalnym odchyleniu,
- interfejs pokazuje wartość bieżącą, minimum i maksimum dla bieżącej sesji.

#### FR-MON-002

System musi odczytywać RPM oraz aktualny procent sterowania dla każdego wykrytego wentylatora, jeśli sprzęt to udostępnia.

Kryteria akceptacji:

- wszystkie aktywne wentylatory są widoczne na dashboardzie,
- brak odczytu jest oznaczony jawnie, a nie jako wartość zero,
- zmiana RPM po sterowaniu jest widoczna bez restartu aplikacji.

#### FR-MON-003

System musi pokazywać podstawowe wskaźniki obciążenia systemu wpływające na chłodzenie.

Kryteria akceptacji:

- UI pokazuje co najmniej CPU load, GPU load i użycie RAM,
- odczyty nie powodują zauważalnego obciążenia ponad budżet wydajności,
- dane są aktualizowane w tym samym cyklu co monitoring temperatur.

### 9.3 Ręczne sterowanie

#### FR-CTL-001

Użytkownik musi móc ręcznie ustawić prędkość obsługiwanego kanału w procentach.

Kryteria akceptacji:

- zmiana wartości jest stosowana w czasie poniżej 1 sekundy,
- kanał CPU_FAN nie może zejść poniżej skonfigurowanego bezpiecznego minimum,
- dostępne są akcje Reset i Full Speed.

#### FR-CTL-002

System musi wspierać grupowanie kanałów na potrzeby wspólnego sterowania.

Kryteria akceptacji:

- użytkownik może przypisać wiele kanałów do jednej grupy,
- grupa może być sterowana jednym suwakiem lub jedną krzywą,
- kanał może należeć tylko do jednej aktywnej grupy jednocześnie.

### 9.4 Krzywe i profile

#### FR-CUR-001

Użytkownik musi móc utworzyć krzywą wentylatora opartą o wybrany sensor.

Kryteria akceptacji:

- krzywa ma minimum 4 i maksimum 8 punktów kontrolnych,
- oś X reprezentuje temperaturę, a oś Y procent sterowania,
- użytkownik może ustawić histerezę i wygładzanie zmian.

#### FR-CUR-002

System musi walidować krzywe przed zapisaniem.

Kryteria akceptacji:

- punkty krzywej nie mogą tworzyć wartości ujemnych ani malejących temperatur,
- dla CPU_FAN krzywa nie może zejść poniżej bezpiecznego minimum,
- przy zapisaniu niepoprawnej konfiguracji użytkownik dostaje czytelny komunikat błędu.

#### FR-PROF-001

System musi udostępniać profile Silent, Balanced, Performance i Custom.

Kryteria akceptacji:

- przełączenie profilu działa bez restartu aplikacji,
- profil aktywny jest jasno oznaczony na dashboardzie i w tray,
- po restarcie aplikacja przywraca ostatni poprawnie zapisany profil.

### 9.5 Bezpieczeństwo i failsafe

#### FR-SAFE-001

Aplikacja musi przechodzić w stan bezpieczny przy błędzie krytycznym.

Stan bezpieczny oznacza:

- powrót do ostatniej poprawnej konfiguracji albo
- wymuszenie wysokiej prędkości na sterowanych kanałach, jeśli poprzednia konfiguracja jest niedostępna.

Kryteria akceptacji:

- utrata sensora używanego przez aktywną krzywą wywołuje stan bezpieczny,
- nieudany zapis na kontrolerze wywołuje stan bezpieczny,
- przejście w stan bezpieczny jest logowane i widoczne dla użytkownika.

#### FR-SAFE-002

System musi ostrzegać użytkownika o temperaturach krytycznych.

Kryteria akceptacji:

- progi alarmowe są konfigurowalne dla CPU i GPU,
- alarm wyświetla powiadomienie systemowe i stan w UI,
- użytkownik może wywołać akcję Full Speed jednym kliknięciem.

#### FR-SAFE-003

Aplikacja musi wykrywać konflikt z innym aktywnym narzędziem do sterowania wentylatorami.

Kryteria akceptacji:

- użytkownik dostaje ostrzeżenie o możliwym konflikcie,
- aplikacja rekomenduje przejście w Monitoring Only,
- zdarzenie jest zapisywane do logów.

### 9.6 Interfejs użytkownika

#### FR-UI-001

System musi mieć dashboard z najważniejszymi informacjami i skrótami akcji.

Kryteria akceptacji:

- dashboard pokazuje temperatury, RPM, aktywny profil i status kontroli,
- główne akcje są dostępne bez przechodzenia przez wiele ekranów,
- układ działa poprawnie od 1280x720 w górę.

#### FR-UI-002

System musi zapewniać edytor krzywej z wizualnym podglądem.

Kryteria akceptacji:

- użytkownik może przeciągać punkty lub wpisywać wartości ręcznie,
- aktualna temperatura i wynik krzywej są zaznaczone na wykresie,
- zmiany można przetestować i anulować przed zapisaniem.

#### FR-UI-003

System musi działać w tray i umożliwiać podstawowe akcje bez otwierania głównego okna.

Kryteria akceptacji:

- tray pokazuje status aplikacji,
- menu tray umożliwia Show, Hide, zmianę profilu i Exit,
- użytkownik może uruchomić aplikację z autostartem zminimalizowaną do tray.

### 9.7 Konfiguracja, logi i odzyskiwanie

#### FR-CONF-001

Aplikacja musi zapisywać konfigurację lokalnie i posiadać mechanizm odzyskania ostatniej poprawnej konfiguracji.

Kryteria akceptacji:

- konfiguracja jest odtwarzana po restarcie aplikacji,
- uszkodzony plik konfiguracji nie blokuje uruchomienia aplikacji,
- przy uszkodzonej konfiguracji system wraca do ustawień bezpiecznych i informuje użytkownika.

#### FR-CONF-002

Aplikacja musi prowadzić logi diagnostyczne.

Kryteria akceptacji:

- log zawiera start aplikacji, wykrycie sprzętu, zmianę profilu, wejście w failsafe i błędy,
- log może zostać skopiowany lub wyeksportowany przez użytkownika,
- logi nie zawierają danych osobowych poza podstawą diagnostyczną systemu.

## 10. Wymagania niefunkcjonalne

### 10.1 Wydajność

- Zużycie CPU w tle: do 2 procent przy domyślnym interwale odświeżania.
- Zużycie RAM: do 150 MB w typowej sesji.
- Czas startu aplikacji: do 3 sekund bez trybu kontroli i do 5 sekund z walidacją sprzętu.
- Czas reakcji UI na akcję użytkownika: do 100 ms dla standardowych interakcji.

### 10.2 Niezawodność

- Aplikacja nie może doprowadzić do wyłączenia CPU_FAN przez pojedynczy błąd UI.
- Awaria pojedynczego sensora nie może powodować crasha całej aplikacji.
- Crash-free sessions w becie: minimum 99,5 procent.

### 10.3 Bezpieczeństwo

- Tryb zapisu wymaga uprawnień administratora.
- Aplikacja nie wysyła telemetrii w wersji 1.0 bez jawnej zgody użytkownika.
- Konfiguracja i logi są przechowywane lokalnie.

### 10.4 Użyteczność

- Pierwsze uruchomienie musi zawierać prosty onboarding i klasyfikację sprzętu.
- Status wsparcia sprzętu musi być zrozumiały także dla nietechnicznego użytkownika.
- Ostrzeżenia bezpieczeństwa muszą używać prostego języka i jasnych konsekwencji.

## 11. Macierz wsparcia sprzętu dla wersji 1.0

Wersja 1.0 nie komunikuje "wspieramy wszystko". Komunikuje "wspieramy tylko to, co przetestowaliśmy".

- Full Control: płyty główne i kontrolery z walidowanym odczytem oraz zapisem.
- Monitoring Only: sprzęt z wiarygodnym odczytem, ale bez bezpiecznej ścieżki zapisu.
- Unsupported: sprzęt bez wiarygodnego odczytu lub z nieakceptowalnym ryzykiem zapisu.

Warunek wydania 1.0:

- minimum 3 jawnie przetestowane konfiguracje sprzętowe w trybie Full Control,
- minimum 10 konfiguracji zweryfikowanych w trybie Monitoring Only,
- publiczna lista kompatybilności w dokumentacji.

## 12. Metryki sukcesu

- co najmniej 90 procent testowanych instalacji poprawnie klasyfikuje sprzęt przy pierwszym uruchomieniu,
- mniej niż 2 procent sesji kończy się wejściem w failsafe z winy aplikacji,
- co najmniej 80 procent użytkowników beta ocenia onboarding jako zrozumiały,
- czas zastosowania ręcznej zmiany prędkości wynosi poniżej 1 sekundy na wspieranym sprzęcie.

## 13. Ryzyka i działania ograniczające

- Ryzyko: różnice między producentami płyt głównych i kontrolerami są większe niż zakładano.
  Działanie: ograniczenie 1.0 do listy walidowanych konfiguracji.

- Ryzyko: konflikt z oprogramowaniem producenta lub ustawieniami BIOS.
  Działanie: wykrywanie konfliktów, ostrzeżenia i tryb Monitoring Only.

- Ryzyko: sensory zwracają błędne lub opóźnione dane.
  Działanie: walidacja odczytów, histereza, wygładzanie i failsafe.

- Ryzyko: użytkownik nie rozumie ryzyka zmiany CPU_FAN.
  Działanie: onboarding, progi minimalne i brak możliwości zejścia poniżej bezpiecznych limitów.

## 14. Kryteria gotowości do wydania

- wszystkie wymagania z sekcji bezpieczeństwa mają przejście testów ręcznych i automatycznych tam, gdzie to możliwe,
- istnieje działająca ścieżka odzyskania po uszkodzonej konfiguracji,
- lista kompatybilności jest opublikowana,
- znane ograniczenia są opisane w dokumentacji,
- beta na walidowanym sprzęcie nie wykazuje regresji krytycznych dotyczących temperatur lub sterowania.

## 15. Poza zakresem wersji 1.0

- Linux i macOS
- RGB i oświetlenie
- overclocking CPU i GPU
- chmura, konta użytkowników i synchronizacja ustawień
- marketplace lub społecznościowe repozytorium profili
- system pluginów
- integracja z zewnętrznymi kontrolerami wodnymi klasy enterprise
