using System;
using System.Configuration;
using System.Globalization;
using System.ServiceModel;
using EegWcfSystem.Common.Contracts;
using EegWcfSystem.Common.Faults;
using EegWcfSystem.Server.Storage;

namespace EegWcfSystem.Server.Services
{
    // Singleton namjerno: kod ovog školskog projekta postoji jedan aktivan prenos u trenutku.
    // Time se izbjegava česta greška kod Streamed/PerCall režima: PushSample ne vidi StartSession stanje.
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single,
                     ConcurrencyMode = ConcurrencyMode.Single)]
    public class EegService : IEegService, IDisposable
    {
        private readonly object _sync = new object();

        private EegMeta _meta;
        private int _lastRowIndex = -1;
        private SessionWriter _writer;
        private EegSample _previousSample;
        private DateTime? _previousTimestamp;
        private bool _disposed;

        private readonly double _channelMin;
        private readonly double _channelMax;
        private readonly int _batteryLow;
        private readonly int _contactQualityMin;
        private readonly double _excitementSpikeThreshold;
        private readonly int _timestampSkewMaxMs;

        // Zadatak 8: delegati i događaji.
        public event Action<EegMeta> OnTransferStarted;
        public event Action<EegSample> OnSampleReceived;
        public event Action<EegMeta, int> OnTransferCompleted;
        public event Action<string> OnWarningRaised;

        public EegService()
        {
            _channelMin = ReadDouble("ChannelMinValue", 0);
            _channelMax = ReadDouble("ChannelMaxValue", 10000);
            _batteryLow = ReadInt("BatteryLowThreshold", 20);
            _contactQualityMin = ReadInt("ContactQualityMin", 50);
            _excitementSpikeThreshold = ReadDouble("ExcitementSpikeThreshold", 0.30);
            _timestampSkewMaxMs = ReadInt("TimestampSkewMaxMs", 2000);

            // Dinamička registracija pretplatnika preko +=.
            OnTransferStarted += ConsoleTransferStarted;
            OnSampleReceived += ConsoleSampleReceived;
            OnTransferCompleted += ConsoleTransferCompleted;
            OnWarningRaised += ConsoleWarningRaised;

            // Demonstracija da je podržana i odjava pretplatnika preko -=.
            OnWarningRaised += TemporaryWarningSubscriber;
            OnWarningRaised -= TemporaryWarningSubscriber;
        }

        public AckResponse StartSession(EegMeta meta)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                ValidateMeta(meta);

                if (_writer != null)
                {
                    // Ako je prethodni prenos prekinut, zatvori stare resurse prije nove sesije.
                    _writer.Dispose();
                    _writer = null;
                }

                _meta = meta;
                _lastRowIndex = -1;
                _previousSample = null;
                _previousTimestamp = null;
                _writer = new SessionWriter(meta);

                OnTransferStarted?.Invoke(meta);

                return new AckResponse
                {
                    Success = true,
                    Status = SessionStatus.IN_PROGRESS,
                    Message = "ACK: Session started",
                    LastRowIndex = -1
                };
            }
        }

        public AckResponse PushSample(EegSample sample)
        {
            lock (_sync)
            {
                ThrowIfDisposed();

                if (_meta == null || _writer == null)
                {
                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Reason = "PushSample je pozvan prije StartSession",
                            Rule = "Session.NotStarted",
                            RowIndex = sample == null ? -1 : sample.RowIndex
                        },
                        new FaultReason("StartSession nije pozvan"));
                }

                ValidateSampleOrThrow(sample);

                // Validan sample se upisuje u session.csv.
                _writer.AppendSample(sample);
                _lastRowIndex = sample.RowIndex;

                OnSampleReceived?.Invoke(sample);

                // Zadatak 9 i 10: analitika i warning-i.
                AnalyzeWarnings(sample);

                _previousSample = sample;
                _previousTimestamp = sample.Timestamp;

                return new AckResponse
                {
                    Success = true,
                    Status = SessionStatus.IN_PROGRESS,
                    Message = "ACK: Sample accepted",
                    LastRowIndex = sample.RowIndex
                };
            }
        }

        public AckResponse EndSession()
        {
            lock (_sync)
            {
                ThrowIfDisposed();

                if (_meta == null)
                {
                    throw new FaultException<ValidationFault>(
                        new ValidationFault
                        {
                            Reason = "EndSession je pozvan bez aktivne sesije",
                            Rule = "Session.NotStarted",
                            RowIndex = -1
                        },
                        new FaultReason("StartSession nije pozvan"));
                }

                var finishedMeta = _meta;
                int last = _lastRowIndex;

                _writer?.Dispose();
                _writer = null;
                _meta = null;
                _previousSample = null;
                _previousTimestamp = null;
                _lastRowIndex = -1;

                OnTransferCompleted?.Invoke(finishedMeta, last);

                return new AckResponse
                {
                    Success = true,
                    Status = SessionStatus.COMPLETED,
                    Message = "ACK: Session completed",
                    LastRowIndex = last
                };
            }
        }

        private void ValidateMeta(EegMeta meta)
        {
            if (meta == null)
                ThrowValidation(-1, "Meta paket ne postoji", "StartSession.MetaRequired", null);

            if (meta.ParticipantId <= 0)
                ThrowValidation(-1, "ParticipantId mora biti pozitivan", "StartSession.ParticipantId", null);

            if (string.IsNullOrWhiteSpace(meta.FileName))
                ThrowValidation(-1, "FileName je obavezan", "StartSession.FileName", null);

            if (meta.TotalRows < 0)
                ThrowValidation(-1, "TotalRows ne može biti negativan", "StartSession.TotalRows", null);

            if (string.IsNullOrWhiteSpace(meta.SchemaVersion))
                ThrowValidation(-1, "SchemaVersion je obavezan", "StartSession.SchemaVersion", null);
        }

        private void ValidateSampleOrThrow(EegSample s)
        {
            if (s == null)
                ThrowDataFormat(-1, "sample", "Sample je null", string.Empty);

            if (s.RowIndex <= _lastRowIndex)
                ThrowValidation(s.RowIndex,
                    $"RowIndex nije monoton ({s.RowIndex} <= {_lastRowIndex})",
                    "RowIndex.Monotonic", SessionWriter.ToCsvLine(s));

            if (s.Timestamp == default(DateTime))
                ThrowDataFormat(s.RowIndex, "Timestamp", "Timestamp nije validan", SessionWriter.ToCsvLine(s));

            if (double.IsNaN(s.AF3) || double.IsNaN(s.T7) || double.IsNaN(s.Pz) || double.IsNaN(s.T8) || double.IsNaN(s.AF4))
                ThrowDataFormat(s.RowIndex, "EEGChannel", "Jedan od EEG kanala nije broj", SessionWriter.ToCsvLine(s));

            if (s.Attention < 0 || s.Engagement < 0 || s.Excitement < 0 ||
                s.Interest < 0 || s.Relaxation < 0 || s.Stress < 0)
                ThrowValidation(s.RowIndex, "Kognitivne metrike moraju biti nenegativne",
                    "Metrics.NonNegative", SessionWriter.ToCsvLine(s));

            if (s.Battery < 0 || s.Battery > 100)
                ThrowValidation(s.RowIndex, $"Battery van realnog opsega: {s.Battery}",
                    "Battery.Range", SessionWriter.ToCsvLine(s));

            if (s.ContactQuality < 0 || s.ContactQuality > 100)
                ThrowValidation(s.RowIndex, $"ContactQuality van realnog opsega: {s.ContactQuality}",
                    "ContactQuality.Range", SessionWriter.ToCsvLine(s));
        }

        private void AnalyzeWarnings(EegSample s)
        {
            // Zadatak 10: kvalitet kontakta, baterija i saturacija kanala.
            if (s.ContactQuality < _contactQualityMin)
            {
                RaiseWarning(s, $"PoorContactWarning: ParticipantId={_meta.ParticipantId}, RowIndex={s.RowIndex}, Timestamp={s.Timestamp:dd/MM/yyyy HH:mm:ss}, ContactQuality={s.ContactQuality}, prag={_contactQualityMin}");
            }

            if (s.Battery < _batteryLow)
            {
                RaiseWarning(s, $"LowBatteryWarning: ParticipantId={_meta.ParticipantId}, RowIndex={s.RowIndex}, Timestamp={s.Timestamp:dd/MM/yyyy HH:mm:ss}, Battery={s.Battery}, prag={_batteryLow}");
            }

            CheckChannel(s, "AF3", s.AF3);
            CheckChannel(s, "T7", s.T7);
            CheckChannel(s, "Pz", s.Pz);
            CheckChannel(s, "T8", s.T8);
            CheckChannel(s, "AF4", s.AF4);

            // Zadatak 9: delta Excitement i delta Interest nad uzastopnim mjerenjima.
            if (_previousSample != null)
            {
                double deltaExcitement = s.Excitement - _previousSample.Excitement;
                if (Math.Abs(deltaExcitement) > _excitementSpikeThreshold)
                {
                    string direction = deltaExcitement > 0 ? "rast" : "pad";
                    RaiseWarning(s,
                        $"ExcitementSpike: ParticipantId={_meta.ParticipantId}, RowIndex={s.RowIndex}, Timestamp={s.Timestamp:dd/MM/yyyy HH:mm:ss}, smjer={direction}, prije={_previousSample.Excitement.ToString(CultureInfo.InvariantCulture)}, poslije={s.Excitement.ToString(CultureInfo.InvariantCulture)}, delta={deltaExcitement.ToString(CultureInfo.InvariantCulture)}");
                }

                double deltaInterest = s.Interest - _previousSample.Interest;
                if (Math.Abs(deltaInterest) > _excitementSpikeThreshold)
                {
                    string direction = deltaInterest > 0 ? "rast" : "pad";
                    RaiseWarning(s,
                        $"InterestChange: ParticipantId={_meta.ParticipantId}, RowIndex={s.RowIndex}, Timestamp={s.Timestamp:dd/MM/yyyy HH:mm:ss}, smjer={direction}, prije={_previousSample.Interest.ToString(CultureInfo.InvariantCulture)}, poslije={s.Interest.ToString(CultureInfo.InvariantCulture)}, delta={deltaInterest.ToString(CultureInfo.InvariantCulture)}");
                }
            }

            if (_previousTimestamp.HasValue)
            {
                double diffMs = Math.Abs((s.Timestamp - _previousTimestamp.Value).TotalMilliseconds);
                if (diffMs > _timestampSkewMaxMs)
                {
                    RaiseWarning(s,
                        $"TimestampSkewWarning: ParticipantId={_meta.ParticipantId}, RowIndex={s.RowIndex}, Timestamp={s.Timestamp:dd/MM/yyyy HH:mm:ss}, razlikaMs={diffMs.ToString(CultureInfo.InvariantCulture)}, pragMs={_timestampSkewMaxMs}");
                }
            }
        }

        private void CheckChannel(EegSample s, string channelName, double value)
        {
            if (value < _channelMin || value > _channelMax)
            {
                RaiseWarning(s,
                    $"ChannelSaturationWarning: ParticipantId={_meta.ParticipantId}, RowIndex={s.RowIndex}, Timestamp={s.Timestamp:dd/MM/yyyy HH:mm:ss}, kanal={channelName}, vrijednost={value.ToString(CultureInfo.InvariantCulture)}, opseg=[{_channelMin.ToString(CultureInfo.InvariantCulture)}, {_channelMax.ToString(CultureInfo.InvariantCulture)}]");
            }
        }

        private void RaiseWarning(EegSample s, string message)
        {
            _writer?.AppendReject(_meta.ParticipantId, s.RowIndex, message, SessionWriter.ToCsvLine(s));
            OnWarningRaised?.Invoke(message);
        }

        private void ThrowValidation(int rowIndex, string reason, string rule, string rawLine)
        {
            if (_writer != null && _meta != null)
                _writer.AppendReject(_meta.ParticipantId, rowIndex, $"ValidationFault:{rule}:{reason}", rawLine ?? string.Empty);

            throw new FaultException<ValidationFault>(
                new ValidationFault
                {
                    Reason = reason,
                    Rule = rule,
                    RowIndex = rowIndex
                },
                new FaultReason(reason));
        }

        private void ThrowDataFormat(int rowIndex, string field, string reason, string rawLine)
        {
            if (_writer != null && _meta != null)
                _writer.AppendReject(_meta.ParticipantId, rowIndex, $"DataFormatFault:{field}:{reason}", rawLine ?? string.Empty);

            throw new FaultException<DataFormatFault>(
                new DataFormatFault
                {
                    Reason = reason,
                    Field = field,
                    RowIndex = rowIndex
                },
                new FaultReason(reason));
        }

        private static double ReadDouble(string key, double fallback)
        {
            double value;
            if (double.TryParse(ConfigurationManager.AppSettings[key], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;
            return fallback;
        }

        private static int ReadInt(string key, int fallback)
        {
            int value;
            if (int.TryParse(ConfigurationManager.AppSettings[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return value;
            return fallback;
        }

        private void ConsoleTransferStarted(EegMeta meta)
        {
            Console.WriteLine($"[EVENT] OnTransferStarted: subject={meta.ParticipantId}, file={meta.FileName}, rows={meta.TotalRows}");
            Console.WriteLine("[SERVER] prenos u toku...");
            Console.WriteLine($"[SERVER] session.csv i rejects.csv se nalaze u: Data/{meta.ParticipantId}/{DateTime.Now:yyyy-MM-dd}/");
        }

        private void ConsoleSampleReceived(EegSample sample)
        {
            if (sample.RowIndex == 1 || sample.RowIndex % 500 == 0)
                Console.WriteLine($"[EVENT] OnSampleReceived: row={sample.RowIndex}");
        }

        private void ConsoleTransferCompleted(EegMeta meta, int lastRow)
        {
            Console.WriteLine($"[EVENT] OnTransferCompleted: subject={meta.ParticipantId}, lastRow={lastRow}");
            Console.WriteLine("[SERVER] zavrsen prenos.");
        }

        private void ConsoleWarningRaised(string message)
        {
            Console.WriteLine("[EVENT] OnWarningRaised: " + message);
        }

        private void TemporaryWarningSubscriber(string message)
        {
            // Namjerno prazno: koristi se samo da se u kodu vidi dinamička odjava preko -=.
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EegService));
        }

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

        ~EegService()
        {
            Dispose(false);
        }
    }
}
