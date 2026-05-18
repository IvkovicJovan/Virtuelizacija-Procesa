using System;
using System.Configuration;
using System.IO;
using System.ServiceModel;
using EegWcfSystem.Client.IO;
using EegWcfSystem.Common.Contracts;
using EegWcfSystem.Common.Faults;

namespace EegWcfSystem.Client
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string eegRoot = args.Length > 0
                ? args[0]
                : ConfigurationManager.AppSettings["EegRoot"] ?? "EEG";

            if (!Directory.Exists(eegRoot))
            {
                Console.WriteLine($"[CLIENT] Direktorijum ne postoji: {eegRoot}");
                Console.WriteLine("Napravi folder 'EEG' i ubaci subject_*_results.csv fajlove.");
                Console.ReadLine();
                return;
            }

            var rejectPath = Path.Combine("logs", $"client_rejects_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");

            using (var rejects = new RejectLogger(rejectPath))
            {
                // Rekurzivno traži sve subject_*_results.csv (Task 5)
                var files = Directory.GetFiles(eegRoot, "subject_*_results.csv",
                                               SearchOption.AllDirectories);

                Console.WriteLine($"[CLIENT] Pronađeno fajlova: {files.Length}");
                if (files.Length == 0)
                {
                    Console.WriteLine("[CLIENT] Nema fajlova za obradu.");
                    Console.ReadLine();
                    return;
                }

                foreach (var file in files)
                    ProcessFile(file, rejects);
            }

            Console.WriteLine("[CLIENT] Gotov. ENTER za izlaz.");
            Console.ReadLine();
        }

        private static void ProcessFile(string filePath, RejectLogger rejects)
        {
            EegCsvReader               reader  = null;
            ChannelFactory<IEegService> factory = null;
            IEegService                proxy   = null;

            try
            {
                reader = new EegCsvReader(filePath);
                Console.WriteLine($"[CLIENT] >>> {reader.FileName}  (subject={reader.ParticipantId})");

                factory = new ChannelFactory<IEegService>("EegEndpoint");
                proxy   = factory.CreateChannel();

                // Metadata paket
                var meta = new EegMeta
                {
                    ParticipantId = reader.ParticipantId,
                    FileName      = reader.FileName,
                    TotalRows     = reader.TotalRowsApprox(),
                    SchemaVersion = "1.0"
                };

                var ack = proxy.StartSession(meta);
                Console.WriteLine($"[CLIENT] StartSession -> {ack.Status}  ({ack.Message})");

                // Sekvencijalni send (Task 2 / streaming semantika)
                EegSample sample;
                string    rawLine, parseErr;
                int       sentCount   = 0;
                int       rejectCount = 0;

                while (reader.TryReadNext(out sample, out rawLine, out parseErr))
                {
                    if (sample == null)
                    {
                        // Klijentski parse error
                        rejects.Write(reader.ParticipantId, -1,
                            parseErr ?? "parse error", rawLine);
                        rejectCount++;
                        continue;
                    }

                    try
                    {
                        proxy.PushSample(sample);
                        sentCount++;
                    }
                    catch (FaultException<ValidationFault> vf)
                    {
                        rejects.Write(reader.ParticipantId, sample.RowIndex,
                            $"VALIDATION:{vf.Detail.Rule}:{vf.Detail.Reason}", rawLine);
                        rejectCount++;
                    }
                    catch (FaultException<DataFormatFault> df)
                    {
                        rejects.Write(reader.ParticipantId, sample.RowIndex,
                            $"DATAFORMAT:{df.Detail.Field}:{df.Detail.Reason}", rawLine);
                        rejectCount++;
                    }
                }

                var done = proxy.EndSession();
                Console.WriteLine($"[CLIENT] EndSession -> {done.Status}  " +
                                  $"lastRow={done.LastRowIndex}  " +
                                  $"sent={sentCount}  rejects={rejectCount}");

                ((IClientChannel)proxy).Close();
                factory.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLIENT] GREŠKA na fajlu {filePath}: {ex.Message}");
                try { ((IClientChannel)proxy)?.Abort(); }  catch { /* ignore */ }
                try { factory?.Abort(); }                  catch { /* ignore */ }
            }
            finally
            {
                reader?.Dispose();   // dokaz Dispose pattern-a (Task 4)
            }
        }
    }
}
