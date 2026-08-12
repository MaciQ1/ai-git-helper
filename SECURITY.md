# Security Policy

## Zakres

Projekt przyjmuje dane źródłowe i przekazuje ich fragment do zewnętrznego dostawcy AI. Traktuj `git diff` jako dane potencjalnie wrażliwe.

## Zgłaszanie problemów

Nie publikuj publicznego issue zawierającego token, klucz API, dane użytkownika ani szczegóły exploita. Zgłoszenie przekaż prywatnym kanałem właścicielowi repozytorium lub administratorowi wdrożenia.

## Zasady wdrożenia

- Używaj HTTPS między przeglądarką, reverse proxy i backendem.
- Przechowuj `OPENAI_API_KEY`, `APP_API_KEY` i `APP_API_KEYS` w secret managerze.
- Nie umieszczaj sekretów w `VITE_*`, ponieważ zmienne Vite trafiają do publicznego bundle'a.
- Ustaw `FRONTEND_ORIGINS` na konkretne domeny, nie na wildcard.
- Nie wystawiaj portu backendu bezpośrednio do Internetu.
- Nie włączaj Swaggera publicznie bez dodatkowego uwierzytelnienia.
- Dla wielu instancji użyj rozproszonego rate limitera.
- Regularnie sprawdzaj koszty i limity dostawcy AI.

## Redakcja danych

Backend usuwa część rozpoznawalnych sekretów, m.in. wartości nazwane `API_KEY`, `TOKEN`, `PASSWORD`, tokeny Bearer, wybrane klucze dostawców i bloki kluczy prywatnych. Mechanizm oparty na wzorcach może pominąć nieznany format albo błędnie zredagować fragment kodu. Redakcja nie zastępuje przeglądu danych wejściowych.

## Uwierzytelnianie

Obecny mechanizm `X-API-Key` jest przeznaczony dla małej, zaufanej grupy. Nie zapewnia rejestracji, resetu haseł, MFA ani pełnego cyklu życia konta. Dla publicznego produktu użyj zewnętrznego dostawcy OAuth/OIDC i walidacji tokenów po stronie backendu.
