using System;

namespace RuntimeStuff.Extensions
{
    /// <summary>
    /// Предоставляет методы расширения для преобразования между делегатами Action с разными сигнатурами.
    /// Основное назначение - конвертация между строго типизированными делегатами и делегатами, работающими с object.
    /// </summary>
    public static class RSActionExtensions
    {
        // Группа методов для преобразования строго типизированных делегатов в делегаты, работающие с object

        /// <summary>
        /// Преобразует Action&lt;T1&gt; в Action&lt;object&gt;
        /// </summary>
        public static Action<object> ConvertAction<T1>(this Action<T1> action) => t1 => action((T1)t1);

        /// <summary>
        /// Преобразует Action&lt;T1, T2&gt; в Action&lt;object, object&gt;
        /// </summary>
        public static Action<object, object> ConvertAction<T1, T2>(this Action<T1, T2> action) => (t1, t2) => action((T1)t1, (T2)t2);

        /// <summary>
        /// Преобразует Action&lt;T1, T2, T3&gt; в Action&lt;object, object, object&gt;
        /// </summary>
        public static Action<object, object, object> ConvertAction<T1, T2, T3>(this Action<T1, T2, T3> action) => (t1, t2, t3) => action((T1)t1, (T2)t2, (T3)t3);

        /// <summary>
        /// Преобразует Action&lt;T1, T2, T3, T4&gt; в Action&lt;object, object, object, object&gt;
        /// </summary>
        public static Action<object, object, object, object> ConvertAction<T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action) => (t1, t2, t3, t4) => action((T1)t1, (T2)t2, (T3)t3, (T4)t4);

        /// <summary>
        /// Преобразует Action&lt;T1, T2, T3, T4, T5&gt; в Action&lt;object, object, object, object, object&gt;
        /// </summary>
        public static Action<object, object, object, object, object> ConvertAction<T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action) => (t1, t2, t3, t4, t5) => action((T1)t1, (T2)t2, (T3)t3, (T4)t4, (T5)t5);

        /// <summary>
        /// Преобразует Action&lt;T1, T2, T3, T4, T5, T6&gt; в Action&lt;object, object, object, object, object, object&gt;
        /// </summary>
        public static Action<object, object, object, object, object, object> ConvertAction<T1, T2, T3, T4, T5, T6>(this Action<T1, T2, T3, T4, T5, T6> action) => (t1, t2, t3, t4, t5, t6) => action((T1)t1, (T2)t2, (T3)t3, (T4)t4, (T5)t5, (T6)t6);

        /// <summary>
        /// Преобразует Action&lt;T1, T2, T3, T4, T5, T6, T7&gt; в Action&lt;object, object, object, object, object, object, object&gt;
        /// </summary>
        public static Action<object, object, object, object, object, object, object> ConvertAction<T1, T2, T3, T4, T5, T6, T7>(this Action<T1, T2, T3, T4, T5, T6, T7> action) => (t1, t2, t3, t4, t5, t6, t7) => action((T1)t1, (T2)t2, (T3)t3, (T4)t4, (T5)t5, (T6)t6, (T7)t7);

        /// <summary>
        /// Преобразует Action&lt;T1, T2, T3, T4, T5, T6, T7, T8&gt; в Action&lt;object, object, object, object, object, object, object, object&gt;
        /// </summary>
        public static Action<object, object, object, object, object, object, object, object> ConvertAction<T1, T2, T3, T4, T5, T6, T7, T8>(this Action<T1, T2, T3, T4, T5, T6, T7, T8> action) => (t1, t2, t3, t4, t5, t6, t7, t8) => action((T1)t1, (T2)t2, (T3)t3, (T4)t4, (T5)t5, (T6)t6, (T7)t7, (T8)t8);

        // Группа методов для преобразования делегатов, работающих с object, в строго типизированные делегаты

        /// <summary>
        /// Преобразует Action&lt;object&gt; в Action&lt;T1&gt;
        /// </summary>
        public static Action<T1> ConvertAction<T1>(this Action<object> action) => t1 => action(t1);

        /// <summary>
        /// Преобразует Action&lt;object, object&gt; в Action&lt;T1, T2&gt;
        /// </summary>
        public static Action<T1, T2> ConvertAction<T1, T2>(this Action<object, object> action) => (t1, t2) => action(t1, t2);

        /// <summary>
        /// Преобразует Action&lt;object, object, object&gt; в Action&lt;T1, T2, T3&gt;
        /// </summary>
        public static Action<T1, T2, T3> ConvertAction<T1, T2, T3>(this Action<object, object, object> action) => (t1, t2, t3) => action(t1, t2, t3);

        /// <summary>
        /// Преобразует Action&lt;object, object, object, object&gt; в Action&lt;T1, T2, T3, T4&gt;
        /// </summary>
        public static Action<T1, T2, T3, T4> ConvertAction<T1, T2, T3, T4>(this Action<object, object, object, object> action) => (t1, t2, t3, t4) => action(t1, t2, t3, t4);

        /// <summary>
        /// Преобразует Action&lt;object, object, object, object, object&gt; в Action&lt;T1, T2, T3, T4, T5&gt;
        /// </summary>
        public static Action<T1, T2, T3, T4, T5> ConvertAction<T1, T2, T3, T4, T5>(this Action<object, object, object, object, object> action) => (t1, t2, t3, t4, t5) => action(t1, t2, t3, t4, t5);

        /// <summary>
        /// Преобразует Action&lt;object, object, object, object, object, object&gt; в Action&lt;T1, T2, T3, T4, T5, T6&gt;
        /// </summary>
        public static Action<T1, T2, T3, T4, T5, T6> ConvertAction<T1, T2, T3, T4, T5, T6>(this Action<object, object, object, object, object, object> action) => (t1, t2, t3, t4, t5, t6) => action(t1, t2, t3, t4, t5, t6);

        /// <summary>
        /// Преобразует Action&lt;object, object, object, object, object, object, object&gt; в Action&lt;T1, T2, T3, T4, T5, T6, T7&gt;
        /// </summary>
        public static Action<T1, T2, T3, T4, T5, T6, T7> ConvertAction<T1, T2, T3, T4, T5, T6, T7>(this Action<object, object, object, object, object, object, object> action) => (t1, t2, t3, t4, t5, t6, t7) => action(t1, t2, t3, t4, t5, t6, t7);

        /// <summary>
        /// Преобразует Action&lt;object, object, object, object, object, object, object, object&gt; в Action&lt;T1, T2, T3, T4, T5, T6, T7, T8&gt;
        /// </summary>
        public static Action<T1, T2, T3, T4, T5, T6, T7, T8> ConvertAction<T1, T2, T3, T4, T5, T6, T7, T8>(this Action<object, object, object, object, object, object, object, object> action) => (t1, t2, t3, t4, t5, t6, t7, t8) => action(t1, t2, t3, t4, t5, t6, t7, t8);
    }
}