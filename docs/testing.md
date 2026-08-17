# Testowanie

## Zakres

Testy projektu są podzielone na testy backendu, testy interfejsu, walidację buildów oraz test manualny uruchomionych kontenerów.

| Warstwa | Zakres | Komenda |
| --- | --- | --- |
| Backend | Klient AI, redakcja sekretów, autoryzacja, endpointy i Swagger | `dotnet test .\backend.Tests\CommitGenerator.Tests.csproj` |
| Frontend | Wysłanie klucza i diffu, prezentacja wyniku, obsługa `401` | `npm test` |
| Build frontend | Kompilacja produkcyjnego bundle'a | `npm run build` |
| Zależności | Audyt podatności wysokiego poziomu | `npm audit --audit-level=high` |
| Kontenery | Budowa obrazów Docker Compose | `docker compose build` |

Automatyczne testy nie wykonują requestów do OpenAI ani Gemini. Odpowiedzi providera są zastępowane mockami, dzięki czemu testy są szybkie, powtarzalne i bezkosztowe.

## Testy backendu

Uruchom z katalogu głównego repozytorium:

```powershell
dotnet test .\backend.Tests\CommitGenerator.Tests.csproj
```

Testy obejmują:

- mapowanie odpowiedzi OpenAI-compatible API;
- błędy i niepoprawny JSON providera;
- redakcję typowych sekretów;
- `GET /health`;
- odrzucenie pustego diffu;
- brak klucza dostępu;
- dokumentację Swagger.

## Testy frontendu

```powershell
Set-Location .\frontend
npm ci
npm test
npm run build
npm audit --audit-level=high
```

Testy UI nie wymagają uruchomionego backendu. `fetch` jest mockowany.

## Test manualny Docker Compose

Uruchomienie:

```powershell
docker compose up --build -d
docker compose ps
```

Oczekiwane kontenery:

```text
ai-git-helper-backend-1    Up
ai-git-helper-frontend-1   Up
```

Sprawdzenie health checków:

```powershell
curl.exe http://localhost:8080/health
curl.exe http://localhost:3000/health
```

Oczekiwana odpowiedź:

```json
{"status":"ok"}
```

Test autoryzacji bez klucza:

```powershell
curl.exe -i `
  -X POST http://localhost:8080/api/generate-commit `
  -H "Content-Type: application/json" `
  --data-raw '{"gitDiff":"diff"}'
```

Oczekiwany status: `401`.

Test pustego diffu z poprawnym kluczem:

```powershell
$headers = @{ "X-API-Key" = "wartosc-APP_API_KEY" }
$body = @{ gitDiff = "" } | ConvertTo-Json

try {
    Invoke-RestMethod `
      -Uri http://localhost:8080/api/generate-commit `
      -Method Post `
      -Headers $headers `
      -ContentType "application/json" `
      -Body $body
} catch {
    $_.Exception.Response.StatusCode.value__
}
```

Oczekiwany status: `400`.

Test funkcjonalny w przeglądarce:

1. Otwórz `http://localhost:3000`.
2. Wpisz `APP_API_KEY`.
3. Wklej rzeczywisty diff.
4. Kliknij `Generuj commit i opis PR`.
5. Sprawdź wygenerowany commit message i opis PR.

Logi backendu:

```powershell
docker compose logs -f backend
```

Zatrzymanie kontenerów:

```powershell
docker compose down
```

## Workflow w projekcie docelowym

AI Git Helper analizuje diff projektu docelowego, ale nie uruchamia jego testów.

```powershell
Set-Location C:\sciezka\do\projektu-docelowego
git status
git add .
git diff --cached --no-color | Set-Clipboard
```

Wklej zawartość schowka do pola `Git diff`, wygeneruj wynik, a następnie uruchom testy projektu docelowego. Wyniki testów nie są wklejane do pola `Git diff`.

Po pozytywnych testach użyj wygenerowanego commit message:

```powershell
git commit -m "wygenerowana-tresc-commita"
```

Opis Pull Requesta skopiuj do formularza Pull Requesta na GitHubie. Sekcję `Testing` uzupełnij rzeczywistymi komendami:

```markdown
## Testing

- `dotnet test` - passed
- `npm test` - passed
- `npm run build` - passed
```

## CI

Workflow `.github/workflows/ci.yml` uruchamia na GitHubie build backendu, testy .NET, testy i build frontendu, audit zależności oraz build obrazów Docker. Nie wymaga klucza AI.

## Dalsze rozszerzenia

Nie są wymagane dla obecnego zakresu. Przy rozwoju aplikacji można dodać:

- testy E2E z Playwright;
- raport pokrycia kodu;
- test z lokalnym serwerem mockującym providera;
- testy wydajnościowe limitera;
- testy kontraktowe z rzeczywistym środowiskiem stagingowym.
