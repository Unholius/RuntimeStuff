// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="ExpressionHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace RuntimeStuff.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
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
    public static class ExpressionHelper
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
                Expression<Func<T2, TR2>> expression,
                Expression<Func<T1, T2>> argConverter,
                Expression<Func<TR2, TR1>> resultConverter)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            if (argConverter == null)
            {
                throw new ArgumentNullException(nameof(argConverter));
            }

            if (resultConverter == null)
            {
                throw new ArgumentNullException(nameof(resultConverter));
            }

            // Новый параметр итогового выражения
            var newParameter = Expression.Parameter(typeof(T1), "p");

            // Подставляем newParameter в argConverter
            var convertedArgument = ReplaceParameter(
                argConverter,
                argConverter.Parameters[0],
                newParameter);

            // Подставляем convertedArgument в исходное выражение
            var newBody = ReplaceParameter(
                expression,
                expression.Parameters[0],
                convertedArgument);

            // Подставляем newBody в resultConverter
            var finalBody = ReplaceParameter(
                resultConverter,
                resultConverter.Parameters[0],
                newBody);

            return Expression.Lambda<Func<T1, TR1>>(finalBody, newParameter);
        }

        /// <summary>
        /// Возвращает кэш сведений о члене, представленном в заданном выражении.
        /// </summary>
        /// <param name="expr">Выражение, содержащее ссылку на член, для которого требуется получить кэш сведений. Не должно быть равно
        /// null.</param>
        /// <returns>Объект MemberCache, содержащий сведения о члене, извлечённом из выражения.</returns>
        public static MemberCache GetMemberCache(Expression expr) => MemberCache.Create(GetMemberInfo(expr));

        /// <summary>
        /// Извлекает <see cref="MemberInfo" /> из различных типов узлов выражения.
        /// Поддерживаемые типы узлов: <see cref="LambdaExpression" />, <see cref="BinaryExpression" />,.
        /// <see cref="MemberExpression" />, <see cref="UnaryExpression" />, <see cref="MethodCallExpression" />,
        /// <see cref="ConditionalExpression" />.
        /// </summary>
        /// <param name="expr">Анализируемое выражение.</param>
        /// <returns>Разрешённый <see cref="MemberInfo" />, либо <c>null</c>, если член не удалось определить.</returns>
        public static MemberInfo GetMemberInfo(Expression expr)
        {
            if (expr == null)
            {
                return null;
            }

            switch (expr)
            {
                case LambdaExpression le: return GetMemberInfoFromLambda(le);
                case BinaryExpression be: return GetMemberInfo(be.Left);
                case MemberExpression me: return me.Member;
                case UnaryExpression ue: return GetMemberInfo(ue.Operand);
                case MethodCallExpression mc: return GetMemberInfoFromMethodCall(mc);
                case ConditionalExpression ce: return GetMemberInfo(ce.IfTrue) ?? GetMemberInfo(ce.IfFalse);
                default: return null;
            }
        }

        /// <summary>
        /// Возвращает <see cref="PropertyInfo" />, соответствующий переданному выражению.
        /// </summary>
        /// <param name="expr">Выражение, которое должно представлять доступ к свойству.</param>
        /// <returns>Объект <see cref="PropertyInfo" />, если выражение представляет свойство; иначе <c>null</c>.</returns>
        public static PropertyInfo GetPropertyInfo(Expression expr) => GetMemberInfo(expr) as PropertyInfo;

        /// <summary>
        /// Возвращает информацию о свойстве, заданном лямбда-выражением доступа к нему.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, содержащего свойство.
        /// </typeparam>
        /// <param name="propertySelector">
        /// Лямбда-выражение, описывающее доступ к свойству,
        /// например: <c>x =&gt; x.Name</c>.
        /// </param>
        /// <returns>
        /// Объект <see cref="PropertyInfo"/>, соответствующий выбранному свойству.
        /// </returns>
        /// <remarks>
        /// Метод предназначен для безопасного получения информации о свойстве
        /// без использования строковых имён.
        ///
        /// Поддерживается:
        /// <list type="bullet">
        /// <item><description>Прямой доступ к свойству;</description></item>
        /// <item><description>Доступ к свойствам значимых типов
        /// с неявным приведением к <see cref="object"/> (boxing).</description></item>
        /// </list>
        ///
        /// Использование выражений гарантирует корректность при рефакторинге
        /// и снижает вероятность ошибок, связанных с переименованием свойств.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Генерируется, если <paramref name="propertySelector"/> равен <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Генерируется, если выражение не представляет собой доступ к свойству.
        /// </exception>
        public static PropertyInfo GetPropertyInfo<T>(Expression<Func<T, object>> propertySelector)
        {
            if (propertySelector == null)
            {
                throw new ArgumentNullException(nameof(propertySelector));
            }

            var body = propertySelector.Body;

            // value-type -> object (boxing)
            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            {
                body = unary.Operand;
            }

            if (body is MemberExpression member && member.Member is PropertyInfo pi)
            {
                return pi;
            }

            throw new ArgumentException(@"Expression must be a property access expression.", nameof(propertySelector));
        }

        /// <summary>
        /// Возвращает цепочку свойств, заданную лямбда-выражением доступа к свойству.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, от которого начинается цепочка доступа к свойствам.
        /// </typeparam>
        /// <param name="propertySelector">
        /// Лямбда-выражение, описывающее доступ к свойству или вложенным свойствам,
        /// например: <c>x =&gt; x.Address.City.Name</c>.
        /// </param>
        /// <returns>
        /// Коллекция <see cref="PropertyInfo"/>, представляющая цепочку свойств
        /// в порядке от корневого свойства к конечному.
        /// </returns>
        /// <remarks>
        /// Метод извлекает последовательность свойств из выражения
        /// <see cref="Expression{TDelegate}"/>.
        ///
        /// Поддерживаются выражения:
        /// <list type="bullet">
        /// <item><description>Прямого доступа к свойству;</description></item>
        /// <item><description>Вложенного доступа к свойствам;</description></item>
        /// <item><description>Доступа к значимым типам с неявным приведением к <see cref="object"/>.</description></item>
        /// </list>
        ///
        /// В случае использования поля вместо свойства,
        /// либо более сложных выражений (вызовы методов, индексаторы и т.п.),
        /// цепочка будет прервана и сгенерировано исключение.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Генерируется, если <paramref name="propertySelector"/> равен <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Генерируется, если выражение не представляет собой доступ к свойству
        /// или цепочку доступов к свойствам.
        /// </exception>
        public static IReadOnlyList<PropertyInfo> GetPropertyInfoChain<T>(Expression<Func<T, object>> propertySelector)
        {
            if (propertySelector == null)
            {
                throw new ArgumentNullException(nameof(propertySelector));
            }

            var expr = propertySelector.Body;

            if (expr is UnaryExpression u && u.NodeType == ExpressionType.Convert)
            {
                expr = u.Operand;
            }

            var stack = new Stack<PropertyInfo>();

            while (expr is MemberExpression m)
            {
                if (!(m.Member is PropertyInfo pi))
                {
                    break;
                }

                stack.Push(pi);
                expr = m.Expression;
            }

            return stack.Count == 0 ? throw new ArgumentException("Expression must be a property access.") : stack.ToArray();
        }

        /// <summary>
        /// Возвращает имя свойства, представленного указанным выражением.
        /// </summary>
        /// <param name="expr">Выражение, определяющее свойство, имя которого требуется получить.
        /// Должно представлять обращение к свойству.</param>
        /// <returns>Имя свойства, если выражение представляет доступ к свойству; иначе — null.</returns>
        /// <remarks>Обычно этот метод используется для получения имён свойств в типобезопасной форме,
        /// например, в сценариях привязки данных или проверки значений.
        /// Если переданное выражение не представляет доступ к свойству, метод возвращает null.</remarks>
        public static string GetPropertyName(Expression expr) => GetPropertyInfo(expr)?.Name;

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
        public static object GetValue(Expression member)
        {
            try
            {
                if (member is BinaryExpression be)
                {
                    member = be.Right;
                }

                if (member is MethodCallExpression mce)
                {
                    member = mce.Arguments[1];
                }

                if (member is UnaryExpression ue)
                {
                    return ue.NodeType == ExpressionType.Not ? false : (bool?)null;
                }

                try
                {
                    var objectMember = Expression.Convert(member, typeof(object));
                    var getterLambda = Expression.Lambda<Func<object>>(objectMember);
                    var getter = getterLambda.Compile();
                    var value = getter();
                    return value;
                }
                catch (Exception)
                {
                    if (member is MemberExpression me)
                    {
                        var p = GetPropertyInfo(me);
                        return p?.PropertyType == typeof(bool) ? true : (bool?)null;
                    }

                    throw;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Вспомогательный метод для извлечения <see cref="MemberInfo" /> из <see cref="LambdaExpression" />.
        /// При возможности пытается сопоставить свойство по типу обобщения лямбды.
        /// </summary>
        /// <param name="le">Лямбда-выражение для анализа.</param>
        /// <returns>Найденный <see cref="MemberInfo" /> или <c>null</c>.</returns>
        private static MemberInfo GetMemberInfoFromLambda(LambdaExpression le)
        {
            var propDeclaringType = le.Type.GenericTypeArguments.FirstOrDefault();
            if (propDeclaringType == null)
            {
                return null;
            }

            var pi = GetMemberInfo(le.Body);
            return pi;
        }

        /// <summary>
        /// Вспомогательный метод для извлечения <see cref="MemberInfo" /> из <see cref="MethodCallExpression" />.
        /// Ожидается, что нужный член содержится в первом аргументе вызова метода.
        /// </summary>
        /// <param name="mce">Выражение вызова метода.</param>
        /// <returns>Найденный <see cref="MemberInfo" /> или <c>null</c>.</returns>
        private static MemberInfo GetMemberInfoFromMethodCall(MethodCallExpression mce)
        {
            var pi = GetMemberInfo(mce.Arguments[0]);
            return pi;
        }

        private static Expression ReplaceParameter(LambdaExpression lambda, ParameterExpression source, Expression target)
        {
            return new ParameterReplaceVisitor(source, target)
                .Visit(lambda.Body);
        }

        private sealed class ParameterReplaceVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression source;
            private readonly Expression target;

            public ParameterReplaceVisitor(ParameterExpression source, Expression target)
            {
                this.source = source;
                this.target = target;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return node == this.source ? this.target : base.VisitParameter(node);
            }
        }
    }
}