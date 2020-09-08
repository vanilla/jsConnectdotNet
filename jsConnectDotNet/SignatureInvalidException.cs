namespace Vanilla.JsConnect {
    /// <summary>
    /// Thrown when a token's signature is invalid.
    /// </summary>
    public class SignatureInvalidException: JsConnectException {
        public SignatureInvalidException(string message) : base(message) {
            
        }
    }
}