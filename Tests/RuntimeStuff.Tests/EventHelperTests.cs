using RuntimeStuff.Extensions;

namespace RuntimeStuff.MSTests
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
    }

    internal class PropClass2
    {
        private bool busyChanged;
        public bool BusyChanged
        {
            get { return Get(); }
            set
            {
                if (value)
                {
                    Set(value);
                }
                else
                {
                    Set(value);
                }
            }

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

    internal class PropClass1 : PropertyObserver
    {
        public bool IsBusy
        {
            get => Get<bool>();
            set => Set(value);
        }

        public bool IsBusyChanged { get; set; }

    }
}
