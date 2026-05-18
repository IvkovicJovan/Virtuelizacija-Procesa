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

