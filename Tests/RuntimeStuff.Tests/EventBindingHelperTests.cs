using System.ComponentModel;
using System.Diagnostics;
using System.Helpers;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class EventBindingHelperTests
    {
        [TestMethod]
        public void SetProperty_Speed_Test_01()
        {
            var sw = new Stopwatch();
            var x0 = new TestClass0();
            var x1 = new TestClass1();
            var n = 1_000_000;

            sw.Start();
            for (int i = 0; i < n; i++)
            {
                x0.Id = i;
            }
            sw.Stop();
            var s0 = sw.ElapsedMilliseconds;

            sw.Restart();
            for (int i = 0; i < n; i++)
            {
                x1.Id = i;
            }
            sw.Stop();
            var s1 = sw.ElapsedMilliseconds;

            sw.Restart();
            x1.SuppressNotifyPropertyChange = true;
            for (int i = 0; i < n; i++)
            {
                x1.Id = i;
            }
            sw.Stop();
            var s2 = sw.ElapsedMilliseconds;
        }

        [TestMethod]
        public void BindProperties_Test_01()
        {
            var x1 = new TestClass1() { Id = 1 };
            var x2 = new TestClass1();

            var b = x1.BindToProperty(x => x.Id, x2, x => x.Id);
            Assert.AreEqual(x1.Id, x2.Id);

            x1.Id = 2;
            Assert.AreEqual(x1.Id, x2.Id);

            x1.SuppressNotifyPropertyChange = true;
            x1.Id = 3;
            x1.SuppressNotifyPropertyChange = false;
            Assert.AreNotEqual(x1.Id, x2.Id);

            x2.Id = 4;
            Assert.AreNotEqual(x1.Id, x2.Id);

            x1.Id = 2;
            Assert.AreEqual(x1.Id, x2.Id);

            b.Dispose();
        }

        [TestMethod]
        public void BindProperties_Test_02()
        {
            var x0 = new TestClass0();
            var x1 = new TestClass1();
            EventHelper.BindProperties(x1, "Id", nameof(INotifyPropertyChanged.PropertyChanged), x0, "Id");
        }
    }

    public class TestClass0 : INotifyPropertyChanged
    {
        private int id;

        public int Id
        {
            get => id;
            set
            {
                if (id == value)
                    return;
                id = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Id)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class TestClass1 : PropertyChangedBase
    {
        public int Id
        { 
            get => Get<int>();
            set => Set(value);
        }
    }
}
