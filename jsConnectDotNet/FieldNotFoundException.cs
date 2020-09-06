namespace Vanilla.JsConnect {
    /// <summary>
    /// An exception that represents a missing required field.
    /// </summary>
    public class FieldNotFoundException: JsConnectException {
        /// <summary>
        /// FieldNotFound constructor.
        /// </summary>
        /// <param name="field">The name of the field.</param>
        /// <param name="collection">The name of the collection.</param>
        public FieldNotFoundException(string field, string collection): base($"Missing field: {collection}[{field}]") {
            
        }
    }
}