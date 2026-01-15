# InternshipFramework
# Documentatie
### Opstarten
Op het project op te kunnen starten zijn er een aantal dingen nodig. Deze zullen hieronder beschreven worden.
Als eerste heb je een dongle nodig die als coordinator zal functioneren, vervolgens gebruik je deze [deze](https://www.zigbee2mqtt.io/guide/installation/) link om Zigbee2MQTT te installeren. Kies hiervoor docker. Vervolgens moet je ook een MQTT broker [opstarten](https://www.zigbee2mqtt.io/guide/configuration/mqtt.html). Dit doe je door middel van een docker-compose.yml te creëren in een aparte map (bijvoorbeeld mqtt/ of mosquitto/). In deze map plaats je ook de submappen config, data en log, zodat de Mosquitto-configuratie en data persistent blijven.
De inhoud van de docker-compose.yml is als volgt:
version: "3.8"
services:
  mosquitto:
    image: eclipse-mosquitto:2
    container_name: mosquitto
    restart: unless-stopped
    ports:
      - "1883:1883"
    volumes:
      - ./mosquitto/config:/mosquitto/config
      - ./mosquitto/data:/mosquitto/data
      - ./mosquitto/log:/mosquitto/log

Navigeer vervolgens in de terminal naar de map waarin de docker-compose.yml staat en start de broker met:
docker compose up -d
Vervolgens start je hem met: docker compose up -d 
Vervolgens moet de ESP32-WROOM verbonden zijn over serial connection voor access naar de OpenTherm module, dit is echter een minimaal stukje code en deze requirement kan tijdelijk uitgezet worden in de code door het te commenten. Dit kan gedaan worden in de ServiceMapper
Hierbij is het verstandig op MQTTExplorer te downloaden om de volledige structuur en berichten op de broker te kunnen zien.



## Architectuurcomponenten
### Kernservices Architectuur
De logic layer bestaat uit **14 hoofdserviceklassen**, elk verantwoordelijk voor een afzonderlijk domein binnen het IoT-ecosysteem:
- **OpenThermService** – Beheert seriële communicatie met OpenTherm-gateways  
- **OptionService** – Verwerkt apparaatconfiguratie-opties  
- **ReceiveService** – Verwerkt inkomende MQTT-berichten  
- **SendService** – Beheert uitgaande MQTT-communicatie  
- **SubscriptionService** – Beheert apparaatabonnementen op berichten  
- **ZigbeeClient** – Hoofdorkestrator voor apparaatbeheeroperaties  
- **ZigbeeCommandService** – Verwerkt Azure IoT Hub direct method-commando’s  
- **AzureIoTHubService** – Beheert cloud-naar-apparaatcommunicatie  
- **ConfiguredReportingsService** – Beheert apparaatrapportageconfiguraties  
- **DeviceFilterService** – Verwerkt datafiltering voor apparaatgegevens  
- **DeviceService** – Kernbeheer van de apparaatlevenscyclus  
- **DeviceTemplateService** – Beheert apparaatsjablonen en modelreplicatie  
- **EdgeAdapter** – Integratie met Azure IoT Edge-modules  
- **MqttConnectionService** – Beheer van MQTT-brokerverbindingen  
---
## Gedetailleerde Servicedocumentatie
### OpenThermService
**Doel**  
Beheert seriële poortcommunicatie met ESP-gebaseerde OpenTherm-gateways voor integratie van verwarmingssystemen.
**Belangrijkste kenmerken**
- Thread-veilige seriële poortafhandeling  
- JSON-gebaseerd berichtprotocol  
- Configuratiebeheer voor OpenTherm-parameters  
- Realtime telemetrie-forwarding naar Azure IoT Hub  
#### Kritieke functies
##### `ProcessLoopAsync(CancellationToken)`
**Beschrijving**  
Hoofdverwerkingslus die de volledige OpenTherm-communicatielevenscyclus beheert.
**Procesverloop**
1. Maakt een seriële verbinding met het ESP-apparaat  
2. Verstuurt configuratiegegevens naar het ESP  
3. Luistert naar binnenkomende telemetrieberichten  
4. Stuurt telemetrie door naar Azure IoT Hub  
5. Verwerkt verbindingsfouten met automatische retry-logica  
---
##### `SendConfigToEspAsync(CancellationToken)`
**Beschrijving**  
Haalt OpenTherm-configuraties op uit de database en verzendt deze naar het ESP-apparaat.
**Proces**
- Vraagt OpenTherm-configuraties op uit de repository  
- Converteert configuratiegegevens naar JSON  
- Verstuurt het configuratiepakket via de seriële poort  
- Waarborgt thread-veilige toegang met `SemaphoreSlim`  
---
##### `ListenForIncomingMessagesAsync(CancellationToken)`
**Beschrijving**  
Luistert continu naar binnenkomende JSON-berichten van het ESP-apparaat.
**Kenmerken**
- Asynchrone streaming via `IAsyncEnumerable`  
- Robuuste JSON-deserialisatie met foutafhandeling  
- Detectie en logging van foutief geformatteerde berichten  
- Automatische herverbinding bij communicatieproblemen  
---
### ReceiveService
**Doel**  
Centraal punt voor het ontvangen en verwerken van MQTT-berichten voor alle Zigbee-apparaatcommunicatie.
**Belangrijkste kenmerken**
- Beheer van MQTT-topicabonnementen  
- Verwerking en filtering van apparaatgegevens  
- Detectie van nulwaarden  
- Synchronisatie van apparaatopties  
- Doorsturen van telemetrie naar Azure IoT Hub  
#### Kritieke functies
##### `OnMessageAsync(MqttApplicationMessageReceivedEventArgs)`
**Beschrijving**  
Primaire handler voor alle binnenkomende MQTT-berichten.
**Verwerkingslogica**
1. Extraheert het apparaatadres uit het MQTT-topic  
2. Valideert de abonnementsstatus van het apparaat  
3. Parseert de JSON-payload en extraheert sensorgegevens  
4. Verwerkt speciale sensorgevallen (temperatuur, CO₂, luchtvochtigheid)  
5. Past apparaatspecifieke filters toe (Opgeslagen in database) 
6. Stuurt telemetrie door naar Azure IoT Hub  
7. Verwerkt laat binnenkomende optiegegevens  
---
##### `HandleZeroSensorValuesAsync(string)`
**Beschrijving**  
Verwerkt situaties waarin alle sensorwaarden nul zijn, dit gebeurt alleen met de temperatuur sensor. Deze stuurt 0,0,0 waaarmee hij aangeeft wakker te blijven voor een eventuele nieuwe configuratie. Dit gebeurt wekelijks 1 keer, wegens batterij besparing (Lees verslag/Zie arduino code).
**Proces**
- Haalt rapportageconfiguraties van het apparaat op  
- Extraheert min/max-intervallen en wijzigingsdrempels  
- Herconfigureert apparaatsrapportages via MQTT  
- Verstuurt sequentiële configuratie-updates met vertragingen  
---
#### `SendConfigValueAsync()`
**Beschrijving**  
Stuurt een voor een de reportingn configuratie naar de ESP, dit wordt 1 voor 1 gedaan wegens de ESP in de huidige staat maar 1 bericht accepteert op een topic. Hierom is een 200ms delay toegevoegd, zodat er voldoende tijd is om het bericht op te halen.
### ZigbeeClient
**Doel**  
Hoofdorkestrator die alle Zigbee-apparaatbeheeroperaties coördineert.
**Belangrijkste kenmerken**
- Afhandeling van apparaat-joining en interviews  
- Beheer van rapportageconfiguraties  
- Synchronisatie van apparaatopties  
- Sjabloongebaseerde apparaatcreatie  
#### Kritieke functies
##### `AllowJoinAndListen(int)`
**Beschrijving**  
Staat Zigbee-apparaatjoining toe voor een opgegeven duur en verwerkt interviews. (Check installer design)
**Procesverloop**
1. Abonneert zich op het bridge-events-topic  
2. Activeert netwerkjoining voor een gedefinieerde tijdsperiode  
3. Verwerkt apparaatinterviews:
   - Extraheert apparaateigenschappen en opties (Elke optie krijgt een waarde, de code wilt alleen 2 en 7 omdat dit writable options zijn.)
   - Maakt apparaatsjablonen indien nodig  
   - Zet apparaten in de wachtrij voor verdere verwerking (Het later ophalen van Configured reporting en options) 
4. Verwerkt apparaatcreatie en abonnementen  
5. Past rapportageconfiguraties toe  
6. Deactiveert netwerkjoining na timeout  
---
##### `GetDeviceDetails(string address, string model)`
**Beschrijving**  
Haalt gedetailleerde rapportageconfiguraties voor een apparaat op.
**Proces**
- Zet de reporting tijd van een apparaat naar 0 (Instant data)
- Leest ,als ontvangen ontvangen, de data uit van het device en slaat de opties met hun waardes op
- Na een timeout wordt het device aan een list toegevoegd voor latere behandeling in de ReceiveService  
---
####  `public async Task GetOptionDetails(string address,string model,List<string> readableProps,List<string> descriptions)`
**Beschrijving**  
Haalt alle opties van een apparaat op.
**Proces**
- Abonneert zich op het device-list-topic  
- Vraagt apparaatgegevens op bij de Zigbee-bridge 
- Parseert geconfigureerde rapportages per endpoint  
- Slaat configuraties op in de database  
- Past retry-logica toe bij ontbrekende gegevens  
### ConfiguredReportingsService
**Doel**  
Beheert rapportageconfiguraties die bepalen hoe vaak apparaten data verzenden.
#### Belangrijke functies
##### `GetChangedReportConfigsAsync(List<string>)`
Retourneert alle rapportageconfiguraties die zijn gewijzigd en toegepast moeten worden op apparaten.
---
##### `GetAllReportConfigsForAddressAsync(string)`
Retourneert alle rapportageconfiguraties voor een apparaat met nulintervallen om snelle optie-ophaling mogelijk te maken.
---
### AzureIoTHubService
**Doel**  
Beheert alle communicatie met Azure IoT Hub.
#### Belangrijke functies
##### `SendTelemetryAsync(string deviceId, string property, object value)`
Verstuurt één telemetrie-eigenschap naar Azure IoT Hub.
**Kenmerken**
- Automatische tijdstempeltoewijzing  
- Toevoegen van message properties  
- JSON-serialisatie  
- Robuuste foutafhandeling en logging  
---
##### `SendBatchTelemetryAsync(string deviceId, Dictionary<string, object>)`
Verstuurt meerdere telemetriewaarden in één bericht voor efficiëntie.
---
## Communicatieprotocollen
### MQTT-topicstructuur
| Doel               | Topic                                  |
|--------------------|----------------------------------------|
| Apparaatgegevens   | `zigbee2mqtt/{device_address}`         |
| Apparaatbesturing  | `zigbee2mqtt/{device_address}/set`     |
| Apparaatqueries    | `zigbee2mqtt/{device_address}/get`     |
| Bridge-events      | `zigbee2mqtt/bridge/event`             |
| Bridge-requests    | `zigbee2mqtt/bridge/request/*`         |
---
### Seriële communicatie (OpenTherm)
- **Baudrate:** Meestal `115200`  
- **Protocol:** JSON met newline-terminatie  
- **Berichttypes:**
  - Configuratie  
  - Telemetrie  
  - Parameterupdates  
---
### Azure IoT Hub-integratie
- **Berichtformaat:** JSON  
- **Berichteigenschappen:**
  - `deviceId`  
  - `propertyName`  
- **Batch-ondersteuning:** Meerdere telemetriewaarden per bericht  
---




