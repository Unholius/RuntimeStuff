using System;
using System.Globalization;
using System.Reflection;

namespace RuntimeStuff
{
    internal class PropertyInfoEx : PropertyInfo
    {
        private readonly PropertyInfo prop;

        public PropertyInfoEx(PropertyInfo propertyInfo)
        {
            prop = this;
        }

        public override object[] GetCustomAttributes(bool inherit) => prop.GetCustomAttributes(inherit);

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => prop.GetCustomAttributes(attributeType, inherit);

        public override bool IsDefined(Type attributeType, bool inherit) => prop.IsDefined(attributeType, inherit);

        public override Type DeclaringType => prop.DeclaringType;

        public override string Name => prop.Name;

        public override Type ReflectedType => prop.ReflectedType;

        public override MethodInfo[] GetAccessors(bool nonPublic) => prop.GetAccessors(nonPublic);

        public override MethodInfo GetGetMethod(bool nonPublic) => prop.GetGetMethod(nonPublic);

        public override ParameterInfo[] GetIndexParameters() => prop.GetIndexParameters();

        public override MethodInfo GetSetMethod(bool nonPublic) => prop.GetSetMethod(nonPublic);

        public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture) => prop.GetValue(obj, invokeAttr, binder, index, culture);

        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture) => prop.SetValue(obj, value, invokeAttr, binder, index, culture);

        public override PropertyAttributes Attributes => prop.Attributes;

        public override bool CanRead => prop.CanRead;

        public override bool CanWrite => prop.CanWrite;

        public override Type PropertyType => prop.PropertyType;
    }
}
