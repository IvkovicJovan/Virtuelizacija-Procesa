using System;
using System.IO;

namespace EegWcfSystem.Client.IO
{
    public class RejectLogger : IDisposable
    {
        private FileStream   _fs;
        private StreamWriter _sw;
        private bool         _disposed;

        public RejectLogger(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            _fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _sw = new StreamWriter(_fs) { AutoFlush = true };
            _sw.WriteLine("UtcTime,ParticipantId,RowIndex,Reason,RawLine");
        }

        public void Write(int participantId, int rowIndex, string reason, string rawLine)
        {
            var safeRaw    = (rawLine ?? "").Replace("\"", "\"\"");
            var safeReason = (reason  ?? "").Replace("\"", "\"\"");
            _sw.WriteLine($"{DateTime.UtcNow:o},{participantId},{rowIndex},\"{safeReason}\",\"{safeRaw}\"");
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
                _sw?.Dispose();
                _fs?.Dispose();
            }
            _disposed = true;
        }

        ~RejectLogger() { Dispose(false); }
    }
}
