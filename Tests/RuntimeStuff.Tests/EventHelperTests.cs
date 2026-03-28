using System.ComponentModel;

namespace System.MSTests
{
    [TestClass]
    public class EventHelperTests
    {
        [TestMethod]
        public void Test_BindingProperties_01()
        {
            var pc1 = new PropClass1();
            var pc2 = new PropClass2();
            pc1.BindToProperty(z => z.IsBusy, pc2, z => z.BusyChanged);
            pc1.BindToProperty(z => z.IsBusy, pc1, z => z.IsBusyChanged);
            Assert.IsFalse(pc2.BusyChanged);
            pc1.IsBusy = true;
            Assert.IsTrue(pc2.BusyChanged);
        }

        [TestMethod]
        public void Test_BindingProperties_02()
        {
            var pc1 = new PropClass1() { Prop2 = new PropClass2() };
            var pc2 = new PropClass1();
            pc1.BindToProperty(z => z.Prop2, pc2, z => z.Prop2);
            Assert.IsNotNull(pc2.Prop2);
        }
    }

    internal class PropClass2
    {
        private bool busyChanged;

        public bool BusyChanged
        {
            get => Get();
            set => Set(value);
        }

        private bool Get()
        {
            return busyChanged;
        }

        private void Set(bool v)
        {
            busyChanged = v;
        }
    }

    internal class PropClass1 : ObservableObjectEx
    {
        public bool IsBusy
        {
            get => Get<bool>();
            set => Set(value);
        }

        public bool IsBusyChanged { get; set; }

        public PropClass2 Prop2 { get => Get<PropClass2>(); set => Set(value); }
    }
}