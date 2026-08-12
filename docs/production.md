# Wdrożenie produkcyjne

## HTTPS przez reverse proxy

Docker Compose udostępnia lokalny HTTP. W produkcji TLS powinien kończyć się na reverse proxy, load balancerze albo ingressie. Przykład Nginx przed frontendem:

```nginx
server {
    listen 443 ssl http2;
    server_name git-helper.example.com;

    ssl_certificate /etc/letsencrypt/live/git-helper.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/git-helper.example.com/privkey.pem;

    location / {
        proxy_pass http://frontend:80;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
    }
}
```

Nie kopiuj tego przykładu bez konfiguracji certyfikatów, sieci kontenerowej, nagłówków bezpieczeństwa i polityki firewall. Port backendu powinien być dostępny tylko z sieci Compose lub sieci prywatnej.

## OAuth/OIDC

Klucz `X-API-Key` wystarcza do małego narzędzia wewnętrznego, ale nie jest systemem kont. Przy wielu użytkownikach, audycie lub wymaganiu MFA użyj dostawcy OIDC, np. Entra ID, Keycloak, Auth0 albo dostawcy GitHub/Google. Backend powinien wtedy:

- weryfikować podpis i issuer tokenu;
- sprawdzać audience oraz expiry;
- mapować claim użytkownika na limity i uprawnienia;
- nie przechowywać własnych haseł;
- nie ufać tokenowi przesłanemu wyłącznie przez frontend bez walidacji backendu.

## Rate limiting i Redis

Obecny limiter jest szybki i poprawny dla jednej instancji backendu. Jego stan istnieje tylko w pamięci procesu. Przy dwóch replikach każda może przyjąć pięć żądań na minutę, więc globalny limit nie wynosi już pięć.

W środowisku wieloinstancyjnym zastosuj jeden z wariantów:

- rate limiting na wspólnym gatewayu, np. Nginx, Traefik, Cloudflare albo API Management;
- rozproszony limiter oparty o Redis;
- limitowanie na poziomie dostawcy chmurowego.

Redis nie jest konieczny tylko dlatego, że aplikacja działa w Dockerze. Jest potrzebny, gdy stan limitera musi być współdzielony przez wiele procesów lub hostów.

## Sekrety i prywatność

Redaktor sekretów jest warstwą obrony, nie gwarancją. Najbezpieczniejsza kolejność to:

1. nie commitować sekretów do repozytorium;
2. przed wysłaniem przejrzeć diff;
3. używać secret managera i skanera sekretów w CI;
4. ustalić z dostawcą AI zasady retencji i przetwarzania danych;
5. ograniczyć modelowi dane do niezbędnego fragmentu diffu.

## Operacje

Przed wdrożeniem skonfiguruj:

- monitoring `/health`;
- alerty na `401`, `429`, `502` i wzrost kosztów AI;
- centralne logi bez wartości nagłówka `X-API-Key` i bez treści diffu;
- rotację kluczy aplikacji i kluczy dostawcy AI;
- backup konfiguracji infrastruktury, ale nie samych sekretów w repozytorium.
