using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Vanilla.JsConnect;

namespace Vanilla.JsConnectDotNet.Tests {
    public class JsConnectV3Test {
        private JsConnectV3 _jsc;
        
        /// <summary>
        /// The root directory to help load test data.
        /// </summary>
        public static string RootDirectory => Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"../../.."));

        [SetUp]
        public void Setup() {
            _jsc = new JsConnectV3();
        }

        /// <summary>
        /// Test basic object access.
        /// </summary>
        [Test]
        public void TestGettersSetters() {
            Assert.AreEqual("123", _jsc.SetUniqueID("123").GetUniqueID());
            Assert.AreEqual("foo", _jsc.SetName("foo").GetName());
            Assert.AreEqual("foo@example.com", _jsc.SetEmail("foo@example.com").GetEmail());
            Assert.AreEqual("https://example.com", _jsc.SetPhotoUrl("https://example.com").GetPhotoUrl());

            var roles = new List<object>() {1, 2, 3};
            Assert.AreEqual(roles, _jsc.SetRoles(roles).GetRoles());

            _jsc.SetSigningCredentials("id", "01234567890abcdef");
            Assert.AreEqual("id", _jsc.GetSigningClientID());
            Assert.AreEqual("01234567890abcdef", _jsc.GetSigningSecret());
        }

        /// <summary>
        /// Test a single test.json entry.
        /// </summary>
        /// <param name="name">The name of the test.</param>
        /// <param name="data">The test data.</param>
        [Test, TestCaseSource("ProvideTests")]
        public void TestData(string name, JObject data) {
            _jsc.SetSigningCredentials((string) data["clientID"], (string) data["secret"]);
            _jsc.SetVersion((string) data["version"]);
            _jsc.SetTimestamp((int) data["timestamp"]);

            var user = (JObject) data["user"];
            if (user.Count == 0) {
                _jsc.SetGuest(true);
            } else {
                foreach (var entry in user) {
                    _jsc.SetUserField(entry.Key, JsConnectV3.FromJToken(entry.Value));
                }
            }

            try {
                var requestUri = new Uri("https://example.com?jwt=" + data[JsConnectV3.FIELD_JWT]);
                var responseUrl = _jsc.GenerateResponseLocation(requestUri);
                Assert.False(string.IsNullOrWhiteSpace(data["response"].ToString()));
                AssertJWTUrlsAreEqual(data["response"].ToString(), responseUrl);
            } catch (SignatureInvalidException) {
                Assert.AreEqual("SignatureInvalidException", (data["exception"] ?? "").ToString(),
                    "SignatureInvalidException not expected.");
            } catch (ExpiredException) {
                Assert.AreEqual("ExpiredException", (data["exception"] ?? "").ToString(),
                    "ExpiredException not expected.");
            }
        }

        /// <summary>
        /// Assert that a jsConnect response URLs is correct.
        /// </summary>
        /// <param name="expected">The expected URL</param>
        /// <param name="actual">The actual URL</param>
        private void AssertJWTUrlsAreEqual(string expected, string actual) {
            var expectedUri = new Uri(expected);
            var actualUri = new Uri(actual);

            Assert.AreEqual(expectedUri.Scheme, actualUri.Scheme);
            Assert.AreEqual(expectedUri.Host, actualUri.Host);
            Assert.AreEqual(expectedUri.AbsolutePath, actualUri.AbsolutePath);

            var expectedQuery = HttpUtility.ParseQueryString(expectedUri.Fragment.TrimStart('#'));
            var actualQuery = HttpUtility.ParseQueryString(actualUri.Fragment.TrimStart('#'));
            AssertJWTData(expectedQuery[JsConnectV3.FIELD_JWT], actualQuery[JsConnectV3.FIELD_JWT]);
        }

        /// <summary>
        /// Assert that a JWT string is correct.
        /// </summary>
        /// <param name="expected">The expected JWT string</param>
        /// <param name="actual">The actual JWT string.</param>
        private void AssertJWTData(string expected, string actual) {
            Assert.NotNull(expected, "The expected jwt parameter was missing.");
            Assert.NotNull(actual, "The actual jwt parameter was missing.");

            var expectedDecoded = _jsc.JwtDecode(expected);
            var actualDecoded = _jsc.JwtDecode(actual);
            Assert.AreEqual(expectedDecoded, actualDecoded);
        }

        /// <summary>
        /// Provide tests from the tests.json file.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<TestCaseData> ProvideTests() {
            var str = File.ReadAllText($"{RootDirectory}/tests.json");
            var json = JObject.Parse(str);

            foreach (var o in json) {
                var data = new TestCaseData(o.Key, o.Value);
                data.SetDescription(o.Key);
                yield return data;
            }
        }
    }
}