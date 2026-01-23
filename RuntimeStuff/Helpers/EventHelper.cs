// <copyright file="EventHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Helpers
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary>
    /// Вспомогательный класс для динамической работы с событиями
    /// с использованием Reflection.
    /// </summary>
    /// <remarks>
    /// Класс предоставляет методы для:
    /// <list type="bullet">
    /// <item><description>привязки обработчиков к событиям во время выполнения;</description></item>
    /// <item><description>адаптации событий с различными сигнатурами к унифицированным делегатам;</description></item>
    /// <item><description>безопасного управления подписками через <see cref="IDisposable"/>.</description></item>
    /// </list>
    ///
    /// Предназначен для инфраструктурного кода, динамического связывания,
    /// логирования, трассировки и сценариев, где тип события неизвестен
    /// на этапе компиляции.
    /// </remarks>
    public static class EventHelper
    {
        /// <summary>
        /// Привязывает обработчик к событию объекта по имени события.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, содержащего событие.
        /// </typeparam>
        /// <param name="obj">
        /// Экземпляр объекта, к событию которого выполняется привязка.
        /// </param>
        /// <param name="eventName">
        /// Имя события, к которому необходимо привязать обработчик.
        /// </param>
        /// <param name="actionSenderAndArgs">
        /// Делегат, который будет вызван при возникновении события.
        ///
        /// Первый параметр — объект-источник события (<c>sender</c>),
        /// второй параметр — аргументы события (<c>EventArgs</c> или производный тип).
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, удаляющий привязанный обработчик
        /// при вызове <see cref="IDisposable.Dispose"/>.
        /// </returns>
        /// <remarks>
        /// Метод использует Reflection для поиска события по имени
        /// и динамически создаёт делегат обработчика соответствующего типа.
        ///
        /// Удобен для сценариев, где имя события известно только во время выполнения
        /// (например, динамическое связывание UI или инфраструктурный код).
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Генерируется, если <paramref name="obj"/>,
        /// <paramref name="eventName"/> или <paramref name="actionSenderAndArgs"/> равны <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Может быть сгенерировано, если событие с указанным именем не найдено.
        /// </exception>
        public static IDisposable BindEventToAction<T, TArgs>(
            T obj,
            string eventName,
            Action<T, TArgs> actionSenderAndArgs)
            where T : class
        {
            var eventInfo = obj.GetType().GetEvent(eventName);
            return BindEventToAction(obj, eventInfo, actionSenderAndArgs);
        }

        /// <summary>
        /// Привязывает обработчик к событию объекта,
        /// используя <see cref="EventInfo"/>.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, содержащего событие.
        /// </typeparam>
        /// <param name="obj">
        /// Экземпляр объекта, к событию которого выполняется привязка.
        /// </param>
        /// <param name="eventInfo">
        /// Метаданные события, к которому необходимо привязать обработчик.
        /// </param>
        /// <param name="actionSenderAndArgs">
        /// Делегат, который будет вызван при возникновении события.
        ///
        /// Первый параметр — объект-источник события (<c>sender</c>),
        /// второй параметр — аргументы события.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, удаляющий привязку обработчика
        /// при вызове <see cref="IDisposable.Dispose"/>.
        /// </returns>
        /// <remarks>
        /// Метод:
        /// <list type="bullet">
        /// <item><description>создаёт делегат обработчика, совместимый с типом события;</description></item>
        /// <item><description>подписывается на событие через <see cref="EventInfo.AddEventHandler"/>;</description></item>
        /// <item><description>возвращает объект-обёртку для безопасного отписывания.</description></item>
        /// </list>
        ///
        /// Это позволяет использовать единый <see cref="Action{Object, Object}"/>
        /// для обработки событий с разными сигнатурами.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Генерируется, если <paramref name="obj"/>,
        /// <paramref name="eventInfo"/> или <paramref name="actionSenderAndArgs"/> равны <c>null</c>.
        /// </exception>
        public static IDisposable BindEventToAction<T, TArgs>(
            T obj,
            EventInfo eventInfo,
            Action<T, TArgs> actionSenderAndArgs)
            where T : class
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            if (eventInfo == null)
                throw new ArgumentNullException(nameof(eventInfo));
            if (actionSenderAndArgs == null)
                throw new ArgumentNullException(nameof(actionSenderAndArgs));

            var eventHandlerType = eventInfo.EventHandlerType;
            var handler = CreateEventHandlerDelegate(eventHandlerType, actionSenderAndArgs);

            eventInfo.AddEventHandler(obj, handler);
            return new EventBinding(obj, eventInfo, handler);
        }

        private static Delegate CreateEventHandlerDelegate<T, TArgs>(Type eventHandlerType, Action<T, TArgs> action)
        {
            var invokeMethod = eventHandlerType.GetMethod("Invoke");
            var parameters = invokeMethod.GetParameters();

            if (parameters.Length < 2)
                throw new InvalidOperationException("Event must have at least 2 parameters (sender and args).");

            var senderParam = Expression.Parameter(parameters[0].ParameterType, "sender");
            var argsParam = Expression.Parameter(parameters[1].ParameterType, "args");

            var actionCall = Expression.Call(
                Expression.Constant(action),
                action.GetType().GetMethod("Invoke"),
                Expression.Convert(senderParam, typeof(T)),
                Expression.Convert(argsParam, typeof(TArgs)));

            var lambda = Expression.Lambda(eventHandlerType, actionCall, senderParam, argsParam);
            return lambda.Compile();
        }

        private sealed class EventBinding : IDisposable
        {
            private readonly object target;
            private readonly EventInfo eventInfo;
            private readonly Delegate handler;
            private bool disposed;

            public EventBinding(object target, EventInfo eventInfo, Delegate handler)
            {
                this.target = target;
                this.eventInfo = eventInfo;
                this.handler = handler;
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                eventInfo.RemoveEventHandler(target, handler);
                disposed = true;
            }
        }
    }
}
