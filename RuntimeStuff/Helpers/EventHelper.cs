// <copyright file="EventHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
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
        /// Привязывает обработчик к событию объекта,
        /// используя <see cref="EventInfo"/>.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, содержащего событие.
        /// </typeparam>
        /// <typeparam name="TArgs">
        /// Тип аргумента события.
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

        /// <summary>
        /// Создаёт делегат обработчика события указанного типа на основе переданного действия.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта-источника события (<c>sender</c>).
        /// </typeparam>
        /// <typeparam name="TArgs">
        /// Тип аргументов события (<c>EventArgs</c> или производный тип).
        /// </typeparam>
        /// <param name="eventHandlerType">
        /// Тип делегата обработчика события (например, <see cref="EventHandler"/> или пользовательский делегат).
        /// </param>
        /// <param name="action">
        /// Действие, которое будет вызвано при срабатывании события.
        /// </param>
        /// <returns>
        /// Скомпилированный делегат, совместимый с указанным типом обработчика события.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Выбрасывается, если сигнатура делегата обработчика события содержит менее двух параметров
        /// (ожидаются как минимум <c>sender</c> и аргументы события).
        /// </exception>
        /// <remarks>
        /// Метод динамически создаёт выражение вызова для переданного <paramref name="action"/>,
        /// приводит параметры события к типам <typeparamref name="T"/> и <typeparamref name="TArgs"/>,
        /// а затем компилирует его в делегат заданного типа.
        /// </remarks>
        public static Delegate CreateEventHandlerDelegate<T, TArgs>(Type eventHandlerType, Action<T, TArgs> action)
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

        /// <summary>
        /// Связывает свойства объекта-источника и объекта-приёмника,
        /// подписываясь на указанные события обоих объектов и обеспечивая
        /// двустороннюю синхронизацию значений.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника.
        /// </typeparam>
        /// <typeparam name="TSourceEventArgs">
        /// Тип аргументов события объекта-источника.
        /// </typeparam>
        /// <typeparam name="TDest">
        /// Тип объекта-приёмника.
        /// </typeparam>
        /// <typeparam name="TDestEventArgs">
        /// Тип аргументов события объекта-приёмника.
        /// </typeparam>
        /// <param name="source">
        /// Объект-источник, свойство которого участвует в связывании.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Выражение, указывающее связываемое свойство объекта-источника.
        /// </param>
        /// <param name="sourceEvent">
        /// Событие объекта-источника, при срабатывании которого выполняется обновление
        /// связанного свойства.
        /// </param>
        /// <param name="dest">
        /// Объект-приёмник, свойство которого участвует в связывании.
        /// </param>
        /// <param name="destPropertySelector">
        /// Выражение, указывающее связываемое свойство объекта-приёмника.
        /// </param>
        /// <param name="destEvent">
        /// Событие объекта-приёмника, при срабатывании которого выполняется обновление
        /// связанного свойства.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, управляющий жизненным циклом связывания
        /// и позволяющий отписаться от событий.
        /// </returns>
        /// <remarks>
        /// Метод создаёт экземпляр <c>PropertiesBinding</c>, который инкапсулирует логику
        /// синхронизации свойств, и регистрирует обработчики событий с использованием
        /// <c>EventHelper.BindEventToAction</c>.
        /// Ожидается, что переданные события соответствуют стандартному .NET-паттерну
        /// и используют аргументы, производные от <see cref="EventArgs"/>.
        /// </remarks>
        public static IDisposable BindProperties<TSource, TSourceEventArgs, TDest, TDestEventArgs>(TSource source, Expression<Func<TSource, object>> sourcePropertySelector, EventInfo sourceEvent, TDest dest, Expression<Func<TDest, object>> destPropertySelector, EventInfo destEvent)
            where TSource : class
            where TSourceEventArgs : EventArgs
            where TDest : class
            where TDestEventArgs : EventArgs
        {
            var binding = new PropertiesBinding(source, ExpressionHelper.GetPropertyInfo(sourcePropertySelector), dest, ExpressionHelper.GetPropertyInfo(destPropertySelector));
            EventHelper.BindEventToAction<TSource, TSourceEventArgs>(source, sourceEvent, binding.SrcPropChanged);
            EventHelper.BindEventToAction<TDest, TDestEventArgs>(dest, destEvent, binding.DstPropChanged);
            return binding;
        }

        /// <summary>
        /// Подписывает указанный объект-подписчик на событие источника и выполняет действие
        /// при срабатывании события.
        /// </summary>
        /// <typeparam name="TSubscriber">
        /// Тип объекта-подписчика.
        /// </typeparam>
        /// <typeparam name="TSource">
        /// Тип объекта-источника события.
        /// </typeparam>
        /// <param name="subscriber">
        /// Объект-подписчик, для которого выполняется действие при срабатывании события.
        /// </param>
        /// <param name="eventSource">
        /// Источник события.
        /// </param>
        /// <param name="sourceEvent">
        /// Событие источника, на которое необходимо подписаться.
        /// </param>
        /// <param name="action">
        /// Действие, которое будет вызвано при срабатывании события. Получает объект-подписчик
        /// и объект-источник события.
        /// </param>
        /// <remarks>
        /// Метод использует BindEventToAction{TSource,TEventArgs} для привязки
        /// события к действию и обеспечивает удобный способ связывать события с конкретными
        /// подписчиками без ручной реализации обработчиков.
        /// </remarks>
        public static void Subscribe<TSubscriber, TSource>(TSubscriber subscriber, TSource eventSource, EventInfo sourceEvent, Action<TSubscriber, TSource> action)
            where TSubscriber : class
            where TSource : class
        {
            BindEventToAction<TSource, object>(eventSource, sourceEvent, (s, e) => action(subscriber, s));
        }

        private sealed class EventBinding : IDisposable
        {
            private readonly EventInfo eventInfo;
            private readonly Delegate handler;
            private readonly object target;
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

        private sealed class PropertiesBinding : IDisposable
        {
            private readonly object dest;
            private readonly PropertyInfo destPropertyInfo;
            private readonly object source;
            private readonly PropertyInfo sourcePropertyInfo;

            public PropertiesBinding(object src, PropertyInfo srcPropInfo, object dest, PropertyInfo destPropInfo)
            {
                sourcePropertyInfo = srcPropInfo;
                destPropertyInfo = destPropInfo;
                source = src;
                this.dest = dest;
            }

            /// <summary>
            /// Освобождает ресурсы, связанные с привязкой свойств,
            /// и отписывает обработчики событий изменения свойств
            /// у источника и приёмника.
            /// </summary>
            /// <remarks>
            /// Метод снимает подписку с события <see cref="INotifyPropertyChanged.PropertyChanged"/>
            /// у объектов <c>source</c> и <c>dest</c>, если они реализуют
            /// <see cref="INotifyPropertyChanged"/>. После вызова метода
            /// объект <c>PropertiesBinding</c> больше не синхронизирует свойства.
            /// </remarks>
            public void Dispose()
            {
                if (source is INotifyPropertyChanged srcNotify)
                    srcNotify.PropertyChanged -= SrcPropChanged;

                if (dest is INotifyPropertyChanged destNotify)
                    destNotify.PropertyChanged -= DstPropChanged;
            }

            internal void DstPropChanged(object sender, object args)
            {
                if (args is PropertyChangedEventArgs pc && pc.PropertyName != sourcePropertyInfo.Name)
                    return;

                var senderValue = destPropertyInfo.GetValue(sender);
                var destValue = sourcePropertyInfo.GetValue(source);
                if (EqualityComparer<object>.Default.Equals(senderValue, destValue))
                    return;

                sourcePropertyInfo.SetValue(source, senderValue);
            }

            internal void SrcPropChanged(object sender, object args)
            {
                if (args is PropertyChangedEventArgs pc && pc.PropertyName != sourcePropertyInfo.Name)
                    return;

                var srcValue = sourcePropertyInfo.GetValue(sender);
                var destValue = destPropertyInfo.GetValue(dest);
                if (EqualityComparer<object>.Default.Equals(srcValue, destValue))
                    return;

                destPropertyInfo.SetValue(dest, srcValue);
            }
        }
    }
}