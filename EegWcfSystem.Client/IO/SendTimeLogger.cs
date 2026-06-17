using System;
using System.Globalization;
using System.IO;

namespace EegWcfSystem.Client.IO
{
    // Zadatak 7: klijent lokalno bilježi vrijeme slanja svakog reda.
    public class SendTimeLogger : IDisposable
    {
        private readonly FileStream _fs;
        private readonly StreamWriter _sw;
        private bool _disposed;

        public SendTimeLogger(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            _fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _sw = new StreamWriter(_fs) { AutoFlush = true };
            _sw.WriteLine("UtcSendTime,ParticipantId,RowIndex,ElapsedMs,AckStatus");
        }

        public void Write(int participantId, int rowIndex, double elapsedMs, string ackStatus)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SendTimeLogger));
            _sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0:o},{1},{2},{3},{4}",
                DateTime.UtcNow, participantId, rowIndex, elapsedMs, ackStatus ?? string.Empty));
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
                _sw?.Dispose();
                _fs?.Dispose();
            }
            _disposed = true;
        }

        ~SendTimeLogger()
        {
            Dispose(false);
        }
    }
}
