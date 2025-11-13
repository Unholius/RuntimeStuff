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
            var vm = new ObjectEditorViewModel<TestClassWithBasicProperties>();
            vm.SelectedObject = new TestClassWithBasicProperties();
            vm.Properties.Filter = "[Name] == 'IntProperty'";
            Assert.AreEqual(1, vm.Properties.Count);
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
            vm.SelectedObject = new TestClassWithBasicProperties() { StringProperty = "1" };
            vm.Configuration
                .SetRule(x => x.StringProperty, ["1", "2"], "Значение должно быть '1' или '2'")
                .SetRule(x => x.IntProperty, 0, 10)
                .SetVisible(true, x => x.StringProperty)
                .SetEditable(false, x => x.IntProperty)
                .SetDisplayFormat(@"{0:00000}", x => x.IntProperty)
                ;
            var p1 = vm.Property(x => x.StringProperty);
            p1.Value = "3";
            var p2 = vm.Property(x => x.IntProperty);
            p2.Value = 20;
        }
    }
}
