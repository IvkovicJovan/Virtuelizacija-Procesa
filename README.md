# Virtuelizacija Procesa — EEG WCF projekat

Projekat je usklađen sa PDF zadatkom **„Razmena i skladištenje EEG podataka iz CSV-a korišćenjem WCF servisa, fajl sistema i događajnog modela“**.

## 1. Arhitektura

```text
EEG CSV fajlovi
      ↓
EegWcfSystem.Client
      ↓  StartSession(meta), PushSample(sample), EndSession()
WCF netTcpBinding servis
      ↓
EegWcfSystem.Server
      ↓
Data/<ParticipantId>/<YYYY-MM-DD>/session.csv
Data/<ParticipantId>/<YYYY-MM-DD>/rejects.csv
```

Projekat ima tri dijela:

- `EegWcfSystem.Common` — zajednički WCF ugovor, modeli i fault klase.
- `EegWcfSystem.Client` — čita CSV fajlove, parsira redove i šalje ih serveru.
- `EegWcfSystem.Server` — hostuje WCF servis, prima podatke, validira, upisuje i generiše događaje/warning-e.


### Vizuelna skica arhitekture

![Skica projekta – EEG WCF](docs/skica_projekta_eeg_wcf.png)

## 2. Gdje su StartSession, PushSample i EndSession?

Definicija ugovora:

```text
EegWcfSystem.Common/Contracts/IEegService.cs
```

Klijent ih poziva ovdje:

```text
EegWcfSystem.Client/Program.cs
```

Server ih implementira ovdje:

```text
EegWcfSystem.Server/Services/EegService.cs
```

Najbitnija metoda za slanje podataka je:

```csharp
proxy.PushSample(sample);
```

To je mjesto gdje se jedan parsirani CSV red šalje sa klijenta na WCF servis.

## 3. Pokretanje

1. Otvori `EegWcfSystem.sln` u Visual Studio.
2. Uradi `Build -> Clean Solution`.
3. Uradi `Build -> Rebuild Solution`.
4. Prvo pokreni `EegWcfSystem.Server`.
5. Zatim pokreni `EegWcfSystem.Client`.

Sigurniji način je ručno: prvo server konzola, pa klijent konzola.

## 4. Šta se generiše?

Na serveru se generiše:

```text
EegWcfSystem.Server/bin/Debug/Data/<ParticipantId>/<YYYY-MM-DD>/session.csv
EegWcfSystem.Server/bin/Debug/Data/<ParticipantId>/<YYYY-MM-DD>/rejects.csv
```

Na klijentu se generiše:

```text
EegWcfSystem.Client/bin/Debug/logs/client_rejects_*.csv
EegWcfSystem.Client/bin/Debug/logs/send_times_subject_*.csv
```

## 5. Pokrivenost PDF zadataka

| Zadatak | Implementacija |
|---|---|
| 1. Skica sistema i protokol | `README.md`, `ARHITEKTURA_I_PROTOKOL.md`, `IEegService.cs` |
| 2. WCF servis, ugovori, konfiguracija | `IEegService.cs`, `EegMeta.cs`, `EegSample.cs`, oba `App.config` fajla |
| 3. Validacija i fault izuzeci | `EegService.cs`, `EegFaults.cs` |
| 4. Dispose pattern | `EegCsvReader.cs`, `RejectLogger.cs`, `SendTimeLogger.cs`, `SessionWriter.cs`, `EegService.cs` |
| 5. CSV učitavanje | `EegCsvReader.cs`, `Program.cs`, folder `EEG/` |
| 6. session.csv i rejects.csv | `SessionWriter.cs` |
| 7. Sekvencijalni prenos i log vremena slanja | `Program.cs`, `SendTimeLogger.cs` |
| 8. Delegati i događaji | `EegService.cs` (`OnTransferStarted`, `OnSampleReceived`, `OnTransferCompleted`, `OnWarningRaised`) |
| 9. ΔExcitement i ΔInterest | `EegService.cs`, metoda `AnalyzeWarnings` |
| 10. Baterija, kontakt, saturacija kanala | `EegService.cs`, metoda `AnalyzeWarnings` |

## 6. EEG baza podataka

U projekat je ubačena stvarna baza iz fajla `EEG.rar` koji je poslat naknadno. U toj arhivi se nalazi 20 CSV fajlova:

```text
EegWcfSystem.Client/EEG/subject_1_results.csv
...
EegWcfSystem.Client/EEG/subject_20_results.csv
```

Ukupno ima 874411 redova podataka bez header-a. Kod ne zavisi od fiksnog broja fajlova, već rekurzivno obrađuje sve fajlove oblika `subject_*_results.csv` koje nađe u `EEG/` direktorijumu.

Napomena: PDF u tekstu zadatka pominje 30 fajlova, ali poslata `EEG.rar` arhiva sadrži 20 fajlova. Nisam izmišljao dodatnih 10 fajlova; ako asistent/profesor insistira na svih 30 originalnih CSV fajlova, potrebno je samo dodati nedostajuće `subject_21_results.csv` do `subject_30_results.csv` u isti `EEG/` folder.

## 7. Simulacija prekida veze

U `EegWcfSystem.Client/App.config` postoji:

```xml
<add key="SimulateBreakAfterRows" value="0" />
```

Za normalan rad ostaje `0`. Za demonstraciju prekida veze možeš staviti npr. `10`; klijent će prekinuti poslije 10 poslatih redova, a `finally` blok i `Dispose` će zatvoriti reader/log resurse.
