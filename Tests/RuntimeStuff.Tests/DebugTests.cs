using QuickDialogs.Core.ObjectEditor;
using RuntimeStuff.MSTests.Models;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class DebugTests
    {
        [TestMethod]
        public void Test1()
        {
            try
            {
                var vm = new ObjectEditorViewModel<TestClassWithBasicProperties>();
                vm.SelectedObject = new TestClassWithBasicProperties();
                vm.Properties.Filter = "[Name] == 'Int32'";
                Assert.AreEqual(1, vm.Properties.Count);
            }
            catch (Exception ex)
            {
            }
        }

        [TestMethod]
        public void Test2()
        {
            var vm = new ObjectEditorViewModel<TestClassWithBasicProperties>();
            vm.SelectedObject = new TestClassWithBasicProperties();
            var props = vm.Properties.Select(x => x.Name).ToArray();
            vm.Properties.SortBy = "Name, Type";
            var sortedProps = vm.Properties.Select(x => x.Name).ToArray();
        }

        [TestMethod]
        public void Test3()
        {
            var vm = new ObjectEditorViewModel();
            vm.SelectedObject = new TestClassWithBasicProperties() ;
            var props = vm.Properties.Select(x => x.Name).ToArray();
            vm.Properties.SortBy = "Name DESC";
            var sortedProps = vm.Properties.Select(x => x.Name).ToArray();
        }

        [TestMethod]
        public void Test4()
        {
            var vm = new ObjectEditorViewModel<TestClassWithBasicProperties>();
            vm.SelectedObject = new TestClassWithBasicProperties() { Str = "1" };
            vm.Configuration
                .SetRule(x => x.Str, ["1", "2"], "Значение должно быть '1' или '2'")
                .SetRule(x => x.Int32, 0, 10)
                .SetVisible(true, x => x.Str)
                .SetEditable(false, x => x.Int32)
                .SetDisplayFormat(@"{0:00000}", x => x.Int32)
                ;
            var p1 = vm.Property(x => x.Str);
            p1.Value = "3";
            var p2 = vm.Property(x => x.Int32);
            p2.Value = 20;
        }
    }
}
