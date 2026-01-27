using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RuntimeStuff.Extensions;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class EventHelperTests
    {
        [TestMethod]
        public void Test_BindingProperties_01()
        {
            var x = new PropClass();
            x.BindToProperty(z => z.IsBusy, x, z => z.BusyChanged);
            x.IsBusy = true;
            Assert.IsTrue(x.BusyChanged);
        }
    }

    internal class PropClass : PropertyObserver
    {
        public bool IsBusy
        {
            get => Get<bool>();
            set => Set(value);
        }

        public bool BusyChanged { get; set; }
    }
}
