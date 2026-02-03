using RuntimeStuff.Helpers;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class XmlHelperTests
    {
        const string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><multistatus xmlns=\"DAV:\"><response id=\"1\"><href>/webdav/s_user2%40deepmail.tokyo/</href><propstat><prop><max-file-size-bytes>30971000</max-file-size-bytes></prop><status>HTTP/1.1 200 OK</status></propstat></response><response id=\"2\"><href>/webdav/s_user2%40deepmail.tokyo/a5d80190-9e02-4369-8f6e-41923da50c0e/</href><propstat><prop><max-file-size-bytes>30971000</max-file-size-bytes></prop><status>HTTP/1.1 200 OK</status></propstat></response><response><href>/webdav/s_user2%40deepmail.tokyo/141dde91-b60a-4f7c-a7c5-e2d84463e92f/</href><propstat><prop><max-file-size-bytes>30971000</max-file-size-bytes></prop><status>HTTP/1.1 200 OK</status></propstat></response><response><href>/webdav/s_user2%40deepmail.tokyo/c77a55b9-bd75-4706-acc7-a23c2733e3d9/</href><propstat><prop><max-file-size-bytes>30971000</max-file-size-bytes></prop><status>HTTP/1.1 200 OK</status></propstat></response></multistatus>";
        [TestMethod]
        public void Test_01()
        {
            var contents = XmlHelper.GetContents(xml, "response", x => x.Contains("a5d80190-9e02-4369-8f6e-41923da50c0e"));
            var values = XmlHelper.GetValues(contents[0], "max-file-size-bytes");
        }

        [TestMethod]
        public void Test_02()
        {
            var values = XmlHelper.GetValues(xml, "max-file-size-bytes");
        }

        [TestMethod]
        public void Test_03()
        {
            var values = XmlHelper.GetValues(xml, "max-file-size-bytes", "response", x => x.Contains("a5d80190-9e02-4369-8f6e-41923da50c0e"));
        }

        [TestMethod]
        public void Test_04()
        {
            var values = XmlHelper.GetAttributes(xml, "response");
        }
    }
}
