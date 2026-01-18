using RuntimeStuff.Extensions;
using System.Net.Http;

namespace RuntimeStuff.MSTests
{
    //[TestClass]
    public class HttpClientExtensionsTests
    {
        private static HttpClient? http;

        [TestInitialize]
        public void TestInitialize()
        {
            http = new HttpClient().WithBaseUrl("https://jsonplaceholder.typicode.com");
        }

        [TestMethod]
        public async Task HttpClientExtensionsTest_01()
        {
            var todos = await http.GetAsync("/todos", null);

            //var q = new Dictionary<string, object>()
            //{
            //    { "postId", 1 },
            //};

            //Task.Delay(1000).Wait();
            //var comments = await http.SendAsync(HttpMethod.Get, "comments", q);

            //Task.Delay(1000).Wait();
            //var post = await http.PostAsync("/posts", new
            //{
            //    title = "foo",
            //    body = "bar",
            //    userId = 1
            //});
        }
    }
}
