using System;
using System.Globalization;
using System.IO;
using EegWcfSystem.Common.Contracts;

namespace EegWcfSystem.Server.Storage
{
    // Zadatak 6: serversko skladištenje u Data/<ParticipantId>/<YYYY-MM-DD>/.
    // Klasa namjerno implementira IDisposable jer drži FileStream/StreamWriter resurse.
    public class SessionWriter : IDisposable
    {
        private readonly FileStream _sessionStream;
        private readonly StreamWriter _sessionWriter;
        private readonly FileStream _rejectStream;
        private readonly StreamWriter _rejectWriter;
        private bool _disposed;

        public string SessionPath { get; private set; }
        public string RejectsPath { get; private set; }

        public SessionWriter(EegMeta meta)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            string dir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Data",
                meta.ParticipantId.ToString(CultureInfo.InvariantCulture),
                DateTime.Now.ToString("yyyy-MM-dd"));

            Directory.CreateDirectory(dir);

            SessionPath = Path.Combine(dir, "session.csv");
            RejectsPath = Path.Combine(dir, "rejects.csv");

            _sessionStream = new FileStream(SessionPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _sessionWriter = new StreamWriter(_sessionStream) { AutoFlush = true };
            _sessionWriter.WriteLine("RowIndex,Timestamp,AF3,T7,Pz,T8,AF4,Attention,Engagement,Excitement,Interest,Relaxation,Stress,Battery,ContactQuality,SlideIndex,SetIndex");

            _rejectStream = new FileStream(RejectsPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _rejectWriter = new StreamWriter(_rejectStream) { AutoFlush = true };
            _rejectWriter.WriteLine("UtcTime,ParticipantId,RowIndex,Reason,RawLine");
        }

        public void AppendSample(EegSample s)
        {
            ThrowIfDisposed();
            if (s == null) throw new ArgumentNullException(nameof(s));
            _sessionWriter.WriteLine(ToCsvLine(s));
        }

        public void AppendReject(int participantId, int rowIndex, string reason, string rawLine)
        {
            ThrowIfDisposed();
            string safeReason = Escape(reason);
            string safeRaw = Escape(rawLine);
            _rejectWriter.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0:o},{1},{2},\"{3}\",\"{4}\"",
                DateTime.UtcNow, participantId, rowIndex, safeReason, safeRaw));
        }

        public static string ToCsvLine(EegSample s)
        {
            var ci = CultureInfo.InvariantCulture;
            return string.Join(",", new[]
            {
                s.RowIndex.ToString(ci),
                s.Timestamp.ToString("dd/MM/yyyy HH:mm:ss", ci),
                s.AF3.ToString(ci),
                s.T7.ToString(ci),
                s.Pz.ToString(ci),
                s.T8.ToString(ci),
                s.AF4.ToString(ci),
                s.Attention.ToString(ci),
                s.Engagement.ToString(ci),
                s.Excitement.ToString(ci),
                s.Interest.ToString(ci),
                s.Relaxation.ToString(ci),
                s.Stress.ToString(ci),
                s.Battery.ToString(ci),
                s.ContactQuality.ToString(ci),
                s.SlideIndex.ToString(ci),
                s.SetIndex.ToString(ci)
            });
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\"", "\"\"");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SessionWriter));
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
                _sessionWriter?.Flush();
                _rejectWriter?.Flush();
                _sessionWriter?.Dispose();
                _rejectWriter?.Dispose();
                _sessionStream?.Dispose();
                _rejectStream?.Dispose();
            }
            _disposed = true;
        }

        ~SessionWriter()
        {
            Dispose(false);
        }
    }
}
