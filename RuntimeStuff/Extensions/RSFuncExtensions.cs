using System;

namespace RuntimeStuff.Extensions
{
    /// <summary>
    /// Предоставляет методы расширения для преобразования между делегатами Func с разными сигнатурами.
    /// Основное назначение - конвертация между строго типизированными делегатами и делегатами, работающими с object.
    /// </summary>
    public static class RSFuncExtensions
    {
        // Группа методов для преобразования строго типизированных делегатов в делегаты, работающие с object

        /// <summary>
        /// Преобразует Func&lt;T1, R&gt; в Func&lt;object, object&gt;
        /// </summary>
        public static Func<object, object> ConvertFunc<T1, R>(this Func<T1, R> func) => t1 => func((T1)t1);

        /// <summary>
        /// Преобразует Func&lt;T1, T2, R&gt; в Func&lt;object, object, object&gt;
        /// </summary>
        public static Func<object, object, object> ConvertFunc<T1, T2, R>(this Func<T1, T2, R> func) => (t1, t2) => func((T1)t1, (T2)t2);

        /// <summary>
        /// Преобразует Func&lt;T1, T2, T3, R&gt; в Func&lt;object, object, object, object&gt;
        /// </summary>
        public static Func<object, object, object, object> ConvertFunc<T1, T2, T3, R>(this Func<T1, T2, T3, R> func) => (t1, t2, t3) => func((T1)t1, (T2)t2, (T3)t3);

        /// <summary>
        /// Преобразует Func&lt;T1, T2, T3, T4, R&gt; в Func&lt;object, object, object, object, object&gt;
        /// </summary>
        public static Func<object, object, object, object, object> ConvertFunc<T1, T2, T3, T4, R>(this Func<T1, T2, T3, T4, R> func) => (t1, t2, t3, t4) => func((T1)t1, (T2)t2, (T3)t3, (T4)t4);

        /// <summary>
        /// Преобразует Func&lt;T1, T2, T3, T4, T5, R&gt; в Func&lt;object, object, object, object, object, object&gt;
        /// </summary>
        public static Func<object, object, object, object, object, object> ConvertFunc<T1, T2, T3, T4, T5, R>(this Func<T1, T2, T3, T4, T5, R> func) => (t1, t2, t3, t4, t5) => func((T1)t1, (T2)t2, (T3)t3, (T4)t4, (T5)t5);

        /// <summary>
        /// Преобразует Func&lt;T1, T2, T3, T4, T5, T6, R&gt; в Func&lt;object, object, object, object, object, object, object&gt;
        /// </summary>
        public static Func<object, object, object, object, object, object, object> ConvertFunc<T1, T2, T3, T4, T5, T6, R>(this Func<T1, T2, T3, T4, T5, T6, R> func) => (t1, t2, t3, t4, t5, t6) => func((T1)t1, (T2)t2, (T3)t3, (T4)t4, (T5)t5, (T6)t6);

        /// <summary>
        /// Преобразует Func&lt;T1, T2, T3, T4, T5, T6, T7, R&gt; в Func&lt;object, object, object, object, object, object, object, object&gt;
        /// </summary>
        public static Func<object, object, object, object, object, object, object, object> ConvertFunc<T1, T2, T3, T4, T5, T6, T7, R>(this Func<T1, T2, T3, T4, T5, T6, T7, R> func) => (t1, t2, t3, t4, t5, t6, t7) => func((T1)t1, (T2)t2, (T3)t3, (T4)t4, (T5)t5, (T6)t6, (T7)t7);

        /// <summary>
        /// Преобразует Func&lt;T1, T2, T3, T4, T5, T6, T7, T8, R&gt; в Func&lt;object, object, object, object, object, object, object, object, object&gt;
        /// </summary>
        public static Func<object, object, object, object, object, object, object, object, object> ConvertFunc<T1, T2, T3, T4, T5, T6, T7, T8, R>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, R> func) => (t1, t2, t3, t4, t5, t6, t7, t8) => func((T1)t1, (T2)t2, (T3)t3, (T4)t4, (T5)t5, (T6)t6, (T7)t7, (T8)t8);

        // Группа методов для преобразования делегатов, работающих с object, в строго типизированные делегаты

        /// <summary>
        /// Преобразует Func&lt;object, object&gt; в Func&lt;T1, R&gt;
        /// </summary>
        public static Func<T1, R> ConvertFunc<T1, R>(this Func<object, object> func) => t1 => (R)func(t1);

        /// <summary>
        /// Преобразует Func&lt;object, object, object&gt; в Func&lt;T1, T2, R&gt;
        /// </summary>
        public static Func<T1, T2, R> ConvertFunc<T1, T2, R>(this Func<object, object, object> func) => (t1, t2) => (R)func(t1, t2);

        /// <summary>
        /// Преобразует Func&lt;object, object, object, object&gt; в Func&lt;T1, T2, T3, R&gt;
        /// </summary>
        public static Func<T1, T2, T3, R> ConvertFunc<T1, T2, T3, R>(this Func<object, object, object, object> func) => (t1, t2, t3) => (R)func(t1, t2, t3);

        /// <summary>
        /// Преобразует Func&lt;object, object, object, object, object&gt; в Func&lt;T1, T2, T3, T4, R&gt;
        /// </summary>
        public static Func<T1, T2, T3, T4, R> ConvertFunc<T1, T2, T3, T4, R>(this Func<object, object, object, object, object> func) => (t1, t2, t3, t4) => (R)func(t1, t2, t3, t4);

        /// <summary>
        /// Преобразует Func&lt;object, object, object, object, object, object&gt; в Func&lt;T1, T2, T3, T4, T5, R&gt;
        /// </summary>
        public static Func<T1, T2, T3, T4, T5, R> ConvertFunc<T1, T2, T3, T4, T5, R>(this Func<object, object, object, object, object, object> func) => (t1, t2, t3, t4, t5) => (R)func(t1, t2, t3, t4, t5);

        /// <summary>
        /// Преобразует Func&lt;object, object, object, object, object, object, object&gt; в Func&lt;T1, T2, T3, T4, T5, T6, R&gt;
        /// </summary>
        public static Func<T1, T2, T3, T4, T5, T6, R> ConvertFunc<T1, T2, T3, T4, T5, T6, R>(this Func<object, object, object, object, object, object, object> func) => (t1, t2, t3, t4, t5, t6) => (R)func(t1, t2, t3, t4, t5, t6);

        /// <summary>
        /// Преобразует Func&lt;object, object, object, object, object, object, object, object&gt; в Func&lt;T1, T2, T3, T4, T5, T6, T7, R&gt;
        /// </summary>
        public static Func<T1, T2, T3, T4, T5, T6, T7, R> ConvertFunc<T1, T2, T3, T4, T5, T6, T7, R>(this Func<object, object, object, object, object, object, object, object> func) => (t1, t2, t3, t4, t5, t6, t7) => (R)func(t1, t2, t3, t4, t5, t6, t7);

        /// <summary>
        /// Преобразует Func&lt;object, object, object, object, object, object, object, object, object&gt; в Func&lt;T1, T2, T3, T4, T5, T6, T7, T8, R&gt;
        /// </summary>
        public static Func<T1, T2, T3, T4, T5, T6, T7, T8, R> ConvertFunc<T1, T2, T3, T4, T5, T6, T7, T8, R>(this Func<object, object, object, object, object, object, object, object, object> func) => (t1, t2, t3, t4, t5, t6, t7, t8) => (R)func(t1, t2, t3, t4, t5, t6, t7, t8);
    }
}