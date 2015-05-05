using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace TestHttp2
{
    class Program {

        static void Main(string[] args) {

            Console.WriteLine("TestHttp2");
            Console.WriteLine("Tests http2/spdy support via NPN, ALPN, HTTP2 Direct, HTTP2 Updrade");
            Console.WriteLine("Usage: ");
            Console.WriteLine("  TestHttp2.exe --host <host>    to test signgle host");
            Console.WriteLine("  TestHttp2.exe --top-1m         to test Alexa million sites");
            Console.WriteLine();

            if (args.Length == 0) return;

            if ((args[0] == "--host") && (args.Length > 1)) {
                var host = args[1];
                var hostEntry = Dns.GetHostEntry(host);
                var result = ProtocolExtentions.TestHost(hostEntry).Result;
                PrintResult(result.Item2);
            }

            if (args[0] == "--top-1m") {
                Crawl();
            }
            
            Console.WriteLine("Finished. Press any key...");
            Console.ReadLine();

        }

        private static readonly Dictionary<ProtocolTests, string>  Protocols = new Dictionary<ProtocolTests, string>() {
                { ProtocolTests.Spdy1,    "spdy/1"   },
                { ProtocolTests.Spdy2,    "spdy/2"   },
                { ProtocolTests.Spdy3,    "spdy/3"   },
                { ProtocolTests.Spdy3_1,  "spdy/3.1" },
                { ProtocolTests.Http2,    "h2"       },
                { ProtocolTests.Http2c,   "h2c"      },
            };
        private static readonly Dictionary<ProtocolTests, string> Advertising = new Dictionary<ProtocolTests, string>() {
                { ProtocolTests.NPN,           "NPN"           },
                { ProtocolTests.ALPN,          "ALPN"          },
                { ProtocolTests.Http2_Upgrade, "HTTP2 Upgrade" },
                { ProtocolTests.Http2_Direct,  "HTTP2 Direct"  }
            };


        public static void PrintResult(ProtocolTests tests) {

            if (tests == ProtocolTests.None) {
                Console.WriteLine("Has no support for http2/spdy");
                return;
            }

            Console.WriteLine("Supports protocols: "+string.Join(", ", Protocols.Where(x => (tests & x.Key) > 0).Select(x => x.Value)));
            Console.WriteLine("Advertised via: " + string.Join(", ", Advertising.Where(x => (tests & x.Key) > 0).Select(x => x.Value)));
            Console.WriteLine();
        }
        
        public static string GetProtocolTestName(int idx) {
            var t = (ProtocolTests) (1 << idx);
            string result;
            if (Protocols.TryGetValue(t, out result)) return result;
            if (Advertising.TryGetValue(t, out result)) return result;
            return null;
        }
        
        public static void PrintStats(int[] stats, int count, TimeSpan elapsed) {
            Console.WriteLine("Stats for top {0} sites ({1:g}):", count, elapsed);
            
            var protocolStats = stats.Select((x, idx) => Tuple.Create(GetProtocolTestName(idx), x))
                                     .Where(t => !string.IsNullOrEmpty(t.Item1))
                                     .Select(t => string.Format("{0,-13} {1,7} {2,5:N1}%", t.Item1, t.Item2, ((double)(100 * t.Item2)) / (double)(count)));
            Console.WriteLine(string.Join("\r\n", protocolStats));
            
            int workerThreads, ioTreads;
            ThreadPool.GetAvailableThreads(out workerThreads, out ioTreads);
            Console.WriteLine("Worker threads: {0}, IO threads :{1}", workerThreads, ioTreads);
            
            Console.WriteLine();
        }


        private static void Crawl() {
            var lockObj = new object();
            var stats = new int[11];
            var cnt = 0;
            var sw = new Stopwatch();
            var outfile = File.CreateText("output.csv");
            Action<Tuple<string,ProtocolTests>> logAction = (x => 
            {
                lock (lockObj) {
                    cnt++;
                    for (var i = 0; i < stats.Length; i++) {
                        if (((int)(x.Item2) & (1 << i)) != 0) stats[i]++;
                    }
                    outfile.WriteLine("{0}\t{1}", x.Item1, (int)(x.Item2));
                    
                    if (cnt%1000 != 0) return;
                    
                    PrintStats(stats, cnt, sw.Elapsed);
                    outfile.Flush();
                }
            });
            

            var pipeline = TaskUtils.GetAlexaTop1mlnUrls("http://s3.amazonaws.com/alexa-static/top-1m.csv.zip")
                                    .Select(Dns.GetHostEntryAsync)
                                    .Throttle(3000)
                                    .Select(host => ProtocolExtentions.TestHost(host.Result)
                                                                      .ContinueWith(x =>logAction(x.Result)))
                                    .Throttle(6000);
            sw.Start();
            pipeline.Run();
            sw.Stop();
            outfile.Close();
        }


    }
}
