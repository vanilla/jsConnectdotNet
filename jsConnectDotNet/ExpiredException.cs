namespace Vanilla.JsConnect {
    /// <summary>
    /// Thrown when a token has expired.
    /// </summary>
    public class ExpiredException: JsConnectException {
        public ExpiredException(string message) : base(message) {
            
        }
    }
}