// <copyright file="MemberCache.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Reflection
{
    using System;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using StandardExtensions;

    /// <summary>
    /// Предоставляет кэшированную информацию о членах типа (класса, структуры, интерфейса) и их метаданных.
    /// Обеспечивает высокопроизводительный доступ к свойствам, полям, методам, атрибутам и другой рефлексионной информации.
    /// Поддерживает ORM-специфичные метаданные (таблицы, столбцы, ключи) и сериализационные атрибуты (JSON, XML).
    /// </summary>
    /// <remarks>
    /// Класс является оберткой над System.Reflection.MemberInfo с расширенными возможностями и кэшированием.
    /// Все экземпляры кэшируются для избежания накладных расходов на рефлексию.
    /// </remarks>
    public class MemberCache : MemberInfo
    {
        /// <summary>
        /// Статический кэш экземпляров MemberCache для типов.
        /// </summary>
        protected static readonly ConcurrentDictionary<Type, MemberCache> Cache = new();

        private static readonly char[] NamesSeparator = ['.'];
        private static long elapsedMilliseconds;
        private readonly Type type;
        private string jsonName;
        private Attribute[] memberAttributes;
        private FieldInfo propertyBackingField;
        private bool? propertyBackingFieldExists;
        private string xmlAttr;
        private string xmlElem;

        private MemberCache(Type parentType, MemberInfo memberInfo)
        {
            var sw = Stopwatch.StartNew();

            if (memberInfo == null)
            {
                throw new ArgumentNullException(nameof(memberInfo));
            }

            this.ParentType = parentType;
            this.MemberInfo = memberInfo;

            var pi = this.MemberInfo as PropertyInfo;
            var fi = this.MemberInfo as FieldInfo;
            var mi = this.MemberInfo as MethodInfo;
            var ci = this.MemberInfo as ConstructorInfo;
            var mc = this.MemberInfo as MemberCache;
            var ei = this.MemberInfo as EventInfo;

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

            if (mc != null)
            {
                this.type = mc.Type;
            }

            if (ei != null)
            {
                this.type = ei.EventHandlerType;
            }

            this.IsDictionary = this.type.IsDictionary();
            this.IsDelegate = this.type.IsDelegate();
            this.IsFloat = this.type.IsFloat();
            this.IsNullable = this.type.IsNullable();
            this.IsNumeric = this.type.IsNumeric();
            this.IsBoolean = this.type.IsBoolean();
            this.IsBasic = this.type.IsBasic();
            this.IsEnum = this.type?.IsEnum ?? false;
            this.IsConst = fi != null && fi.IsLiteral && !fi.IsInitOnly;
            this.IsObject = this.type == typeof(object);
            this.IsTuple = this.type.IsTuple();
            this.IsProperty = pi != null;
            this.IsEvent = ei != null;
            this.IsField = fi != null;
            this.IsMethod = mi != null;
            this.IsConstructor = ci != null;
            this.IsPublic = IsMemberPublic(this.MemberInfo);
            this.IsPrivate = IsMemberPrivate(this.MemberInfo);
            this.IsCollection = this.type.IsCollection();
            this.ElementType = this.Type.GetCollectionItemType();
            this.IsBasicCollection = this.IsCollection && this.ElementType.IsBasic();
            this.CanWrite = pi != null ? pi.CanWrite : fi != null;
            this.CanRead = pi != null ? pi.CanRead : fi != null;
            this.Name = this.MemberInfo.Name.Split(NamesSeparator, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? string.Empty;
            this.GetAttributes();
            this.Description = this.MemberInfo.GetCustomAttributes<DescriptionAttribute>().FirstOrDefault()?.Description;
            this.DisplayName = this.MemberInfo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

            var da = this.GetAttribute("DisplayAttribute");
            if (da != null)
            {
                if (string.IsNullOrEmpty(this.DisplayName))
                {
                    this.DisplayName = da?.GetType().GetMethod("GetName")?.Invoke(da, null)?.ToString();
                }

                if (string.IsNullOrEmpty(this.Description))
                {
                    this.Description = da?.GetType().GetMethod("GetDescription")?.Invoke(da, null)?.ToString();
                }

                this.GroupName = da?.GetType().GetMethod("GetGroupName")?.Invoke(da, null)?.ToString();
                this.ShortName = da?.GetType().GetMethod("GetShortName")?.Invoke(da, null)?.ToString();
                this.Prompt = da?.GetType().GetProperty("GetPrompt")?.GetValue(da)?.ToString();
                this.Order = (int?)da?.GetType().GetMethod("GetOrder")?.Invoke(da, null);
                this.AutoGenerateFilter = (bool?)da?.GetType().GetMethod("GetAutoGenerateFilter")?.Invoke(da, null);
                this.AutoGenerateField = (bool?)da?.GetType().GetMethod("GetAutoGenerateField")?.Invoke(da, null);
            }

            {
                if (pi != null)
                {
                    this.PropertyType = pi.PropertyType;
                    this.IsIndexer = pi.GetIndexParameters().Length != 0;
                    this.IsSetterPublic = pi.GetSetMethod()?.IsPublic == true;
                    this.IsSetterPrivate = pi.GetSetMethod() == null || pi.GetSetMethod()?.IsPrivate == true;
                    this.IsGetterPublic = pi.GetGetMethod()?.IsPublic == true;
                    this.IsGetterPrivate = pi.GetGetMethod() == null || pi.GetGetMethod()?.IsPrivate == true;
                    this.TableName = this.ParentType.GetAttribute("TableAttribute")?.GetValue<string>("Name") ?? this.ParentType.Name;
                    this.SchemaName = this.ParentType.GetAttribute("TableAttribute")?.GetValue<string>("Schema") ?? string.Empty;
                    {
                        var keyAttr = this.GetAttribute("KeyAttribute");
                        var colAttr = this.GetAttribute("ColumnAttribute");
                        var fkAttr = this.GetAttribute("ForeignKeyAttribute");
                        this.IsPrimaryKey = keyAttr != null || string.Equals(this.Name, "id", StringComparison.OrdinalIgnoreCase);
                        this.IsForeignKey = fkAttr != null;
                        this.IsColumn = this.MemberInfo.HasAnyAttribute("ColumnAttribute", "KeyAttribute") || (this.IsBasic && this.MemberInfo.HasAnyAttribute("ForeignKeyAttribute"));

                        try
                        {
                            this.Setter = Obj.GetMemberSetter(pi);

                            if (this.Setter == null && this.PropertyBackingField != null)
                            {
                                this.Setter = Obj.GetMemberSetter(this.PropertyBackingField);
                            }
                        }
                        catch (Exception)
                        {
                            this.Setter = (o, v) => pi.SetValue(o, v);
                        }

                        try
                        {
                            this.Getter = pi.GetPropertyGetter();
                        }
                        catch (Exception)
                        {
                            this.Getter = o => pi.GetValue(o);
                        }

                        this.ForeignKeyName = fkAttr?.GetType().GetProperty("Name")?.GetValue(fkAttr)?.ToString() ?? string.Empty;
                    }
                }
                else
                {
                    if (fi == null)
                    {
                        return;
                    }

                    this.IsSetterPublic = true;
                    this.IsSetterPrivate = false;
                    this.IsGetterPublic = true;
                    this.IsGetterPrivate = false;
                    this.FieldType = fi.FieldType;
                    try
                    {
                        this.Setter = fi.GetMemberSetter();
                    }
                    catch
                    {
                        this.Setter = (obj, value) => fi.SetValue(obj, value);
                    }

                    try
                    {
                        this.Getter = fi.GetFieldGetter();
                    }
                    catch (Exception)
                    {
                        this.Getter = x => fi.GetValue(x);
                    }
                }
            }

            sw.Stop();
            elapsedMilliseconds += sw.ElapsedMilliseconds;
            Debug.WriteLine($"MEMBERCACHE: {memberInfo} ({sw.ElapsedMilliseconds} ms. / {elapsedMilliseconds})");
        }

        /// <summary>
        /// Возвращает массив атрибутов, примененных к этому члену типа.
        /// </summary>
        public Attribute[] Attributes => this.GetAttributes();

        /// <summary>
        /// Получает значение свойства AutoGenerateField из атрибута DisplayAttribute.
        /// </summary>
        public bool? AutoGenerateField { get; }

        /// <summary>
        /// Получает значение свойства AutoGenerateFilter из атрибута DisplayAttribute.
        /// </summary>
        public bool? AutoGenerateFilter { get; }

        /// <summary>
        /// Получает значение, указывающее, можно ли читать значение члена (свойство или поле).
        /// </summary>
        public bool CanRead { get; }

        /// <summary>
        /// Получает значение, указывающее, можно ли записывать значение члена (свойство или поле).
        /// </summary>
        public bool CanWrite { get; }

        /// <summary>
        /// Получает имя столбца в базе данных, соответствующее этому члену.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// Получает тип, который объявляет этот член.
        /// </summary>
        public override Type DeclaringType => this.MemberInfo.DeclaringType;

        /// <summary>
        /// Получает делегат для вызова конструктора по умолчанию (без параметров) текущего типа.
        /// </summary>
        public Func<object> DefaultConstructor { get; }

        /// <summary>
        /// Получает описание члена из атрибута <see cref="DescriptionAttribute"/>.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Получает отображаемое имя члена из атрибута <see cref="DisplayNameAttribute"/> или DisplayAttribute.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Получает тип элементов коллекции, если текущий член является коллекцией.
        /// </summary>
        public Type ElementType { get; }

        /// <summary>
        /// Получает тип поля, если текущий член является полем.
        /// </summary>
        public Type FieldType { get; }

        /// <summary>
        /// Получает имя внешнего ключа в базе данных, если член помечен атрибутом ForeignKeyAttribute.
        /// </summary>
        public string ForeignKeyName { get; }

        /// <summary>
        /// Получает делегат для чтения значения члена.
        /// </summary>
        public Func<object, object> Getter { get; }

        /// <summary>
        /// Получает имя группы из атрибута DisplayAttribute.
        /// </summary>
        public string GroupName { get; }

        /// <summary>
        /// Порядковый номер объявления <see cref="MemberInfo"/> в <see cref="DeclaringType"/> в пределах категории <see cref="MemberTypes"/>.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли поле автоматическим для свойства.
        /// </summary>
        public bool IsBackingField { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип базовым <see cref="ReflectionServices.BasicTypes"/>.
        /// </summary>
        public bool IsBasic { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член коллекцией базовых типов.
        /// </summary>
        public bool IsBasicCollection { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип логическим (bool).
        /// </summary>
        public bool IsBoolean { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип классом.
        /// </summary>
        public bool IsClass => this.Type.IsClass;

        /// <summary>
        /// Получает значение, указывающее, является ли член коллекцией.
        /// </summary>
        public bool IsCollection { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член колонкой в базе данных.
        /// </summary>
        public bool IsColumn { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член константой.
        /// </summary>
        public bool IsConst { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член конструктором.
        /// </summary>
        public bool IsConstructor { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип делегатом.
        /// </summary>
        public bool IsDelegate { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип словарем.
        /// </summary>
        public bool IsDictionary { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип перечислением (enum).
        /// </summary>
        public bool IsEnum { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член событием.
        /// </summary>
        public bool IsEvent { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член полем.
        /// </summary>
        public bool IsField { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип числом с плавающей запятой (float, double, decimal).
        /// </summary>
        public bool IsFloat { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член внешним ключом в базе данных.
        /// </summary>
        public bool IsForeignKey { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли геттер свойства приватным.
        /// </summary>
        public bool IsGetterPrivate { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли геттер свойства публичным.
        /// </summary>
        public bool IsGetterPublic { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли первичный ключ идентификатором (автоинкрементным числом или GUID).
        /// </summary>
        public bool IsIdentity => this.IsPrimaryKey && (this.Type.IsNumeric(false) || this.Type == typeof(Guid));

        /// <summary>
        /// Является ли член индексатором, this[]. (Определяется по количеству параметров индексаторов > 0 <see cref="PropertyInfo.GetIndexParameters"/>).
        /// </summary>
        public bool IsIndexer { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип интерфейсом.
        /// </summary>
        public bool IsInterface => this.Type.IsInterface;

        /// <summary>
        /// Получает значение, указывающее, является ли член методом.
        /// </summary>
        public bool IsMethod { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип nullable (Nullable&lt;T&gt;).
        /// </summary>
        public bool IsNullable { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип числовым.
        /// </summary>
        public bool IsNumeric { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип System.Object.
        /// </summary>
        public bool IsObject { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член первичным ключом в базе данных.
        /// </summary>
        public bool IsPrimaryKey { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член приватным.
        /// </summary>
        public bool IsPrivate { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член свойством.
        /// </summary>
        public bool IsProperty { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член публичным.
        /// </summary>
        public bool IsPublic { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли сеттер свойства приватным.
        /// </summary>
        public bool IsSetterPrivate { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли сеттер свойства публичным.
        /// </summary>
        public bool IsSetterPublic { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип кортежем (Tuple).
        /// </summary>
        public bool IsTuple { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип значимым типом (value type).
        /// </summary>
        public bool IsValueType => this.type.IsValueType;

        /// <summary>
        /// Получает имя для сериализации JSON из атрибутов JsonProperty, JsonPropertyName и т.д.
        /// </summary>
        public string JsonName
        {
            get
            {
                if (this.jsonName != null)
                {
                    return this.jsonName;
                }

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

                return this.jsonName;
            }
        }

        /// <summary>
        /// Получает тип члена (свойство, метод, поле и т.д.).
        /// </summary>
        public override MemberTypes MemberType => this.MemberInfo.MemberType;

        /// <summary>
        /// Получает имя члена.
        /// </summary>
        public override sealed string Name { get; }

        /// <summary>
        /// Получает значение свойства Order из атрибута DisplayAttribute.
        /// </summary>
        public int? Order { get; }

        /// <summary>
        /// Получает родительский MemberCache (для вложенных членов).
        /// </summary>
        public Type ParentType { get; private set; }

        /// <summary>
        /// Получает значение свойства Prompt из атрибута DisplayAttribute.
        /// </summary>
        public string Prompt { get; }

        /// <summary>
        /// Получает поле, которое является backing-полем для автосвойства.
        /// </summary>
        public FieldInfo PropertyBackingField
        {
            get
            {
                if (this.propertyBackingField != null || (this.propertyBackingFieldExists.HasValue && !this.propertyBackingFieldExists.Value))
                {
                    return this.propertyBackingField;
                }

                try
                {
                    this.propertyBackingField = this.AsPropertyInfo().GetGetMethod(true).GetFieldInfo();
                }
                catch
                {
                    this.propertyBackingFieldExists = false;
                    return null;
                }

                this.propertyBackingFieldExists = true;
                return this.propertyBackingField;
            }
        }

        /// <summary>
        /// Получает тип свойства, если текущий член является свойством.
        /// </summary>
        public Type PropertyType { get; }

        /// <summary>
        /// the class object that was used to obtain this member.
        /// </summary>
        /// <remarks>This property may differ from the declaring type if the member was obtained through
        /// reflection on a derived class. Use this property to determine the type through which reflection was
        /// performed.</remarks>
        public override Type ReflectedType => this.MemberInfo.ReflectedType;

        /// <summary>
        /// Берется из атрибута TableAttribute, если его нет, то не заполняется.
        /// </summary>
        public string SchemaName { get; }

        /// <summary>
        /// Получает делегат для записи значения члена.
        /// </summary>
        public Action<object, object> Setter { get; }

        /// <summary>
        /// Получает значение свойства ShortName из атрибута DisplayAttribute.
        /// </summary>
        public string ShortName { get; }

        /// <summary>
        /// Берется из атрибута TableAttribute, если его нет, то берется простое имя класса.
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// Получает тип, связанный с этим членом.
        /// </summary>
        /// <remarks>
        /// Для свойства возвращает PropertyType, для поля - FieldType, для метода - ReturnType, для типа - сам тип.
        /// </remarks>
        public Type Type => this.type;

        /// <summary>
        /// Получает имя атрибута XML для сериализации из атрибутов XmlAttribute, XmlAttributeAttribute и т.д.
        /// </summary>
        public string XmlAttributeName
        {
            get
            {
                if (this.xmlAttr != null)
                {
                    return this.xmlAttr;
                }

                var xmlAttrs = this.GetAttributes().Where(x => x.GetType().Name.StartsWith("Xml")).ToArray();
                if (xmlAttrs.Length > 0)
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

                return this.xmlAttr ??= string.Empty;
            }
        }

        /// <summary>
        /// Получает имя элемента XML для сериализации из атрибутов XmlElement, XmlElementAttribute и т.д.
        /// </summary>
        public string XmlElementName
        {
            get
            {
                if (this.xmlElem != null)
                {
                    return this.xmlElem;
                }

                var xmlAttrs = this.GetAttributes().Where(x => x.GetType().Name.StartsWith("Xml")).ToArray();
                if (xmlAttrs.Length > 0)
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

                this.xmlElem ??= string.Empty;
                return this.xmlElem;
            }
        }

        /// <summary>
        /// Получает имя для сериализации XML (устаревшее, используйте XmlElementName или XmlAttributeName).
        /// </summary>
        public string XmlName { get; } = null;

        /// <summary>
        /// Внутренний объект MemberInfo, который кэшируется этим экземпляром.
        /// </summary>
        internal MemberInfo MemberInfo { get; }

        /// <summary>
        /// Создает или получает из кэша экземпляр MemberCache для указанного MemberInfo.
        /// </summary>
        /// <typeparam name="T">Тип.</typeparam>
        /// <param name="memberInfo">Информация о члене типа.</param>
        /// <returns>Кэшированная информация о члене типа.</returns>
        /// <exception cref="InvalidOperationException">Выбрасывается, если DeclaringType равен null.</exception>
        public static MemberCache Get<T>(MemberInfo memberInfo)
        {
            return Get(typeof(T), memberInfo);
        }

        /// <summary>
        /// Создает или получает из кэша экземпляр MemberCache для указанного MemberInfo.
        /// </summary>
        /// <param name="type">Тип которому принадлежит member.</param>
        /// <param name="member">Член типа.</param>
        /// <returns>Кэшированная информация о члене.</returns>
        /// <exception cref="InvalidOperationException">Выбрасывается, если DeclaringType равен null.</exception>
        public static MemberCache Get(Type type, MemberInfo member)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            switch (member)
            {
                case MemberCache me:
                    return me;

                case Type t:
                    throw new NotSupportedException();

                default:
                    {
                        var memberCache = Cache.GetOrAdd(type ?? throw new InvalidOperationException(), x => new MemberCache(type, member));
                        return memberCache;
                    }
            }
        }

        /// <summary>
        /// Получает значения объектов по заданному пути к свойствам.
        /// </summary>
        /// <param name="source">Объект, из которого извлекаются значения.</param>
        /// <param name="path">Массив <see cref="MemberCache"/>, представляющий последовательность свойств для извлечения.</param>
        /// <returns>
        /// Массив значений объектов, соответствующих каждому элементу пути.
        /// Если хотя бы одно значение по пути равно <c>null</c>, возвращается пустой массив.
        /// </returns>
        /// <remarks>
        /// Метод проходит по каждому элементу пути, вызывая <see cref="MemberCache.GetValue(object)"/> для текущего объекта.
        /// Текущий объект обновляется на значение предыдущего элемента пути.
        /// </remarks>
        public static object[] GetPathValues(object source, params MemberCache[] path)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var values = new object[path.Length];
            var x = source;
            var i = 0;
            foreach (var pathItem in path)
            {
                var v = pathItem.GetValue(x);
                if (v == null)
                {
                    return values;
                }

                values[i++] = v;
                x = v;
            }

            return values;
        }

        /// <summary>
        /// Определяет, является ли указанный член типа приватным (<c>private</c>).
        /// </summary>
        /// <param name="memberInfo">
        /// Метаданные члена типа, для которого требуется проверить уровень доступа.
        /// Поддерживаются следующие типы:
        /// <see cref="PropertyInfo"/>, <see cref="FieldInfo"/>, <see cref="MethodInfo"/>,
        /// <see cref="EventInfo"/>, <see cref="Type"/>, <see cref="ConstructorInfo"/>.
        /// </param>
        /// <returns>
        /// <c>true</c>, если член типа имеет модификатор доступа <c>private</c>;
        /// <c>false</c> — в противном случае.
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// Выбрасывается, если тип <paramref name="memberInfo"/> не поддерживается
        /// для проверки модификатора доступа.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Логика определения приватности:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <see cref="PropertyInfo"/> — проверяется наличие хотя бы одного приватного аксессора
        /// (getter или setter).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="FieldInfo"/> — используется свойство FieldInfo.IsPrivate.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="MethodInfo"/> — используется свойство MethodInfo.IsPrivate.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="EventInfo"/> — проверяется приватность методов добавления или удаления обработчика
        /// (<c>add</c>/<c>remove</c>).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="Type"/> — считается приватным, если тип не является публичным
        /// (<see cref="Type.IsPublic"/> равен <c>false</c>).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="ConstructorInfo"/> — используется свойство ConstructorInfo.IsPrivate.
        /// </description>
        /// </item>
        /// </list>
        /// <para>
        /// Обратите внимание, что для вложенных типов приватность также может определяться
        /// через <see cref="Type.IsNestedPrivate"/>.
        /// </para>
        /// </remarks>
        public static bool IsMemberPrivate(MemberInfo memberInfo)
        {
            return memberInfo switch
            {
                PropertyInfo pi => pi.GetAccessors().Any(m => m.IsPrivate),
                FieldInfo fi => fi.IsPrivate,
                MethodInfo mi => mi.IsPrivate,
                EventInfo ei => ei.AddMethod?.IsPrivate == true || ei.RemoveMethod?.IsPrivate == true,
                Type t => !t.IsPublic,
                ConstructorInfo ci => ci.IsPrivate,
                _ => throw new NotSupportedException($"Member type {memberInfo.GetType()} is not supported for IsPublic check."),
            };
        }

        /// <summary>
        /// Определяет, является ли указанный член типа публичным (<c>public</c>).
        /// </summary>
        /// <param name="memberInfo">
        /// Метаданные члена типа, для которого требуется проверить уровень доступа.
        /// Поддерживаются следующие типы:
        /// <see cref="PropertyInfo"/>, <see cref="FieldInfo"/>, <see cref="MethodInfo"/>,
        /// <see cref="EventInfo"/>, <see cref="Type"/>, <see cref="ConstructorInfo"/>.
        /// </param>
        /// <returns>
        /// <c>true</c>, если член типа является публичным;
        /// <c>false</c> — если член не является публичным.
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// Выбрасывается, если тип <paramref name="memberInfo"/> не поддерживается
        /// для проверки модификатора доступа.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Логика определения публичности:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <see cref="PropertyInfo"/> — проверяется наличие хотя бы одного публичного аксессора
        /// (getter или setter).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="FieldInfo"/> — используется свойство <see cref="FieldInfo.IsPublic"/>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="MethodInfo"/> — используется свойство MethodInfo.IsPublic.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="EventInfo"/> — проверяется публичность методов добавления или удаления обработчика
        /// (<c>add</c>/<c>remove</c>).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="Type"/> — используется свойство <see cref="Type.IsPublic"/>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="ConstructorInfo"/> — используется свойство ConstructorInfo.IsPublic.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        public static bool IsMemberPublic(MemberInfo memberInfo)
        {
            return memberInfo switch
            {
                PropertyInfo pi => pi.GetAccessors().Any(m => m.IsPublic),
                FieldInfo fi => fi.IsPublic,
                MethodInfo mi => mi.IsPublic,
                EventInfo ei => ei.AddMethod?.IsPublic == true || ei.RemoveMethod?.IsPublic == true,
                Type t => t.IsPublic,
                ConstructorInfo ci => ci.IsPublic,
                _ => throw new NotSupportedException($"Member type {memberInfo.GetType()} is not supported for IsPublic check."),
            };
        }

        /// <summary>
        /// Преобразует текущий MemberCache в EventInfo.
        /// </summary>
        /// <returns>EventInfo или null, если текущий член не является событием.</returns>
        public EventInfo AsEventInfo() => this.MemberInfo as EventInfo;

        /// <summary>
        /// Преобразует текущий MemberCache в FieldInfo.
        /// </summary>
        /// <returns>FieldInfo или null, если текущий член не является полем.</returns>
        public FieldInfo AsFieldInfo() => this.MemberInfo as FieldInfo;

        /// <summary>
        /// Преобразует текущий MemberCache в MethodInfo.
        /// </summary>
        /// <returns>MethodInfo или null, если текущий член не является методом.</returns>
        public MethodInfo AsMethodInfo() => this.MemberInfo as MethodInfo;

        /// <summary>
        /// Преобразует текущий MemberCache в PropertyInfo.
        /// </summary>
        /// <returns>PropertyInfo или null, если текущий член не является свойством.</returns>
        public PropertyInfo AsPropertyInfo() => this.MemberInfo as PropertyInfo;

        /// <summary>
        /// Получает атрибут по имени типа атрибута.
        /// </summary>
        /// <param name="attributeTypeName">Имя типа атрибута (с суффиксом Attribute или без).</param>
        /// <param name="stringComparison">Сравнение имени.</param>
        /// <returns>Экземпляр атрибута или null, если атрибут не найден.</returns>
        public Attribute GetAttribute(string attributeTypeName, StringComparison stringComparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrWhiteSpace(attributeTypeName))
            {
                return null;
            }

            if (!attributeTypeName.EndsWith(nameof(Attribute)))
            {
                attributeTypeName += nameof(Attribute);
            }

            return this.memberAttributes
                .FirstOrDefault(x => x.GetType().Name.Equals(attributeTypeName, stringComparison));
        }

        /// <summary>
        /// Получает полное имя столбца с указанными префиксом и суффиксом для имен.
        /// </summary>
        /// <param name="namePrefix">Префикс для имен (например, "[").</param>
        /// <param name="nameSuffix">Суффикс для имен (например, "]").</param>
        /// <param name="fullName">Полное имя включает в себя имя таблицы и схемы, если указаны.</param>
        /// <returns>Полное имя столбца.</returns>
        public string GetColumnName(string namePrefix, string nameSuffix, bool fullName = true)
            => (fullName ? this.GetTableName(namePrefix, nameSuffix) + "." : string.Empty) + $"{namePrefix}{this.ColumnName}{nameSuffix}";

        /// <summary>
        /// Возвращает массив всех настраиваемых атрибутов, примененных к этому члену.
        /// </summary>
        /// <param name="inherit">true для поиска цепочки наследования этого члена для поиска атрибутов; в противном случае — false.</param>
        /// <returns>Массив настраиваемых атрибутов.</returns>
        public override object[] GetCustomAttributes(bool inherit) => this.MemberInfo.GetCustomAttributes(inherit);

        /// <summary>
        /// Возвращает массив настраиваемых атрибутов, примененных к этому члену и идентифицируемых типом <see cref="Type"/>.
        /// </summary>
        /// <param name="attributeType">Тип атрибута для поиска.</param>
        /// <param name="inherit">true для поиска цепочки наследования этого члена для поиска атрибутов; в противном случае — false.</param>
        /// <returns>Массив настраиваемых атрибутов.</returns>
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => this.MemberInfo.GetCustomAttributes(attributeType, inherit);

        /// <summary>
        /// Получает полное имя столбца в формате [Схема].[Таблица].[Столбец] с квадратными скобками.
        /// </summary>
        /// <returns>Полное имя столбца.</returns>
        public string GetFullColumnName() => this.GetColumnName("[", "]");

        /// <summary>
        /// Получает полное имя таблицы в формате [Схема].[Таблица] с квадратными скобками.
        /// </summary>
        /// <returns>Полное имя таблицы.</returns>
        public string GetFullTableName() => this.GetTableName("[", "]");

        /// <summary>
        /// Получает полное имя таблицы с указанными префиксом и суффиксом для имен.
        /// </summary>
        /// <param name="namePrefix">Префикс для имен (например, "[").</param>
        /// <param name="nameSuffix">Суффикс для имен (например, "]").</param>
        /// <returns>Полное имя таблицы.</returns>
        public string GetTableName(string namePrefix, string nameSuffix)
        {
            var fullTableName = $"{namePrefix}{this.TableName}{nameSuffix}";
            if (!string.IsNullOrWhiteSpace(this.SchemaName))
            {
                fullTableName = $"{namePrefix}{this.SchemaName}{nameSuffix}." + fullTableName;
            }

            return fullTableName;
        }

        /// <summary>
        /// Получает значение члена для указанного экземпляра через скомпилированный делегат <see cref="Getter"/>.
        /// </summary>
        /// <param name="instance">Экземпляр объекта.</param>
        /// <returns>Значение члена.</returns>
        public object GetValue(object instance) => this.Getter(instance);

        /// <summary>
        /// Получает значение члена для указанного экземпляра и преобразует его к указанному типу.
        /// </summary>
        /// <typeparam name="T">Тип, к которому преобразуется значение.</typeparam>
        /// <param name="instance">Экземпляр объекта.</param>
        /// <returns>Значение члена, преобразованное к типу T.</returns>
        public T GetValue<T>(object instance) => (T)this.Getter(instance);

        /// <summary>
        /// Вызывает базовый метод, представленный этим экземпляром, используя указанный объект в качестве цели и
        /// предоставленные параметры.
        /// </summary>
        /// <remarks>Если метод является методом экземпляра, параметр instance должен иметь тип,
        /// совместимый с объявляющим типом метода. Если метод статический, параметр instance
        /// игнорируется. Типы параметров должны соответствовать сигнатуре метода, иначе может быть выдано исключение.</remarks>
        /// <param name="instance">Объект, для которого вызывается метод. Для статических методов этот параметр игнорируется.</param>
        /// <param name="parameters">Массив объектов, передаваемых в качестве аргументов методу. Количество, порядок и типы параметров должны
        /// соответствовать сигнатуре метода.</param>
        /// <returns>Возвращаемое значение вызванного метода или null, если метод не возвращает значения.</returns>
        public object Invoke(object instance, params object[] parameters) => this.AsMethodInfo()?.Invoke(instance, parameters);

        /// <summary>
        /// Определяет, применен ли к этому члену один или несколько атрибутов, идентифицируемых типом <see cref="Type"/>.
        /// </summary>
        /// <param name="attributeType">Тип атрибута для поиска.</param>
        /// <param name="inherit">true для поиска цепочки наследования этого члена для поиска атрибутов; в противном случае — false.</param>
        /// <returns>true, если к этому члену применен один или несколько экземпляров атрибута; в противном случае — false.</returns>
        public override bool IsDefined(Type attributeType, bool inherit) => this.MemberInfo.IsDefined(attributeType, inherit);

        /// <summary>
        /// Возвращает строковое представление текущего члена в формате "DeclaringType.Name.Name(Type.Name)".
        /// </summary>
        /// <returns>Строковое представление члена.</returns>
        public override string ToString()
        {
            return $"{(this.IsPublic ? "public" : "private")} {this.Type.Name} [{this.DeclaringType?.Name}].[{this.Name}] {{{(this.IsGetterPublic ? " get;" : string.Empty)}{(this.IsSetterPublic ? " set;" : string.Empty)} }}";
        }
    }
}