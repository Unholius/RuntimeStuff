//namespace RuntimeStuff.MSTests
//{
//    [TestClass]
//    public class Obj_Copy_Tests
//    {
//        class Source
//        {
//            public int Id { get; set; }
//            public string Name { get; set; }
//            public string Extra { get; set; }
//            public string NullProp { get; set; }
//        }

//        class Target
//        {
//            public int Id { get; set; }
//            public string Name { get; set; }
//            public string Renamed { get; set; }
//            public string NullProp { get; set; }
//        }

//        [TestMethod]
//        public void Copy_Properties_With_Same_Names()
//        {
//            var src = new Source { Id = 1, Name = "Test", Extra = "X" };
//            var tgt = new Target();

//            Obj.Copy(src, tgt, false, true, false);

//            Assert.AreEqual(1, tgt.Id);
//            Assert.AreEqual("Test", tgt.Name);
//            Assert.IsNull(tgt.Renamed);
//        }

//        [TestMethod]
//        public void Copy_Properties_With_Mapper()
//        {
//            var src = new Source { Id = 2, Name = "Bob", Extra = "Y" };
//            var tgt = new Target();

//            var mapper = new Dictionary<string, string>
//            {
//                { "Name", "Renamed" }
//            };

//            Obj.Copy(src, tgt, false, true, false, mapper: mapper);

//            Assert.AreEqual(2, tgt.Id);
//            Assert.IsNull(tgt.Name);
//            Assert.AreEqual("Bob", tgt.Renamed);
//        }

//        [TestMethod]
//        public void Copy_DeepProcessing_Clones_Values()
//        {
//            var src = new Source { Id = 3, Name = "Deep", Extra = "Z" };
//            var tgt = new Target();

//            Obj.Copy(src, tgt, true, true, false);

//            Assert.AreEqual(3, tgt.Id);
//            Assert.AreEqual("Deep", tgt.Name);
//        }

//        [TestMethod]
//        public void Copy_OverwriteOnlyNullProperties()
//        {
//            var src = new Source { Id = 4, Name = "A", Extra = "B", NullProp = "fromSrc" };
//            var tgt = new Target { Id = 0, Name = null, NullProp = null };

//            Obj.Copy(src, tgt, false, true, true);

//            Assert.AreEqual(4, tgt.Id);
//            Assert.AreEqual("A", tgt.Name);
//            Assert.AreEqual("fromSrc", tgt.NullProp);
//        }

//        [TestMethod]
//        public void Copy_OverwritePropertiesWithNullValues()
//        {
//            var src = new Source { Id = 5, Name = null, Extra = "C", NullProp = null };
//            var tgt = new Target { Id = 10, Name = "old", NullProp = "old" };

//            Obj.Copy(src, tgt, false, false, false, mapper: null);

//            // Name and NullProp should not be overwritten because src is null and overwritePropertiesWithNullValues=false
//            Assert.AreEqual(5, tgt.Id);
//            Assert.AreEqual("old", tgt.Name);
//            Assert.AreEqual("old", tgt.NullProp);
//        }
//    }
//}
