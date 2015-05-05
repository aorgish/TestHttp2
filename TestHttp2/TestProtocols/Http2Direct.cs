namespace TestHttp2.TestProtocols {

    // RTFM : https://http2.github.io/http2-spec/#known-http

    public class Http2Direct : IProtocolTest {

        public int Port { get { return 80; } }

        public byte[] GetRequestBody(string host) {

            var http2PrefaceAndSettings = new byte[] {
                 // connectionPreface = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"
                 0x50, 0x52, 0x49, 0x20, 0x2a, 0x20, 0x48, 0x54, 0x54, 0x50, 0x2f, 0x32, 
                 0x2e, 0x30, 0x0d, 0x0a, 0x0d, 0x0a, 0x53, 0x4d, 0x0d, 0x0a, 0x0d, 0x0a,
                 // Settings Frame (Empty)
                 0x00, 0x00, 0x00,      //  Length
                 0x04,                  //  Type+Flags
                 0x00, 0x00, 0x00, 0x00 //  R+StreamIdentifier
            };
            
            return http2PrefaceAndSettings;
        }

        public ProtocolTests TestResult(byte[] response, int length) {
            // Response : Settings Frame 
            // 00 00 0c                             = Length of Payload
            // 04                                   = Type+Flags  
            // 00 00 00 00  00                      = R+StreamIdentifier
            // 00 03 00 00 00 64 00 04 00 00 ff ff  = Payload

            if ((length >= 8) && ((response[3] == 0x4)) && ((response[0] << 16) + (response[1] << 8) + response[2] == length - 9))
                return ProtocolTests.Http2_Direct | ProtocolTests.Http2c;

            return ProtocolTests.None;
        }
    }


}
