# Czyścik — Pro (mini-app)

Krótko: prosta aplikacja WPF do czyszczenia wybranych ścieżek, z `Turbo Cleaner`, logiem i licznikiem odzyskanych bajtów.

Jak zbudować:

1. Otwórz `Czyscik.sln` lub projekt `Czyscik/Czyscik.csproj` w Visual Studio 2022/2023 z zainstalowanym workloadem .NET Desktop.
2. Zbuduj w konfiguracji `Release`.

Instalator MSI (szybka instrukcja):

1. Zbuduj projekt w trybie Release.
2. Zainstaluj rozszerzenie "Visual Studio Installer Projects".
3. Dodaj nowy projekt typu `Setup Project`, wskaż plik EXE z `Czyscik\bin\Release\net6.0-windows`.
4. Zbuduj projekt instalatora otrzymasz plik `.msi`.

Uwagi:
- Aplikacja działa na ścieżkach wybranych przez użytkownika i domyślnych presetach.
- Log zapisuje się w `%LOCALAPPDATA%\\Czyscik\\czyscik.log`.
- Opróżnianie kosza używa systemowego wywołania; z powodu uprawnień rozmiar opróżnionych plików może być oznaczony jako n/d w podsumowaniu.

 Jeśli chcesz, mogę:
 - dodać GUI do wyboru ścieżek przez eksplorator (folder picker) — zrobione.
 - dodać szczegółowy widok logu w oknie aplikacji — zrobione.
 - dodać możliwość podglądu listy plików i potwierdzenia przed usunięciem — zrobione.

 Pliki dodane:
 - `FilesPreviewWindow.xaml` / `FilesPreviewWindow.xaml.cs` — okno pokazujące listę plików dla wybranego katalogu i pozwalające usunąć je pojedynczo.
 - `Czyscik.sln` — proste rozwiązanie Visual Studio.

 Jak testować lokalnie:

 1. Otwórz `Czyscik/Czyscik.sln` w Visual Studio.
 2. Upewnij się, że projekt `Czyscik` ma ustawione `UseWindowsForms` (już skonfigurowane) i zbuduj.
 3. Uruchom aplikację; wybierz folder, użyj `Preview` by zobaczyć co zostanie usunięte, lub `Pokaż pliki` dla pojedynczego katalogu.
