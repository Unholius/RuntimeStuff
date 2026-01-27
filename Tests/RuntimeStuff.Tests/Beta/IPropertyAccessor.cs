using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuntimeStuff.MSTests.Beta
{
    public interface IPropertyAccessor
    {
        Func<object> Get { get; }
        Action<object, object> Set { get; }
    }
}
