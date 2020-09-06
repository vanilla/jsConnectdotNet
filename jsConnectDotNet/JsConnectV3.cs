using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Web;
using Json.Net;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

// ReSharper disable MemberCanBePrivate.Global

namespace Vanilla.JsConnect {
    /**
 * Implements the jsConnect 3 protocol.
 */
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
    public const string FIELD_CLIENT_ID = "kid";
    public const string FIELD_TARGET = "t";

    public const int TIMEOUT = 600;

    private string _signingSecret = "";

    private string _signingClientID = "";

    private Dictionary<string, object> _user;

    private bool _guest = false;

    private string _signingAlgorithm;

    private string _version = null;

    private DateTime? _timestamp = null;

    /**
     * JsConnect constructor.
     */
    public JsConnectV3() {
        _user = new Dictionary<string, object>();;
        _signingAlgorithm = ALG_HS256;
    }

    /**
     * Validate a value that cannot be empty.
     *
     * @param value The value to test.
     * @param valueName The name of the value for the exception message.
     * @throws InvalidValueException Throws an exception when the value is empty.
     */
    protected static void ValidateNotEmpty(object value, string valueName) {
        if (value == null) {
            throw new InvalidValueException(valueName + " is required.");
        }
        if (value is string && (string)value == "") {
            throw new InvalidValueException(valueName + " cannot be empty.");
        }
    }

    /**
     * Set a field on the current user.
     *
     * @param key   The key on the user.
     * @param value The value to set. This must be a basic type that can be JSON encoded.
     * @return this
     */
    public JsConnectV3 SetUserField(string key, object value) {
        this._user[key] = value;
        return this;
    }

    /**
     * Set a field on the current user.
     *
     * @param key The key on the user.
     * @return
     */
    public object GetUserField(string key) {
        return this._user.GetValueOrDefault(key, null);
    }

    /**
     * Get the current user's username.
     *
     * @return
     */
    public string GetName() {
        return (string) this.GetUserField(FIELD_NAME);
    }

    /**
     * Set the current user's username.
     *
     * @param name The new name.
     * @return this
     */
    public JsConnectV3 SetName(string name) {
        return this.SetUserField(FIELD_NAME, name);
    }

    /**
     * Get the current user's avatar.
     *
     * @return
     */
    public string GetPhotoUrl() {
        return (string) this.GetUserField(FIELD_PHOTO);
    }

    /**
     * Set the current user's avatar.
     *
     * @param photo The new photo URL.
     * @return this
     */
    public JsConnectV3 SetPhotoUrl(string photo) {
        return this.SetUserField(FIELD_PHOTO, photo);
    }

    /**
     * Get the current user's unique ID.
     *
     * @return
     */
    public string GetUniqueID() {
        return (string) this.GetUserField(FIELD_UNIQUE_ID);
    }

    /**
     * Validate that a field exists in a collection.
     *
     * @param field The name of the field to validate.
     * @param collection The collection to look at.
     * @param collectionName The name of the collection.
     * @param validateEmpty If true, make sure the value is also not empty.
     * @return Returns the field value if there are no errors.
     * @throws FieldNotFoundException Throws an exception when the field is not in the array.
     * @throws InvalidValueException Throws an exception when the collection isn"t an array or the value is empty.
     */
    protected static object ValidateFieldExists(string field, object collection, string collectionName, bool validateEmpty) {
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

    /**
     * Set the current user's unique ID.
     *
     * @param id The new unique ID.
     * @return $this
     */
    public JsConnectV3 SetUniqueID(string id) {
        return this.SetUserField(FIELD_UNIQUE_ID, id);
    }

    /**
     * Generate the location for an SSO redirect.
     *
     * @param requestJWT
     * @return string
     */
    public string GenerateResponseLocation(string requestJwt) {
        // Validate the request token.
        JwtSecurityToken request = this.JwtDecode(requestJwt);
        var user = this.IsGuest() ? new Dictionary<string, object>() : this.GetUser();

        var state = (IDictionary<string, object>)(request.Payload.ContainsKey(FIELD_STATE) ? request.Payload[FIELD_STATE] : new Dictionary<string, object>());

        var response = this.JwtEncode(user, state);
        var location = (request.Payload[JsConnectV3.FIELD_REDIRECT_URL] ?? "").ToString() + "#jwt=" + response;
        return location;
    }

    /**
     * Generate the response location from a URI object.
     *
     * @param uri The request URI.
     * @return Returns a string URI.
     */
    public string GenerateResponseLocation(NameValueCollection query) {
        ValidateFieldExists(FIELD_JWT, query, "query", true);
        var jwt = query.Get(FIELD_JWT) ?? "";

        return GenerateResponseLocation(jwt);
    }

    public string GenerateResponseLocation(Uri uri) {
        var query = HttpUtility.ParseQueryString(uri.Query);
        return GenerateResponseLocation(query);
    }

    /**
     * Get the current user's email address.
     *
     * @return
     */
    public string GetEmail() {
        return (string) this.GetUserField(FIELD_EMAIL);
    }

    /**
     * Set the current user's email address.
     *
     * @param email The user's email address.
     * @return this
     */
    public JsConnectV3 SetEmail(string email) {
        return this.SetUserField(FIELD_EMAIL, email);
    }

    /**
     * Decode a JWT.
     *
     * @param jwt The JWT to decode.
     * @return array Returns the payload of the decoded JWT.
     */
    protected JwtSecurityToken JwtDecode(string jwt) {
        var handler = new JwtSecurityTokenHandler();
        var rawToken = handler.ReadJwtToken(jwt);
        
        var key = new JsonWebKey();
        key.Kid = GetSigningClientID();
        key.K = GetSigningSecret();
        key.Alg = rawToken.Header.Alg;

        // var key = new SymmetricSecurityKey(System.Text.Encoding.Default.GetBytes(this._signingSecret));
        // key.KeyId = this.GetSigningClientID();
        
        var validationParams = new TokenValidationParameters {
            ValidAlgorithms = new[] { ALG_HS256, ALG_HS384, ALG_HS512 },
            IssuerSigningKey = key
        };
        
        if (this._timestamp != null) {
            validationParams.ClockSkew = DateTime.UtcNow.Subtract((DateTime)this._timestamp);
        }
        
        var claims = handler.ValidateToken(jwt, validationParams, out var token);

        return (JwtSecurityToken)token;
    }

    private IEnumerable<SecurityKey> ResolveSigningKey(string token, SecurityToken securitytoken, string kid, TokenValidationParameters validationparameters) {
        throw new NotImplementedException();
    }

    /**
     * Whether or not the user is signed in.
     */
    public bool IsGuest() {
        return this._guest;
    }

    /**
     * Set whether or not the user is signed in.
     *
     * @param isGuest The new value.
     */
    public JsConnectV3 SetGuest(bool isGuest) {
        this._guest = isGuest;
        return this;
    }

    /// <summary>
    /// Get the signing credentials for creating new JWTs.
    /// </summary>
    /// <returns></returns>
    protected SigningCredentials GetSigningCredentials() {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this._signingSecret));
        var alg = _signingAlgorithm switch {
            JsConnectV3.ALG_HS256 => SecurityAlgorithms.Sha256,
            JsConnectV3.ALG_HS384 => SecurityAlgorithms.Sha384,
            JsConnectV3.ALG_HS512 => SecurityAlgorithms.Sha512,
            _ => throw new InvalidValueException("Invalid signing algorithm: " + _signingAlgorithm)
        };
        
        var result = new SigningCredentials(securityKey, alg);
        return result;
    }
    
    public static uint ToUnixTimestamp(DateTime dateTime) {
        return (uint)(TimeZoneInfo.ConvertTimeToUtc(dateTime) - 
                new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;
    }

    /**
     * Wrap a payload in a JWT.
     *
     * @param user  The user part of the response.
     * @param state The state to pass back to Vanilla.
     */
    protected string JwtEncode(IDictionary<string, object> user, IDictionary<string, object> state) {
        var credentials = GetSigningCredentials();
        var now = ToUnixTimestamp(this.GetTimestamp());

        var header = new JwtHeader(credentials) {{FIELD_CLIENT_ID, _signingClientID}};

        var payload = new JwtPayload(new List<Claim>{
            new Claim("v", GetVersion()),
            new Claim("iat", now.ToString(), ClaimValueTypes.Integer32),
            new Claim("exp", (now + TIMEOUT).ToString(), ClaimValueTypes.Integer32),
            new Claim(FIELD_USER, JsonConvert.SerializeObject(GetUser()), JsonClaimValueTypes.Json),
            new Claim(FIELD_STATE, JsonConvert.SerializeObject(state), JsonClaimValueTypes.Json)
        });

        var jwt = new JwtSecurityToken(header, payload);
        var handler = new JwtSecurityTokenHandler();
        
        var tokenString = handler.WriteToken(jwt);
        
        return tokenString;
    }

    /**
     * Get the current timestamp.
     * <p>
     * This time is used for signing and verifying tokens.
     */
    protected DateTime GetTimestamp() {
        return _timestamp ?? DateTime.UtcNow;
    }

    /**
     * Override the timestamp used to validate and sign JWTs.
     *
     * @param timestamp The new timestamp.
     * @return Returns this.
     */
    public JsConnectV3 SetTimestamp(DateTime? timestamp) {
        this._timestamp = timestamp;
        return this;
    }

    public JsConnectV3 SetTimestamp(int timestamp) {
        return SetTimestamp(DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime);
    }

    /**
     * Get the secret that is used to sign JWTs.
     */
    public string GetSigningSecret() {
        return this._signingSecret;
    }

    /**
     * Get the client ID that is used to sign JWTs.
     *
     * @return string
     */
    public string GetSigningClientID() {
        return this._signingClientID;
    }

    /**
     * Set the credentials that will be used to sign requests.
     *
     * @param clientID The client ID used as the key ID in responses.
     * @param secret   The secret used to sign responses and validate requests.
     */
    public JsConnectV3 SetSigningCredentials(string clientId, string secret) {
        this._signingClientID = clientId;
        this._signingSecret = secret;
        return this;
    }

    /**
     * Get the currently signed in user.
     *
     * @return Returns a map that contains all of the fields on a user.
     */
    public IDictionary<string, object> GetUser() {
        return _user;
    }

    /**
     * Get the roles on the user.
     *
     * @return Returns a list of roles.
     */
    public ICollection<object> GetRoles() {
        return (List<object>) this.GetUserField(FIELD_ROLES);
    }

    /**
     * Set the roles on the user.
     *
     * @param roles A list of role names or IDs.
     */
    public JsConnectV3 SetRoles(ICollection<object> roles) {
        this.SetUserField(JsConnectV3.FIELD_ROLES, roles);
        return this;
    }

    /**
     * Get the version used to sign responses.
     *
     * @return Returns a version string.
     */
    public string GetVersion() {
        return this._version == null ? VERSION : this._version;
    }

    /**
     * Override the version used in JWT claims.
     *
     * @param version The version override.
     * @return Returns this.
     */
    public JsConnectV3 SetVersion(string version) {
        _version = version;
        return this;
    }
}
}