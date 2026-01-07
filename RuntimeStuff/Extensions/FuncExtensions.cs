namespace RuntimeStuff.Extensions
{
    using System;
    using RuntimeStuff.Helpers;

    /// <summary>
    ///     Предоставляет методы расширения для преобразования делегатов типа <see cref="Func" /> с произвольным числом
    ///     параметров в делегаты, принимающие и возвращающие значения типа <see cref="object" />. Это позволяет выполнять
    ///     универсальное преобразование типов аргументов и результата для дальнейшей передачи или вызова в динамических
    ///     сценариях.
    /// </summary>
    /// <remarks>
    ///     Методы этого класса полезны при работе с делегатами, когда требуется унифицировать сигнатуру для
    ///     передачи между компонентами, использующими тип <see cref="object" /> вместо конкретных типов. Для преобразования
    ///     типов аргументов и результата можно указать соответствующие функции-конвертеры. Если конвертер не задан,
    ///     используется приведение типов через <see langword="object" />. Следует убедиться, что передаваемые значения
    ///     совместимы с ожидаемыми типами, чтобы избежать ошибок времени выполнения.
    /// </remarks>
    public static class FuncExtensions
    {
        public static Func<T1, TR> ConvertFunc<T1, TR>(this Func<object, object> func)
        {
            if (func == null)
            {
                throw new ArgumentNullException(nameof(func));
            }

            return arg =>
            {
                object result = func(arg);

                // null → default
                if (result == null)
                {
                    return default;
                }

                return (TR)result;
            };
        }

        /// <summary>
        ///     Преобразует функцию с типизированным входом и выходом в функцию, принимающую и возвращающую значения типа
        ///     object, с возможностью указать преобразователи для входного и выходного значений.
        /// </summary>
        /// <remarks>
        ///     Полученная функция может быть полезна для универсального вызова типизированных функций,
        ///     например, при работе с рефлексией или динамическими сценариями. Если преобразователи не указаны, используется
        ///     стандартное приведение типов, что может привести к исключениям при несовпадении типов.
        /// </remarks>
        /// <typeparam name="TR">Тип возвращаемого значения исходной функции.</typeparam>
        /// <param name="func">Исходная функция, которую требуется преобразовать. Не может быть равна null.</param>
        /// к типу T1 с помощью приведения.
        /// <param name="resultConverter">
        ///     Функция для преобразования результата типа R в тип object. Если не указана, результат
        ///     приводится к типу object.
        /// </param>
        /// <returns>
        ///     Функция, принимающая значение типа object, преобразующая его к типу T1, вызывающая исходную функцию и
        ///     возвращающая результат в виде object.
        /// </returns>
        public static Func<object> ConvertFunc<TR>(
            this Func<TR> func,
            Func<TR, object> resultConverter = null) => ConvertFunc<TR, object>(func, resultConverter);

        /// <summary>
        ///     Преобразует функцию с типизированным входом и выходом в функцию, принимающую и возвращающую значения типа
        ///     object, с возможностью указать преобразователи для входного и выходного значений.
        /// </summary>
        /// <remarks>
        ///     Полученная функция может быть полезна для универсального вызова типизированных функций,
        ///     например, при работе с рефлексией или динамическими сценариями. Если преобразователи не указаны, используется
        ///     стандартное приведение типов, что может привести к исключениям при несовпадении типов.
        /// </remarks>
        /// <typeparam name="T1">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="TR">Тип возвращаемого значения исходной функции.</typeparam>
        /// <param name="func">Исходная функция, которую требуется преобразовать. Не может быть равна null.</param>
        /// <param name="converter1">
        ///     Функция для преобразования входного значения типа object в тип T1. Если не указана, входное значение приводится
        ///     к типу T1 с помощью приведения.
        /// </param>
        /// <param name="resultConverter">
        ///     Функция для преобразования результата типа R в тип object. Если не указана, результат
        ///     приводится к типу object.
        /// </param>
        /// <returns>
        ///     Функция, принимающая значение типа object, преобразующая его к типу T1, вызывающая исходную функцию и
        ///     возвращающая результат в виде object.
        /// </returns>
        public static Func<object, object> ConvertFunc<T1, TR>(
            this Func<T1, TR> func,
            Func<object, T1> converter1,
            Func<TR, object> resultConverter = null) => ConvertFunc<T1, TR, object, object>(func, converter1, resultConverter);

        /// <summary>
        ///     Преобразует функцию с типизированным входом и выходом в функцию, принимающую и возвращающую значения типа
        ///     object, с возможностью указать преобразователи для входного и выходного значений.
        /// </summary>
        /// <remarks>
        ///     Полученная функция может быть полезна для универсального вызова типизированных функций,
        ///     например, при работе с рефлексией или динамическими сценариями. Если преобразователи не указаны, используется
        ///     стандартное приведение типов, что может привести к исключениям при несовпадении типов.
        /// </remarks>
        /// <typeparam name="T1">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="TR1">Тип возвращаемого значения исходной функции.</typeparam>
        /// <typeparam name="TR2">Тип возвращаемого значения преобразованной функции.</typeparam>
        /// <param name="func">Исходная функция, которую требуется преобразовать. Не может быть равна null.</param>
        /// <param name="converter1">
        ///     Функция для преобразования входного значения типа object в тип T1. Если не указана, входное значение приводится
        ///     к типу T1 с помощью приведения.
        /// </param>
        /// <param name="resultConverter">
        ///     Функция для преобразования результата типа R1 в тип R2. Если не указана, результат
        ///     приводится к типу object.
        /// </param>
        /// <returns>
        ///     Функция, принимающая значение типа object, преобразующая его к типу T1, вызывающая исходную функцию и
        ///     возвращающая результат в виде object.
        /// </returns>
        public static Func<object, TR2> ConvertFunc<T1, TR1, TR2>(
            this Func<T1, TR1> func,
            Func<object, T1> converter1 = null,
            Func<TR1, TR2> resultConverter = null) => ConvertFunc<T1, TR1, object, TR2>(func, converter1, resultConverter);

        public static Func<object, TR1> ConvertFunc<T1, TR1>(this Func<T1, TR1> func) => ConvertFunc<T1, TR1, object, TR1>(func);

        /// <summary>
        ///     Преобразует функцию с типизированным входом и выходом в функцию, принимающую и возвращающую значения типа
        ///     object, с возможностью указать преобразователи для входного и выходного значений.
        /// </summary>
        /// <remarks>
        ///     Полученная функция может быть полезна для универсального вызова типизированных функций,
        ///     например, при работе с рефлексией или динамическими сценариями. Если преобразователи не указаны, используется
        ///     стандартное приведение типов, что может привести к исключениям при несовпадении типов.
        /// </remarks>
        /// <typeparam name="T1">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="T2">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="TR">Тип возвращаемого значения исходной функции.</typeparam>
        /// <param name="func">Исходная функция, которую требуется преобразовать. Не может быть равна null.</param>
        /// <param name="converter1">
        ///     Функция для преобразования входного значения типа object в тип T1. Если не указана, входное
        ///     значение приводится к типу T1 с помощью приведения.
        /// </param>
        /// <param name="converter2">
        ///     Функция для преобразования входного значения типа object в тип T2. Если не указана, входное
        ///     значение приводится к типу T1 с помощью приведения.
        /// </param>
        /// <param name="resultConverter">
        ///     Функция для преобразования результата типа R в тип object. Если не указана, результат
        ///     приводится к типу object.
        /// </param>
        public static Func<object, object, object> ConvertFunc<T1, T2, TR>(
            this Func<T1, T2, TR> func,
            Func<object, T1> converter1,
            Func<object, T2> converter2,
            Func<TR, object> resultConverter) => ConvertFunc<T1, T2, TR, object, object, object>(func, converter1, converter2, resultConverter);

        public static Func<object, object, TR2> ConvertFunc<T1, T2, TR1, TR2>(
            this Func<T1, T2, TR1> func,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null,
            Func<TR1, TR2> resultConverter = null) => ConvertFunc<T1, T2, TR1, object, object, TR2>(func, converter1, converter2, resultConverter);

        public static Func<object, object, TR1> ConvertFunc<T1, T2, TR1>(
            this Func<T1, T2, TR1> func,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null) => ConvertFunc<T1, T2, TR1, object, object, TR1>(func, converter1, converter2);

        /// <summary>
        ///     Преобразует функцию с типизированным входом и выходом в функцию, принимающую и возвращающую значения типа
        ///     object, с возможностью указать преобразователи для входного и выходного значений.
        /// </summary>
        /// <remarks>
        ///     Полученная функция может быть полезна для универсального вызова типизированных функций,
        ///     например, при работе с рефлексией или динамическими сценариями. Если преобразователи не указаны, используется
        ///     стандартное приведение типов, что может привести к исключениям при несовпадении типов.
        /// </remarks>
        /// <typeparam name="T1">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="T2">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="T3">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="TR">Тип возвращаемого значения исходной функции.</typeparam>
        /// <param name="func">Исходная функция, которую требуется преобразовать. Не может быть равна null.</param>
        /// <param name="converter1">
        ///     Функция для преобразования входного значения типа object в тип T1. Если не указана, входное
        ///     значение приводится к типу T1 с помощью приведения.
        /// </param>
        /// <param name="converter2">
        ///     Функция для преобразования входного значения типа object в тип T2. Если не указана, входное
        ///     значение приводится к типу T2 с помощью приведения.
        /// </param>
        /// <param name="converter3">
        ///     Функция для преобразования входного значения типа object в тип T3. Если не указана, входное
        ///     значение приводится к типу T3 с помощью приведения.
        /// </param>
        /// <param name="resultConverter">
        ///     Функция для преобразования результата типа R в тип object. Если не указана, результат
        ///     приводится к типу object.
        /// </param>
        public static Func<object, object, object, object> ConvertFunc<T1, T2, T3, TR>(
            this Func<T1, T2, T3, TR> func,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null,
            Func<object, T3> converter3 = null,
            Func<TR, object> resultConverter = null) => ConvertFunc<T1, T2, T3, TR, object, object, object, object>(func, converter1, converter2, converter3, resultConverter);

        /// <summary>
        ///     Преобразует функцию с типизированным входом и выходом в функцию, принимающую и возвращающую значения типа
        ///     object, с возможностью указать преобразователи для входного и выходного значений.
        /// </summary>
        /// <remarks>
        ///     Полученная функция может быть полезна для универсального вызова типизированных функций,
        ///     например, при работе с рефлексией или динамическими сценариями. Если преобразователи не указаны, используется
        ///     стандартное приведение типов, что может привести к исключениям при несовпадении типов.
        /// </remarks>
        /// <typeparam name="T1">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="T2">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="T3">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="T4">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="TR">Тип возвращаемого значения исходной функции.</typeparam>
        /// <param name="func">Исходная функция, которую требуется преобразовать. Не может быть равна null.</param>
        /// <param name="converter1">
        ///     Функция для преобразования входного значения типа object в тип T1. Если не указана, входное
        ///     значение приводится к типу T1 с помощью приведения.
        /// </param>
        /// <param name="converter2">
        ///     Функция для преобразования входного значения типа object в тип T2. Если не указана, входное
        ///     значение приводится к типу T2 с помощью приведения.
        /// </param>
        /// <param name="converter3">
        ///     Функция для преобразования входного значения типа object в тип T3. Если не указана, входное
        ///     значение приводится к типу T3 с помощью приведения.
        /// </param>
        /// <param name="converter4">
        ///     Функция для преобразования входного значения типа object в тип T4. Если не указана, входное
        ///     значение приводится к типу T4 с помощью приведения.
        /// </param>
        /// <param name="resultConverter">
        ///     Функция для преобразования результата типа R в тип object. Если не указана, результат
        ///     приводится к типу object.
        /// </param>
        public static Func<object, object, object, object, object> ConvertFunc<T1, T2, T3, T4, TR>(
            this Func<T1, T2, T3, T4, TR> func,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null,
            Func<object, T3> converter3 = null,
            Func<object, T4> converter4 = null,
            Func<TR, object> resultConverter = null) => ConvertFunc<T1, T2, T3, T4, TR, object, object, object, object, object>(func, converter1, converter2, converter3, converter4, resultConverter);

        /// <summary>
        ///     Преобразует функцию с типизированным входом и выходом в функцию, принимающую и возвращающую значения типа
        ///     object, с возможностью указать преобразователи для входного и выходного значений.
        /// </summary>
        /// <remarks>
        ///     Полученная функция может быть полезна для универсального вызова типизированных функций,
        ///     например, при работе с рефлексией или динамическими сценариями. Если преобразователи не указаны, используется
        ///     стандартное приведение типов, что может привести к исключениям при несовпадении типов.
        /// </remarks>
        /// <typeparam name="T1">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="T2">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="T3">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="T4">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="T5">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="TR">Тип возвращаемого значения исходной функции.</typeparam>
        /// <param name="func">Исходная функция, которую требуется преобразовать. Не может быть равна null.</param>
        /// <param name="converter1">
        ///     Функция для преобразования входного значения типа object в тип T1. Если не указана, входное
        ///     значение приводится к типу T1 с помощью приведения.
        /// </param>
        /// <param name="converter2">
        ///     Функция для преобразования входного значения типа object в тип T2. Если не указана, входное
        ///     значение приводится к типу T2 с помощью приведения.
        /// </param>
        /// <param name="converter3">
        ///     Функция для преобразования входного значения типа object в тип T3. Если не указана, входное
        ///     значение приводится к типу T3 с помощью приведения.
        /// </param>
        /// <param name="converter4">
        ///     Функция для преобразования входного значения типа object в тип T4. Если не указана, входное
        ///     значение приводится к типу T4 с помощью приведения.
        /// </param>
        /// <param name="converter5">
        ///     Функция для преобразования входного значения типа object в тип T5. Если не указана, входное
        ///     значение приводится к типу T5 с помощью приведения.
        /// </param>
        /// <param name="resultConverter">
        ///     Функция для преобразования результата типа R в тип object. Если не указана, результат
        ///     приводится к типу object.
        /// </param>
        public static Func<object, object, object, object, object, object> ConvertFunc<T1, T2, T3, T4, T5, TR>(
            this Func<T1, T2, T3, T4, T5, TR> func,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null,
            Func<object, T3> converter3 = null,
            Func<object, T4> converter4 = null,
            Func<object, T5> converter5 = null,
            Func<TR, object> resultConverter = null) => ConvertFunc<T1, T2, T3, T4, T5, TR, object, object, object, object, object, object>(func, converter1, converter2, converter3, converter4, converter5, resultConverter);

        /// <summary>
        ///     Преобразует функцию с шестью типизированными аргументами в функцию, принимающую аргументы типа object, с
        ///     возможностью указать преобразователи для каждого аргумента и результата.
        /// </summary>
        /// <remarks>
        ///     Если преобразователь для аргумента не указан, используется стандартное приведение типа. При
        ///     некорректном типе аргумента может возникнуть исключение во время выполнения. Метод полезен для интеграции
        ///     типизированных функций с динамическими сценариями, например, при работе с рефлексией или универсальными
        ///     обработчиками.
        /// </remarks>
        /// <typeparam name="T1">Тип первого аргумента исходной функции.</typeparam>
        /// <typeparam name="T2">Тип второго аргумента исходной функции.</typeparam>
        /// <typeparam name="T3">Тип третьего аргумента исходной функции.</typeparam>
        /// <typeparam name="T4">Тип четвертого аргумента исходной функции.</typeparam>
        /// <typeparam name="T5">Тип пятого аргумента исходной функции.</typeparam>
        /// <typeparam name="T6">Тип шестого аргумента исходной функции.</typeparam>
        /// <typeparam name="TR">Тип возвращаемого значения исходной функции.</typeparam>
        /// <param name="func">
        ///     Исходная функция, принимающая шесть аргументов указанных типов и возвращающая результат типа R. Не может быть
        ///     null.
        /// </param>
        /// <param name="converter1">
        ///     Функция для преобразования первого аргумента из object в тип T1. Если не указана, выполняется
        ///     приведение типа.
        /// </param>
        /// <param name="converter2">
        ///     Функция для преобразования второго аргумента из object в тип T2. Если не указана, выполняется
        ///     приведение типа.
        /// </param>
        /// <param name="converter3">
        ///     Функция для преобразования третьего аргумента из object в тип T3. Если не указана, выполняется
        ///     приведение типа.
        /// </param>
        /// <param name="converter4">
        ///     Функция для преобразования четвертого аргумента из object в тип T4. Если не указана, выполняется приведение
        ///     типа.
        /// </param>
        /// <param name="converter5">
        ///     Функция для преобразования пятого аргумента из object в тип T5. Если не указана, выполняется
        ///     приведение типа.
        /// </param>
        /// <param name="converter6">
        ///     Функция для преобразования шестого аргумента из object в тип T6. Если не указана, выполняется
        ///     приведение типа.
        /// </param>
        /// <param name="resultConverter">
        ///     Функция для преобразования результата типа R в object. Если не указана, результат
        ///     возвращается как object.
        /// </param>
        /// <returns>
        ///     Функция, принимающая шесть аргументов типа object, преобразующая их к соответствующим типам и возвращающая
        ///     результат в виде object.
        /// </returns>
        public static Func<object, object, object, object, object, object, object> ConvertFunc<T1, T2, T3, T4, T5, T6, TR>(
            this Func<T1, T2, T3, T4, T5, T6, TR> func,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null,
            Func<object, T3> converter3 = null,
            Func<object, T4> converter4 = null,
            Func<object, T5> converter5 = null,
            Func<object, T6> converter6 = null,
            Func<TR, object> resultConverter = null) => ConvertFunc<T1, T2, T3, T4, T5, T6, TR, object, object, object, object, object, object, object>(func, converter1, converter2, converter3, converter4, converter5, converter6, resultConverter);

        /// <summary>
        ///     Преобразует функцию с семью типизированными аргументами в функцию, принимающую аргументы типа object, с
        ///     возможностью указать конвертеры для каждого аргумента и результата.
        /// </summary>
        /// <remarks>
        ///     Если конвертеры не указаны, для преобразования аргументов и результата используется
        ///     стандартное приведение типов. Метод полезен для интеграции типизированных функций с универсальными интерфейсами,
        ///     где аргументы и результат представлены как object.
        /// </remarks>
        /// <typeparam name="T1">Тип первого аргумента исходной функции.</typeparam>
        /// <typeparam name="T2">Тип второго аргумента исходной функции.</typeparam>
        /// <typeparam name="T3">Тип третьего аргумента исходной функции.</typeparam>
        /// <typeparam name="T4">Тип четвертого аргумента исходной функции.</typeparam>
        /// <typeparam name="T5">Тип пятого аргумента исходной функции.</typeparam>
        /// <typeparam name="T6">Тип шестого аргумента исходной функции.</typeparam>
        /// <typeparam name="T7">Тип седьмого аргумента исходной функции.</typeparam>
        /// <typeparam name="TR">Тип возвращаемого значения исходной функции.</typeparam>
        /// <param name="func">Исходная функция, принимающая семь типизированных аргументов и возвращающая результат типа R.</param>
        /// <param name="converter1">
        ///     Необязательный конвертер для преобразования первого аргумента из object в тип T1. Если не указан, используется
        ///     приведение типа.
        /// </param>
        /// <param name="converter2">
        ///     Необязательный конвертер для преобразования второго аргумента из object в тип T2. Если не указан, используется
        ///     приведение типа.
        /// </param>
        /// <param name="converter3">
        ///     Необязательный конвертер для преобразования третьего аргумента из object в тип T3. Если не указан, используется
        ///     приведение типа.
        /// </param>
        /// <param name="converter4">
        ///     Необязательный конвертер для преобразования четвертого аргумента из object в тип T4. Если не указан,
        ///     используется приведение типа.
        /// </param>
        /// <param name="converter5">
        ///     Необязательный конвертер для преобразования пятого аргумента из object в тип T5. Если не указан, используется
        ///     приведение типа.
        /// </param>
        /// <param name="converter6">
        ///     Необязательный конвертер для преобразования шестого аргумента из object в тип T6. Если не указан, используется
        ///     приведение типа.
        /// </param>
        /// <param name="converter7">
        ///     Необязательный конвертер для преобразования седьмого аргумента из object в тип T7. Если не указан, используется
        ///     приведение типа.
        /// </param>
        /// <param name="resultConverter">
        ///     Необязательный конвертер для преобразования результата из типа R в object. Если не указан, результат приводится
        ///     к object.
        /// </param>
        /// <returns>
        ///     Функция, принимающая семь аргументов типа object и возвращающая результат типа object, с применением указанных
        ///     конвертеров.
        /// </returns>
        public static Func<object, object, object, object, object, object, object, object> ConvertFunc<T1, T2, T3, T4, T5,
            T6, T7, TR>(
            this Func<T1, T2, T3, T4, T5, T6, T7, TR> func,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null,
            Func<object, T3> converter3 = null,
            Func<object, T4> converter4 = null,
            Func<object, T5> converter5 = null,
            Func<object, T6> converter6 = null,
            Func<object, T7> converter7 = null,
            Func<TR, object> resultConverter = null) => ConvertFunc<T1, T2, T3, T4, T5, T6, T7, TR, object, object, object, object, object, object, object, object>(func, converter1, converter2, converter3, converter4, converter5, converter6, converter7, resultConverter);

        /// <summary>
        ///     Преобразует функцию с восемью типизированными аргументами в функцию, принимающую аргументы типа object, с
        ///     возможностью указать конвертеры для каждого аргумента и результата.
        /// </summary>
        /// <remarks>
        ///     Если конвертеры не указаны, для преобразования аргументов и результата используется
        ///     стандартное приведение типов. Метод полезен для интеграции типизированных функций с динамическими или
        ///     универсальными интерфейсами, где аргументы и результат представлены как object.
        /// </remarks>
        /// <typeparam name="T1">Тип первого аргумента исходной функции.</typeparam>
        /// <typeparam name="T2">Тип второго аргумента исходной функции.</typeparam>
        /// <typeparam name="T3">Тип третьего аргумента исходной функции.</typeparam>
        /// <typeparam name="T4">Тип четвертого аргумента исходной функции.</typeparam>
        /// <typeparam name="T5">Тип пятого аргумента исходной функции.</typeparam>
        /// <typeparam name="T6">Тип шестого аргумента исходной функции.</typeparam>
        /// <typeparam name="T7">Тип седьмого аргумента исходной функции.</typeparam>
        /// <typeparam name="T8">Тип восьмого аргумента исходной функции.</typeparam>
        /// <typeparam name="TR">Тип возвращаемого значения исходной функции.</typeparam>
        /// <param name="func">Исходная функция, принимающая восемь типизированных аргументов и возвращающая результат типа R.</param>
        /// <param name="converter1">
        ///     Необязательный конвертер для преобразования первого аргумента из object в тип T1. Если не указан, выполняется
        ///     приведение типа.
        /// </param>
        /// <param name="converter2">
        ///     Необязательный конвертер для преобразования второго аргумента из object в тип T2. Если не указан, выполняется
        ///     приведение типа.
        /// </param>
        /// <param name="converter3">
        ///     Необязательный конвертер для преобразования третьего аргумента из object в тип T3. Если не указан, выполняется
        ///     приведение типа.
        /// </param>
        /// <param name="converter4">
        ///     Необязательный конвертер для преобразования четвертого аргумента из object в тип T4. Если не указан, выполняется
        ///     приведение типа.
        /// </param>
        /// <param name="converter5">
        ///     Необязательный конвертер для преобразования пятого аргумента из object в тип T5. Если не указан, выполняется
        ///     приведение типа.
        /// </param>
        /// <param name="converter6">
        ///     Необязательный конвертер для преобразования шестого аргумента из object в тип T6. Если не указан, выполняется
        ///     приведение типа.
        /// </param>
        /// <param name="converter7">
        ///     Необязательный конвертер для преобразования седьмого аргумента из object в тип T7. Если не указан, выполняется
        ///     приведение типа.
        /// </param>
        /// <param name="converter8">
        ///     Необязательный конвертер для преобразования восьмого аргумента из object в тип T8. Если не указан, выполняется
        ///     приведение типа.
        /// </param>
        /// <param name="resultConverter">
        ///     Необязательная функция для преобразования результата типа R в object. Если не указана, результат приводится к
        ///     object.
        /// </param>
        /// <returns>
        ///     Функция, принимающая восемь аргументов типа object и возвращающая результат типа object, с применением указанных
        ///     конвертеров.
        /// </returns>
        public static Func<object, object, object, object, object, object, object, object, object> ConvertFunc<T1, T2, T3,
            T4, T5, T6, T7, T8, TR>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, TR> func,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null,
            Func<object, T3> converter3 = null,
            Func<object, T4> converter4 = null,
            Func<object, T5> converter5 = null,
            Func<object, T6> converter6 = null,
            Func<object, T7> converter7 = null,
            Func<object, T8> converter8 = null,
            Func<TR, object> resultConverter = null) => ConvertFunc<T1, T2, T3, T4, T5, T6, T7, T8, TR, object, object, object, object, object, object, object, object, object>(func, converter1, converter2, converter3, converter4, converter5, converter6, converter7, converter8, resultConverter);

        // -------------------------------- Дополнительные методы расширения для Func с большим числом параметров можно добавить здесь ---------------------------------

        /// <summary>
        ///     Преобразует функцию без параметров, возвращающую значение типа <typeparamref name="TR1" />, в функцию без
        ///     параметров, возвращающую значение типа <typeparamref name="TR2" /> с помощью указанного преобразователя результата
        ///     или стандартного преобразования типа.
        /// </summary>
        /// <remarks>
        ///     Если преобразователь результата не указан, для преобразования значения используется
        ///     стандартный механизм <see cref="Obj.ChangeType{T}" />. Метод может быть полезен для адаптации функций к
        ///     требуемому типу результата, например, при работе с обобщёнными API.
        /// </remarks>
        /// <typeparam name="TR1">Тип исходного значения, возвращаемого исходной функцией.</typeparam>
        /// <typeparam name="TR2">Тип значения, возвращаемого преобразованной функцией.</typeparam>
        /// <param name="func">
        ///     Исходная функция без параметров, возвращающая значение типа <typeparamref name="TR1" />. Не может
        ///     быть равна null.
        /// </param>
        /// <param name="resultConverter">
        ///     Функция преобразования результата, принимающая значение типа <typeparamref name="TR1" /> и возвращающая значение
        ///     типа <typeparamref name="TR2" />. Если не указана, используется стандартное преобразование типа.
        /// </param>
        /// <returns>Функция без параметров, возвращающая значение типа <typeparamref name="TR2" />.</returns>
        public static Func<TR2> ConvertFunc<TR1, TR2>(
            this Func<TR1> func,
            Func<TR1, TR2> resultConverter = null) => () =>
                                                               {
                                                                   var r = func();
                                                                   return resultConverter == null ? Obj.ChangeType<TR2>(r) : resultConverter(r);
                                                               };

        /// <summary>
        ///     Преобразует функцию с типизированным входом и выходом в функцию, принимающую и возвращающую значения типа
        ///     object, с возможностью указать преобразователи для входного и выходного значений.
        /// </summary>
        /// <remarks>
        ///     Полученная функция может быть полезна для универсального вызова типизированных функций,
        ///     например, при работе с рефлексией или динамическими сценариями. Если преобразователи не указаны, используется
        ///     стандартное приведение типов, что может привести к исключениям при несовпадении типов.
        /// </remarks>
        /// <typeparam name="T1">Тип входного параметра исходной функции.</typeparam>
        /// <typeparam name="TR1">Тип возвращаемого значения исходной функции.</typeparam>
        /// <typeparam name="TU1">Тип входного параметра преобразованной функции.</typeparam>
        /// <typeparam name="TR2">Тип возвращаемого значения преобразованной функции.</typeparam>
        /// <param name="func">Исходная функция, которую требуется преобразовать. Не может быть равна null.</param>
        /// <param name="converter1">
        ///     Функция для преобразования входного значения типа object в тип T1. Если не указана, входное значение приводится
        ///     к типу T1 с помощью приведения.
        /// </param>
        /// <param name="resultConverter">
        ///     Функция для преобразования результата типа R в тип object. Если не указана, результат
        ///     приводится к типу object.
        /// </param>
        /// <returns>
        ///     Функция, принимающая значение типа object, преобразующая его к типу T1, вызывающая исходную функцию и
        ///     возвращающая результат в виде object.
        /// </returns>
        public static Func<TU1, TR2> ConvertFunc<T1, TR1, TU1, TR2>(
            this Func<T1, TR1> func,
            Func<TU1, T1> converter1 = null,
            Func<TR1, TR2> resultConverter = null) => f =>
                                                               {
                                                                   var r = func(converter1 == null ? Obj.ChangeType<T1>(f) : converter1(f));
                                                                   return resultConverter == null ? Obj.ChangeType<TR2>(r) : resultConverter(r);
                                                               };

        /// <summary>
        ///     Преобразует функцию с двумя аргументами в новую функцию с изменёнными типами входных и выходного параметров,
        ///     используя указанные преобразователи.
        /// </summary>
        /// <remarks>
        ///     Если преобразователи не указаны, для преобразования типов используется стандартный механизм,
        ///     реализованный в TypeHelper.ChangeType. Это позволяет использовать функцию с аргументами и возвращаемым значением
        ///     других типов без явного указания преобразователей.
        /// </remarks>
        /// <typeparam name="T1">Тип первого входного параметра исходной функции.</typeparam>
        /// <typeparam name="T2">Тип второго входного параметра исходной функции.</typeparam>
        /// <typeparam name="TR1">Тип возвращаемого значения исходной функции.</typeparam>
        /// <typeparam name="TU1">Тип первого входного параметра результирующей функции.</typeparam>
        /// <typeparam name="TU2">Тип второго входного параметра результирующей функции.</typeparam>
        /// <typeparam name="TR2">Тип возвращаемого значения результирующей функции.</typeparam>
        /// <param name="func">Исходная функция, которую требуется преобразовать.</param>
        /// <param name="converter1">
        ///     Функция для преобразования первого входного параметра из типа U1 в тип T1. Если не указана, используется
        ///     стандартное преобразование типов.
        /// </param>
        /// <param name="converter2">
        ///     Функция для преобразования второго входного параметра из типа U2 в тип T2. Если не указана, используется
        ///     стандартное преобразование типов.
        /// </param>
        /// <param name="r2">
        ///     Функция для преобразования возвращаемого значения из типа R1 в тип R2. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <returns>
        ///     Функция, принимающая параметры типов U1 и U2 и возвращающая значение типа R2, с применёнными преобразованиями
        ///     входных и выходного параметров.
        /// </returns>
        public static Func<TU1, TU2, TR2> ConvertFunc<T1, T2, TR1, TU1, TU2, TR2>(
            this Func<T1, T2, TR1> func,
            Func<TU1, T1> converter1 = null,
            Func<TU2, T2> converter2 = null,
            Func<TR1, TR2> r2 = null) => (t1, t2) =>
                                                  {
                                                      var r1 = func(
                                                          converter1 == null ? Obj.ChangeType<T1>(t1) : converter1(t1),
                                                          converter2 == null ? Obj.ChangeType<T2>(t2) : converter2(t2));
                                                      return r2 == null ? Obj.ChangeType<TR2>(r1) : r2(r1);
                                                  };

        /// <summary>
        ///     Преобразует функцию с тремя аргументами и возвращаемым значением, позволяя использовать преобразователи типов
        ///     для входных и выходного параметров.
        /// </summary>
        /// <remarks>
        ///     Если преобразователи не заданы, для преобразования типов используется стандартный механизм.
        ///     Метод полезен для адаптации функций к различным типам входных и выходных данных.
        /// </remarks>
        /// <typeparam name="T1">Тип первого входного параметра исходной функции.</typeparam>
        /// <typeparam name="T2">Тип второго входного параметра исходной функции.</typeparam>
        /// <typeparam name="T3">Тип третьего входного параметра исходной функции.</typeparam>
        /// <typeparam name="TR1">Тип возвращаемого значения исходной функции.</typeparam>
        /// <typeparam name="TU1">Тип первого входного параметра результирующей функции.</typeparam>
        /// <typeparam name="TU2">Тип второго входного параметра результирующей функции.</typeparam>
        /// <typeparam name="TU3">Тип третьего входного параметра результирующей функции.</typeparam>
        /// <typeparam name="TR2">Тип возвращаемого значения результирующей функции.</typeparam>
        /// <param name="func">Исходная функция, принимающая три параметра типа T1, T2, T3 и возвращающая значение типа R1.</param>
        /// <param name="converter1">
        ///     Функция преобразования первого входного параметра из типа U1 в тип T1. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter2">
        ///     Функция преобразования второго входного параметра из типа U2 в тип T2. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter3">
        ///     Функция преобразования третьего входного параметра из типа U3 в тип T3. Если не указана, используется
        ///     стандартное преобразование типов.
        /// </param>
        /// <param name="r2">
        ///     Функция преобразования возвращаемого значения из типа R1 в тип R2. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <returns>
        ///     Функция, принимающая три параметра типа U1, TU2, TU3 и возвращающая значение типа R2, с применением указанных
        ///     преобразователей.
        /// </returns>
        public static Func<TU1, TU2, TU3, TR2> ConvertFunc<T1, T2, T3, TR1, TU1, TU2, TU3, TR2>(
            this Func<T1, T2, T3, TR1> func,
            Func<TU1, T1> converter1 = null,
            Func<TU2, T2> converter2 = null,
            Func<TU3, T3> converter3 = null,
            Func<TR1, TR2> r2 = null) => (t1, t2, t3) =>
                                                  {
                                                      var r1 = func(
                                                          converter1 == null ? Obj.ChangeType<T1>(t1) : converter1(t1),
                                                          converter2 == null ? Obj.ChangeType<T2>(t2) : converter2(t2),
                                                          converter3 == null ? Obj.ChangeType<T3>(t3) : converter3(t3));
                                                      return r2 == null ? Obj.ChangeType<TR2>(r1) : r2(r1);
                                                  };

        /// <summary>
        ///     Преобразует функцию с четырьмя аргументами, позволяя задать преобразование типов входных параметров и
        ///     возвращаемого значения.
        /// </summary>
        /// <remarks>
        ///     Если преобразующие функции не заданы, для преобразования типов используется стандартное
        ///     приведение через TypeHelper.ChangeType. Это может быть полезно для адаптации функций к различным типам данных
        ///     без необходимости ручного преобразования.
        /// </remarks>
        /// <typeparam name="T1">Тип первого исходного аргумента функции.</typeparam>
        /// <typeparam name="T2">Тип второго исходного аргумента функции.</typeparam>
        /// <typeparam name="T3">Тип третьего исходного аргумента функции.</typeparam>
        /// <typeparam name="T4">Тип четвертого исходного аргумента функции.</typeparam>
        /// <typeparam name="TR1">Тип исходного возвращаемого значения функции.</typeparam>
        /// <typeparam name="TU1">Тип первого аргумента преобразованной функции.</typeparam>
        /// <typeparam name="TU2">Тип второго аргумента преобразованной функции.</typeparam>
        /// <typeparam name="TU3">Тип третьего аргумента преобразованной функции.</typeparam>
        /// <typeparam name="TU4">Тип четвертого аргумента преобразованной функции.</typeparam>
        /// <typeparam name="TR2">Тип возвращаемого значения преобразованной функции.</typeparam>
        /// <param name="func">Исходная функция, принимающая четыре аргумента и возвращающая результат.</param>
        /// <param name="converter1">
        ///     Функция преобразования первого аргумента. Если не указана, используется приведение типа по
        ///     умолчанию.
        /// </param>
        /// <param name="converter2">
        ///     Функция преобразования второго аргумента. Если не указана, используется приведение типа по
        ///     умолчанию.
        /// </param>
        /// <param name="converter3">
        ///     Функция преобразования третьего аргумента. Если не указана, используется приведение типа по
        ///     умолчанию.
        /// </param>
        /// <param name="converter4">
        ///     Функция преобразования четвертого аргумента. Если не указана, используется приведение типа по
        ///     умолчанию.
        /// </param>
        /// <param name="r2">
        ///     Функция преобразования возвращаемого значения. Если не указана, используется приведение типа по
        ///     умолчанию.
        /// </param>
        /// <returns>Функция, принимающая четыре аргумента преобразованных типов и возвращающая результат преобразованного типа.</returns>
        public static Func<TU1, TU2, TU3, TU4, TR2> ConvertFunc<T1, T2, T3, T4, TR1, TU1, TU2, TU3, TU4, TR2>(
            this Func<T1, T2, T3, T4, TR1> func,
            Func<TU1, T1> converter1 = null,
            Func<TU2, T2> converter2 = null,
            Func<TU3, T3> converter3 = null,
            Func<TU4, T4> converter4 = null,
            Func<TR1, TR2> r2 = null) => (t1, t2, t3, t4) =>
                                                  {
                                                      var r1 = func(
                                                          converter1 == null ? Obj.ChangeType<T1>(t1) : converter1(t1),
                                                          converter2 == null ? Obj.ChangeType<T2>(t2) : converter2(t2),
                                                          converter3 == null ? Obj.ChangeType<T3>(t3) : converter3(t3),
                                                          converter4 == null ? Obj.ChangeType<T4>(t4) : converter4(t4));
                                                      return r2 == null ? Obj.ChangeType<TR2>(r1) : r2(r1);
                                                  };

        /// <summary>
        ///     Преобразует функцию с пятью аргументами, позволяя задать преобразование типов входных параметров и возвращаемого
        ///     значения.
        /// </summary>
        /// <remarks>
        ///     Если преобразователь для параметра или результата не указан, будет выполнено стандартное
        ///     приведение типа. Метод полезен для адаптации сигнатур функций при работе с обобщёнными или динамическими
        ///     данными.
        /// </remarks>
        /// <typeparam name="T1">Тип первого исходного параметра функции.</typeparam>
        /// <typeparam name="T2">Тип второго исходного параметра функции.</typeparam>
        /// <typeparam name="T3">Тип третьего исходного параметра функции.</typeparam>
        /// <typeparam name="T4">Тип четвертого исходного параметра функции.</typeparam>
        /// <typeparam name="T5">Тип пятого исходного параметра функции.</typeparam>
        /// <typeparam name="TR1">Тип исходного возвращаемого значения функции.</typeparam>
        /// <typeparam name="TU1">Тип первого параметра преобразованной функции.</typeparam>
        /// <typeparam name="TU2">Тип второго параметра преобразованной функции.</typeparam>
        /// <typeparam name="TU3">Тип третьего параметра преобразованной функции.</typeparam>
        /// <typeparam name="TU4">Тип четвертого параметра преобразованной функции.</typeparam>
        /// <typeparam name="TU5">Тип пятого параметра преобразованной функции.</typeparam>
        /// <typeparam name="TR2">Тип возвращаемого значения преобразованной функции.</typeparam>
        /// <param name="func">Исходная функция, которую требуется преобразовать.</param>
        /// <param name="converter1">
        ///     Функция преобразования первого параметра. Если не указана, используется приведение типа по
        ///     умолчанию.
        /// </param>
        /// <param name="converter2">
        ///     Функция преобразования второго параметра. Если не указана, используется приведение типа по
        ///     умолчанию.
        /// </param>
        /// <param name="converter3">
        ///     Функция преобразования третьего параметра. Если не указана, используется приведение типа по
        ///     умолчанию.
        /// </param>
        /// <param name="converter4">
        ///     Функция преобразования четвертого параметра. Если не указана, используется приведение типа по
        ///     умолчанию.
        /// </param>
        /// <param name="converter5">
        ///     Функция преобразования пятого параметра. Если не указана, используется приведение типа по
        ///     умолчанию.
        /// </param>
        /// <param name="r2">
        ///     Функция преобразования возвращаемого значения. Если не указана, используется приведение типа по
        ///     умолчанию.
        /// </param>
        /// <returns>Функция, принимающая пять параметров преобразованных типов и возвращающая преобразованное значение.</returns>
        public static Func<TU1, TU2, TU3, TU4, TU5, TR2> ConvertFunc<T1, T2, T3, T4, T5, TR1, TU1, TU2, TU3, TU4, TU5, TR2>(
            this Func<T1, T2, T3, T4, T5, TR1> func,
            Func<TU1, T1> converter1 = null,
            Func<TU2, T2> converter2 = null,
            Func<TU3, T3> converter3 = null,
            Func<TU4, T4> converter4 = null,
            Func<TU5, T5> converter5 = null,
            Func<TR1, TR2> r2 = null) => (t1, t2, t3, t4, t5) =>
                                                  {
                                                      var r1 = func(
                                                          converter1 == null ? Obj.ChangeType<T1>(t1) : converter1(t1),
                                                          converter2 == null ? Obj.ChangeType<T2>(t2) : converter2(t2),
                                                          converter3 == null ? Obj.ChangeType<T3>(t3) : converter3(t3),
                                                          converter4 == null ? Obj.ChangeType<T4>(t4) : converter4(t4),
                                                          converter5 == null ? Obj.ChangeType<T5>(t5) : converter5(t5));
                                                      return r2 == null ? Obj.ChangeType<TR2>(r1) : r2(r1);
                                                  };

        /// <summary>
        ///     Преобразует функцию с шестью аргументами, позволяя задать преобразование типов входных параметров и
        ///     возвращаемого значения.
        /// </summary>
        /// <remarks>
        ///     Если преобразующие функции не заданы, для преобразования типов используется стандартный
        ///     механизм преобразования. Это позволяет гибко адаптировать сигнатуру исходной функции к требуемым типам без
        ///     изменения её логики.
        /// </remarks>
        /// <typeparam name="T1">Тип первого исходного аргумента функции.</typeparam>
        /// <typeparam name="T2">Тип второго исходного аргумента функции.</typeparam>
        /// <typeparam name="T3">Тип третьего исходного аргумента функции.</typeparam>
        /// <typeparam name="T4">Тип четвертого исходного аргумента функции.</typeparam>
        /// <typeparam name="T5">Тип пятого исходного аргумента функции.</typeparam>
        /// <typeparam name="T6">Тип шестого исходного аргумента функции.</typeparam>
        /// <typeparam name="TR1">Тип исходного возвращаемого значения функции.</typeparam>
        /// <typeparam name="TU1">Тип первого аргумента преобразованной функции.</typeparam>
        /// <typeparam name="TU2">Тип второго аргумента преобразованной функции.</typeparam>
        /// <typeparam name="TU3">Тип третьего аргумента преобразованной функции.</typeparam>
        /// <typeparam name="TU4">Тип четвертого аргумента преобразованной функции.</typeparam>
        /// <typeparam name="TU5">Тип пятого аргумента преобразованной функции.</typeparam>
        /// <typeparam name="TU6">Тип шестого аргумента преобразованной функции.</typeparam>
        /// <typeparam name="TR2">Тип возвращаемого значения преобразованной функции.</typeparam>
        /// <param name="func">Исходная функция с шестью аргументами, которую требуется преобразовать.</param>
        /// <param name="converter1">
        ///     Функция преобразования первого аргумента из типа U1 в тип T1. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter2">
        ///     Функция преобразования второго аргумента из типа U2 в тип T2. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter3">
        ///     Функция преобразования третьего аргумента из типа U3 в тип T3. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter4">
        ///     Функция преобразования четвертого аргумента из типа U4 в тип T4. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter5">
        ///     Функция преобразования пятого аргумента из типа U5 в тип T5. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter6">
        ///     Функция преобразования шестого аргумента из типа U6 в тип T6. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="r2">
        ///     Функция преобразования возвращаемого значения из типа R1 в тип R2. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <returns>Функция, принимающая шесть аргументов новых типов и возвращающая результат преобразованного типа.</returns>
        public static Func<TU1, TU2, TU3, TU4, TU5, TU6, TR2> ConvertFunc<T1, T2, T3, T4, T5, T6, TR1, TU1, TU2, TU3, TU4, TU5, TU6, TR2>(
            this Func<T1, T2, T3, T4, T5, T6, TR1> func,
            Func<TU1, T1> converter1 = null,
            Func<TU2, T2> converter2 = null,
            Func<TU3, T3> converter3 = null,
            Func<TU4, T4> converter4 = null,
            Func<TU5, T5> converter5 = null,
            Func<TU6, T6> converter6 = null,
            Func<TR1, TR2> r2 = null) => (t1, t2, t3, t4, t5, t6) =>
                                                  {
                                                      var r1 = func(
                                                          converter1 == null ? Obj.ChangeType<T1>(t1) : converter1(t1),
                                                          converter2 == null ? Obj.ChangeType<T2>(t2) : converter2(t2),
                                                          converter3 == null ? Obj.ChangeType<T3>(t3) : converter3(t3),
                                                          converter4 == null ? Obj.ChangeType<T4>(t4) : converter4(t4),
                                                          converter5 == null ? Obj.ChangeType<T5>(t5) : converter5(t5),
                                                          converter6 == null ? Obj.ChangeType<T6>(t6) : converter6(t6));
                                                      return r2 == null ? Obj.ChangeType<TR2>(r1) : r2(r1);
                                                  };

        /// <summary>
        ///     Преобразует функцию с семью аргументами, позволяя задать преобразование типов входных параметров и возвращаемого
        ///     значения.
        /// </summary>
        /// <remarks>
        ///     Если преобразователь для аргумента или результата не указан, используется стандартное
        ///     преобразование типов через TypeHelper.ChangeType. Это позволяет гибко адаптировать сигнатуру исходной функции к
        ///     требуемым типам без ручного преобразования.
        /// </remarks>
        /// <typeparam name="T1">Тип первого исходного аргумента функции.</typeparam>
        /// <typeparam name="T2">Тип второго исходного аргумента функции.</typeparam>
        /// <typeparam name="T3">Тип третьего исходного аргумента функции.</typeparam>
        /// <typeparam name="T4">Тип четвертого исходного аргумента функции.</typeparam>
        /// <typeparam name="T5">Тип пятого исходного аргумента функции.</typeparam>
        /// <typeparam name="T6">Тип шестого исходного аргумента функции.</typeparam>
        /// <typeparam name="T7">Тип седьмого исходного аргумента функции.</typeparam>
        /// <typeparam name="TR1">Тип исходного возвращаемого значения функции.</typeparam>
        /// <typeparam name="TU1">Тип первого преобразованного аргумента.</typeparam>
        /// <typeparam name="TU2">Тип второго преобразованного аргумента.</typeparam>
        /// <typeparam name="TU3">Тип третьего преобразованного аргумента.</typeparam>
        /// <typeparam name="TU4">Тип четвертого преобразованного аргумента.</typeparam>
        /// <typeparam name="TU5">Тип пятого преобразованного аргумента.</typeparam>
        /// <typeparam name="TU6">Тип шестого преобразованного аргумента.</typeparam>
        /// <typeparam name="TU7">Тип седьмого преобразованного аргумента.</typeparam>
        /// <typeparam name="TR2">Тип преобразованного возвращаемого значения.</typeparam>
        /// <param name="func">Исходная функция с семью аргументами, которую требуется преобразовать.</param>
        /// <param name="converter1">
        ///     Функция преобразования первого аргумента из типа U1 в T1. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter2">
        ///     Функция преобразования второго аргумента из типа U2 в T2. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter3">
        ///     Функция преобразования третьего аргумента из типа U3 в T3. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter4">
        ///     Функция преобразования четвертого аргумента из типа U4 в T4. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter5">
        ///     Функция преобразования пятого аргумента из типа U5 в T5. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter6">
        ///     Функция преобразования шестого аргумента из типа U6 в T6. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter7">
        ///     Функция преобразования седьмого аргумента из типа U7 в T7. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="r2">
        ///     Функция преобразования возвращаемого значения из типа R1 в R2. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <returns>
        ///     Функция, принимающая аргументы типов U1–U7 и возвращающая значение типа R2, с применёнными преобразованиями к
        ///     каждому аргументу и результату.
        /// </returns>
        public static Func<TU1, TU2, TU3, TU4, TU5, TU6, TU7, TR2> ConvertFunc<T1, T2, T3, T4, T5, T6, T7, TR1, TU1, TU2, TU3, TU4, TU5,
            TU6, TU7, TR2>(
            this Func<T1, T2, T3, T4, T5, T6, T7, TR1> func,
            Func<TU1, T1> converter1 = null,
            Func<TU2, T2> converter2 = null,
            Func<TU3, T3> converter3 = null,
            Func<TU4, T4> converter4 = null,
            Func<TU5, T5> converter5 = null,
            Func<TU6, T6> converter6 = null,
            Func<TU7, T7> converter7 = null,
            Func<TR1, TR2> r2 = null) => (t1, t2, t3, t4, t5, t6, t7) =>
                                                  {
                                                      var r1 = func(
                                                          converter1 == null ? Obj.ChangeType<T1>(t1) : converter1(t1),
                                                          converter2 == null ? Obj.ChangeType<T2>(t2) : converter2(t2),
                                                          converter3 == null ? Obj.ChangeType<T3>(t3) : converter3(t3),
                                                          converter4 == null ? Obj.ChangeType<T4>(t4) : converter4(t4),
                                                          converter5 == null ? Obj.ChangeType<T5>(t5) : converter5(t5),
                                                          converter6 == null ? Obj.ChangeType<T6>(t6) : converter6(t6),
                                                          converter7 == null ? Obj.ChangeType<T7>(t7) : converter7(t7));
                                                      return r2 == null ? Obj.ChangeType<TR2>(r1) : r2(r1);
                                                  };

        /// <summary>
        ///     Преобразует функцию с восемью аргументами, позволяя задать преобразование типов входных параметров и результата.
        /// </summary>
        /// <remarks>
        ///     Если преобразователь для параметра не указан, используется стандартное преобразование типов
        ///     через TypeHelper.ChangeType. Это позволяет гибко адаптировать сигнатуру функции к различным типам входных данных
        ///     и результата.
        /// </remarks>
        /// <typeparam name="T1">Тип первого исходного параметра функции.</typeparam>
        /// <typeparam name="T2">Тип второго исходного параметра функции.</typeparam>
        /// <typeparam name="T3">Тип третьего исходного параметра функции.</typeparam>
        /// <typeparam name="T4">Тип четвертого исходного параметра функции.</typeparam>
        /// <typeparam name="T5">Тип пятого исходного параметра функции.</typeparam>
        /// <typeparam name="T6">Тип шестого исходного параметра функции.</typeparam>
        /// <typeparam name="T7">Тип седьмого исходного параметра функции.</typeparam>
        /// <typeparam name="T8">Тип восьмого исходного параметра функции.</typeparam>
        /// <typeparam name="TR1">Тип возвращаемого значения исходной функции.</typeparam>
        /// <typeparam name="TU1">Тип первого входного параметра преобразованной функции.</typeparam>
        /// <typeparam name="TU2">Тип второго входного параметра преобразованной функции.</typeparam>
        /// <typeparam name="TU3">Тип третьего входного параметра преобразованной функции.</typeparam>
        /// <typeparam name="TU4">Тип четвертого входного параметра преобразованной функции.</typeparam>
        /// <typeparam name="TU5">Тип пятого входного параметра преобразованной функции.</typeparam>
        /// <typeparam name="TU6">Тип шестого входного параметра преобразованной функции.</typeparam>
        /// <typeparam name="TU7">Тип седьмого входного параметра преобразованной функции.</typeparam>
        /// <typeparam name="TU8">Тип восьмого входного параметра преобразованной функции.</typeparam>
        /// <typeparam name="TR2">Тип возвращаемого значения преобразованной функции.</typeparam>
        /// <param name="func">Исходная функция, принимающая восемь параметров и возвращающая значение типа R1.</param>
        /// <param name="converter1">
        ///     Функция преобразования первого входного параметра из типа U1 в тип T1. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter2">
        ///     Функция преобразования второго входного параметра из типа U2 в тип T2. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter3">
        ///     Функция преобразования третьего входного параметра из типа U3 в тип T3. Если не указана, используется
        ///     стандартное преобразование типов.
        /// </param>
        /// <param name="converter4">
        ///     Функция преобразования четвертого входного параметра из типа U4 в тип T4. Если не указана, используется
        ///     стандартное преобразование типов.
        /// </param>
        /// <param name="converter5">
        ///     Функция преобразования пятого входного параметра из типа U5 в тип T5. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter6">
        ///     Функция преобразования шестого входного параметра из типа U6 в тип T6. Если не указана, используется стандартное
        ///     преобразование типов.
        /// </param>
        /// <param name="converter7">
        ///     Функция преобразования седьмого входного параметра из типа U7 в тип T7. Если не указана, используется
        ///     стандартное преобразование типов.
        /// </param>
        /// <param name="converter8">
        ///     Функция преобразования восьмого входного параметра из типа U8 в тип T8. Если не указана, используется
        ///     стандартное преобразование типов.
        /// </param>
        /// <param name="r2">
        ///     Функция преобразования результата из типа R1 в тип R2. Если не указана, используется стандартное преобразование
        ///     типов.
        /// </param>
        /// <returns>Функция, принимающая восемь параметров преобразованных типов и возвращающая результат преобразованного типа.</returns>
        public static Func<TU1, TU2, TU3, TU4, TU5, TU6, TU7, TU8, TR2> ConvertFunc<T1, T2, T3, T4, T5, T6, T7, T8, TR1, TU1, TU2, TU3,
            TU4, TU5, TU6, TU7, TU8, TR2>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, TR1> func,
            Func<TU1, T1> converter1 = null,
            Func<TU2, T2> converter2 = null,
            Func<TU3, T3> converter3 = null,
            Func<TU4, T4> converter4 = null,
            Func<TU5, T5> converter5 = null,
            Func<TU6, T6> converter6 = null,
            Func<TU7, T7> converter7 = null,
            Func<TU8, T8> converter8 = null,
            Func<TR1, TR2> r2 = null) => (t1, t2, t3, t4, t5, t6, t7, t8) =>
                                                  {
                                                      var r1 = func(
                                                          converter1 == null ? Obj.ChangeType<T1>(t1) : converter1(t1),
                                                          converter2 == null ? Obj.ChangeType<T2>(t2) : converter2(t2),
                                                          converter3 == null ? Obj.ChangeType<T3>(t3) : converter3(t3),
                                                          converter4 == null ? Obj.ChangeType<T4>(t4) : converter4(t4),
                                                          converter5 == null ? Obj.ChangeType<T5>(t5) : converter5(t5),
                                                          converter6 == null ? Obj.ChangeType<T6>(t6) : converter6(t6),
                                                          converter7 == null ? Obj.ChangeType<T7>(t7) : converter7(t7),
                                                          converter8 == null ? Obj.ChangeType<T8>(t8) : converter8(t8));
                                                      return r2 == null ? Obj.ChangeType<TR2>(r1) : r2(r1);
                                                  };
    }
}