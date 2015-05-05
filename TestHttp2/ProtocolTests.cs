using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TestHttp2.TestProtocols;

namespace TestHttp2
{

    [Flags]
    public enum ProtocolTests {
        None     = 0,
        Http2    = 1 << 1,
        Http2c   = 1 << 2,
        Spdy1    = 1 << 3, 
        Spdy2    = 1 << 4,
        Spdy3    = 1 << 5,
        Spdy3_1  = 1 << 6,
        NPN      = 1 << 7,
        ALPN     = 1 << 8,
        Http2_Upgrade = 1 << 9,
        Http2_Direct  = 1 << 10
    }


    public interface IProtocolTest {
        int Port { get; }
        byte[] GetRequestBody(string host);
        ProtocolTests TestResult(byte[] response, int length);
    }

    public static class ProtocolExtentions {

        public static readonly IProtocolTest[] TestSuite = new IProtocolTest[] {
            new Tls12Npn(),
            new Tls12Alpn(),
            new Http2Upgrade(),
            new Http2Direct()
        };

        public static Dictionary<string, ProtocolTests> KnownProtocols = new Dictionary<string, ProtocolTests>() {
            {@"http/1.0", ProtocolTests.None},
            {@"http/1.1", ProtocolTests.None},
            {@"h2",       ProtocolTests.Http2},
            {@"h2c",      ProtocolTests.Http2c},
            {@"h2-12",    ProtocolTests.Http2},
            {@"h2-13",    ProtocolTests.Http2},
            {@"h2-14",    ProtocolTests.Http2},
            {@"h2-15",    ProtocolTests.Http2},
            {@"h2-16",    ProtocolTests.Http2},
            {@"h2-17",    ProtocolTests.Http2},
            {@"spdy/1",   ProtocolTests.Spdy1},
            {@"spdy/2",   ProtocolTests.Spdy2},
            {@"spdy/3",   ProtocolTests.Spdy3},
            {@"spdy/3.1", ProtocolTests.Spdy3_1},
            {@"spdy/3.1-fb-0.5", ProtocolTests.Spdy3_1}
        };

        
        public static async Task<Tuple<string,ProtocolTests>> TestHost(IPHostEntry host) {
            var tests = TestSuite.Select(x=>TestHostProtocol(host,x));
            return await Task.WhenAll(tests)
                             .ContinueWith(tasks => Tuple.Create(host.HostName, tasks.Result.Aggregate((x, y) => x | y)));
        }


        public static async Task<ProtocolTests> TestHostProtocol(IPHostEntry host, IProtocolTest test) {
            var timeout = TimeSpan.FromSeconds(0.2);
            var response = new byte[10240];
            var result = ProtocolTests.None;
            try {
                using (var client = new TcpClient()) {
                    var ar = client.BeginConnect(host.AddressList[0], test.Port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeout, false)) {
                        client.Close();
                        return result;
                    }
                    client.EndConnect(ar);

                    using (var stream = client.GetStream()) {
                        stream.ReadTimeout = 200;
                        var clientHello = test.GetRequestBody(host.HostName);
                        await stream.WriteAsync(clientHello, 0, clientHello.Length);
                        var length = await stream.ReadAsync(response, 0, response.Length);
                        result |= test.TestResult(response, length);
                    }
                }
            } catch (Exception ex) {
                if ((ex.InnerException == null) || (ex.InnerException.HResult != -2147467259)) {
                    Console.WriteLine(host.HostName + " : " + ex.Message);
                }
            }
            return result;
        }


        public static byte[] SetCurrentTime(this byte[] request) {
            var now = DateTime.Now.ToUniversalTime();
            var t = now.Subtract(new DateTime(1970, 1, 1));
            var time = BitConverter.GetBytes((uint)t.TotalSeconds);
            if (BitConverter.IsLittleEndian) Array.Reverse(time);
            Array.Copy(time, 0, request, 11, time.Length);
            return request;
        }


        public static bool GetTlsExtention(this byte[] r, int signature, out int eOffset, out int eLength) {
            eOffset = -1;
            eLength = -1;
            var sessionLen = r[43];
            var idx = 47 + sessionLen;
            var len = (r[idx] << 8) + r[idx + 1] + 49 + sessionLen;
            idx += 2;
            while (idx < len) {
                var extSignature = (r[idx] << 8) + (r[idx + 1]);
                var extLen = (r[idx + 2] << 8) + (r[idx + 3]);
                idx += 4;
                if (extSignature == signature) {
                    eOffset = idx;
                    eLength = extLen;
                    return true;
                }
                idx += extLen;
            }
            return false;
        }


        public static ProtocolTests ExtractNpnProtocols(this byte[] r, int offset, int len) {
            var result = ProtocolTests.None;

            for (var i = 0; i < len; ) {
                var offs = offset + i;
                var l = r[offs];
                var proto = Encoding.ASCII.GetString(r, offs + 1, l);
                ProtocolTests value;
                if (KnownProtocols.TryGetValue(proto, out value)) {
                    result |= value;
                } else {
                    Console.WriteLine("Unknown NPN protocol : " + proto);
                }
                i += l + 1;
            }
            
            return result;
        }

    }

}
