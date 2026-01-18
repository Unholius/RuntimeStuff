namespace RuntimeStuff
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using RuntimeStuff.Helpers;

    public class MemberCache : MemberInfo
    {
        protected static readonly ConcurrentDictionary<Type, MemberCache> TypeCache = new ConcurrentDictionary<Type, MemberCache>();
        private readonly CasePriorityDictionary<Attribute> memberAttributesMap = new CasePriorityDictionary<Attribute>();
        private readonly ConcurrentDictionary<MemberInfo, MemberCache> memberCacheMap = new ConcurrentDictionary<MemberInfo, MemberCache>();
        private readonly CasePriorityDictionary<EventInfo> memberEventsMap = new CasePriorityDictionary<EventInfo>();
        private readonly CasePriorityDictionary<FieldInfo> memberFieldsMap = new CasePriorityDictionary<FieldInfo>();
        private readonly CasePriorityDictionary<MethodInfo> memberMethodsMap = new CasePriorityDictionary<MethodInfo>();
        private readonly CasePriorityDictionary<PropertyInfo> memberPropertiesMap = new CasePriorityDictionary<PropertyInfo>();
        private readonly Type type;
        private readonly MemberCache typeCache;
        private Type[] baseTypes;
        private MemberCache[] columns;
        private MemberCache[] fks;
        private string jsonName;
        private Attribute[] memberAttributes;
        private ConstructorInfo[] memberConstructors;
        private EventInfo[] memberEvents;
        private FieldInfo[] memberFields;
        private PropertyInfo[] memberProperties;
        private MethodInfo[] methods;
        private MemberCache[] pks;
        private MemberCache[] propertiesCache;
        private MemberCache[] publicBasicProperties = null;
        private MemberCache[] publicProperties = null;
        private MemberCache[] tables;

        private string xmlAttr;

        private string xmlElem;

        public MemberCache(MemberInfo memberInfo)
            : this(memberInfo, null)
        {
        }

        private MemberCache(MemberInfo memberInfo, MemberCache parent)
        {
            if (memberInfo == null)
            {
                throw new ArgumentNullException(nameof(memberInfo));
            }

            this.Parent = parent;
            this.typeCache = memberInfo as MemberCache;
            if (this.typeCache != null)
            {
                memberInfo = this.typeCache.MemberInfo;
            }

            this.MemberInfo = memberInfo;

            var t = this.MemberInfo as Type;
            var pi = this.MemberInfo as PropertyInfo;
            var fi = this.MemberInfo as FieldInfo;
            var mi = this.MemberInfo as MethodInfo;
            var ci = this.MemberInfo as ConstructorInfo;
            var mx = this.MemberInfo as MemberCache;
            var e = this.MemberInfo as EventInfo;

            if (t != null)
            {
                this.type = t;
            }

            if (pi != null)
            {
                this.type = pi.PropertyType;
            }

            if (fi != null)
            {
                this.type = fi.FieldType;
            }

            if (mi != null)
            {
                this.type = mi.ReturnType;
            }

            if (ci != null)
            {
                this.type = ci.DeclaringType;
            }

            if (mx != null)
            {
                this.type = mx.Type;
            }

            if (e != null)
            {
                this.type = e.EventHandlerType;
            }

            this.IsDictionary = this.typeCache?.IsDictionary ?? Obj.IsDictionary(this.type);
            this.IsDelegate = this.typeCache?.IsDelegate ?? Obj.IsDelegate(this.type);
            this.IsFloat = this.typeCache?.IsFloat ?? Obj.IsFloat(this.type);
            this.IsNullable = this.typeCache?.IsNullable ?? Obj.IsNullable(this.type);
            this.IsNumeric = this.typeCache?.IsNumeric ?? Obj.IsNumeric(this.type);
            this.IsBoolean = this.typeCache?.IsBoolean ?? Obj.IsBoolean(this.type);
            this.IsBasic = this.typeCache?.IsBasic ?? Obj.IsBasic(this.type);
            this.IsEnum = this.typeCache?.IsEnum ?? type?.IsEnum ?? false;
            this.IsConst = this.typeCache?.IsConst ?? (fi != null && fi.IsLiteral && !fi.IsInitOnly);
            this.IsObject = this.typeCache?.IsObject ?? this.type == typeof(object);
            this.IsTuple = this.typeCache?.IsTuple ?? Obj.IsTuple(this.type);
            this.IsProperty = pi != null;
            this.IsEvent = e != null;
            this.IsField = fi != null;
            this.IsType = t != null;
            this.IsMethod = mi != null;
            this.IsConstructor = ci != null;
            this.IsPublic = this.typeCache?.IsPublic ?? Obj.IsPublic(this.MemberInfo);
            this.IsPrivate = this.typeCache?.IsPrivate ?? Obj.IsPrivate(this.MemberInfo);
            this.IsCollection = this.typeCache?.IsCollection ?? Obj.IsCollection(this.type);
            this.ElementType = this.IsCollection ? this.typeCache?.ElementType ?? Obj.GetCollectionItemType(this.Type) : null;
            this.IsBasicCollection = this.typeCache?.IsBasicCollection ?? (this.IsCollection && Obj.IsBasic(this.ElementType));
            this.CanWrite = pi != null ? pi.CanWrite : fi != null;
            this.CanRead = pi != null ? pi.CanRead : fi != null;
            this.Name = this.typeCache?.Name ?? this.MemberInfo.Name.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? string.Empty;

            this.Description = this.typeCache?.Description ??
                          this.MemberInfo.GetCustomAttributes<DescriptionAttribute>().FirstOrDefault()?.Description;
            this.DisplayName = this.typeCache?.DisplayName ?? this.MemberInfo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

            if (this.DisplayName == null)
            {
                var da = GetAttribute("DisplayAttribute");
                this.DisplayName = da?.GetType().GetProperty("Name")?.GetValue(da)?.ToString();
                this.GroupName = da?.GetType().GetProperty("GroupName")?.GetValue(da)?.ToString();
            }

            if (this.IsType && !this.IsBasic)
            {
                this.DefaultConstructor = this.typeCache?.DefaultConstructor ?? CreateConstructorDelegate(t);

                if (this.typeCache == null)
                {
                    var tblAttr = this.GetAttribute("TableAttribute");
                    if (tblAttr != null)
                    {
                        var tblNameProperty = tblAttr.GetType().GetProperty("Name");
                        var tblSchemaProperty = tblAttr.GetType().GetProperty("Schema");
                        this.TableName = tblNameProperty?.GetValue(tblAttr)?.ToString();
                        this.SchemaName = tblSchemaProperty?.GetValue(tblAttr)?.ToString();
                    }
                    else
                    {
                        this.TableName = this.Name;
                    }
                }
                else
                {
                    this.TableName = this.typeCache.TableName;
                    this.SchemaName = this.typeCache.SchemaName;
                }
            }

            if (pi != null)
            {
                if (this.Parent == null && pi?.DeclaringType != null)
                {
                    this.Parent = Create(pi.DeclaringType);
                }

                this.PropertyType = pi.PropertyType;
                this.IsSetterPublic = pi.GetSetMethod()?.IsPublic == true;
                this.IsSetterPrivate = pi.GetSetMethod()?.IsPrivate == true;
                this.IsGetterPublic = pi.GetGetMethod()?.IsPublic == true;
                this.IsGetterPrivate = pi.GetGetMethod()?.IsPrivate == true;
                this.TableName = this.Parent?.TableName;
                this.SchemaName = this.Parent?.SchemaName;

                if (this.typeCache == null)
                {
                    var keyAttr = this.GetAttribute("KeyAttribute");
                    var colAttr = this.GetAttribute("ColumnAttribute");
                    var fkAttr = this.GetAttribute("ForeignKeyAttribute");
                    this.IsPrimaryKey = keyAttr != null || string.Equals(this.Name, "id", StringComparison.OrdinalIgnoreCase);
                    this.IsForeignKey = fkAttr != null;
                    try
                    {
                        this.PropertyBackingField = this.Parent.GetFields().FirstOrDefault(x => x.Name == $"<{this.Name}>k__BackingField") ??
                                               Obj.GetFieldInfoFromGetAccessor(pi.GetGetMethod(true));
                    }
                    catch
                    {
                        // ignore
                    }

                    try
                    {
                        this.Setter = Obj.PropertySetterCache.Get(pi);

                        if (this.Setter == null && this.PropertyBackingField != null)
                        {
                            this.Setter = Obj.FieldSetterCache.Get(this.PropertyBackingField);
                        }
                    }
                    catch (Exception)
                    {
                        this.Setter = (o, v) => pi.SetValue(o, v);
                    }

                    try
                    {
                        this.Getter = Obj.PropertyGetterCache.Get(pi);
                    }
                    catch (Exception)
                    {
                        this.Getter = o => pi.GetValue(o);
                    }

                    this.TableName = this.Parent?.TableName;
                    this.ColumnName = colAttr != null
                        ? colAttr.GetType().GetProperty("Name")?.GetValue(colAttr)?.ToString() ?? this.Name
                        : this.Name;

                    this.ForeignKeyName = fkAttr?.GetType().GetProperty("Name")?.GetValue(fkAttr)?.ToString() ??
                                        string.Empty;
                }
                else
                {
                    this.Setter = this.typeCache.Setter;
                    this.Getter = this.typeCache.Getter;
                    this.PropertyBackingField = this.typeCache.PropertyBackingField;
                    this.ColumnName = this.typeCache.ColumnName;
                    this.ForeignKeyName = this.typeCache.ForeignKeyName;
                    this.IsPrimaryKey = this.typeCache.IsPrimaryKey;
                    this.IsForeignKey = this.typeCache.IsForeignKey;
                }
            }

            if (fi != null)
            {
                this.IsSetterPublic = true;
                this.IsSetterPrivate = false;
                this.IsGetterPublic = true;
                this.IsGetterPrivate = false;
                this.FieldType = fi.FieldType;
                try
                {
                    this.Setter = this.typeCache?.Setter ?? Obj.FieldSetterCache.Get(fi);
                }
                catch
                {
                    this.Setter = (obj, value) => fi.SetValue(obj, value);
                }

                try
                {
                    this.Getter = this.typeCache?.Getter ?? Obj.FieldGetterCache.Get(fi);
                }
                catch (Exception)
                {
                    this.Getter = x => fi.GetValue(x);
                }
            }
        }

        public static BindingFlags DefaultBindingFlags { get; set; } = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic;

        public static Dictionary<Type, Type> InterfaceToInstanceMap { get; } = new Dictionary<Type, Type>
        {
            { typeof(IEnumerable), typeof(List<object>) },
            { typeof(IEnumerable<>), typeof(List<>) },
            { typeof(ICollection), typeof(ObservableCollection<object>) },
            { typeof(ICollection<>), typeof(ObservableCollection<>) },
            { typeof(IDictionary<,>), typeof(Dictionary<,>) },
        };

        public Type[] BaseTypes
        {
            get
            {
                if (this.baseTypes != null)
                {
                    return this.baseTypes;
                }

                this.baseTypes = Obj.GetBaseTypes(this.type, getInterfaces: true);
                return this.baseTypes;
            }
        }

        public bool CanRead { get; }

        public bool CanWrite { get; }

        public string ColumnName { get; }

        public MemberCache[] ColumnProperties
        {
            get
            {
                if (columns != null)
                    return columns;

                columns = this.typeCache?.ColumnProperties ?? this.PublicBasicProperties.Where(x =>
                        !x.IsPrimaryKey
                        && x.GetAttribute("ColumnAttribute") != null
                        && x.GetAttribute("NotMappedAttribute") == null)
                    .ToArray();

                if (columns.Length == 0)
                {
                    columns = this.PublicBasicProperties.Where(x => !x.IsPrimaryKey)
                        .ToArray();
                }

                return columns;
            }
        }

        public ConstructorInfo[] Constructors => this.GetConstructors();

        public override Type DeclaringType => this.MemberInfo.DeclaringType;

        public Func<object> DefaultConstructor { get; }

        public string Description { get; }

        public string DisplayName { get; }

        public Type ElementType { get; }

        public Type FieldType { get; }

        public string ForeignKeyName { get; }

        public MemberCache[] ForeignKeys
        {
            get
            {
                if (fks != null)
                    return fks;

                fks = this.typeCache?.ForeignKeys ?? this.PublicBasicProperties
                    .Where(x => x.GetAttribute("ForeignKeyAttribute") != null)
                    .ToArray();

                return fks;
            }
        }

        public Func<object, object> Getter { get; }
        public string GroupName { get; }

        public bool IsBasic { get; }

        public bool IsBasicCollection { get; }

        public bool IsBoolean { get; }

        public bool IsClass => this.Type.IsClass;

        public bool IsCollection { get; }

        public bool IsConst { get; }

        public bool IsConstructor { get; }

        public bool IsDelegate { get; }

        public bool IsDictionary { get; }

        public bool IsEnum { get; }

        public bool IsEvent { get; }

        public bool IsField { get; }

        public bool IsFloat { get; }

        public bool IsForeignKey { get; }

        public bool IsGetterPrivate { get; }

        public bool IsGetterPublic { get; }

        public bool IsIdentity => this.IsPrimaryKey && (Obj.IsNumeric(this.Type, false) || this.Type == typeof(Guid));

        public bool IsInterface => this.Type.IsInterface;

        public bool IsMethod { get; set; }

        public bool IsNullable { get; }

        public bool IsNumeric { get; }

        public bool IsObject { get; }

        public bool IsPrimaryKey { get; set; }

        public bool IsPrivate { get; }

        public bool IsProperty { get; }

        public bool IsPublic { get; }

        public bool IsSetterPrivate { get; }

        public bool IsSetterPublic { get; }

        public bool IsTuple { get; }

        public bool IsType { get; }

        public bool IsValueType => this.type.IsValueType;

        public string JsonName
        {
            get
            {
                if (this.jsonName != null)
                {
                    return this.jsonName;
                }

                if (this.typeCache == null)
                {
                    this.jsonName = string.Empty;
                    var jsonAttr = this.GetAttributes().FirstOrDefault(x => x.GetType().Name.StartsWith("Json"));
                    if (jsonAttr == null)
                    {
                        return this.jsonName;
                    }

                    var propName = jsonAttr.GetType().GetProperties().FirstOrDefault(p => p.Name.EndsWith("Name"));
                    if (propName != null)
                    {
                        this.jsonName = propName.GetValue(jsonAttr)?.ToString();
                    }
                }
                else
                {
                    this.jsonName = this.typeCache.jsonName;
                }

                return this.jsonName;
            }
        }

        public override MemberTypes MemberType => this.MemberInfo.MemberType;

        public override sealed string Name { get; }

        public MemberCache Parent { get; private set; }

        public MemberCache[] PrimaryKeys
        {
            get
            {
                if (pks != null)
                    return pks;

                pks = this.typeCache?.PrimaryKeys ??
                      this.PublicBasicProperties.Where(x => x.GetAttribute("KeyAttribute") != null).ToArray();

                if (pks.Length == 0)
                {
                    var p =
                        this.PublicBasicProperties.FirstOrDefault(x =>
                            x.Name.Equals("id", StringComparison.OrdinalIgnoreCase)) ??
                        this.PublicBasicProperties.FirstOrDefault(x =>
                            x.Name.Equals(this.TableName + "id", StringComparison.OrdinalIgnoreCase));
                    if (p != null)
                    {
                        pks = new[] { p };
                    }
                }

                return pks;
            }
        }

        public MemberCache[] Properties
        {
            get
            {
                if (this.propertiesCache != null)
                {
                    return this.propertiesCache;
                }

                propertiesCache = GetProperties().Select(x => new MemberCache(x, this)).ToArray();
                memberPropertiesMap.Init(memberProperties, x => x.Name);
                return this.propertiesCache;
            }
        }

        public FieldInfo PropertyBackingField { get; }

        public Type PropertyType { get; }

        public Dictionary<string, MemberCache> PublicBasicEnumerableProperties { get; }

        public MemberCache[] PublicBasicProperties
        {
            get
            {
                if (this.publicBasicProperties != null)
                {
                    return this.publicBasicProperties;
                }

                this.publicBasicProperties = this.PublicProperties.Where(x => x.IsBasic).ToArray();
                return this.publicBasicProperties;
            }
        }

        public Dictionary<string, MemberCache> PublicEnumerableProperties { get; }

        public Dictionary<string, MemberCache> PublicFields { get; }

        public MemberCache[] PublicProperties
        {
            get
            {
                if (this.publicProperties != null)
                {
                    return this.publicProperties;
                }

                this.publicProperties = this.Properties.Where(x => x.IsPublic).ToArray();
                return this.publicProperties;
            }
        }

        public override Type ReflectedType => this.MemberInfo.ReflectedType;

        public string SchemaName { get; }

        public Action<object, object> Setter { get; }

        public string TableName { get; }

        public Type Type => this.type;

        public string XmlAttributeName
        {
            get
            {
                if (this.xmlAttr != null)
                {
                    return this.xmlAttr;
                }

                if (this.typeCache == null)
                {
                    var xmlAttrs = this.GetAttributes().Where(x => x.GetType().Name.StartsWith("Xml")).ToArray();
                    if (xmlAttrs.Any())
                    {
                        foreach (var xa in xmlAttrs)
                        {
                            var propName = xa.GetType().GetProperties().FirstOrDefault(p => p.Name.EndsWith("Name"));
                            switch (propName?.Name)
                            {
                                case "ElementName":
                                    this.xmlElem = propName.GetValue(xa)?.ToString();
                                    break;

                                case "AttributeName":
                                    this.xmlAttr = propName.GetValue(xa)?.ToString();
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    this.xmlAttr = this.typeCache.xmlAttr;
                }

                return this.xmlAttr ?? (this.xmlAttr = string.Empty);
            }
        }

        public string XmlElementName
        {
            get
            {
                if (this.xmlElem != null)
                {
                    return this.xmlElem;
                }

                if (this.typeCache == null)
                {
                    var xmlAttrs = this.GetAttributes().Where(x => x.GetType().Name.StartsWith("Xml")).ToArray();
                    if (xmlAttrs.Any())
                    {
                        foreach (var xa in xmlAttrs)
                        {
                            var propName = xa.GetType().GetProperties().FirstOrDefault(p => p.Name.EndsWith("Name"));
                            switch (propName?.Name)
                            {
                                case "ElementName":
                                    this.xmlElem = propName.GetValue(xa)?.ToString();
                                    break;

                                case "AttributeName":
                                    this.xmlAttr = propName.GetValue(xa)?.ToString();
                                    break;
                            }
                        }
                    }

                    if (this.xmlElem == null)
                    {
                        this.xmlElem = string.Empty;
                    }
                }
                else
                {
                    this.xmlElem = this.typeCache.xmlElem;
                }

                return this.xmlElem;
            }
        }

        public string XmlName { get; } = null;

        internal MemberInfo MemberInfo { get; }

        public object this[object source, string memberName]
        {
            get => this.GetMember(memberName, MemberTypes.All)?.GetValue(source);

            set => this.GetMember(memberName, MemberTypes.All)?.SetValue(source, value);
        }

        public MemberCache this[string memberName, MemberTypes memberType = MemberTypes.Property] => GetMember(memberName, memberType);

        public MemberCache this[MemberInfo memberInfo]
        {
            get
            {
                return memberCacheMap.GetOrAdd(memberInfo, x => new MemberCache(x, this));
            }
        }

        public static MemberCache Create(MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case MemberCache me:
                    return me;

                case Type t:
                    return TypeCache.GetOrAdd(t, x => new MemberCache(x, null));

                default:
                    {
                        var declaringTypeCache = TypeCache.GetOrAdd(memberInfo.DeclaringType ?? throw new InvalidOperationException(), x => new MemberCache(x, null));
                        return declaringTypeCache[memberInfo];
                    }
            }
        }

        public static Func<object> CreateConstructorDelegate(Type type)
        {
            if (type == null)
            {
                return null;
            }

            var constructorInfo = type.GetConstructor(Type.EmptyTypes);
            if (constructorInfo == null)
                return null;

            var ctor = type.IsGenericTypeDefinition
                ? () => Activator.CreateInstance(type)
                : Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(constructorInfo), typeof(object)))
                    .Compile();

            return ctor;
        }

        public static implicit operator ConstructorInfo(MemberCache mc)
        {
            var constructorInfo = mc.AsConstructorInfo();
            return constructorInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to ConstructorInfo. Member is a {mc.MemberType}.");
        }

        public static implicit operator EventInfo(MemberCache mc)
        {
            var eventInfo = mc.AsEventInfo();
            return eventInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to EventInfo. Member is a {mc.MemberType}.");
        }

        public static implicit operator FieldInfo(MemberCache mc)
        {
            var fieldInfo = mc.AsFieldInfo();
            return fieldInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to FieldInfo. Member is a {mc.MemberType}.");
        }

        public static implicit operator MemberCache(PropertyInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        public static implicit operator MemberCache(FieldInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        public static implicit operator MemberCache(MethodInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        public static implicit operator MemberCache(EventInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        public static implicit operator MemberCache(ConstructorInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        public static implicit operator MemberCache(Type memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        public static implicit operator MethodInfo(MemberCache mc)
        {
            var methodInfo = mc.AsMethodInfo();
            return methodInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to MethodInfo. Member is a {mc.MemberType}.");
        }

        public static implicit operator PropertyInfo(MemberCache mc)
        {
            var propertyInfo = mc.AsPropertyInfo();
            return propertyInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to PropertyInfo. Member is a {mc.MemberType}.");
        }

        public static implicit operator Type(MemberCache mc)
        {
            return mc.Type;
        }

        public static object New(Type type, params object[] ctorArgs)
        {
            if (ctorArgs == null)
            {
                ctorArgs = Array.Empty<object>();
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            var typeInfo = Create(type);
            if (typeInfo.DefaultConstructor != null && ctorArgs.Length == 0)
            {
                return typeInfo.DefaultConstructor();
            }

            if (typeInfo.IsDelegate)
            {
                return null;
            }

            if (type.IsInterface)
            {
                if (typeInfo.IsCollection)
                {
                    if (!InterfaceToInstanceMap.TryGetValue(type, out var lstType))
                    {
                        InterfaceToInstanceMap.TryGetValue(type.GetGenericTypeDefinition(), out lstType);
                    }

                    var genericArgs = type.GetGenericArguments();
                    if (genericArgs.Length == 0)
                    {
                        genericArgs = new[] { typeof(object) };
                    }

                    if (lstType != null && lstType.IsGenericTypeDefinition)
                    {
                        lstType = lstType.MakeGenericType(genericArgs);
                    }

                    if (lstType != null)
                    {
                        return Activator.CreateInstance(lstType);
                    }
                }

                throw new NotImplementedException();
            }

            if (type.IsArray)
            {
                if (ctorArgs.Length == 0)
                {
                    return Activator.CreateInstance(type, 0);
                }

                if (ctorArgs.Length == 1 && ctorArgs[0] is int)
                {
                    return Activator.CreateInstance(type, ctorArgs[0]);
                }

                return Activator.CreateInstance(type, ctorArgs.Length);
            }

            if (type.IsEnum)
            {
                return ctorArgs.FirstOrDefault(x => x?.GetType() == type) ?? Obj.Default(type);
            }

            if (type == typeof(string) && ctorArgs.Length == 0)
            {
                return string.Empty;
            }

            var defaultCtor = typeInfo.DefaultConstructor;
            if (defaultCtor != null && ctorArgs.Length == 0)
            {
                try
                {
                    return defaultCtor();
                }
                catch
                {
                    return Obj.Default(type);
                }
            }

            var ctor = typeInfo.GetConstructorByArgs(ref ctorArgs);

            if (ctor == null && type.IsValueType)
            {
                return Obj.Default(type);
            }

            if (ctor == null)
            {
                throw new InvalidOperationException(
                    $"Не найден конструктор для типа '{type}' с аргументами '{string.Join(",", ctorArgs.Select(arg => arg?.GetType()))}'");
            }

            return ctor.Invoke(ctorArgs);
        }

        public ConstructorInfo AsConstructorInfo() => this.MemberInfo as ConstructorInfo;

        public EventInfo AsEventInfo() => this.MemberInfo as EventInfo;

        public FieldInfo AsFieldInfo() => this.MemberInfo as FieldInfo;

        public MethodInfo AsMethodInfo() => this.MemberInfo as MethodInfo;

        public PropertyInfo AsPropertyInfo() => this.MemberInfo as PropertyInfo;

        public Type AsType() => this.MemberInfo as Type;

        public TAttribute GetAttribute<TAttribute>(string attributeTypeName)
            where TAttribute : Attribute
            => GetAttribute(attributeTypeName) as TAttribute;

        public Attribute GetAttribute(string attributeTypeName)
        {
            if (string.IsNullOrWhiteSpace(attributeTypeName))
                return null;
            if (!attributeTypeName.EndsWith(nameof(Attribute)))
                attributeTypeName += nameof(Attribute);

            return memberAttributesMap.GetOrAdd(
                attributeTypeName,
                n => GetAttributes().FirstOrDefault(x => x.GetType().Name.Equals(n)) ?? GetAttributes().FirstOrDefault(x => x.GetType().Name.Equals(n, StringComparison.OrdinalIgnoreCase)),
                a => a.GetType().Name);
        }

        public Attribute[] GetAttributes()
        {
            if (this.memberAttributes != null)
            {
                return this.memberAttributes;
            }

            this.memberAttributes = this.MemberInfo
                .GetCustomAttributes()
                .Concat(this.BaseTypes.SelectMany(x => x.GetCustomAttributes()))
                .Distinct()
                .ToArray();

            memberAttributesMap.Init(memberAttributes, x => x.GetType().Name);

            return this.memberAttributes;
        }

        public MemberCache[] GetColumns()
        {
            if (this.columns != null)
            {
                return this.columns;
            }

            this.columns = this.GetProperties()
                .Select(pi => this[pi])
                .Where(x => x.IsProperty &&
                            x.IsPublic &&
                            x.IsBasic &&
                            !x.IsCollection &&
                            x.HasAnyAttributeOfType("ColumnAttribute", "KeyAttribute", "ForeignKeyAttribute")
                            && !x.HasAnyAttributeOfType("NotMappedAttribute"))
                .ToArray();

            return this.columns;
        }

        public ConstructorInfo GetConstructorByArgs(ref object[] ctorArgs)
        {
            var args = ctorArgs;
            foreach (var c in this.GetConstructors())
            {
                var pAll = c.GetParameters();
                if (pAll.Length == ctorArgs.Length && All(ctorArgs, (_, i) =>
                        Obj.IsImplements(args[i]?.GetType(), pAll[i].ParameterType)))
                {
                    return c;
                }

                var pNoDef = c.GetParameters().Where(p => !p.HasDefaultValue).ToArray();

                if (pNoDef.Length == ctorArgs.Length && All(ctorArgs, (_, i) => Obj.IsImplements(args[i]?.GetType(), pNoDef[i].ParameterType)))
                {
                    Array.Resize(ref ctorArgs, pAll.Length);
                    for (var i = pNoDef.Length; i < pAll.Length; i++)
                    {
                        ctorArgs[i] = pAll[i].DefaultValue;
                    }

                    return c;
                }
            }

            var ctor = this.Constructors.FirstOrDefault(x => x.GetParameters().Length == args.Length);
            if (ctor != null)
            {
                var ctorParameters = ctor.GetParameters();
                ctorArgs = ctorParameters.Select((x, i) => Obj.ChangeType(args[i], x.ParameterType)).ToArray();
                return ctor;
            }

            return null;
        }

        public ConstructorInfo[] GetConstructors()
        {
            if (this.memberConstructors != null)
            {
                return this.memberConstructors;
            }

            this.memberConstructors = this.type.GetConstructors(DefaultBindingFlags)
                .Concat(this.BaseTypes.Where(x => !x.IsInterface)
                .SelectMany(x => x.GetConstructors(DefaultBindingFlags)))
                .OrderBy(c => c.GetParameters().Length)
                .Distinct()
                .ToArray();
            return this.memberConstructors;
        }

        public override object[] GetCustomAttributes(bool inherit) => this.MemberInfo.GetCustomAttributes(inherit);

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => this.MemberInfo.GetCustomAttributes(attributeType, inherit);

        public EventInfo[] GetEvents()
        {
            if (this.memberEvents != null)
            {
                return this.memberEvents;
            }

            this.memberEvents = this.type.GetEvents(DefaultBindingFlags)
                .Concat(this.BaseTypes.Where(x => !x.IsInterface)
                .SelectMany(x => x.GetEvents(DefaultBindingFlags)))
                .Distinct()
                .ToArray();
            return this.memberEvents;
        }

        public MemberCache GetField(string fieldName, bool ignoreCase = true) => GetMember(fieldName, MemberTypes.Field, ignoreCase);

        public FieldInfo[] GetFields()
        {
            if (this.memberFields != null)
            {
                return this.memberFields;
            }

            this.memberFields = this.type.GetFields(DefaultBindingFlags)
                .Concat(this.BaseTypes.Where(x => !x.IsInterface)
                    .SelectMany(x => x.GetFields(DefaultBindingFlags)))
                .Distinct()
                .ToArray();

            this.memberFieldsMap.Init(memberFields, x => x.Name);

            return this.memberFields;
        }

        public MemberCache GetForeignKey(Type children)
        {
            var childrenCache = Create(children);
            return childrenCache.ForeignKeys.FirstOrDefault(fk =>
            {
                var nav = childrenCache.GetProperty(fk.ForeignKeyName);
                return nav?.PropertyType == this.Type;
            });
        }

        public MemberCache[] GetForeignKeys()
        {
            if (this.fks != null)
            {
                return this.fks;
            }

            this.fks = this.GetColumns()
                .Where(x => x.IsForeignKey)
                .ToArray();

            return this.fks;
        }

        public string GetFullColumnName() => this.GetFullColumnName("[", "]");

        public string GetFullColumnName(string namePrefix, string nameSuffix, string defaultSchemaName = null) => this.GetFullTableName(namePrefix, nameSuffix, defaultSchemaName) +
                   $".{namePrefix}{this.ColumnName}{nameSuffix}";

        public string GetFullTableName() => this.GetFullTableName("[", "]");

        public string GetFullTableName(string namePrefix, string nameSuffix, string defaultSchemaName = null)
        {
            var schema = string.IsNullOrWhiteSpace(this.SchemaName) ? defaultSchemaName : this.SchemaName;
            var fullTableName = $"{namePrefix}{this.TableName}{nameSuffix}";
            if (!string.IsNullOrWhiteSpace(schema))
            {
                fullTableName = $"{namePrefix}{schema}{nameSuffix}." + fullTableName;
            }

            return fullTableName;
        }

        public TMember GetMember<TMember>(string memberName, MemberTypes memberType, bool ignoreCase = true)
                                                                                                                            where TMember : MemberInfo
        {
            return (TMember)GetMember(memberName, memberType, ignoreCase).MemberInfo;
        }

        public MemberCache GetMember(string memberName, MemberTypes memberType, bool ignoreCase = true)
        {
            switch (memberType)
            {
                case MemberTypes.Property:
                    var propInfo = memberPropertiesMap.GetOrAdd(memberName, x => type.GetProperty(x, DefaultBindingFlags), p => p.Name, ignoreCase);
                    return propInfo == null ? null : memberCacheMap.GetOrAdd(propInfo, x => new MemberCache(x, this));

                case MemberTypes.Field:
                    var fieldInfo = memberFieldsMap.GetOrAdd(memberName, x => type.GetField(x, DefaultBindingFlags), f => f.Name, ignoreCase);
                    return fieldInfo == null ? null : memberCacheMap.GetOrAdd(fieldInfo, x => new MemberCache(x, this));

                case MemberTypes.Method:
                    var methodInfo = memberMethodsMap.GetOrAdd(memberName, x => type.GetMethod(x, DefaultBindingFlags), m => m.Name, ignoreCase);
                    return methodInfo == null ? null : memberCacheMap.GetOrAdd(methodInfo, x => new MemberCache(x, this));

                case MemberTypes.Event:
                    var eventInfo = memberEventsMap.GetOrAdd(memberName, x => type.GetEvent(x, DefaultBindingFlags), e => e.Name, ignoreCase);
                    return eventInfo == null ? null : memberCacheMap.GetOrAdd(eventInfo, x => new MemberCache(x, this));

                case MemberTypes.All:
                    return GetMember(memberName, MemberTypes.Property, ignoreCase) ??
                           GetMember(memberName, MemberTypes.Field, ignoreCase) ??
                           GetMember(memberName, MemberTypes.Method, ignoreCase) ??
                           GetMember(memberName, MemberTypes.Event, ignoreCase);

                case MemberTypes.Constructor:
                case MemberTypes.Custom:
                case MemberTypes.NestedType:
                case MemberTypes.TypeInfo:
                default:
                    throw new NotSupportedException(nameof(memberType));
            }
        }

        public MethodInfo[] GetMethods()
        {
            if (this.methods != null)
            {
                return this.methods;
            }

            this.methods = this.type.GetMethods(DefaultBindingFlags)
                .Concat(this.BaseTypes.Where(x => !x.IsInterface)
                    .SelectMany(x => x.GetMethods(DefaultBindingFlags)))
                .Distinct()
                .ToArray();

            return this.methods;
        }

        public MemberCache[] GetPrimaryKeys()
        {
            if (this.pks != null)
            {
                return this.pks;
            }

            this.pks = this.GetColumns()
                .Where(x => x.IsPrimaryKey)
                .ToArray();

            return this.pks;
        }

        public PropertyInfo[] GetProperties()
        {
            if (this.memberProperties != null)
            {
                return this.memberProperties;
            }

            var props = this.type.GetProperties(DefaultBindingFlags)
                .Concat(
                    this.BaseTypes
                        .Where(x => !x.IsInterface)
                        .SelectMany(x => x.GetProperties(DefaultBindingFlags)))
                .ToList();

            var seen = new HashSet<string>();

            this.memberProperties = props
                .Where(p => seen.Add(p.Name))
                .ToArray();

            return this.memberProperties;
        }

        public PropertyInfo GetProperty(string propertyName) => this[propertyName]?.AsPropertyInfo();

        public MemberCache[] GetTables()
        {
            if (this.tables != null)
            {
                return this.tables;
            }

            this.tables = this.Properties.Where(x =>
                x.IsProperty &&
                x.IsPublic &&
                ((x.IsCollection &&
                !x.IsBasicCollection) || !x.IsBasic) &&
                !x.HasAnyAttributeOfType("ColumnAttribute", "NotMappedAttribute", "Key"))
                .ToArray();

            return this.tables;
        }

        public object GetValue(object instance) => this.Getter(instance);

        public T GetValue<T>(object instance) => Obj.ChangeType<T>(this.Getter(instance));

        public bool HasAllAttributeOfType(params string[] attributeTypeNames)
        {
            return attributeTypeNames.All(x => GetAttribute(x) != null);
        }

        public bool HasAnyAttributeOfType(params string[] attributeTypeNames)
        {
            return attributeTypeNames.Any(x => GetAttribute(x) != null);
        }

        public override bool IsDefined(Type attributeType, bool inherit) => this.MemberInfo.IsDefined(attributeType, inherit);

        public virtual void SetValue(object source, object value, Func<object, object> valueConverter = null) => this.Setter(source, valueConverter == null ? Obj.ChangeType(value, this.Type) : valueConverter(value));

        public Dictionary<string, object> ToDictionary<T>(T instance, params string[] propertyNames)
            where T : class
        {
            var dic = new Dictionary<string, object>();

            this.ToDictionary(instance, dic, propertyNames);

            return dic;
        }

        public void ToDictionary<T>(T instance, Dictionary<string, object> dictionary, params string[] propertyNames)
            where T : class
        {
            var props = propertyNames.Any()
                ? this.PublicBasicProperties.Where(x => propertyNames.Contains(x.Name)).ToArray()
                : this.PublicBasicProperties;

            foreach (var mi in props)
            {
                dictionary[mi.Name] = mi.GetValue(instance);
            }
        }

        public override string ToString() => $"{this.DeclaringType?.Name}{this.Name}({this.Type.Name})";

        private static bool All<TSource>(IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            var i = 0;
            foreach (var item in source)
            {
                if (!predicate(item, i))
                {
                    return false;
                }

                i++;
            }

            return true;
        }

        private sealed class CasePriorityDictionary<TValue>
            where TValue : class
        {
            private readonly Dictionary<string, TValue> exactMap =
                new Dictionary<string, TValue>(StringComparer.Ordinal);

            private readonly Dictionary<string, TValue> ignoreCaseMap =
                new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);

            public CasePriorityDictionary()
            {
            }

            public CasePriorityDictionary(
                IEnumerable<TValue> source,
                Func<TValue, string> keySelector)
            {
                Init(source, keySelector);
            }

            public TValue this[string key, bool ignoreCase = true]
            {
                get => Get(key, ignoreCase);

                set => this.Add(key, value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TValue GetOrAdd(string key, Func<string, TValue> valueFactory, Func<TValue, string> keySelector, bool ignoreCase = true)
            {
                if (key == null)
                    return null;
                if (valueFactory == null)
                    throw new ArgumentNullException(nameof(valueFactory));

                if (this.exactMap.TryGetValue(key, out var value))
                    return value;

                if (ignoreCase && this.ignoreCaseMap.TryGetValue(key, out value))
                    return value;

                value = valueFactory(key);

                if (value != null)
                {
                    this.exactMap[keySelector(value)] = value;
                    this.ignoreCaseMap[key] = value;
                }
                else
                {
                    this.exactMap[key] = null;
                    this.ignoreCaseMap[key] = null;
                }

                return value;
            }

            internal void Init(IEnumerable<TValue> source, Func<TValue, string> keySelector)
            {
                if (source is null)
                {
                    throw new ArgumentNullException(nameof(source));
                }

                if (keySelector is null)
                {
                    throw new ArgumentNullException(nameof(keySelector));
                }

                foreach (var item in source)
                {
                    if (item is null)
                        continue;

                    var key = keySelector(item);
                    if (key == null)
                        continue;

                    if (!this.exactMap.ContainsKey(key))
                        this.exactMap[key] = item;

                    if (!this.ignoreCaseMap.ContainsKey(key))
                        this.ignoreCaseMap[key] = item;
                }
            }

            private void Add(string key, TValue value)
            {
                this.exactMap[key] = value;
                this.ignoreCaseMap[key] = value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private TValue Get(string key, bool ignoreCase = true)
            {
                if (key == null)
                    return null;

                if (this.exactMap.TryGetValue(key, out var v1))
                    return v1;

                return ignoreCase && this.ignoreCaseMap.TryGetValue(key, out var v2) ? v2 : null;
            }
        }
    }
}