using System.Text;

namespace TestHttp2.TestProtocols {

    // RTFM : https://http2.github.io/http2-spec/#discover-http

    public class Http2Upgrade : IProtocolTest {

        public int Port { get { return 80; } }

        public byte[] GetRequestBody(string host) {
            var body = "GET / HTTP/1.1\r\n"+
                       "Host: "+host+"\r\n"+
                       "Connection: Upgrade HTTP2-Settings\r\n"+
                       "Upgrade: h2c-14\r\n" +       // TODO : Check h2c only
                       "HTTP2-Settings: \r\n\r\n";
            var result = Encoding.ASCII.GetBytes(body);
            return result;
        }

        public ProtocolTests TestResult(byte[] response, int length)
        {
            var response101 = new[] {  
                0x48, 0x54, 0x54, 0x50, 0x2f, 0x31, 0x2e, 0x31, 0x20, 0x31, 0x30, 0x31, 0x20 // "HTTP/1.1 101 "
            };
            for (var i = 0; i < response101.Length; i++) {
                if (response[i] != response101[i]) return ProtocolTests.None;
            }
            return ProtocolTests.Http2_Upgrade | ProtocolTests.Http2c;
        }
    }

}
