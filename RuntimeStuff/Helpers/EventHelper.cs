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
    using RuntimeStuff.Internal;

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
            Action<T, TArgs> action)
        {
            if (eventInfo == null)
                throw new ArgumentNullException(nameof(eventInfo));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var eventHandlerType = eventInfo.EventHandlerType;
            var handler = CreateEventHandlerDelegate(eventHandlerType, action);

            eventInfo.AddEventHandler(obj, handler);
            return new EventBinding(obj, eventInfo, handler);
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
        /// <typeparam name="TDest">
        /// Тип объекта-приёмника.
        /// </typeparam>
        /// <typeparam name="TDestProp">
        /// Тип свойства-назначения.
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
        /// <param name="bindingDirection">Параметр направления связи.</param>
        /// <param name="sourceToDestConverter">Конвертор значения свойства источника в тип свойства назначения.</param>
        /// <param name="destToSourceConverter">Конвертор значения свойства назначения в тип свойства источника.</param>
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
        public static IDisposable BindProperties<TSource, TSourceProp, TDest, TDestProp>(
            TSource source,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            EventInfo sourceEvent,
            TDest dest,
            Expression<Func<TDest, TDestProp>> destPropertySelector,
            EventInfo destEvent,
            BindingDirection bindingDirection = BindingDirection.TwoWay,
            Func<TSourceProp, TDestProp> sourceToDestConverter = null,
            Func<TDestProp, TSourceProp> destToSourceConverter = null)
            where TSource : class
            where TDest : class
        {
            var srcProp = ExpressionHelper.GetPropertyInfo(sourcePropertySelector);
            var dstProp = ExpressionHelper.GetPropertyInfo(destPropertySelector);

            var disposables = new List<IDisposable>();
            if (sourceEvent != null)
            {
                disposables.Add(
                    new WeakEventSubscription<PropertiesBinding<TSourceProp, TDestProp>>(
                        source,
                        sourceEvent,
                        new PropertiesBinding<TSourceProp, TDestProp>(source, srcProp, dest, dstProp, sourceToDestConverter, destToSourceConverter),
                        (binding, sender, args) => binding.SrcPropChanged(sender, args)));
            }

            if (bindingDirection == BindingDirection.TwoWay && destEvent != null)
            {
                disposables.Add(
                    new WeakEventSubscription<PropertiesBinding<TSourceProp, TDestProp>>(
                        dest,
                        destEvent,
                        new PropertiesBinding<TSourceProp, TDestProp>(source, srcProp, dest, dstProp, sourceToDestConverter, destToSourceConverter),
                        (binding, sender, args) => binding.DstPropChanged(sender, args)));
            }

            return new CompositeDisposable(disposables);
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
            if (invokeMethod == null) return null;
            var parameters = invokeMethod.GetParameters();

            if (parameters.Length < 2)
                throw new InvalidOperationException("Event must have at least 2 parameters (sender and args).");

            var senderParam = Expression.Parameter(parameters[0].ParameterType, "sender");
            var argsParam = Expression.Parameter(parameters[1].ParameterType, "args");

            var actionCall = Expression.Call(
                Expression.Constant(action),
                action.GetType().GetMethod("Invoke") ?? throw new InvalidOperationException(),
                Expression.Convert(senderParam, typeof(T)),
                Expression.Convert(argsParam, typeof(TArgs)));

            var lambda = Expression.Lambda(eventHandlerType, actionCall, senderParam, argsParam);
            return lambda.Compile();
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

        private sealed class CompositeDisposable : IDisposable
        {
            private readonly IEnumerable<IDisposable> disposables;

            public CompositeDisposable(IEnumerable<IDisposable> disposables)
            {
                this.disposables = disposables;
            }

            public void Dispose()
            {
                foreach (var d in disposables)
                    d.Dispose();
            }
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

        private sealed class PropertiesBinding<TSrc, TDest> : IDisposable
        {
            private readonly object dest;
            private readonly PropertyInfo destPropertyInfo;
            private readonly object source;
            private readonly PropertyInfo sourcePropertyInfo;
            private readonly Func<TSrc, TDest> sourceToDestConverter;
            private readonly Func<TDest, TSrc> destToSourceConverter;

            public PropertiesBinding(object src, PropertyInfo srcPropInfo, object dest, PropertyInfo destPropInfo, Func<TSrc, TDest> sourceToDestConverter = null, Func<TDest, TSrc> destToSourceConverter = null)
            {
                sourcePropertyInfo = srcPropInfo;
                destPropertyInfo = destPropInfo;
                this.sourceToDestConverter = sourceToDestConverter;
                this.destToSourceConverter = destToSourceConverter;
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
                var convertedValue = destToSourceConverter != null
                    ? destToSourceConverter((TDest)senderValue)
                    : senderValue;
                if (EqualityComparer<object>.Default.Equals(destValue, convertedValue))
                    return;

                sourcePropertyInfo.SetValue(source, convertedValue);
            }

            internal void SrcPropChanged(object sender, object args)
            {
                if (args is PropertyChangedEventArgs pc && pc.PropertyName != sourcePropertyInfo.Name)
                    return;

                var senderValue = sourcePropertyInfo.GetValue(sender);
                var destValue = destPropertyInfo.GetValue(dest);
                var convertedValue = sourceToDestConverter != null
                    ? sourceToDestConverter((TSrc)senderValue)
                    : senderValue;
                if (EqualityComparer<object>.Default.Equals(destValue, convertedValue))
                    return;

                destPropertyInfo.SetValue(dest, convertedValue);
            }
        }
    }
}