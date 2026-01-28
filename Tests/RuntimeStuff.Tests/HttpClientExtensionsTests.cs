//using RuntimeStuff.Extensions;
//using System.Net.Http;

//namespace RuntimeStuff.MSTests
//{
//#if DEBUG
//    [TestClass]
//#endif
//    public class HttpClientExtensionsTests
//    {
//        private static HttpClient? http;

//        [TestInitialize]
//        public void TestInitialize()
//        {
//            http = new HttpClient().WithBaseUrl("https://jsonplaceholder.typicode.com");
//        }

//        [TestMethod]
//        public async Task HttpClientExtensionsTest_01()
//        {
//            //https://rutracker.org/forum/tracker.php?f=1755&nm=%E0%F0%E8%FF+2025
//            http = new HttpClient().WithBaseUrl("https://rutracker.org/forum/tracker.php");
//            var search = await http.PostAsync(null, new Dictionary<string, object>() { { "f", 1755 }, { "nm", "Ария 2025" } });

//            //var q = new Dictionary<string, object>()
//            //{
//            //    { "postId", 1 },
//            //};

//            //Task.Delay(1000).Wait();
//            //var comments = await http.SendAsync(HttpMethod.Get, "comments", q);

//            //Task.Delay(1000).Wait();
//            //var post = await http.PostAsync("/posts", new
//            //{
//            //    title = "foo",
//            //    body = "bar",
//            //    userId = 1
//            //});
//        }
//    }
//}
