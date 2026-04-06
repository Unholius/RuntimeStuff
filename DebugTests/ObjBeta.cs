using System;
using System.Collections.Generic;
using System.Text;

namespace DebugTests
{
    internal class ObjBeta
    {
        //private static IEnumerable<T> GetMembersInternal<T>(object obj, Func<T, bool> memberFilter, bool recursive, bool searchInCollections, HashSet<object> visited)
        //{
        //    if (obj == null)
        //    {
        //        yield break;
        //    }

        //    var type = obj.GetType();

        //    // Для примитивов и строк обходим только если тип совпадает с T
        //    if (type.IsPrimitive || obj is string)
        //    {
        //        if (obj is T tValue && (memberFilter == null || memberFilter(tValue)))
        //        {
        //            yield return tValue;
        //        }

        //        yield break;
        //    }

        //    if (!visited.Add(obj))
        //    {
        //        yield break;
        //    }

        //    // Если коллекция и нужно искать в коллекциях
        //    if (searchInCollections && obj is IEnumerable enumerable)
        //    {
        //        foreach (var item in enumerable)
        //        {
        //            foreach (var nested in GetMembersInternal(item, memberFilter, recursive, true, visited))
        //            {
        //                yield return nested;
        //            }
        //        }
        //    }

        //    // Поля
        //    var fields = GetFieldsMap(type).Values;
        //    foreach (var field in fields)
        //    {
        //        var value = field.GetValue(obj);
        //        if (value == null)
        //        {
        //            continue;
        //        }

        //        if (value is T tValue && (memberFilter == null || memberFilter(tValue)))
        //        {
        //            yield return tValue;
        //        }

        //        if (recursive && !value.GetType().IsPrimitive && !(value is string))
        //        {
        //            foreach (var nested in GetMembersInternal(value, memberFilter, true, searchInCollections, visited))
        //            {
        //                yield return nested;
        //            }
        //        }
        //    }

        //    // Свойства
        //    var properties = GetPropertiesMap(type).Values.Where(p => p.GetMethod != null);
        //    foreach (var prop in properties)
        //    {
        //        object value;
        //        try
        //        {
        //            value = prop.GetValue(obj);
        //        }
        //        catch
        //        {
        //            continue; // Пропускаем свойства с исключениями
        //        }

        //        switch (value)
        //        {
        //            case null:
        //                continue;
        //            case T tValue when memberFilter == null || memberFilter(tValue):
        //                yield return tValue;
        //                break;
        //        }

        //        if (recursive && !value.GetType().IsPrimitive && !(value is string))
        //        {
        //            foreach (var nested in GetMembersInternal(value, memberFilter, true, searchInCollections, visited))
        //            {
        //                yield return nested;
        //            }
        //        }
        //    }
        //}
    }
}
