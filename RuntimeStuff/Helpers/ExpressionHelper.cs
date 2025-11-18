using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using RuntimeStuff.Helpers;


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
    /// Пытается вычислить значение указанного выражения <paramref name="member"/>.
    /// Поддерживает распространённые формы выражений (binary, method call, unary, member и т.д.).
    /// В некоторых случаях, когда прямое вычисление невозможно, метод возвращает специальные
    /// значения для булевых выражений.
    /// </summary>
    /// <param name="member">Выражение, значение которого требуется получить.</param>
    /// <returns>
    /// Полученное значение как <see cref="object"/>, или <c>null</c>, если значение не может быть определено.
    /// Для некоторых булевых member-выражений метод может возвращать <c>true</c> или <c>false</c>,
    /// когда непосредственная компиляция выражения не удалась.
    /// </returns>
    public static object GetValue(Expression member)
    {
        try
        {
            if (member is BinaryExpression be) member = be.Right;
            if (member is MethodCallExpression mce) member = mce.Arguments[1];

            if (member is UnaryExpression ue) return ue.NodeType == ExpressionType.Not ? false : (bool?)null;
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
    /// Возвращает <see cref="PropertyInfo"/>, соответствующий переданному выражению.
    /// </summary>
    /// <param name="expr">Выражение, которое должно представлять доступ к свойству.</param>
    /// <returns>
    /// Объект <see cref="PropertyInfo"/>, если выражение представляет свойство; иначе <c>null</c>.
    /// </returns>
    public static PropertyInfo GetPropertyInfo(Expression expr)
    {
        return GetMemberInfo(expr) as PropertyInfo;
    }

    /// <summary>
    /// Извлекает <see cref="MemberInfo"/> из различных типов узлов выражения.
    /// Поддерживаемые типы узлов: <see cref="LambdaExpression"/>, <see cref="BinaryExpression"/>,

    /// <see cref="MemberExpression"/>, <see cref="UnaryExpression"/>, <see cref="MethodCallExpression"/>,

    /// <see cref="ConditionalExpression"/>.
    /// </summary>
    /// <param name="expr">Анализируемое выражение.</param>
    /// <returns>
    /// Разрешённый <see cref="MemberInfo"/>, либо <c>null</c>, если член не удалось определить.
    /// </returns>
    public static MemberInfo GetMemberInfo(Expression expr)
    {
        if (expr == null)
            return null;
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
    /// Возвращает имя свойства, представленного указанным выражением.
    /// </summary>
    /// <remarks>
    /// Обычно этот метод используется для получения имён свойств в типобезопасной форме,
    /// например, в сценариях привязки данных или проверки значений.
    /// Если переданное выражение не представляет доступ к свойству, метод возвращает null.
    /// </remarks>
    /// <param name="expr">
    /// Выражение, определяющее свойство, имя которого требуется получить.
    /// Должно представлять обращение к свойству.
    /// </param>
    /// <returns>
    /// Имя свойства, если выражение представляет доступ к свойству; иначе — null.
    /// </returns>
    public static string GetPropertyName(Expression expr) => GetPropertyInfo(expr)?.Name;

    /// <summary>
    /// Вспомогательный метод для извлечения <see cref="MemberInfo"/> из <see cref="LambdaExpression"/>.
    /// При возможности пытается сопоставить свойство по типу обобщения лямбды.
    /// </summary>
    /// <param name="le">Лямбда-выражение для анализа.</param>
    /// <returns>Найденный <see cref="MemberInfo"/> или <c>null</c>.</returns>
    private static MemberInfo GetMemberInfoFromLambda(LambdaExpression le)
    {
        var propDeclaringType = le.Type.GenericTypeArguments.FirstOrDefault();
        var pi = GetMemberInfo(le.Body);
        pi = TypeHelper.GetProperty(propDeclaringType, pi?.Name, StringComparison.Ordinal) ?? pi;
        return pi;
    }

    /// <summary>
    /// Вспомогательный метод для извлечения <see cref="MemberInfo"/> из <see cref="MethodCallExpression"/>.
    /// Ожидается, что нужный член содержится в первом аргументе вызова метода.
    /// </summary>
    /// <param name="mce">Выражение вызова метода.</param>
    /// <returns>Найденный <see cref="MemberInfo"/> или <c>null</c>.</returns>
    private static MemberInfo GetMemberInfoFromMethodCall(MethodCallExpression mce)
    {
        var pi = GetMemberInfo(mce.Arguments[0]);
        return pi;
    }
}