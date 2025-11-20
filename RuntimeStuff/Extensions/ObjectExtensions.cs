using System;
using RuntimeStuff.Helpers;

namespace RuntimeStuff.Extensions
{
    /// <summary>
    /// Предоставляет методы расширения для получения значений свойств объекта по их именам с помощью кэшируемых делегатов.
    /// Упрощает и ускоряет доступ к свойствам объектов без необходимости прямого использования Reflection.
    /// </summary>
    /// <remarks>Методы класса позволяют получать значения одного или нескольких свойств объекта по их именам,
    /// поддерживают преобразование типов и используют внутреннее кэширование делегатов для повышения производительности при
    /// повторных вызовах. Поддерживаются как ссылочные, так и значимые типы свойств. Рекомендуется использовать для
    /// сценариев, где требуется динамический доступ к свойствам по имени, например, при работе с динамическими данными или
    /// сериализации. Все методы предназначены для объектов ссылочных типов; если исходный объект равен null, будет
    /// выброшено исключение.</remarks>
    public static class ObjectExtensions
    {
        /// <summary>
        ///     Получает значение указанного свойства объекта по имени с помощью кэшируемого делегата.
        ///     Позволяет быстро извлекать значения свойств без постоянного использования Reflection.
        ///     Особенности:
        ///     - Автоматически создает и кэширует делегат-геттер для типа и имени свойства.
        ///     - Поддерживает как ссылочные, так и значимые типы свойств (boxing выполняется автоматически).
        ///     - При повторных вызовах для того же типа и свойства используется уже скомпилированный делегат.
        ///     Пример:
        ///     <code>
        /// var person = new Person { Name = "Alice" };
        /// var value = PropertyHelper.GetValue(person, "Name"); // "Alice"
        /// </code>
        /// </summary>
        public static object GetPropertyValue<T>(T source, string propertyName) where T : class
        {
            return TypeHelper.GetValue(source, propertyName);
        }

        /// <summary>
        ///     Получает значения свойств объекта в указанном порядке
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Исходный объект</param>
        /// <param name="propertyNames">Имена свойств объекта с учетом регистра</param>
        /// <returns></returns>
        public static object[] GetPropertyValues<T>(this T source, params string[] propertyNames) where T : class
        {
            return TypeHelper.GetPropertyValues(source, propertyNames);
        }

        /// <summary>
        ///     Получает значения свойств объекта в указанном порядке и преобразует в указанный тип через
        ///     <see cref="TypeHelper.ChangeType{T}(object, IFormatProvider)" />
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <param name="source">Исходный объект</param>
        /// <param name="propertyNames">Имена свойств объекта с учетом регистра</param>
        /// <returns></returns>
        public static TValue[] GetPropertyValues<TObject, TValue>(this TObject source, params string[] propertyNames)
            where TObject : class
        {
            return TypeHelper.GetPropertyValues<TObject, TValue>(source, propertyNames);
        }
    }
}