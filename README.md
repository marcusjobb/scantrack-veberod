# ScanTrack — Distribuerat paketnätverk

> Vecka 36 · Docker · ITHS Administrera molnlösningar

---

## Scenario

ScanTrack AB hanterar pakettransporter mellan svenska städer. Varje stad har ett eget system — en **nod** — som tar emot paket och vidarebefordrar dem mot destinationen längs kortaste väg.

Ni är teamet som bygger **er stads nod**. En annan grupp bygger en annan stad. I slutet av dagen ska paketen röra sig genom Sverige i realtid.

---

## Arkitektur

Varje nod är en containeriserad .NET Web API. Den:

1. **Registrerar sig** mot ScanTrack-registret vid uppstart
2. **Tar emot paket** via `POST /paket`
3. **Räknar ut nästa hopp** med Dijkstras algoritm (baserat på `data/cities.csv`)
4. **Vidarebefordrar** paketet till nästa stad
5. **Levererar** om paketet nått sin destination

Paketet bär med sig en historik — listan med alla städer det passerat. Det förhindrar loopar.

```
Göteborg ──→ POST /paket {destination: "Sundsvall", history: ["Göteborg"]}
    ↓
   Dijkstra: kortaste väg är Göteborg→Stockholm→Gävle→Sundsvall
    ↓
Vidarebefordrar till Stockholm...

Stockholm ──→ history: ["Göteborg", "Stockholm"]
    ↓
Vidarebefordrar till Gävle...

Gävle ──→ history: ["Göteborg", "Stockholm", "Gävle"]
    ↓
Vidarebefordrar till Sundsvall...

Sundsvall ──→ destination == jag → levererat! ✓
```

---

## Miljövariabler

Tre variabler måste sättas när noden startas:

| Variabel | Exempel | Beskrivning |
|----------|---------|-------------|
| `CITY_NAME` | `Göteborg` | Er stads namn (måste matcha cities.csv) |
| `NODE_URL` | `http://20.100.x.x:8080` | Er nods publika URL (IP från ACI) |
| `REGISTRY_URL` | *(delas ut av läraren)* | Centralt register för alla noder |

---

## Endpoints

| Endpoint | Metod | Beskrivning |
|----------|-------|-------------|
| `/paket` | POST | Ta emot och vidarebefordra ett paket |
| `/paket` | GET | Lista paket som levererats till denna nod |
| `/status` | GET | Nodens hälsostatus |
| `/route?from=X&to=Y` | GET | Visa beräknad rutt (för testning) |
| `/swagger` | GET | API-dokumentation |

---

## Din uppgift

### Steg 1 — Förstå koden

Läs igenom projektet. Testa att köra det lokalt:

```bash
cd ScanTrackNode
dotnet run
```

Öppna `http://localhost:5000/swagger` och testa `/route?from=Linköping&to=Stockholm`.

### Steg 2 — Implementera `ForwardAsync`

Öppna `Services/PackageForwarder.cs`. Implementera metoden `ForwardAsync` enligt instruktionerna i kommentarerna.

Testa lokalt innan du går vidare.

### Steg 3 — Skriv Dockerfile

Öppna `ScanTrackNode/Dockerfile`. Fyll i de saknade raderna (multi-stage build — se föreläsningen).

> **OBS:** Bygg från **repo-roten**, inte inifrån `ScanTrackNode/`. Annars hittas inte `data/cities.csv`.

```bash
# Stå i repo-roten (där README.md och data/ finns)
docker build -f ScanTrackNode/Dockerfile -t scantrack-node .

docker run -p 8080:8080 \
  -e CITY_NAME=Göteborg \
  -e NODE_URL=http://localhost:8080 \
  -e REGISTRY_URL=<url från läraren> \
  scantrack-node
```

Tips i Dockerfile: kopiera CSV-filen från repo-roten med `COPY data/ /data/`

### Steg 4 — Deploya till Azure

```bash
# Skapa resource group och container registry
az group create --name rg-scantrack-[dittnamn] --location northeurope

az acr create \
  --name acrscantrack[dittnamn] \
  --resource-group rg-scantrack-[dittnamn] \
  --sku Basic \
  --admin-enabled true

# Logga in mot ACR och pusha imagen
az acr login --name acrscantrack[dittnamn]

docker tag scantrack-node acrscantrack[dittnamn].azurecr.io/scantrack-node:v1
docker push acrscantrack[dittnamn].azurecr.io/scantrack-node:v1

# Hämta ACR-lösenord och starta noden
ACR_PASSWORD=$(az acr credential show \
  --name acrscantrack[dittnamn] \
  --query "passwords[0].value" -o tsv)

az container create \
  --name scantrack-<er-stad> \
  --resource-group rg-scantrack-[dittnamn] \
  --image acrscantrack[dittnamn].azurecr.io/scantrack-node:v1 \
  --ports 8080 \
  --ip-address Public \
  --registry-login-server acrscantrack[dittnamn].azurecr.io \
  --registry-username acrscantrack[dittnamn] \
  --registry-password "$ACR_PASSWORD" \
  --environment-variables \
    CITY_NAME=<er-stad> \
    NODE_URL=http://<er-ip>:8080 \
    REGISTRY_URL=<url från läraren>
```

> **Tips:** Skapa noden utan `NODE_URL` först, hämta IP-adressen med `az container show --query ipAddress.ip -o tsv`, ta sedan bort och skapa om med rätt IP.

### Steg 5 — Skicka ett paket

När alla grupper är uppe — skicka ett paket och se det röra sig genom Sverige:

```bash
curl -X POST http://<er-ip>:8080/paket \
  -H "Content-Type: application/json" \
  -d '{
    "destination": "Umeå",
    "payload": "3 kartonger kaffe",
    "history": []
  }'
```

Titta på loggarna:

```bash
az container logs --name scantrack-<er-stad> --resource-group rg-scantrack-[dittnamn] --follow
```

---

## Städer

| Stad | Grupp |
|------|-------|
| Helsingborg | Grupp 1 |
| Malmö | Grupp 2 |
| Göteborg | Grupp 3 |
| Borås | Grupp 4 |
| Trollhättan | Grupp 5 |
| Karlstad | Grupp 6 |
| Jönköping | Grupp 7 |
| Växjö | Grupp 8 |
| Kalmar | Grupp 9 |
| Skövde | Grupp 10 |
| Linköping | Grupp 11 |
| Norrköping | Grupp 12 |
| Stockholm | Grupp 13 |
| Uppsala | Grupp 14 |
| Västerås | Grupp 15 |
| Örebro | Grupp 16 |
| Falun | Grupp 17 |
| Gävle | Grupp 18 |
| Härnösand | Grupp 19 |
| Sundsvall | Grupp 20 |
| Östersund | Grupp 21 |
| Umeå | Grupp 22 |
| Luleå | Grupp 23 |

---

## Inlämning

- Länk till er live-nod (`/status` ska svara)
- Ifylld `rapport_mall.md`
- Skärmbild: ett paket som passerat er nod (syns i loggarna)

Deadline: *(sätts av läraren)*

---

## Nästa vecka (v38)

Filen `.github/workflows/deploy.yml` innehåller ett skelett för CI/CD. I v38 automatiserar ni denna deploy — sparade kommandon från v36 är bra att ha till hands.
