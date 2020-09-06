namespace Vanilla.JsConnect {
    /// <summary>
    /// An exception that is thrown when a value in a jsConnect JWT is invalid somehow.
    /// </summary>
    public class InvalidValueException: JsConnectException {
        public InvalidValueException(string message) : base(message) {
            
        }
    }
}