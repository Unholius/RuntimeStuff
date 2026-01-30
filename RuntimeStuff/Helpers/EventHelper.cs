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
    /// <item><description>Привязки обработчиков к событиям во время выполнения;</description></item>
    /// <item><description>Адаптации событий с различными сигнатурами к унифицированным делегатам;</description></item>
    /// <item><description>Безопасного управления подписками через <see cref="IDisposable"/>.</description></item>
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
        /// <param name="action">
        /// Делегат, который будет вызван при возникновении события.
        ///
        /// Первый параметр — объект-источник события (<c>sender</c>),
        /// второй параметр — аргументы события.
        /// </param>
        /// <param name="canExecuteAction">Условие для выполнения делегата.</param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, удаляющий привязку обработчика
        /// при вызове <see cref="IDisposable.Dispose"/>.
        /// </returns>
        /// <remarks>
        /// Метод:
        /// <list type="bullet">
        /// <item><description>Создаёт делегат обработчика, совместимый с типом события;</description></item>
        /// <item><description>Подписывается на событие через <see cref="EventInfo.AddEventHandler"/>;</description></item>
        /// <item><description>Возвращает объект-обёртку для безопасного отписывания.</description></item>
        /// </list>
        ///
        /// Это позволяет использовать единый <see cref="Action{Object, Object}"/>
        /// для обработки событий с разными сигнатурами.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Генерируется, если <paramref name="obj"/>,
        /// <paramref name="eventInfo"/> или <paramref name="action"/> равны <c>null</c>.
        /// </exception>
        public static IDisposable BindEventToAction<T, TArgs>(
            T obj,
            EventInfo eventInfo,
            Action<T, TArgs> action,
            Func<T, TArgs, bool> canExecuteAction = null)
        {
            if (eventInfo == null)
                throw new ArgumentNullException(nameof(eventInfo));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var binding = new EventBinding<T, TArgs>(obj, eventInfo, action, canExecuteAction);
            var handler = CreateEventHandlerDelegate<T, TArgs>(eventInfo.EventHandlerType, binding.OnEvent);
            binding.ActionHandler = handler;

            eventInfo.AddEventHandler(obj, handler);
            return binding;
        }

        /// <summary>
        /// Связывает свойства объекта-источника и объекта-приёмника,
        /// подписываясь на указанные события обоих объектов и обеспечивая
        /// двустороннюю синхронизацию значений.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника.
        /// </typeparam>
        /// <typeparam name="TSourceProp">
        /// Тип свойства-источника.
        /// </typeparam>
        /// <typeparam name="TTarget">
        /// Тип объекта-приёмника.
        /// </typeparam>
        /// <typeparam name="TTargetProp">
        /// Тип свойства-назначения.
        /// </typeparam>
        /// <param name="source">
        /// Объект-источник, свойство которого участвует в связывании.
        /// </param>
        /// <param name="sourceProperty">
        /// Выражение, указывающее связываемое свойство объекта-источника.
        /// </param>
        /// <param name="sourceEvent">
        /// Событие объекта-источника, при срабатывании которого выполняется обновление
        /// связанного свойства.
        /// </param>
        /// <param name="target">
        /// Объект-приёмник, свойство которого участвует в связывании.
        /// </param>
        /// <param name="targetProperty">
        /// Выражение, указывающее связываемое свойство объекта-приёмника.
        /// </param>
        /// <param name="targetEvent">
        /// Событие объекта-приёмника, при срабатывании которого выполняется обновление
        /// связанного свойства.
        /// </param>
        /// <param name="bindingDirection">Параметр направления связи.</param>
        /// <param name="sourceValueToTargetValueConverter">Конвертор значения свойства источника в тип свойства назначения.</param>
        /// <param name="targetValueToSourceValueConverter">Конвертор значения свойства назначения в тип свойства источника.</param>
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
        public static IDisposable BindProperties<TSource, TSourceProp, TSourceEventArgs, TTarget, TTargetProp, TTargetEventArgs>(
            TSource source,
            PropertyInfo sourceProperty,
            EventInfo sourceEvent,
            Func<TSource, TSourceEventArgs, bool> canAcceptSourceEvent,
            TTarget target,
            PropertyInfo targetProperty,
            EventInfo targetEvent,
            Func<TTarget, TTargetEventArgs, bool> canAcceptTargetEvent,
            Func<TSourceProp, TTargetProp> sourceValueToTargetValueConverter,
            Func<TTargetProp, TSourceProp> targetValueToSourceValueConverter,
            Action<object, PropertyChangedEventArgs> onPropertyChanged)
            where TSource : class
            where TTarget : class
            where TSourceEventArgs : EventArgs
            where TTargetEventArgs : EventArgs
        {
            var pb = new PropertiesBinding<TSource, TSourceProp, TSourceEventArgs, TTarget, TTargetProp, TTargetEventArgs>(source, sourceProperty, sourceEvent, canAcceptSourceEvent, target, targetProperty, targetEvent, canAcceptTargetEvent, sourceValueToTargetValueConverter, targetValueToSourceValueConverter, onPropertyChanged);
            if (sourceEvent != null)
            {
                var eventHandlerType = sourceEvent.EventHandlerType;
                var eventHandler = CreateEventHandlerDelegate<TSource, object>(eventHandlerType, pb.OnSourceEvent);
                sourceEvent.AddEventHandler(source, eventHandler);
                pb.SrcEventHandler = eventHandler;
            }

            if (targetEvent != null)
            {
                var eventHandlerType = targetEvent.EventHandlerType;
                var eventHandler = CreateEventHandlerDelegate<TTarget, object>(eventHandlerType, pb.OnTargetEvent);
                targetEvent.AddEventHandler(target, eventHandler);
                pb.DstEventHandler = eventHandler;
            }

            return pb;
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
        public static Delegate CreateEventHandlerDelegate<T, TArgs>(
            Type eventHandlerType,
            Action<T, TArgs> action)
        {
            if (eventHandlerType == null)
                throw new ArgumentNullException(nameof(eventHandlerType));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var invokeMethod = eventHandlerType.GetMethod("Invoke")
                               ?? throw new InvalidOperationException("Event handler has no Invoke method.");

            var parameters = invokeMethod.GetParameters();
            if (parameters.Length < 2)
                throw new InvalidOperationException("Event must have at least 2 parameters (sender and args).");

            var senderParam = Expression.Parameter(parameters[0].ParameterType, "sender");
            var argsParam = Expression.Parameter(parameters[1].ParameterType, "args");

            var actionInvoke = action.GetType().GetMethod("Invoke")
                               ?? throw new InvalidOperationException();

            var body = Expression.Call(
                Expression.Constant(action),
                actionInvoke,
                Expression.Convert(senderParam, typeof(T)),
                Expression.Convert(argsParam, typeof(TArgs)));

            return Expression
                .Lambda(eventHandlerType, body, senderParam, argsParam)
                .Compile();
        }

        /// <summary>
        /// Отписывает ранее привязанный обработчик от указанного события объекта.
        /// </summary>
        /// <param name="obj">
        /// Объект-источник события, от которого необходимо отписать обработчик.
        /// </param>
        /// <param name="eventInfo">
        /// Метаданные события, от которого выполняется отписка.
        /// </param>
        /// <param name="actionHandler">
        /// Делегат обработчика, который был ранее подписан на событие.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="eventInfo"/> или <paramref name="actionHandler"/> равны <c>null</c>.
        /// </exception>
        /// <remarks>
        /// Метод выполняет прямой вызов <see cref="EventInfo.RemoveEventHandler(object, Delegate)"/>
        /// и предполагает, что переданный делегат полностью соответствует ранее
        /// зарегистрированному обработчику события.
        /// </remarks>
        public static void UnBindActionFromEvent(
            object obj,
            EventInfo eventInfo,
            Delegate actionHandler)
        {
            if (eventInfo == null)
                throw new ArgumentNullException(nameof(eventInfo));
            if (actionHandler == null)
                throw new ArgumentNullException(nameof(actionHandler));

            eventInfo.RemoveEventHandler(obj, actionHandler);
        }

        private sealed class EventBinding<TSource, TEventArgs> : IDisposable
        {
            private readonly EventInfo eventInfo;
            private readonly object target;
            private readonly Action<TSource, TEventArgs> action;
            private readonly Func<TSource, TEventArgs, bool> canExecute;
            private bool disposed;

            public EventBinding(TSource target, EventInfo eventInfo, Action<TSource, TEventArgs> action, Func<TSource, TEventArgs, bool> canExecute)
            {
                this.target = target;
                this.eventInfo = eventInfo;
                this.action = action;
                this.canExecute = canExecute;
            }

            ~EventBinding()
            {
                Dispose();
            }

            public Delegate ActionHandler { get; internal set; }

            public void Dispose()
            {
                if (disposed)
                    return;

                eventInfo.RemoveEventHandler(target, ActionHandler);
                disposed = true;
            }

            public void OnEvent(TSource source, TEventArgs args)
            {
                if (canExecute != null && !this.canExecute(source, args))
                    return;
                this.action(source, args);
            }
        }

        private sealed class PropertiesBinding<TSrc, TSrcValue, TSrcArgs, TTarget, TTargetValue, TTargetArgs> : IDisposable
            where TSrc : class
            where TTarget : class
            where TSrcArgs : EventArgs
            where TTargetArgs : EventArgs
        {
            private WeakReference target;
            private EventInfo targetEvent;
            private PropertyInfo targetPropertyInfo;
            private Func<TSrcValue, TTargetValue> sourceToTargetConverter;
            private Func<TTargetValue, TSrcValue> targetToSourceConverter;
            private Func<TTarget, TTargetArgs, bool> canAcceptTargetEvent;
            private Func<TSrc, TSrcArgs, bool> canAcceptSourceEvent;
            private bool disposed;
            private WeakReference source;
            private EventInfo sourceEvent;
            private PropertyInfo sourcePropertyInfo;
            private Action<object, PropertyChangedEventArgs> onPropertyChanged;

            public PropertiesBinding(
                object src,
                PropertyInfo srcPropInfo,
                EventInfo sourceEvent,
                Func<TSrc, TSrcArgs, bool> canAcceptSourceEvent,
                object target,
                PropertyInfo targetPropInfo,
                EventInfo targetEvent,
                Func<TTarget, TTargetArgs, bool> canAcceptTargetEvent,
                Func<TSrcValue, TTargetValue> sourceToTargetConverter,
                Func<TTargetValue, TSrcValue> targetToSourceConverter,
                Action<object, PropertyChangedEventArgs> onPropertyChanged)
            {
                this.sourcePropertyInfo = srcPropInfo;
                this.targetPropertyInfo = targetPropInfo;
                this.sourceToTargetConverter = sourceToTargetConverter;
                this.targetToSourceConverter = targetToSourceConverter;
                this.sourceEvent = sourceEvent;
                this.targetEvent = targetEvent;
                this.source = new WeakReference(src);
                this.target = new WeakReference(target);
                this.canAcceptSourceEvent = canAcceptSourceEvent;
                this.canAcceptTargetEvent = canAcceptTargetEvent;
                this.onPropertyChanged = onPropertyChanged;
            }

            internal Delegate DstEventHandler { get; set; }

            internal Delegate SrcEventHandler { get; set; }

            /// <summary>
            /// Освобождает ресурсы, связанные с привязкой свойств,
            /// и отписывает обработчики событий изменения свойств
            /// у источника и приёмника.
            ///
            /// </summary>
            /// <remarks>
            /// Метод снимает подписку с события <see cref="INotifyPropertyChanged.PropertyChanged"/>
            /// у объектов <c>source</c> и <c>target</c>, если они реализуют
            /// <see cref="INotifyPropertyChanged"/>. После вызова метода
            /// объект <c>PropertiesBinding</c> больше не синхронизирует свойства.
            /// </remarks>
            public void Dispose()
            {
                if (disposed)
                    return;

                var src = source?.Target;
                var dst = target?.Target;

                if (src != null && sourceEvent != null && SrcEventHandler != null)
                    EventHelper.UnBindActionFromEvent(source.Target, sourceEvent, SrcEventHandler);

                if (dst != null && targetEvent != null && DstEventHandler != null)
                    EventHelper.UnBindActionFromEvent(target.Target, targetEvent, DstEventHandler);
                this.sourcePropertyInfo = null;
                this.targetPropertyInfo = null;
                this.sourceToTargetConverter = null;
                this.targetToSourceConverter = null;
                this.sourceEvent = null;
                this.targetEvent = null;
                this.source = null;
                this.target = null;
                disposed = true;
            }

            internal void OnTargetEvent(object sender, object args)
            {
                if (canAcceptTargetEvent == null && args is PropertyChangedEventArgs pc && pc.PropertyName != sourcePropertyInfo.Name)
                    return;

                if (canAcceptTargetEvent != null && sender is TTarget s && args is TTargetArgs a && !canAcceptTargetEvent(s, a))
                    return;

                if (source.Target == null || target.Target == null)
                {
                    Dispose();
                    return;
                }

                var senderValue = targetPropertyInfo.GetValue(sender);
                var targetValue = sourcePropertyInfo.GetValue(source.Target);
                var convertedValue = targetToSourceConverter != null
                    ? targetToSourceConverter((TTargetValue)senderValue)
                    : senderValue;
                if (EqualityComparer<TSrcValue>.Default.Equals((TSrcValue)targetValue, (TSrcValue)convertedValue))
                    return;

                sourcePropertyInfo.SetValue(source.Target, convertedValue);
                onPropertyChanged?.Invoke(source.Target, new PropertyChangedEventArgs(sourcePropertyInfo.Name));
            }

            internal void OnSourceEvent(object sender, object args)
            {
                if (canAcceptSourceEvent == null && args is PropertyChangedEventArgs pc && pc.PropertyName != sourcePropertyInfo.Name)
                    return;

                if (canAcceptSourceEvent != null && sender is TSrc src && args is TSrcArgs srcArgs && !canAcceptSourceEvent(src, srcArgs))
                    return;

                if (source.Target == null)
                {
                    Dispose();
                    return;
                }

                if (target.Target != null)
                {
                    var senderValue = sourcePropertyInfo.GetValue(sender);
                    var targetValue = targetPropertyInfo.GetValue(target.Target);
                    var convertedValue = sourceToTargetConverter != null
                        ? sourceToTargetConverter((TSrcValue)senderValue)
                        : senderValue;
                    if (EqualityComparer<TTargetValue>.Default.Equals((TTargetValue)targetValue, (TTargetValue)convertedValue))
                        return;

                    targetPropertyInfo.SetValue(target.Target, convertedValue);
                }

                onPropertyChanged?.Invoke(target.Target, new PropertyChangedEventArgs(targetPropertyInfo.Name));
            }
        }
    }
}