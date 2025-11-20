using System;
using System.Collections.Generic;
using System.Reflection;
using RuntimeStuff.Helpers;

namespace RuntimeStuff.Options
{
    public abstract class OptionsBase
    {
        protected readonly Dictionary<string, PropertyInfo> PropertyMap;

        protected OptionsBase()
        {
            PropertyMap = TypeHelper.GetPropertiesMap(this.GetType());
        }

        protected OptionsBase(IDictionary<string, object> paramValues) : this()
        {
            foreach (var kvp in paramValues) PropertyMap[kvp.Key].SetValue(this, kvp.Value);
        }

        public object this[string name]
        {
            get => Get<object>(name);
            set => Set(name, value);
        }

        public TValue Get<TValue>(string name)
        {
            return typeof(TValue) == typeof(object) ? (TValue)PropertyMap[name].GetValue(this) : TypeHelper.ChangeType<TValue>(PropertyMap[name].GetValue(this));
        }

        public bool Set<TValue>(string name, TValue value)
        {
            if (!PropertyMap.TryGetValue(name, out var p))
                return false;
            try
            {
                p.SetValue(this, TypeHelper.ChangeType(value, p.PropertyType));
            }
            catch
            {
                return false;
            }

            return true;
        }

        public Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>();
            foreach (var prop in PropertyMap)
            {
                var value = prop.Value.GetValue(this);
                dict[prop.Key] = value;
            }

            return dict;
        }
    }

    public abstract class OptionsBase<T>: OptionsBase where T : OptionsBase<T>, new()
    {

        public static T Default => new T();

        public T Clone()
        {
            return (T)MemberwiseClone();
        }

        public T Merge(OptionsBase other)
        {
            foreach (var prop in PropertyMap)
            {
                var value = prop.Value.GetValue(other);
                if (value != null)
                    prop.Value.SetValue(this, value);
            }

            return (T)this;
        }

        public static T Build(Action<T> configure)
        {
            var instance = Default;
            configure(instance);
            return instance;
        }
    }
}