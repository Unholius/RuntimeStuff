using FastMember;
using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Helpers;
using System.Reflection;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class EventBindingHelperTests
    {
        [TestMethod]
        public void SetProperty_Speed_Test_02()
        {
            var sw = new Stopwatch();
            var d0 = new Dictionary<string, object>();
            for (int i = 0; i < 100; i++)
                d0[$"p{i}"] = 0;

            var d1 = new Hashtable();
            for (int i = 0; i < 100; i++)
                d1[$"p{i}"] = 0;

            var d2 = new ConcurrentDictionary<string, object>();
            for (int i = 0; i < 100; i++)
                d2[$"p{i}"] = 0;

            var n = 1_000_000;

            sw.Start();
            for (int i = 0; i < n; i++)
            {
                var x = d0["p50"];
            }
            sw.Stop();
            var s0 = sw.ElapsedMilliseconds;

            sw.Restart();
            for (int i = 0; i < n; i++)
            {
                var x = d1["p50"];
            }
            sw.Stop();
            var s1 = sw.ElapsedMilliseconds;

            sw.Restart();
            for (int i = 0; i < n; i++)
            {
                var x = d2["p50"];
            }
            sw.Stop();
            var s2 = sw.ElapsedMilliseconds;

        }

        [TestMethod]
        public void SetProperty_Speed_Test_01()
        {
            var sw = new Stopwatch();
            var x0 = new TestClass0();
            var x1 = new ObservableTestClass1();
            var n = 1_000_000;
            var dt = new DataTable("Results")
                .AddCol("Task")
                .AddCol<long>("Ms")
                .AddRow("Тест скорости установки одного свойства на кол-во итераций: ", n);

            sw.Start();
            for (int i = 0; i < n; i++)
            {
                x0.Id = i;
            }
            sw.Stop();
            var s0 = sw.ElapsedMilliseconds;
            dt.AddRow("Прямое присваивание x.Id = i", s0);

            sw.Restart();
            for (int i = 0; i < n; i++)
            {
                x1.Id = i;
            }
            sw.Stop();
            var s1 = sw.ElapsedMilliseconds;
            dt.AddRow("присваивание через Куликовский аналог BaseObject: ObservavleObject.Set с NotifyPropertyChange", s1);

            x1.SuspendNotifications(true);
            sw.Restart();
            for (int i = 0; i < n; i++)
            {
                x1.Id = i;
            }
            sw.Stop();
            var s2 = sw.ElapsedMilliseconds;
            dt.AddRow("присваивание через ObservavleObject.Set БЕЗ NotifyPropertyChange", s2);

            var mc = MemberCache.Get<TestClass0>();

            sw.Restart();
            for (int i = 0; i < n; i++)
            {
                //mc.Properties[0].Setter(x0, i);
                mc[x0, "Id"] = i;
            }
            sw.Stop();
            var s3 = sw.ElapsedMilliseconds;
            dt.AddRow("Присваивание через динамический Obj.Set", s3);

            var setter1 = Obj.GetMemberSetter<TestClass0>("Id");
            sw.Restart();
            for (int i = 0; i < n; i++)
            {
                setter1(x0, i);
            }
            sw.Stop();
            var s4 = sw.ElapsedMilliseconds;
            dt.AddRow("Присваивание через скомпилированный IL делегат Action<object, object>", s4);

            var prop1 = TypeHelper.GetPublicProperty<TestClass0>("Id");
            sw.Restart();
            for (int i = 0; i < n; i++)
            {
                prop1.SetValue(x0, i);
            }
            sw.Stop();
            var s5 = sw.ElapsedMilliseconds;
            dt.AddRow("Присваивание через reflection PropertyInfo.SetValue", s5);

            var ta = TypeAccessor.Create(typeof(TestClass0));
            sw.Restart();
            for (long i = 0; i < n; i++)
            {
                ta[x0, "Id"] = TypeHelper.ChangeType<int>(i);
            }
            sw.Stop();
            var s6 = sw.ElapsedMilliseconds;
            dt.AddRow("Присваивание через nuget FastMember", s6);
        }

        [TestMethod]
        public void BindProperties_Test_01()
        {
            var x1 = new ObservableTestClass1() { Id = 1 };
            var x2 = new ObservableTestClass1();

            var b = x1.BindToProperty(x => x.Id, x2, x => x.Id);
            Assert.AreEqual(x1.Id, x2.Id);

            x1.Id = 2;
            Assert.AreEqual(x1.Id, x2.Id);

            x1.SuspendNotifications(true);
            x1.Id = 3;
            x1.SuspendNotifications(false);
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
            var x1 = new ObservableTestClass1();
            EventHelper.BindProperties(x1, "Id", nameof(INotifyPropertyChanged.PropertyChanged), x0, "Id");
        }

        [TestMethod]
        public void BindProperties_Test_03()
        {
            var x1 = new ObservableTestClass1();
            var x2 = new ObservableTestClass2();
            x1.Id = 1;
            Assert.AreEqual(1, x1.Id);
            Assert.AreEqual(0, x2.Id);
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

        private string name;
        public string Name
        {
            get => name;
            set
            {
                if (name == value)
                    return;
                name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class ObservableTestClass1 : ObservableObject
    {
        public int Id
        { 
            get => Get<int>();
            set => Set(value);
        }

        public string Name
        {
            get => Get<string>();
            set => Set(value);
        }
    }

    public class ObservableTestClass2 : ObservableObject
    {
        public int Zero
        {
            get => Get<int>();
            set => Set(value);
        }

        public int Id
        {
            get => Get<int>();
            set => Set(value);
        }
    }
}
