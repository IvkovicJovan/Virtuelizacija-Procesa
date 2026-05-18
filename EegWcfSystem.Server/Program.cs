using System;
using System.ServiceModel;
using EegWcfSystem.Server.Services;

namespace EegWcfSystem.Server
{
    public class Program
    {
        public static void Main()
        {
            using (var host = new ServiceHost(typeof(EegService)))
            {
                host.Opened += (s, e) => Console.WriteLine("[SERVER] WCF host otvoren.");
                host.Open();

                Console.WriteLine("[SERVER] EEG servis pokrenut. Endpoint-ovi:");
                foreach (var ep in host.Description.Endpoints)
                    Console.WriteLine($"  {ep.Address}");

                Console.WriteLine("Pritisni ENTER za zatvaranje...");
                Console.ReadLine();
            }
        }
    }
}
