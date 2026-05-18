using System;
using System.Globalization;
using System.IO;
using EegWcfSystem.Common.Contracts;

namespace EegWcfSystem.Client.IO
{
    public class EegCsvReader : IDisposable
    {
        private FileStream   _fs;
        private StreamReader _sr;
        private bool         _disposed;
        private int          _rowIndex;

        private static readonly string[] ExpectedHeader = new[]
        {
            "Timestamp","AF3","T7","Pz","T8","AF4",
            "Attention","Engagement","Excitement","Interest","Relaxation","Stress",
            "Battery","ContactQuality","SlideIndex","SetIndex"
        };

        public int    ParticipantId { get; }
        public string FileName      { get; }
        public string FilePath      { get; }

        public EegCsvReader(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))              throw new FileNotFoundException("CSV ne postoji", filePath);

            FilePath      = filePath;
            FileName      = Path.GetFileName(filePath);
            ParticipantId = ExtractParticipantId(FileName);

            _fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _sr = new StreamReader(_fs);

            ValidateHeader();
        }

        private static int ExtractParticipantId(string fileName)
        {
            // subject_19_results.csv -> 19
            var name  = Path.GetFileNameWithoutExtension(fileName);
            var parts = name.Split('_');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var id)) return id;
            throw new FormatException($"Ne mogu da izvucem ParticipantId iz '{fileName}'");
        }

        private void ValidateHeader()
        {
            var header = _sr.ReadLine();
            if (header == null) throw new InvalidDataException("CSV je prazan");

            var cols = header.Split(',');
            if (cols.Length != ExpectedHeader.Length)
                throw new InvalidDataException(
                    $"Pogresan broj kolona: ima {cols.Length}, ocekujemo {ExpectedHeader.Length}");

            for (int i = 0; i < ExpectedHeader.Length; i++)
            {
                if (!string.Equals(cols[i].Trim(), ExpectedHeader[i], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        $"Kolona {i} je '{cols[i]}', ocekujem '{ExpectedHeader[i]}'");
            }
        }

        /// <summary>
        /// Vraca sledeci red kao EegSample.
        /// Ako red ne moze da se parsira, rawLine sadrzi original a sample je null
        /// — pozivalac upisuje u reject log.
        /// Vraca false kad nema vise redova.
        /// </summary>
        public bool TryReadNext(out EegSample sample, out string rawLine, out string errorMessage)
        {
            sample       = null;
            rawLine      = null;
            errorMessage = null;

            if (_disposed) throw new ObjectDisposedException(nameof(EegCsvReader));
            if (_sr.EndOfStream) return false;

            rawLine = _sr.ReadLine();

            // preskoci prazne redove rekurzivno
            if (string.IsNullOrWhiteSpace(rawLine))
                return TryReadNext(out sample, out rawLine, out errorMessage);

            var c = rawLine.Split(',');
            if (c.Length != ExpectedHeader.Length)
            {
                errorMessage = $"Broj kolona {c.Length} != {ExpectedHeader.Length}";
                _rowIndex++;
                return true;   // vraćamo true, ali sample je null
            }

            try
            {
                var ci = CultureInfo.InvariantCulture;
                sample = new EegSample
                {
                    RowIndex       = _rowIndex,
                    Timestamp      = DateTime.ParseExact(c[0].Trim(), "dd/MM/yyyy HH:mm:ss", ci),
                    AF3            = double.Parse(c[1],  ci),
                    T7             = double.Parse(c[2],  ci),
                    Pz             = double.Parse(c[3],  ci),
                    T8             = double.Parse(c[4],  ci),
                    AF4            = double.Parse(c[5],  ci),
                    Attention      = double.Parse(c[6],  ci),
                    Engagement     = double.Parse(c[7],  ci),
                    Excitement     = double.Parse(c[8],  ci),
                    Interest       = double.Parse(c[9],  ci),
                    Relaxation     = double.Parse(c[10], ci),
                    Stress         = double.Parse(c[11], ci),
                    Battery        = int.Parse(c[12],    ci),
                    ContactQuality = int.Parse(c[13],    ci),
                    SlideIndex     = int.Parse(c[14],    ci),
                    SetIndex       = int.Parse(c[15],    ci)
                };
            }
            catch (Exception ex)
            {
                sample       = null;
                errorMessage = ex.Message;
            }

            _rowIndex++;
            return true;
        }

        /// <summary>
        /// Brzo prebrojavanje redova bez učitavanja celog fajla u memoriju.
        /// Korisno za EegMeta.TotalRows.
        /// </summary>
        public int TotalRowsApprox()
        {
            using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sr = new StreamReader(fs))
            {
                int count = 0;
                sr.ReadLine(); // preskoči header
                while (sr.ReadLine() != null) count++;
                return count;
            }
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
                _sr?.Dispose();
                _fs?.Dispose();
            }
            _disposed = true;
        }

        ~EegCsvReader() { Dispose(false); }
    }
}
