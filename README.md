# EegWcfSystem — Kontrolna tačka 1

WCF sistema za prenos EEG podataka (klijent → server) putem `netTcpBinding` sa streaming-om.

---

## Arhitektura

```
┌─────────────────────────────┐                   ┌─────────────────────────────┐
│        CLIENT (.exe)         │                   │        SERVER (.exe)         │
│                              │                   │                              │
│  EEG/ direktorijum           │                   │  ┌────────────────────────┐  │
│   ├─ subject_1_results.csv   │   netTcpBinding   │  │ EegService             │  │
│   ├─ subject_2_results.csv   │   (streaming)     │  │  StartSession(meta)    │  │
│   └─ subject_N_results.csv   │  ◄──────────────► │  │  PushSample(sample)    │  │
│                              │     ACK/NACK      │  │  EndSession()          │  │
│  EegCsvReader (IDisposable)  │                   │  │                        │  │
│   - parse invariant culture  │                   │  │  validacija +          │  │
│   - red po red → PushSample  │                   │  │  Dispose pattern       │  │
│                              │                   │  └───────────┬────────────┘  │
│  RejectLogger (klijent log)  │                   │              │               │
└─────────────────────────────┘                   │              ▼               │
                                                   │  Data/<ParticipantId>/      │
                                                   │       <YYYY-MM-DD>/         │
                                                   │       session.csv  (CP2)    │
                                                   │       rejects.csv  (CP2)    │
                                                   └─────────────────────────────┘
```

---

## Protokol poruka

| Korak | Poruka | Sadržaj | Server odgovor |
|-------|--------|---------|----------------|
| 1 | `StartSession(EegMeta)` | ParticipantId, FileName, TotalRows, SchemaVersion | `Ack{ Status=IN_PROGRESS }` |
| 2 | `PushSample(EegSample)` × N | jedan red CSV-a sa svim poljima + RowIndex | `Ack{ Status=IN_PROGRESS }` ili `FaultException` |
| 3 | `EndSession()` | bez parametara | `Ack{ Status=COMPLETED }` |

Sve greške validacije se vraćaju kao `FaultException<ValidationFault>` ili `FaultException<DataFormatFault>`.  
Klijent ih beleži u `logs/client_rejects_*.csv`, ali nastavlja sa sledećim redom — ne prekida sesiju.

---

## Struktura projekta

```
EegWcfSystem.sln
├── EegWcfSystem.Common     (Class Library .NET 4.8)
│     ├── Contracts/IEegService.cs
│     ├── Contracts/EegMeta.cs
│     ├── Contracts/EegSample.cs
│     ├── Contracts/AckResponse.cs
│     ├── Contracts/SessionStatus.cs
│     └── Faults/EegFaults.cs
├── EegWcfSystem.Server     (Console App .NET 4.8)
│     ├── Services/EegService.cs
│     ├── Storage/SessionWriter.cs   (skelet za CP2)
│     ├── Program.cs
│     └── App.config
└── EegWcfSystem.Client     (Console App .NET 4.8)
      ├── IO/EegCsvReader.cs
      ├── IO/RejectLogger.cs
      ├── Program.cs
      └── App.config
```

---

## Pragovi validacije (App.config — Server)

| Ključ | Podrazumevano | Opis |
|-------|--------------|------|
| `BatteryLowThreshold` | 20 | Upozorenje za nizak bateriju (CP2 event) |
| `ContactQualityMin` | 50 | Minimalni prihvatljivi ContactQuality (CP2 event) |
| `ExcitementSpikeThreshold` | 0.30 | Delta Excitement za spike alarm (CP2) |
| `ChannelMinValue` | 0 | Minimalna vrednost EEG kanala |
| `ChannelMaxValue` | 10000 | Maksimalna vrednost EEG kanala |
| `TimestampSkewMaxMs` | 2000 | Maksimalni dozvoljeni skew timestampa (CP2) |

---

## Pokretanje

### Preduslovi
- Visual Studio 2022 (Community ili noviji)
- Workload: **.NET desktop development** + komponenta **Windows Communication Foundation**
- .NET Framework **4.8**

### Koraci

1. Otvori `EegWcfSystem.sln` u Visual Studio 2022.
2. **Build → Build Solution** (`Ctrl+Shift+B`) — oba projekta moraju da se kompajliraju.
3. Postavi `EegWcfSystem.Server` kao **Startup Project**, pokreni (`F5`).  
   Konzola treba da ispiše:
   ```
   [SERVER] WCF host otvoren.
   [SERVER] EEG servis pokrenut. Endpoint-ovi:
     net.tcp://localhost:9000/EegService
   ```
4. Napravi folder `EEG\` u `EegWcfSystem.Client\bin\Debug\` i ubaci CSV fajlove  
   sa nazivom oblika `subject_<N>_results.csv`.
5. Pokreni `EegWcfSystem.Client.exe` iz drugog prozora (ili postavi kao drugi Startup Project).  
   Videćeš slanje uzoraka i ACK statuse.
6. Po završetku u `logs\` folderu klijenta nalazi se `client_rejects_*.csv`.

### Ako ne radi

| Greška | Uzrok | Rešenje |
|--------|-------|---------|
| `EndpointNotFoundException` | Server nije pokrenut | Pokreni Server pre Clienta |
| `AddressAlreadyInUseException` | Port 9000 zauzet | `netstat -an \| findstr 9000` → zatvori proces |
| `ProtocolException` (poruka prevelika) | `maxReceivedMessageSize` | Povećaj u App.config |
| CSV se ne čita | Pogrešan naziv fajla | Format mora biti `subject_<broj>_results.csv` |

---

## Napomene za CP2

Kod je unapred strukturisan za CP2 bez refaktora:

| CP2 Task | Šta je već u kodu | Šta treba dodati |
|----------|------------------|-----------------|
| Snimanje fajlova | `SessionWriter` skelet sa Dispose | popuniti `AppendSample` / `AppendReject` |
| Dijagnostika | sekvencijalni `PushSample` radi | `Stopwatch` po redu na klijentu |
| Delegati/eventi | `PerSession` instanca čuva stanje | dodati `event` polja u `EegService` |
| ΔExcitement/ΔInterest | `_lastRowIndex` čuva kontekst | zapamti `_prevSample`, računaj delte |
| ContactQuality/Battery alarmi | pragovi se čitaju u konstruktoru | dodati provere posle `ValidateSample` |
