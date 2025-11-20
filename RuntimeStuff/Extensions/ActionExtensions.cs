using System;
using RuntimeStuff.Helpers;
using RuntimeStuff.Properties;

namespace RuntimeStuff.Extensions
{
    /// <summary>
    ///     Предоставляет методы расширения для преобразования между делегатами Action с разными сигнатурами.
    ///     Основное назначение - конвертация между строго типизированными делегатами и делегатами, работающими с object.
    /// </summary>
    public static class ActionExtensions
    {
        /// <summary>
        ///     Универсальное преобразование любого <see cref="Delegate" /> (например, Action, Action{T1,T2,...})
        ///     в <see cref="Action{object[]}" />, принимающий параметры как массив object[].
        /// </summary>
        public static Action<object[]> ConvertAction(this Delegate del)
        {
            if (del == null)
                throw new ArgumentNullException(nameof(del));

            var method = del.Method;
            var parameters = method.GetParameters();
            var paramCount = parameters.Length;

            return args =>
            {
                if (args == null && paramCount > 0)
                    throw new ArgumentNullException(nameof(args));

                if ((args?.Length ?? 0) < paramCount)
                    throw new ArgumentException(string.Format(Resources.ActionExtensions_ConvertAction_Expected_at_least__0__arguments, paramCount), nameof(args));

                // Создаём массив аргументов нужной длины и типов
                var callArgs = new object[paramCount];
                for (var i = 0; i < paramCount; i++)
                {
                    var targetType = parameters[i].ParameterType;
                    var value = args?[i];

                    if (value != null && !targetType.IsInstanceOfType(value))
                        try
                        {
                            callArgs[i] = TypeHelper.ChangeType(value, targetType);
                        }
                        catch
                        {
                            throw new InvalidCastException(
                                $"Cannot convert argument {i + 1} from {value.GetType()} to {targetType}");
                        }
                    else
                        callArgs[i] = value;
                }

                del.DynamicInvoke(callArgs);
            };
        }


// Группа методов для преобразования строго типизированных делегатов в делегаты, работающие с object

        /// <summary>
        ///     Преобразует Action&lt;T1&gt; в Action&lt;object&gt;
        /// </summary>
        public static Action<object> ConvertAction<T1>(this Action<T1> action, Func<object, T1> converter = null)
        {
            return t1 => action(converter == null ? (T1)t1 : converter(t1));
        }

        /// <summary>
        ///     Преобразует <see cref="Action{T1,T2}" /> в <see cref="Action{object,object}" />,
        ///     выполняя приведение или пользовательское преобразование аргументов.
        /// </summary>
        public static Action<object, object> ConvertAction<T1, T2>(
            this Action<T1, T2> action,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null)
        {
            return (t1, t2) => action(
                converter1 == null ? (T1)t1 : converter1(t1),
                converter2 == null ? (T2)t2 : converter2(t2)
            );
        }

        /// <summary>
        ///     Преобразует <see cref="Action{T1,T2,T3}" /> в <see cref="Action{object,object,object}" />,
        ///     выполняя приведение или пользовательское преобразование аргументов.
        /// </summary>
        public static Action<object, object, object> ConvertAction<T1, T2, T3>(
            this Action<T1, T2, T3> action,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null,
            Func<object, T3> converter3 = null)
        {
            return (t1, t2, t3) => action(
                converter1 == null ? (T1)t1 : converter1(t1),
                converter2 == null ? (T2)t2 : converter2(t2),
                converter3 == null ? (T3)t3 : converter3(t3)
            );
        }

        /// <summary>
        ///     Преобразует <see cref="Action{T1,T2,T3,T4}" /> в <see cref="Action{object,object,object,object}" />,
        ///     выполняя приведение или пользовательское преобразование аргументов.
        /// </summary>
        public static Action<object, object, object, object> ConvertAction<T1, T2, T3, T4>(
            this Action<T1, T2, T3, T4> action,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null,
            Func<object, T3> converter3 = null,
            Func<object, T4> converter4 = null)
        {
            return (t1, t2, t3, t4) => action(
                converter1 == null ? (T1)t1 : converter1(t1),
                converter2 == null ? (T2)t2 : converter2(t2),
                converter3 == null ? (T3)t3 : converter3(t3),
                converter4 == null ? (T4)t4 : converter4(t4)
            );
        }

        /// <summary>
        ///     Преобразует Action&lt;T1, T2, T3, T4, T5&gt; в Action&lt;object, object, object, object, object&gt;
        /// </summary>
        public static Action<object, object, object, object, object> ConvertAction<T1, T2, T3, T4, T5>(
            this Action<T1, T2, T3, T4, T5> action,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null,
            Func<object, T3> converter3 = null,
            Func<object, T4> converter4 = null,
            Func<object, T5> converter5 = null)
        {
            return (t1, t2, t3, t4, t5) => action(
                converter1 == null ? (T1)t1 : converter1(t1),
                converter2 == null ? (T2)t2 : converter2(t2),
                converter3 == null ? (T3)t3 : converter3(t3),
                converter4 == null ? (T4)t4 : converter4(t4),
                converter5 == null ? (T5)t5 : converter5(t5)
            );
        }

        /// <summary>
        ///     Преобразует Action&lt;T1, T2, T3, T4, T5, T6&gt; в Action&lt;object, object, object, object, object, object&gt;
        /// </summary>
        public static Action<object, object, object, object, object, object> ConvertAction<T1, T2, T3, T4, T5, T6>(
            this Action<T1, T2, T3, T4, T5, T6> action,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null,
            Func<object, T3> converter3 = null,
            Func<object, T4> converter4 = null,
            Func<object, T5> converter5 = null,
            Func<object, T6> converter6 = null)
        {
            return (t1, t2, t3, t4, t5, t6) => action(
                converter1 == null ? (T1)t1 : converter1(t1),
                converter2 == null ? (T2)t2 : converter2(t2),
                converter3 == null ? (T3)t3 : converter3(t3),
                converter4 == null ? (T4)t4 : converter4(t4),
                converter5 == null ? (T5)t5 : converter5(t5),
                converter6 == null ? (T6)t6 : converter6(t6)
            );
        }

        /// <summary>
        ///     Преобразует Action&lt;T1, T2, T3, T4, T5, T6, T7&gt; в Action&lt;object, object, object, object, object, object,
        ///     object&gt;
        /// </summary>
        public static Action<object, object, object, object, object, object, object> ConvertAction<T1, T2, T3, T4, T5, T6,
            T7>(
            this Action<T1, T2, T3, T4, T5, T6, T7> action,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null,
            Func<object, T3> converter3 = null,
            Func<object, T4> converter4 = null,
            Func<object, T5> converter5 = null,
            Func<object, T6> converter6 = null,
            Func<object, T7> converter7 = null)
        {
            return (t1, t2, t3, t4, t5, t6, t7) => action(
                converter1 == null ? (T1)t1 : converter1(t1),
                converter2 == null ? (T2)t2 : converter2(t2),
                converter3 == null ? (T3)t3 : converter3(t3),
                converter4 == null ? (T4)t4 : converter4(t4),
                converter5 == null ? (T5)t5 : converter5(t5),
                converter6 == null ? (T6)t6 : converter6(t6),
                converter7 == null ? (T7)t7 : converter7(t7)
            );
        }

        /// <summary>
        ///     Преобразует Action&lt;T1, T2, T3, T4, T5, T6, T7, T8&gt; в Action&lt;object, object, object, object, object,
        ///     object, object, object&gt;
        /// </summary>
        public static Action<object, object, object, object, object, object, object, object> ConvertAction<T1, T2, T3, T4,
            T5, T6, T7, T8>(
            this Action<T1, T2, T3, T4, T5, T6, T7, T8> action,
            Func<object, T1> converter1 = null,
            Func<object, T2> converter2 = null,
            Func<object, T3> converter3 = null,
            Func<object, T4> converter4 = null,
            Func<object, T5> converter5 = null,
            Func<object, T6> converter6 = null,
            Func<object, T7> converter7 = null,
            Func<object, T8> converter8 = null)
        {
            return (t1, t2, t3, t4, t5, t6, t7, t8) => action(
                converter1 == null ? (T1)t1 : converter1(t1),
                converter2 == null ? (T2)t2 : converter2(t2),
                converter3 == null ? (T3)t3 : converter3(t3),
                converter4 == null ? (T4)t4 : converter4(t4),
                converter5 == null ? (T5)t5 : converter5(t5),
                converter6 == null ? (T6)t6 : converter6(t6),
                converter7 == null ? (T7)t7 : converter7(t7),
                converter8 == null ? (T8)t8 : converter8(t8)
            );
        }

        // Группа методов для преобразования делегатов, работающих с object, в строго типизированные делегаты

        /// <summary>
        ///     Преобразует Action&lt;object&gt; в Action&lt;T1&gt;
        /// </summary>
        public static Action<T1> ConvertAction<T1>(this Action<object> action)
        {
            return t1 => action(t1);
        }

        /// <summary>
        ///     Преобразует Action&lt;object, object&gt; в Action&lt;T1, T2&gt;
        /// </summary>
        public static Action<T1, T2> ConvertAction<T1, T2>(this Action<object, object> action)
        {
            return (t1, t2) => action(t1, t2);
        }

        /// <summary>
        ///     Преобразует Action&lt;object, object, object&gt; в Action&lt;T1, T2, T3&gt;
        /// </summary>
        public static Action<T1, T2, T3> ConvertAction<T1, T2, T3>(this Action<object, object, object> action)
        {
            return (t1, t2, t3) => action(t1, t2, t3);
        }

        /// <summary>
        ///     Преобразует Action&lt;object, object, object, object&gt; в Action&lt;T1, T2, T3, T4&gt;
        /// </summary>
        public static Action<T1, T2, T3, T4> ConvertAction<T1, T2, T3, T4>(
            this Action<object, object, object, object> action)
        {
            return (t1, t2, t3, t4) => action(t1, t2, t3, t4);
        }

        /// <summary>
        ///     Преобразует Action&lt;object, object, object, object, object&gt; в Action&lt;T1, T2, T3, T4, T5&gt;
        /// </summary>
        public static Action<T1, T2, T3, T4, T5> ConvertAction<T1, T2, T3, T4, T5>(
            this Action<object, object, object, object, object> action)
        {
            return (t1, t2, t3, t4, t5) => action(t1, t2, t3, t4, t5);
        }

        /// <summary>
        ///     Преобразует Action&lt;object, object, object, object, object, object&gt; в Action&lt;T1, T2, T3, T4, T5, T6&gt;
        /// </summary>
        public static Action<T1, T2, T3, T4, T5, T6> ConvertAction<T1, T2, T3, T4, T5, T6>(
            this Action<object, object, object, object, object, object> action)
        {
            return (t1, t2, t3, t4, t5, t6) => action(t1, t2, t3, t4, t5, t6);
        }

        /// <summary>
        ///     Преобразует Action&lt;object, object, object, object, object, object, object&gt; в Action&lt;T1, T2, T3, T4, T5,
        ///     T6, T7&gt;
        /// </summary>
        public static Action<T1, T2, T3, T4, T5, T6, T7> ConvertAction<T1, T2, T3, T4, T5, T6, T7>(
            this Action<object, object, object, object, object, object, object> action)
        {
            return (t1, t2, t3, t4, t5, t6, t7) => action(t1, t2, t3, t4, t5, t6, t7);
        }

        /// <summary>
        ///     Преобразует Action&lt;object, object, object, object, object, object, object, object&gt; в Action&lt;T1, T2, T3,
        ///     T4, T5, T6, T7, T8&gt;
        /// </summary>
        public static Action<T1, T2, T3, T4, T5, T6, T7, T8> ConvertAction<T1, T2, T3, T4, T5, T6, T7, T8>(
            this Action<object, object, object, object, object, object, object, object> action)
        {
            return (t1, t2, t3, t4, t5, t6, t7, t8) => action(t1, t2, t3, t4, t5, t6, t7, t8);
        }
    }
}