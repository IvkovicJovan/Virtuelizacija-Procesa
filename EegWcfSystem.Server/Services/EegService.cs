using System;
using System.Globalization;
using System.ServiceModel;
using EegWcfSystem.Common.Contracts;
using EegWcfSystem.Common.Faults;
using EegWcfSystem.Server.Storage;

namespace EegWcfSystem.Server.Services
{
    // PerSession da svaka sesija ima svoj state (RowIndex, writer, meta).
    // Ovo je kljuc za CP2 — eventi/alarmi imaju kontekst sesije.
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession,
                     ConcurrencyMode = ConcurrencyMode.Single)]
    public class EegService : IEegService, IDisposable
    {
        private EegMeta       _meta;
        private int           _lastRowIndex = -1;
        private SessionWriter _writer;      // koristi se u CP2 (zadatak 6)
        private bool          _disposed;

        // Pragovi iz app.config — vec citamo da budu spremni za CP2
        private readonly double _channelMin;
        private readonly double _channelMax;
        private readonly int    _batteryLow;
        private readonly int    _contactQualityMin;

        public EegService()
        {
            _channelMin        = double.Parse(System.Configuration.ConfigurationManager.AppSettings["ChannelMinValue"]     ?? "0",     CultureInfo.InvariantCulture);
            _channelMax        = double.Parse(System.Configuration.ConfigurationManager.AppSettings["ChannelMaxValue"]     ?? "10000", CultureInfo.InvariantCulture);
            _batteryLow        = int.Parse   (System.Configuration.ConfigurationManager.AppSettings["BatteryLowThreshold"] ?? "20");
            _contactQualityMin = int.Parse   (System.Configuration.ConfigurationManager.AppSettings["ContactQualityMin"]   ?? "50");
        }

        // ----------------------------------------------------------------
        // StartSession
        // ----------------------------------------------------------------
        public AckResponse StartSession(EegMeta meta)
        {
            if (meta == null || meta.ParticipantId <= 0 || string.IsNullOrWhiteSpace(meta.FileName))
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault
                    {
                        Reason   = "Meta polja su nevalidna",
                        Rule     = "StartSession.MetaRequired",
                        RowIndex = -1
                    },
                    new FaultReason("Nevalidan StartSession meta paket"));
            }

            _meta         = meta;
            _lastRowIndex = -1;
            _writer       = new SessionWriter(meta);   // CP1: prazna impl, CP2: puna

            Console.WriteLine($"[SERVER] StartSession: subject={meta.ParticipantId}  file={meta.FileName}  rows={meta.TotalRows}");

            return new AckResponse
            {
                Success      = true,
                Status       = SessionStatus.IN_PROGRESS,
                Message      = "Session started",
                LastRowIndex = -1
            };
        }

        // ----------------------------------------------------------------
        // PushSample
        // ----------------------------------------------------------------
        public AckResponse PushSample(EegSample sample)
        {
            if (_meta == null)
                throw new FaultException<ValidationFault>(
                    new ValidationFault
                    {
                        Reason   = "Sesija nije pokrenuta",
                        Rule     = "Session.NotStarted",
                        RowIndex = sample?.RowIndex ?? -1
                    },
                    new FaultReason("StartSession nije pozvan"));

            ValidateSample(sample);

            _writer.AppendSample(sample);        // CP2: stvarno upisivanje u session.csv
            _lastRowIndex = sample.RowIndex;

            // ispis svakih 500 redova da ne spamuje konzolu
            if (sample.RowIndex % 500 == 0)
                Console.WriteLine($"[SERVER] prenos u toku... row={sample.RowIndex}");

            return new AckResponse
            {
                Success      = true,
                Status       = SessionStatus.IN_PROGRESS,
                LastRowIndex = sample.RowIndex
            };
        }

        // ----------------------------------------------------------------
        // EndSession
        // ----------------------------------------------------------------
        public AckResponse EndSession()
        {
            Console.WriteLine($"[SERVER] zavrsen prenos: subject={_meta?.ParticipantId}  lastRow={_lastRowIndex}");
            _writer?.Dispose();
            _writer = null;

            return new AckResponse
            {
                Success      = true,
                Status       = SessionStatus.COMPLETED,
                LastRowIndex = _lastRowIndex,
                Message      = "Session completed"
            };
        }

        // ----------------------------------------------------------------
        // Validacija uzorka
        // ----------------------------------------------------------------
        private void ValidateSample(EegSample s)
        {
            if (s == null)
                throw new FaultException<DataFormatFault>(
                    new DataFormatFault { Reason = "Sample je null", Field = "sample", RowIndex = -1 });

            // Monotoni rast RowIndex
            if (s.RowIndex <= _lastRowIndex)
                throw new FaultException<ValidationFault>(
                    new ValidationFault
                    {
                        Reason   = $"RowIndex nije monoton ({s.RowIndex} <= {_lastRowIndex})",
                        Rule     = "RowIndex.Monotonic",
                        RowIndex = s.RowIndex
                    });

            // Validan timestamp
            if (s.Timestamp == default(DateTime))
                throw new FaultException<DataFormatFault>(
                    new DataFormatFault { Reason = "Timestamp nije parsiran", Field = "Timestamp", RowIndex = s.RowIndex });

            // Nenegativne metrike
            if (s.Attention  < 0 || s.Engagement < 0 || s.Excitement < 0 ||
                s.Interest   < 0 || s.Relaxation < 0 || s.Stress     < 0)
                throw new FaultException<ValidationFault>(
                    new ValidationFault
                    {
                        Reason   = "Negativna kognitivna metrika",
                        Rule     = "Metrics.NonNegative",
                        RowIndex = s.RowIndex
                    });

            // Realan opseg baterije
            if (s.Battery < 0 || s.Battery > 100)
                throw new FaultException<ValidationFault>(
                    new ValidationFault
                    {
                        Reason   = $"Battery van opsega: {s.Battery}",
                        Rule     = "Battery.Range",
                        RowIndex = s.RowIndex
                    });

            // Realan opseg ContactQuality
            if (s.ContactQuality < 0 || s.ContactQuality > 100)
                throw new FaultException<ValidationFault>(
                    new ValidationFault
                    {
                        Reason   = $"ContactQuality van opsega: {s.ContactQuality}",
                        Rule     = "ContactQuality.Range",
                        RowIndex = s.RowIndex
                    });
        }

        // ----------------------------------------------------------------
        // Dispose pattern (Task 4)
        // ----------------------------------------------------------------
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _writer?.Dispose();
                _writer = null;
            }
            _disposed = true;
        }

        ~EegService() { Dispose(false); }
    }
}
