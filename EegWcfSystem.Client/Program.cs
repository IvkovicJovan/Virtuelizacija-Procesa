using System;
using System.Configuration;
using System.Diagnostics;
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
            string configuredRoot = args.Length > 0
                ? args[0]
                : (ConfigurationManager.AppSettings["EegRoot"] ?? "EEG");

            string eegRoot = ResolveEegRoot(configuredRoot);
            if (eegRoot == null)
            {
                Console.WriteLine("[CLIENT] Direktorijum EEG nije pronadjen.");
                Console.WriteLine("[CLIENT] Ocekivano: folder EEG sa fajlovima subject_*_results.csv.");
                Console.WriteLine("[CLIENT] Provjerene putanje: " + configuredRoot + ", bin\\Debug\\EEG, projektni EEG.");
                Console.ReadLine();
                return;
            }

            int simulateBreakAfterRows = ReadIntSetting("SimulateBreakAfterRows", 0);
            string rejectPath = Path.Combine("logs", string.Format("client_rejects_{0:yyyyMMdd_HHmmss}.csv", DateTime.UtcNow));

            using (var rejects = new RejectLogger(rejectPath))
            {
                var files = Directory.GetFiles(eegRoot, "subject_*_results.csv", SearchOption.AllDirectories);

                Console.WriteLine("[CLIENT] EEG folder: " + eegRoot);
                Console.WriteLine("[CLIENT] Pronadjeno fajlova: " + files.Length);

                if (files.Length == 0)
                {
                    Console.WriteLine("[CLIENT] Nema fajlova za obradu.");
                    Console.ReadLine();
                    return;
                }

                foreach (var file in files)
                    ProcessFile(file, rejects, simulateBreakAfterRows);
            }

            Console.WriteLine("[CLIENT] Gotov. ENTER za izlaz.");
            Console.ReadLine();
        }

        private static void ProcessFile(string filePath, RejectLogger rejects, int simulateBreakAfterRows)
        {
            EegCsvReader reader = null;
            ChannelFactory<IEegService> factory = null;
            IEegService proxy = null;
            IClientChannel channel = null;
            SendTimeLogger sendLogger = null;
            bool endSessionCalled = false;

            try
            {
                reader = new EegCsvReader(filePath);
                Console.WriteLine(string.Format("[CLIENT] >>> {0} (subject={1})", reader.FileName, reader.ParticipantId));

                string sendLogPath = Path.Combine("logs", string.Format("send_times_subject_{0}_{1:yyyyMMdd_HHmmss}.csv", reader.ParticipantId, DateTime.UtcNow));
                sendLogger = new SendTimeLogger(sendLogPath);

                factory = new ChannelFactory<IEegService>("EegEndpoint");
                proxy = factory.CreateChannel();
                channel = (IClientChannel)proxy;

                var meta = new EegMeta
                {
                    ParticipantId = reader.ParticipantId,
                    FileName = reader.FileName,
                    TotalRows = reader.TotalRowsApprox(),
                    SchemaVersion = "1.0"
                };

                AckResponse startAck = proxy.StartSession(meta);
                Console.WriteLine(string.Format("[CLIENT] StartSession -> {0} ({1})", startAck.Status, startAck.Message));

                EegSample sample;
                string rawLine;
                string parseErr;
                int sentCount = 0;
                int rejectCount = 0;

                while (reader.TryReadNext(out sample, out rawLine, out parseErr))
                {
                    if (sample == null)
                    {
                        rejects.Write(reader.ParticipantId, -1, parseErr ?? "parse error", rawLine);
                        rejectCount++;
                        continue;
                    }

                    try
                    {
                        var sw = Stopwatch.StartNew();
                        AckResponse ack = proxy.PushSample(sample);
                        sw.Stop();

                        sendLogger.Write(reader.ParticipantId, sample.RowIndex, sw.Elapsed.TotalMilliseconds, ack.Status.ToString());
                        sentCount++;

                        if (simulateBreakAfterRows > 0 && sentCount >= simulateBreakAfterRows)
                            throw new IOException("SIMULACIJA PREKIDA VEZE poslije " + sentCount + " poslatih redova.");
                    }
                    catch (FaultException<ValidationFault> vf)
                    {
                        rejects.Write(reader.ParticipantId, sample.RowIndex,
                            "VALIDATION:" + vf.Detail.Rule + ":" + vf.Detail.Reason, rawLine);
                        rejectCount++;
                    }
                    catch (FaultException<DataFormatFault> df)
                    {
                        rejects.Write(reader.ParticipantId, sample.RowIndex,
                            "DATAFORMAT:" + df.Detail.Field + ":" + df.Detail.Reason, rawLine);
                        rejectCount++;
                    }
                }

                AckResponse done = proxy.EndSession();
                endSessionCalled = true;

                Console.WriteLine(string.Format("[CLIENT] EndSession -> {0} lastRow={1} sent={2} rejects={3}",
                    done.Status, done.LastRowIndex, sentCount, rejectCount));

                CloseChannel(channel, factory);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[CLIENT] GRESKA na fajlu " + filePath + ": " + ex.Message);

                if (!endSessionCalled)
                    Console.WriteLine("[CLIENT] Sesija nije normalno zavrsena; Abort oslobadja WCF konekciju.");

                AbortChannel(channel, factory);
            }
            finally
            {
                // Dokaz Dispose pattern-a: čak i kod exception-a zatvaraju se CSV reader i log streamovi.
                sendLogger?.Dispose();
                reader?.Dispose();
            }
        }

        private static string ResolveEegRoot(string configuredRoot)
        {
            string[] candidates = new[]
            {
                configuredRoot,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configuredRoot),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EEG"),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", configuredRoot)),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "EEG"))
            };

            foreach (var c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c) && Directory.Exists(c))
                    return Path.GetFullPath(c);
            }
            return null;
        }

        private static int ReadIntSetting(string key, int fallback)
        {
            int value;
            if (int.TryParse(ConfigurationManager.AppSettings[key], out value)) return value;
            return fallback;
        }

        private static void CloseChannel(IClientChannel channel, ChannelFactory<IEegService> factory)
        {
            try
            {
                if (channel != null && channel.State != CommunicationState.Faulted)
                    channel.Close();
                else if (channel != null)
                    channel.Abort();
            }
            catch
            {
                try { channel?.Abort(); } catch { }
            }

            try
            {
                if (factory != null && factory.State != CommunicationState.Faulted)
                    factory.Close();
                else if (factory != null)
                    factory.Abort();
            }
            catch
            {
                try { factory?.Abort(); } catch { }
            }
        }

        private static void AbortChannel(IClientChannel channel, ChannelFactory<IEegService> factory)
        {
            try { channel?.Abort(); } catch { }
            try { factory?.Abort(); } catch { }
        }
    }
}
