using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Web;
using JWT;
using JWT.Algorithms;
using JWT.Builder;
using JWT.Exceptions;
using Newtonsoft.Json.Linq;

namespace Vanilla.JsConnect {
    /// <summary>
    /// Implements the jsConnect 3 protocol.
    /// </summary>
    public class JsConnectV3 {
        public const string VERSION = "dotnet:3";

        public const string ALG_HS256 = "HS256";
        public const string ALG_HS384 = "HS384";
        public const string ALG_HS512 = "HS512";

        public const string FIELD_UNIQUE_ID = "id";
        public const string FIELD_PHOTO = "photo";
        public const string FIELD_NAME = "name";
        public const string FIELD_EMAIL = "email";
        public const string FIELD_ROLES = "roles";
        public const string FIELD_JWT = "jwt";
        public const string FIELD_STATE = "st";
        public const string FIELD_USER = "u";
        public const string FIELD_REDIRECT_URL = "rurl";
        // public const string FIELD_CLIENT_ID = "kid";
        // public const string FIELD_TARGET = "t";

        public const int TIMEOUT = 600;

        private string _signingSecret = "";

        private string _signingClientID = "";

        private Dictionary<string, object> _user;

        private bool _guest = false;

        private string _signingAlgorithm;

        private string _version = null;

        private long _timestamp = 0;

        /// <summary>
        /// JsConnect constructor.
        /// </summary>
        public JsConnectV3() {
            _user = new Dictionary<string, object>();
            _signingAlgorithm = ALG_HS256;
        }

        /// <summary>
        /// Validate a value that cannot be empty.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <param name="valueName">The name of the value for the exception message.</param>
        /// <exception cref="InvalidValueException">Throws an exception when the value is empty.</exception>
        protected static void ValidateNotEmpty(object value, string valueName) {
            if (value == null) {
                throw new InvalidValueException(valueName + " is required.");
            }

            if (value is string && (string) value == "") {
                throw new InvalidValueException(valueName + " cannot be empty.");
            }
        }

        /// <summary>
        /// Set a field on the current user.
        /// </summary>
        /// <param name="key">The key on the user.</param>
        /// <param name="value">The value to set. This must be a basic type that can be JSON encoded.</param>
        /// <returns>this</returns>
        public JsConnectV3 SetUserField(string key, object value) {
            this._user[key] = value;
            return this;
        }

        /// <summary>
        /// Set a field on the current user.
        /// </summary>
        /// <param name="key">The key on the user.</param>
        /// <returns>Returns the value of the user field or null.</returns>
        public object GetUserField(string key) {
            return this._user.GetValueOrDefault(key, null);
        }

        /// <summary>
        /// Get the current user's username.
        /// </summary>
        /// <returns>Returns the username.</returns>
        public string GetName() {
            return (string) this.GetUserField(FIELD_NAME);
        }

        /// <summary>
        /// Set the current user's username.
        /// </summary>
        /// <param name="name">The new name.</param>
        /// <returns>this</returns>
        public JsConnectV3 SetName(string name) {
            return this.SetUserField(FIELD_NAME, name);
        }

        /// <summary>
        /// Get the current user's avatar.
        /// </summary>
        /// <returns>Returns the photo URL.</returns>
        public string GetPhotoUrl() {
            return (string) this.GetUserField(FIELD_PHOTO);
        }

        /// <summary>
        /// Set the current user's avatar.
        /// </summary>
        /// <param name="photo">The new photo URL.</param>
        /// <returns>this</returns>
        public JsConnectV3 SetPhotoUrl(string photo) {
            return this.SetUserField(FIELD_PHOTO, photo);
        }

        /// <summary>
        /// Get the current user's unique ID.
        /// </summary>
        /// <returns>Returns a string ID.</returns>
        public string GetUniqueID() {
            return (string) this.GetUserField(FIELD_UNIQUE_ID);
        }

        /// <summary>
        /// Validate that a field exists in a collection.
        /// </summary>
        /// <param name="field">The name of the field to validate.</param>
        /// <param name="collection">The collection to look at.</param>
        /// <param name="collectionName">The name of the collection.</param>
        /// <param name="validateEmpty">If true, make sure the value is also not empty.</param>
        /// <returns></returns>
        /// <exception cref="FieldNotFoundException">Throws an exception when the field is not in the array.</exception>
        /// <exception cref="InvalidValueException">Throws an exception when the collection isn"t an array or the value is empty.</exception>
        protected static object ValidateFieldExists(string field, object collection, string collectionName,
            bool validateEmpty) {
            object value;

            if (collection is NameValueCollection query) {
                value = query[field];

                if (value == null) {
                    throw new FieldNotFoundException(field, collectionName);
                }
            } else if (collection is IDictionary<string, object> dict) {
                if (!dict.ContainsKey(field)) {
                    throw new FieldNotFoundException(field, collectionName);
                }

                value = dict[field];
            } else {
                throw new InvalidValueException("Invalid array: $collectionName");
            }

            if (validateEmpty && ((value is string str && str == "") || value == null)) {
                throw new InvalidValueException("Field cannot be empty: " + collectionName + "[" + field + "]");
            }

            return value;
        }

        /// <summary>
        /// Set the current user's unique ID.
        /// </summary>
        /// <param name="id">The new unique ID.</param>
        /// <returns></returns>
        public JsConnectV3 SetUniqueID(string id) {
            return this.SetUserField(FIELD_UNIQUE_ID, id);
        }

        /// <summary>
        /// Generate the location for an SSO redirect.
        /// </summary>
        /// <param name="requestJwt"></param>
        /// <returns></returns>
        public string GenerateResponseLocation(string requestJwt) {
            // Validate the request token.
            var request = this.JwtDecode(requestJwt);
            var user = this.IsGuest() ? new Dictionary<string, object>() : this.GetUser();

            var state = request.ContainsKey(FIELD_STATE)
                ? request[FIELD_STATE]
                : new Dictionary<string, object>();

            var response = this.JwtEncode(user, state);
            var location = (request[JsConnectV3.FIELD_REDIRECT_URL] ?? "").ToString() + "#jwt=" + response;
            return location;
        }

        /// <summary>
        /// Generate the response location from a URI object.
        /// </summary>
        /// <param name="query">The request's query string.</param>
        /// <returns>Returns a URL to be used in a 302 redirect location header.</returns>
        public string GenerateResponseLocation(NameValueCollection query) {
            ValidateFieldExists(FIELD_JWT, query, "query", true);
            var jwt = query.Get(FIELD_JWT) ?? "";

            return GenerateResponseLocation(jwt);
        }

        public string GenerateResponseLocation(Uri uri) {
            var query = HttpUtility.ParseQueryString(uri.Query);
            return GenerateResponseLocation(query);
        }

        /// <summary>
        /// Get the current user's email address.
        /// </summary>
        /// <returns></returns>
        public string GetEmail() {
            return (string) this.GetUserField(FIELD_EMAIL);
        }

        /// <summary>
        /// Set the current user's email address.
        /// </summary>
        /// <param name="email">The user's email address.</param>
        /// <returns></returns>
        public JsConnectV3 SetEmail(string email) {
            return this.SetUserField(FIELD_EMAIL, email);
        }

        /// <summary>
        /// Create the algorithm object used for signing requests.
        /// </summary>
        /// <returns></returns>
        protected static IJwtAlgorithm CreateAlgorithm(string algorithm) {
            return algorithm switch {
                ALG_HS256 => new HMACSHA256Algorithm(),
                ALG_HS384 => new HMACSHA384Algorithm(),
                ALG_HS512 => new HMACSHA512Algorithm(),
                _ => throw new InvalidValueException("Unsupported algorithm: " + algorithm)
            };
        }

        /// <summary>
        /// Decode a JWT.
        /// </summary>
        /// <param name="jwt">The JWT to decode.</param>
        /// <returns>Returns the payload of the decoded JWT.</returns>
        /// <exception cref="ExpiredException"></exception>
        /// <exception cref="SignatureInvalidException"></exception>
        public IDictionary<string, object> JwtDecode(string jwt) {
            try {
                var builder = CreateJwtBuilder(true);

                var raw = builder.Decode<JObject>(jwt);
                var result = FromJToken(raw);
                
                return (Dictionary<string, object>)result;
            } catch (TokenExpiredException ex) {
                throw new ExpiredException(ex.Message);
            } catch (SignatureVerificationException ex) {
                throw new SignatureInvalidException(ex.Message);
            }
        }

        /// <summary>
        /// Convert a JToken into basic objects.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="InvalidValueException"></exception>
        /// <exception cref="Exception"></exception>
        public static object FromJToken(object value) {
            if (value is JToken jtoken) {
                switch (jtoken.Type) {
                    case JTokenType.Boolean:
                        return (bool) jtoken;
                    case JTokenType.Integer:
                        return (int) jtoken;
                    case JTokenType.Float:
                        return (float) jtoken;
                    case JTokenType.Null:
                        return null;
                    case JTokenType.String:
                        return (string) jtoken;
                    case JTokenType.Array:
                        if (value is JArray jarray) {
                            var list = new List<object>();
                            foreach (var entry in jarray) {
                                list.Add(FromJToken(entry));
                            }
                            return list;
                        } else {
                            throw new InvalidValueException("The decoded value is supposed to be a JArray.");
                        }
                    case JTokenType.Object:
                        if (value is JObject jobject) {
                            var dict = new Dictionary<string, object>();
                            foreach (var (key, jToken) in jobject) {
                                dict[key] = FromJToken(jToken);
                            }

                            return dict;
                        } else {
                            throw new InvalidValueException("The decoded value is supposed to be a JObject.");
                        }
                    default:
                        throw new Exception($"Unknown JSON type: {jtoken.Type}");
                }
            }

            return value;
        }

        /// <summary>
        /// Provides dates for signing and validating signatures.
        ///
        /// This class is meant to allow for testing of fixed dates.
        /// </summary>
        private class FixedDateTimeProvider : IDateTimeProvider {
            private readonly long _now;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="now">Pass the current timestamp or zero to always get the current time.</param>
            public FixedDateTimeProvider(long now) {
                _now = now;
            }

            /// <summary>
            /// Get the current time.
            ///
            /// This will use the fixed timestamp if non-zero or the current time.
            /// </summary>
            /// <returns></returns>
            public DateTimeOffset GetNow() {
                return _now <= 0 ? DateTimeOffset.UtcNow : DateTimeOffset.FromUnixTimeSeconds(_now);
            }
        }

        private class AlgorithmFactory : IAlgorithmFactory {
            public IJwtAlgorithm Create(JwtDecoderContext context) {
                return CreateAlgorithm(context.Header.Algorithm);
            }
        }

        /// <summary>
        /// Whether or not the user is signed in.
        /// </summary>
        /// <returns></returns>
        public bool IsGuest() {
            return this._guest;
        }

       
        /// <summary>
        /// Set whether or not the user is signed in.
        /// </summary>
        /// <param name="isGuest">The new value.</param>
        /// <returns></returns>
        public JsConnectV3 SetGuest(bool isGuest) {
            this._guest = isGuest;
            return this;
        }

        /// <summary>
        /// Create the JWT builder for encoding/decoding JWTs.
        /// </summary>
        /// <param name="forDecoding">
        /// Set to true if using the builder for decoding or false if encoding. If you are unsure use false which will
        /// work for decoding, but only with one algorithm.
        /// </param>
        /// <returns></returns>
        public JwtBuilder CreateJwtBuilder(bool forDecoding) {
            // IJsonSerializer serializer = new JsonNetSerializer();
            IDateTimeProvider dateTimeProvider = new FixedDateTimeProvider(_timestamp);
            // IJwtValidator validator = new JwtValidator(serializer, dateTimeProvider);
            // IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
            // IJwtAlgorithm algorithm = new HMACSHA256Algorithm();
            // IJwtDecoder decoder = new JwtDecoder(serializer, validator, urlEncoder, algorithm);
            var builder = new JwtBuilder()
                // .WithSerializer(new Serializer(new JsonNetSerializer()))
                .WithSecret(_signingSecret)
                .WithDateTimeProvider(dateTimeProvider)
                .WithVerifySignature(true)
                .AddHeader(HeaderName.KeyId, GetSigningClientID())
                .AddClaim("v", GetVersion());

            if (forDecoding) {
                builder.WithAlgorithmFactory(new AlgorithmFactory());
            } else {
                builder.WithAlgorithm(CreateAlgorithm(_signingAlgorithm));
            }
            
            return builder;
        }

        /// <summary>
        /// Wrap a payload in a JWT.
        /// </summary>
        /// <param name="user">The user part of the response.</param>
        /// <param name="state">The state to pass back to Vanilla.</param>
        /// <returns></returns>
        protected string JwtEncode(IDictionary<string, object> user, object state) {
            var now = this.GetTimestamp();
            var builder = CreateJwtBuilder(false)
                    .AddClaim("iat", now)
                    .AddClaim("exp", now + TIMEOUT)
                    .AddClaim(FIELD_USER, GetUser())
                    .AddClaim(FIELD_STATE, state)
                ;
            var tokenString = builder.Encode();
            return tokenString;
        }

        /// <summary>
        /// Get the current timestamp.
        ///
        /// This time is used for signing and verifying tokens.
        /// </summary>
        /// <returns></returns>
        protected long GetTimestamp() {
            return _timestamp == 0 ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : _timestamp;
        }

        /// <summary>
        /// Override the timestamp used to validate and sign JWTs.
        /// </summary>
        /// <param name="timestamp">The new timestamp.</param>
        /// <returns>this</returns>
        public JsConnectV3 SetTimestamp(long timestamp) {
            this._timestamp = timestamp;
            return this;
        }
        
        /// <summary>
        /// Get the secret that is used to sign JWTs.
        /// </summary>
        /// <returns></returns>
        public string GetSigningSecret() {
            return this._signingSecret;
        }

        /// <summary>
        /// Get the client ID that is used to sign JWTs.
        /// </summary>
        /// <returns></returns>
        public string GetSigningClientID() {
            return this._signingClientID;
        }

        /// <summary>
        /// Set the credentials that will be used to sign requests.
        /// </summary>
        /// <param name="clientID">The client ID used as the key ID in responses.</param>
        /// <param name="secret">The secret used to sign responses and validate requests.</param>
        /// <returns></returns>
        /// <exception cref="InvalidValueException"></exception>
        public JsConnectV3 SetSigningCredentials(string clientID, string secret) {
            this._signingClientID = clientID;

            if (Encoding.UTF8.GetBytes(secret).Length < 16) {
                throw new InvalidValueException("The secret must be at least 16 characters long.");
            }

            this._signingSecret = secret;
            return this;
        }

        /// <summary>
        /// Get the currently signed in user. 
        /// </summary>
        /// <returns>Returns a map that contains all of the fields on a user.</returns>
        public IDictionary<string, object> GetUser() {
            return _user;
        }
        
        /// <summary>
        /// Get the roles on the user. 
        /// </summary>
        /// <returns>Returns a list of roles.</returns>
        public ICollection<object> GetRoles() {
            return (List<object>) this.GetUserField(FIELD_ROLES);
        }

        /// <summary>
        /// Set the roles on the user.
        /// </summary>
        /// <param name="roles"></param>
        /// <returns>A list of role names or IDs.</returns>
        public JsConnectV3 SetRoles(ICollection<object> roles) {
            this.SetUserField(JsConnectV3.FIELD_ROLES, roles);
            return this;
        }

        /// <summary>
        /// Get the version used to sign responses.
        /// </summary>
        /// <returns>Returns a version string.</returns>
        public string GetVersion() {
            return _version ?? VERSION;
        }
        
        /// <summary>
        /// Override the version used in JWT claims.
        /// </summary>
        /// <param name="version">The version override.</param>
        /// <returns>this</returns>
        public JsConnectV3 SetVersion(string version) {
            _version = version;
            return this;
        }
    }
}