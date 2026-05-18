using System;
using System.IO;
using EegWcfSystem.Common.Contracts;

namespace EegWcfSystem.Server.Storage
{
    // CP1: prazna implementacija (samo Dispose pattern radi).
    // CP2 (Task 6): popunjava session.csv i rejects.csv u Data/<id>/<date>/.
    public class SessionWriter : IDisposable
    {
        private FileStream   _fs;
        private StreamWriter _sw;
        private bool         _disposed;

        public SessionWriter(EegMeta meta)
        {
            // CP2: otvori file stream ovde
            // var dir = Path.Combine("Data",
            //               meta.ParticipantId.ToString(),
            //               DateTime.UtcNow.ToString("yyyy-MM-dd"));
            // Directory.CreateDirectory(dir);
            // _fs = new FileStream(Path.Combine(dir, "session.csv"),
            //             FileMode.Create, FileAccess.Write, FileShare.Read);
            // _sw = new StreamWriter(_fs);
            // _sw.WriteLine("RowIndex,Timestamp,AF3,T7,Pz,T8,AF4," +
            //               "Attention,Engagement,Excitement,Interest,Relaxation,Stress," +
            //               "Battery,ContactQuality,SlideIndex,SetIndex");
        }

        public void AppendSample(EegSample s)
        {
            // CP2:
            // _sw.WriteLine(string.Join(",",
            //     s.RowIndex,
            //     s.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"),
            //     s.AF3.ToString(System.Globalization.CultureInfo.InvariantCulture),
            //     s.T7 .ToString(System.Globalization.CultureInfo.InvariantCulture),
            //     s.Pz .ToString(System.Globalization.CultureInfo.InvariantCulture),
            //     s.T8 .ToString(System.Globalization.CultureInfo.InvariantCulture),
            //     s.AF4.ToString(System.Globalization.CultureInfo.InvariantCulture),
            //     s.Attention  .ToString(System.Globalization.CultureInfo.InvariantCulture),
            //     s.Engagement .ToString(System.Globalization.CultureInfo.InvariantCulture),
            //     s.Excitement .ToString(System.Globalization.CultureInfo.InvariantCulture),
            //     s.Interest   .ToString(System.Globalization.CultureInfo.InvariantCulture),
            //     s.Relaxation .ToString(System.Globalization.CultureInfo.InvariantCulture),
            //     s.Stress     .ToString(System.Globalization.CultureInfo.InvariantCulture),
            //     s.Battery, s.ContactQuality, s.SlideIndex, s.SetIndex));
        }

        public void AppendReject(string rawLine, string reason)
        {
            // CP2: pisanje u rejects.csv
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
                _sw?.Flush();
                _sw?.Dispose();
                _fs?.Dispose();
            }
            _disposed = true;
        }

        ~SessionWriter() { Dispose(false); }
    }
}
