// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 11-10-2025
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="ExpressionExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace System.Linq.Expressions
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Предоставляет вспомогательные методы для анализа и извлечения информации из LINQ-выражений, таких как получение
    /// значения выражения, определение связанного свойства или члена, а также извлечение метаданных о членах типа.
    /// </summary>
    /// <remarks>Класс предназначен для упрощения работы с выражениями в сценариях, где требуется динамический доступ
    /// к значениям или метаданным членов объектов через выражения. Поддерживаются распространённые типы узлов выражений,
    /// включая бинарные, унарные, лямбда- и условные выражения, а также вызовы методов. Методы класса могут быть полезны
    /// при построении динамических запросов, реализации привязки данных или рефлексии на основе выражений. Все методы
    /// являются статическими и потокобезопасны.</remarks>
    public static class ExpressionExtensions
    {
        /// <summary>
        /// Преобразует выражение <see cref="Expression{TDelegate}"/> из типа
        /// <typeparamref name="T2"/> → <typeparamref name="TR2"/>
        /// в выражение типа <typeparamref name="T1"/> → <typeparamref name="TR1"/>.
        /// </summary>
        /// <typeparam name="T1">Тип входного параметра результирующего выражения.</typeparam>
        /// <typeparam name="TR1">Тип результата результирующего выражения.</typeparam>
        /// <typeparam name="T2">Тип входного параметра исходного выражения.</typeparam>
        /// <typeparam name="TR2">Тип результата исходного выражения.</typeparam>
        /// <param name="expression">
        /// Исходное выражение, принимающее параметр типа <typeparamref name="T2"/>
        /// и возвращающее значение типа <typeparamref name="TR2"/>.
        /// </param>
        /// <param name="argConverter">
        /// Выражение-конвертер входного параметра, преобразующее
        /// <typeparamref name="T1"/> в <typeparamref name="T2"/>.
        /// Используется для адаптации аргумента результирующего выражения
        /// к типу, ожидаемому исходным выражением.
        /// </param>
        /// <param name="resultConverter">
        /// Выражение-конвертер результата, преобразующее
        /// <typeparamref name="TR2"/> в <typeparamref name="TR1"/>.
        /// Используется для адаптации результата исходного выражения
        /// к типу результирующего выражения.
        /// </param>
        /// <returns>
        /// Новое выражение типа <see cref="Expression{Func}"/>,
        /// принимающее параметр типа <typeparamref name="T1"/>
        /// и возвращающее значение типа <typeparamref name="TR1"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="expression"/>,
        /// <paramref name="argConverter"/> или <paramref name="resultConverter"/> равны <c>null</c>.
        /// </exception>
        /// <remarks>
        /// Метод выполняет композицию трёх выражений:
        /// <list type="number">
        /// <item>
        /// Преобразует входной параметр <typeparamref name="T1"/> в <typeparamref name="T2"/>
        /// с помощью <paramref name="argConverter"/>.
        /// </item>
        /// <item>
        /// Передаёт преобразованный аргумент в исходное выражение <paramref name="expression"/>.
        /// </item>
        /// <item>
        /// Преобразует результат <typeparamref name="TR2"/> в <typeparamref name="TR1"/>
        /// с помощью <paramref name="resultConverter"/>.
        /// </item>
        /// </list>
        /// Внутри используется замена параметров в дереве выражения.
        /// </remarks>
        public static Expression<Func<T1, TR1>> ConvertExpression<T1, TR1, T2, TR2>(
                this Expression<Func<T2, TR2>> expression,
                Expression<Func<T1, T2>> argConverter,
                Expression<Func<TR2, TR1>> resultConverter) => ExpressionHelper.ConvertExpression(expression, argConverter, resultConverter);

        /// <summary>
        /// Преобразует выражение <see cref="Expression{Func}"/>, возвращающее значение типа
        /// <typeparamref name="TR2"/>, в выражение, возвращающее <see cref="object"/>.
        /// </summary>
        /// <typeparam name="T1">Тип входного параметра выражения.</typeparam>
        /// <typeparam name="TR2">Тип результата исходного выражения.</typeparam>
        /// <param name="expression">
        /// Исходное выражение, принимающее параметр типа <typeparamref name="T1"/>
        /// и возвращающее значение типа <typeparamref name="TR2"/>.
        /// </param>
        /// <returns>
        /// Новое выражение типа <see cref="Expression{Func}"/>,
        /// принимающее параметр типа <typeparamref name="T1"/>
        /// и возвращающее результат, приведённый к типу <see cref="object"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="expression"/> равно <c>null</c>.
        /// </exception>
        /// <remarks>
        /// Метод выполняет обёртку исходного выражения, сохраняя входной параметр,
        /// но приводя его результат к типу <see cref="object"/>.
        /// Может быть полезен при формировании универсальных выражений,
        /// например, для динамической сортировки или построения метаданных.
        /// </remarks>
        public static Expression<Func<T1, object>> ConvertExpression<T1, TR2>(this Expression<Func<T1, TR2>> expression)
            => ExpressionHelper.ConvertExpression<T1, object, T1, TR2>(expression, argConverter => argConverter, resultConverter => resultConverter);

        /// <summary>
        /// Пытается вычислить значение указанного выражения <paramref name="member" />.
        /// Поддерживает распространённые формы выражений (binary, method call, unary, member и т.д.).
        /// В некоторых случаях, когда прямое вычисление невозможно, метод возвращает специальные
        /// значения для булевых выражений.
        /// </summary>
        /// <param name="member">Выражение, значение которого требуется получить.</param>
        /// <returns>Полученное значение как <see cref="object" />, или <c>null</c>, если значение не может быть определено.
        /// Для некоторых булевых member-выражений метод может возвращать <c>true</c> или <c>false</c>,
        /// когда непосредственная компиляция выражения не удалась.</returns>
        public static object GetValue(this Expression member) => ExpressionHelper.GetValue(member);

        /// <summary>
        /// Возвращает <see cref="PropertyInfo" />, соответствующий переданному выражению.
        /// </summary>
        /// <param name="expr">Выражение, которое должно представлять доступ к свойству.</param>
        /// <returns>Объект <see cref="PropertyInfo" />, если выражение представляет свойство; иначе <c>null</c>.</returns>
        public static PropertyInfo GetPropertyInfo(this Expression expr) => ExpressionHelper.GetPropertyInfo(expr);

        /// <summary>
        /// Извлекает <see cref="MemberInfo" /> из различных типов узлов выражения.
        /// Поддерживаемые типы узлов: <see cref="LambdaExpression" />, <see cref="BinaryExpression" />,
        /// <see cref="MemberExpression" />, <see cref="UnaryExpression" />, <see cref="MethodCallExpression" />,
        /// <see cref="ConditionalExpression" />.
        /// </summary>
        /// <param name="expr">Анализируемое выражение.</param>
        /// <returns>Разрешённый <see cref="MemberInfo" />, либо <c>null</c>, если член не удалось определить.</returns>
        public static MemberInfo GetMemberInfo(this Expression expr) => ExpressionHelper.GetMemberInfo(expr);

        /// <summary>
        /// Возвращает кэш сведений о члене, представленном в заданном выражении.
        /// </summary>
        /// <param name="expr">Выражение, содержащее ссылку на член, для которого требуется получить кэш сведений. Не должно быть равно
        /// null.</param>
        /// <returns>Объект MemberCache, содержащий сведения о члене, извлечённом из выражения.</returns>
        public static MemberCache GetMemberCache(this Expression expr) => ExpressionHelper.GetMemberCache(expr);

        /// <summary>
        /// Возвращает имя свойства, представленного указанным выражением.
        /// </summary>
        /// <param name="expr">Выражение, определяющее свойство, имя которого требуется получить.
        /// Должно представлять обращение к свойству.</param>
        /// <returns>Имя свойства, если выражение представляет доступ к свойству; иначе — null.</returns>
        /// <remarks>Обычно этот метод используется для получения имён свойств в типобезопасной форме,
        /// например, в сценариях привязки данных или проверки значений.
        /// Если переданное выражение не представляет доступ к свойству, метод возвращает null.</remarks>
        public static string GetPropertyName(this Expression expr) => ExpressionHelper.GetPropertyName(expr);
    }
}