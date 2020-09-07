using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.IdentityModel.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Vanilla.JsConnect;

namespace Vanilla.JsConnectDotNet.Tests {
    public class JsConnectV3Test {
        private JsConnectV3 _jsc;
        private static string _root;

        [OneTimeSetUp]
        public static void OneTimeSetUp() {
            _root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"../../.."));
        }

        [SetUp]
        public void Setup() {
            _jsc = new JsConnectV3();

            IdentityModelEventSource.Logger.EventCommandExecuted += new EventHandler<EventCommandEventArgs>(
                (
                    (sender, args) => {
                        var foo = "bar";
                    })
            );
        }

        [Test]
        public void TestGettersSetters() {
            Assert.AreEqual("123", _jsc.SetUniqueID("123").GetUniqueID());
            Assert.AreEqual("foo", _jsc.SetName("foo").GetName());
            Assert.AreEqual("foo@example.com", _jsc.SetEmail("foo@example.com").GetEmail());
            Assert.AreEqual("https://example.com", _jsc.SetPhotoUrl("https://example.com").GetPhotoUrl());

            var roles = new List<object>() {1, 2, 3};
            Assert.AreEqual(roles, _jsc.SetRoles(roles).GetRoles());

            _jsc.SetSigningCredentials("id", "secret");
            Assert.AreEqual("id", _jsc.GetSigningClientID());
            Assert.AreEqual("secret", _jsc.GetSigningSecret());
        }

        [Test, TestCaseSource("ProvideTests")]
        public void TestData(string name, JObject data) {
            _jsc.SetSigningCredentials((string) data["clientID"], (string) data["secret"]);
            _jsc.SetVersion((string) data["version"]);
            _jsc.SetTimestamp((int) data["timestamp"]);

            var user = (JObject) data["user"];
            if (user.Count == 0) {
                _jsc.SetGuest(true);
            }
            else {
                foreach (var entry in user) {
                    _jsc.SetUserField(entry.Key, FromJToken(entry.Value));
                }
            }

            // try {
            var requestUri = new Uri("https://example.com?jwt=" + data[JsConnectV3.FIELD_JWT]);
            var responseUrl = _jsc.GenerateResponseLocation(requestUri);
            Assert.False(string.IsNullOrWhiteSpace(data["response"].ToString()));
            // }


            // try {
            //     URI requestUri = new URI("https://example.com?jwt=" + data.get(JsConnectV3.FIELD_JWT));
            //     String responseUrl = jsc.generateResponseLocation(requestUri);
            //     assertTrue(data.containsKey("response"));
            //     assertJWTUrlsEqual((String) data.get("response"), responseUrl);
            // } catch (TokenExpiredException ex) {
            //     assertEquals("ExpiredException", data.get("exception"));
            // } catch (SignatureVerificationException ex) {
            //     assertEquals("SignatureInvalidException", data.get("exception"));
            // }
        }

        private static object FromJToken(JToken value) {
            switch (value.Type) {
                case JTokenType.Boolean:
                    return (bool) value;
                case JTokenType.Integer:
                    return (int) value;
                case JTokenType.Null:
                    return null;
                case JTokenType.String:
                    return (string) value;
                case JTokenType.Array:
                    var list = value.Select(o => FromJToken(o)).ToList();
                    return list;
                case JTokenType.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var entry in (JObject) value) {
                        dict[entry.Key] = FromJToken(entry.Value);
                    }

                    return dict;
                default:
                    throw new Exception($"Unknown JSON type: {value.Type}");
            }
        }

        public static IEnumerable<TestCaseData> ProvideTests() {
            var root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"../../.."));
            var str = File.ReadAllText($"{root}/tests.json");
            var json = JObject.Parse(str);

            foreach (var o in json) {
                var data = new TestCaseData(o.Key, o.Value);
                data.SetDescription(o.Key);
                yield return data;
            }
        }
    }
}