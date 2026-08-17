# AI Git Helper

Aplikacja webowa do generowania `commit message` oraz opisu Pull Request na podstawie `git diff`.

## Spis treści

- [Architektura](#architektura)
- [Szybki start](#szybki-start)
- [Konfiguracja](#konfiguracja)
- [API](#api)
- [Uruchomienie lokalne](#uruchomienie-lokalne)
- [Testowanie](#testowanie)
- [Bezpieczeństwo](#bezpieczeństwo)
- [Wdrożenie produkcyjne](#wdrożenie-produkcyjne)
- [Troubleshooting](#troubleshooting)

## Architektura

```text
Browser
  |
  v
Frontend: React + Vite + Tailwind, Nginx :3000
  |  /api/* przez reverse proxy
  v
Backend: .NET 8 Minimal API :8080
  |
  v
OpenAI-compatible API: OpenAI albo Gemini
```

Backend przechowuje klucz dostawcy AI wyłącznie po swojej stronie. Frontend otrzymuje tylko wygenerowany wynik. Przed zbudowaniem promptu backend redaguje część typowych sekretów z diffu.

## Szybki start

Wymagane są Docker Desktop oraz Docker Compose.

1. Utwórz plik `.env` na podstawie `.env.example`.
2. Ustaw prawdziwy `OPENAI_API_KEY` oraz własny `APP_API_KEY` o długości minimum 32 znaków.
3. Uruchom usługi:

```powershell
Copy-Item .env.example .env
notepad .env
docker compose up --build
```

4. Otwórz `http://localhost:3000`.
5. Wpisz w aplikacji wartość `APP_API_KEY` i wklej git diff.

Adresy:

- Frontend: `http://localhost:3000`
- Backend: `http://localhost:8080`
- Health check: `http://localhost:8080/health`
- Swagger UI: `http://localhost:8080/swagger` tylko gdy `ENABLE_SWAGGER=true` albo backend działa w środowisku Development

Karta przeglądarki pokazuje nazwę `AI Git Helper` i własny favicon. Adres `localhost:3000` pozostaje adresem lokalnego hosta. Jeśli chcesz używać lokalnej nazwy `http://ai-git-helper.local:3000`, dodaj jako administrator do pliku `C:\Windows\System32\drivers\etc\hosts` wpis `127.0.0.1 ai-git-helper.local`, a następnie otwórz ten adres.

Zatrzymanie usług:

```powershell
docker compose down
```

## Konfiguracja

| Zmienna | Wymagana | Domyślna | Opis |
| --- | --- | --- | --- |
| `OPENAI_API_KEY` | tak | brak | Klucz OpenAI albo Gemini. Nie umieszczaj go w frontendzie. |
| `OPENAI_BASE_URL` | nie | OpenAI Chat Completions | Endpoint OpenAI-compatible. |
| `OPENAI_MODEL` | nie | `gpt-4o-mini` | Nazwa modelu wybranego dostawcy. |
| `APP_API_KEY` | tak* | brak | Pojedynczy klucz dostępu do aplikacji, minimum 32 znaki. |
| `APP_API_KEYS` | tak* | pusta | Wiele kluczy w formacie `name:key;name2:key2`. |
| `FRONTEND_ORIGINS` | nie | `http://localhost:3000,http://localhost:5173` | Dozwolone originy CORS, rozdzielone przecinkami. |
| `ENABLE_SWAGGER` | nie | `false` | Włącza Swagger także poza Development. |

`APP_API_KEY` lub `APP_API_KEYS` musi być ustawione. Można użyć jednego albo wielu kluczy. Przykład wielu użytkowników:

```env
APP_API_KEYS=alice:alice-key-0000000000000000000000000000;bob:bob-key-1111111111111111111111111111
```

Dla Gemini ustaw przykładowo:

```env
OPENAI_API_KEY=your-gemini-api-key
OPENAI_BASE_URL=https://generativelanguage.googleapis.com/v1beta/openai/chat/completions
OPENAI_MODEL=gemini-2.0-flash
```

## API

### `GET /health`

Publiczny endpoint liveness. Nie wymaga `X-API-Key`.

Przykładowa odpowiedź:

```json
{ "status": "ok" }
```

### `POST /api/generate-commit`

Wymaga nagłówka:

```text
X-API-Key: <APP_API_KEY>
```

Request:

```json
{
  "gitDiff": "diff --git a/file.txt b/file.txt\n..."
}
```

Response `200`:

```json
{
  "commitMessage": "feat: add commit generator",
  "pullRequestDescription": "### Summary\n- Added ...\n\n### Testing\n- dotnet test"
}
```

Najważniejsze kody odpowiedzi:

| Kod | Znaczenie |
| --- | --- |
| `200` | Wynik został wygenerowany. |
| `400` | Pusty albo zbyt duży diff. Limit wynosi 200 000 znaków. |
| `401` | Brak lub niepoprawny `X-API-Key`. |
| `429` | Przekroczono 5 żądań na minutę dla danego klucza. |
| `502` | Provider AI zwrócił błąd albo niepoprawną odpowiedź. |

Dokumentacja interaktywna OpenAPI jest dostępna pod `/swagger`, gdy jest włączona. Nie włączaj jej publicznie bez dodatkowej ochrony.

## Workflow: diff do Pull Requesta

1. W folderze projektu, który zmieniasz, sprawdź `git status`.
2. Dodaj wybrane pliki przez `git add`.
3. Skopiuj sam diff: `git diff --cached --no-color | Set-Clipboard`.
4. Wklej diff do pola `Git diff` w tej aplikacji.
5. Wygeneruj commit message i opis Pull Requesta.
6. Uruchom testy oraz build właściwego projektu. Nie wklejaj logu testów do pola `Git diff`.
7. Skopiuj opis PR i zastąp `Testing: Not run` rzeczywistymi komendami oraz ich wynikiem.
8. Utwórz commit dopiero po przejściu testów.

Przykładowa sekcja opisu PR:

```markdown
## Testing

- `dotnet test` - passed
- `npm test` - passed
- `npm run build` - passed
```

## Uruchomienie lokalne

Wymagane są .NET 8 SDK oraz Node.js 22.12+.

Terminal backendu, PowerShell:

```powershell
$env:OPENAI_API_KEY="..."
$env:APP_API_KEY="wygenerowany-losowy-klucz-minimum-32-znaki"
dotnet run --project .\backend
```

Terminal frontendu:

```powershell
Set-Location .\frontend
npm ci
npm run dev
```

Otwórz adres pokazany przez Vite, zwykle `http://localhost:5173`. Vite przekazuje lokalnie `/api` do backendu na porcie `8080`.

## Testowanie

Testy backendu, w tym klienta AI, API, autoryzacji i redakcji sekretów:

```powershell
dotnet test .\backend.Tests\CommitGenerator.Tests.csproj
```

Build backendu:

```powershell
dotnet build .\backend\CommitGenerator.csproj
```

Build frontendu i kontrola zależności:

```powershell
Set-Location .\frontend
npm ci
npm test
npm run build
npm audit --audit-level=high
```

Po wypchnięciu zmian na `main`, `dev` albo po utworzeniu Pull Requesta GitHub Actions uruchamia automatycznie build backendu, testy .NET, testy i build frontendu, audit zależności oraz build obrazów Docker. Wynik sprawdzisz w zakładce `Actions` albo w sekcji `Checks` Pull Requesta.

Kontrola Compose i obrazów:

```powershell
docker compose --env-file .env.example config
docker compose build
```

Test API w PowerShellu:

```powershell
$headers = @{ "X-API-Key" = "wpisz-swoj-klucz" }
$body = @{ gitDiff = "diff --git a/file.txt b/file.txt" } | ConvertTo-Json

Invoke-RestMethod `
  -Uri http://localhost:8080/api/generate-commit `
  -Method Post `
  -Headers $headers `
  -ContentType "application/json" `
  -Body $body
```

Test odrzucenia braku klucza:

```powershell
$body = @{ gitDiff = "diff" } | ConvertTo-Json

try {
    Invoke-RestMethod `
      -Uri http://localhost:8080/api/generate-commit `
      -Method Post `
      -ContentType "application/json" `
      -Body $body
} catch {
    $_.Exception.Response.StatusCode.value__
}
```

Oczekiwany status to `401`.

## Bezpieczeństwo

- `OPENAI_API_KEY` pozostaje w backendzie i nie jest przekazywany do obrazu frontendu.
- Każde żądanie generowania wymaga `X-API-Key`.
- Rate limit jest liczony osobno dla poprawnego klucza i działa w pamięci pojedynczego procesu.
- Diff jest redagowany z typowych sekretów przed wysłaniem do AI, ale redakcja nie jest gwarancją wykrycia każdego sekretu.
- Nie wklejaj do aplikacji produkcyjnych tokenów, haseł ani kluczy prywatnych.
- Do publicznego wdrożenia użyj HTTPS, secret managera i OAuth/OIDC zamiast współdzielonego klucza.
- Szczegóły procedur bezpieczeństwa znajdują się w `SECURITY.md`.

## Wdrożenie produkcyjne

Obecny Compose jest ustawiony pod lokalny development. Przy wdrożeniu:

- zakończ TLS na reverse proxy lub ingressie;
- nie publikuj portu backendu `8080` do Internetu;
- ustaw `FRONTEND_ORIGINS` na dokładny adres produkcyjnego frontendu;
- pozostaw `ENABLE_SWAGGER=false`, chyba że Swagger jest chroniony;
- przechowuj klucze w secret managerze, nie w repozytorium;
- przy wielu replikach zastąp lokalny limiter rozwiązaniem rozproszonym, np. Redisem albo limiterem na gatewayu;
- skonfiguruj monitoring błędów, kosztów i czasu odpowiedzi providera AI.

Przykładowe decyzje wdrożeniowe opisano w `docs/production.md`.

## Troubleshooting

### Backend nie startuje

Sprawdź, czy ustawione są `OPENAI_API_KEY` oraz `APP_API_KEY` lub `APP_API_KEYS`. Klucze aplikacji muszą mieć minimum 32 znaki.

### Frontend zwraca `401`

Wpisz dokładnie ten sam klucz, który jest ustawiony na backendzie. Przy wielu kluczach użyj jednego wpisu z `APP_API_KEYS`.

### Provider AI zwraca `502`

Sprawdź endpoint `OPENAI_BASE_URL`, nazwę `OPENAI_MODEL`, ważność klucza i limity u dostawcy. Diff nie jest logowany przez backend.

### Docker nie odpowiada

Uruchom Docker Desktop i sprawdź `docker info`, a następnie wykonaj ponownie `docker compose build`.

### Compose ostrzega o zmiennych, np. `asaj` albo `sakris097`

Jeśli wartość w `.env` zawiera `$nazwa`, Docker Compose interpretuje ją jako zmienną środowiskową. Użyj klucza bez znaków `$` albo zapisz wartość w pojedynczych cudzysłowach. Klucz aplikacji musi mieć minimum 32 znaki.

## Licencja

Projekt nie ma jeszcze przypisanej licencji. Przed publiczną dystrybucją dodaj właściwy plik `LICENSE`.
