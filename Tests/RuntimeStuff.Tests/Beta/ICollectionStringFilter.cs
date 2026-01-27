using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuntimeStuff.MSTests.Beta
{
    public interface ICollectionStringFilter<T>
    {
        IEnumerable<T> Filter(IEnumerable<T> list, string filterExpression);
    }
}
