using System;

namespace Vanilla.JsConnect {
    /// <summary>
    /// Base class for jsConnect exceptions.
    /// </summary>
    public class JsConnectException: Exception {
        public JsConnectException(string message): base(message) {
            
        }
    }
}