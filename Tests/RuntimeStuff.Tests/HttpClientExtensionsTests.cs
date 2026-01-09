using System.Net.Http;
using RuntimeStuff.Extensions;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class HttpClientExtensionsTests
    {
        private HttpClient http;

        [TestInitialize]
        public void TestInitialize()
        {
            http = new HttpClient().WithBaseUrl("https://jsonplaceholder.typicode.com");
        }

        [TestMethod]
        public async Task HttpClientExtensionsTest_01()
        {
            var todos = await http.GetAsync("/todos", null);
        }

        [TestMethod]
        public async Task HttpClientExtensionsTest_02()
        {
            var q = new Dictionary<string, object>()
            {
                { "postId", 1 },
            };

            var comments = await http.SendAsync(HttpMethod.Get, "/comments", q);
        }
    }
}
