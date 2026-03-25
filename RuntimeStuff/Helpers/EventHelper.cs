// <copyright file="EventHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
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
            {
                throw new ArgumentNullException(nameof(eventInfo));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var binding = new EventBinding<T, TArgs>(obj, eventInfo, action, canExecuteAction);
            var handler = CreateEventHandlerDelegate<T, TArgs>(eventInfo.EventHandlerType, binding.OnEvent);
            binding.ActionHandler = handler;

            eventInfo.AddEventHandler(obj, handler);
            return binding;
        }

        /// <summary>
        /// Создаёт привязку между свойствами объекта-источника и объекта-приёмника
        /// на основе указанных событий и правил синхронизации.
        /// </summary>
        /// <typeparam name="TSource">Тип объекта-источника.</typeparam>
        /// <typeparam name="TSourceProp">Тип свойства источника.</typeparam>
        /// <typeparam name="TSourceEventArgs">Тип аргументов события источника.</typeparam>
        /// <typeparam name="TTarget">Тип объекта-приёмника.</typeparam>
        /// <typeparam name="TTargetProp">Тип свойства приёмника.</typeparam>
        /// <typeparam name="TTargetEventArgs">Тип аргументов события приёмника.</typeparam>
        /// <param name="source">
        /// Объект-источник, изменения которого отслеживаются.
        /// </param>
        /// <param name="sourceProperty">
        /// Метаданные свойства источника, участвующего в привязке.
        /// </param>
        /// <param name="sourceEvent">
        /// Событие источника, инициирующее обновление свойства приёмника.
        /// Может быть <c>null</c> для отключения обработки событий источника.
        /// </param>
        /// <param name="canAcceptSourceEvent">
        /// Фильтр, определяющий, следует ли обрабатывать конкретное событие источника.
        /// </param>
        /// <param name="target">
        /// Объект-приёмник, свойство которого синхронизируется с источником.
        /// </param>
        /// <param name="targetProperty">
        /// Метаданные свойства приёмника, участвующего в привязке.
        /// </param>
        /// <param name="targetEvent">
        /// Событие приёмника, инициирующее обновление свойства источника.
        /// Может быть <c>null</c> для односторонней привязки.
        /// </param>
        /// <param name="canAcceptTargetEvent">
        /// Фильтр, определяющий, следует ли обрабатывать конкретное событие приёмника.
        /// </param>
        /// <param name="sourceValueToTargetValueConverter">
        /// Конвертер значения свойства источника в значение свойства приёмника.
        /// </param>
        /// <param name="targetValueToSourceValueConverter">
        /// Конвертер значения свойства приёмника в значение свойства источника.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Дополнительный колбэк, вызываемый после изменения свойства.
        /// </param>
        /// <returns>
        /// Экземпляр <see cref="IDisposable"/>, представляющий созданную привязку
        /// и позволяющий корректно освободить связанные ресурсы.
        /// </returns>
        /// <remarks>
        /// Метод является базовой точкой создания привязок и используется
        /// всеми высокоуровневыми перегрузками <c>Bind*</c>.
        /// При наличии обоих событий формируется двусторонняя привязка,
        /// при отсутствии события приёмника — односторонняя.
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
            var pb = new PropertiesBinding<TSource, TSourceProp, TSourceEventArgs, TTarget, TTargetProp, TTargetEventArgs>(source, new[] { (MemberCache)sourceProperty }, sourceEvent, canAcceptSourceEvent, target, new[] { (MemberCache)targetProperty }, targetEvent, canAcceptTargetEvent, sourceValueToTargetValueConverter, targetValueToSourceValueConverter, onPropertyChanged);
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

            var sourceValue = ((MemberCache)sourceProperty).GetValue(source);
            if (sourceValueToTargetValueConverter != null)
            {
                sourceValue = sourceValueToTargetValueConverter((TSourceProp)sourceValue);
            }

            ((MemberCache)targetProperty).SetValue(target, sourceValue);
            pb.OnTargetEvent(target, new PropertyChangedEventArgs(targetProperty.Name));
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
            {
                throw new ArgumentNullException(nameof(eventHandlerType));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var invokeMethod = eventHandlerType.GetMethod("Invoke")
                               ?? throw new InvalidOperationException("Event handler has no Invoke method.");

            var parameters = invokeMethod.GetParameters();
            if (parameters.Length < 2)
            {
                throw new InvalidOperationException("Event must have at least 2 parameters (sender and args).");
            }

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
        /// <param name="source">
        /// Объект-источник события, от которого необходимо отписать обработчик.
        /// </param>
        /// <param name="eventName">
        /// Имя события, от которого выполняется отписка.
        /// </param>
        /// <param name="actionHandler">
        /// Делегат обработчика, который был ранее подписан на событие.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="eventName"/> или <paramref name="actionHandler"/> равны <c>null</c>.
        /// </exception>
        /// <remarks>
        /// Метод выполняет прямой вызов <see cref="EventInfo.RemoveEventHandler(object, Delegate)"/>
        /// и предполагает, что переданный делегат полностью соответствует ранее
        /// зарегистрированному обработчику события.
        /// </remarks>
        public static void UnBindActionFromEvent(
            object source,
            string eventName,
            Delegate actionHandler)
        {
            var sourceTypeCache = MemberCache.Create(source.GetType());
            var sourceEvent = sourceTypeCache.GetEvent(x => x.Name == eventName);
            UnBindActionFromEvent(source, sourceEvent, actionHandler);
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
            {
                throw new ArgumentNullException(nameof(eventInfo));
            }

            if (actionHandler == null)
            {
                throw new ArgumentNullException(nameof(actionHandler));
            }

            eventInfo.RemoveEventHandler(obj, actionHandler);
        }

        /// <summary>
        /// Подписывает указанное действие на событие объекта.
        /// </summary>
        /// <remarks>
        /// Метод выполняет динамическую подписку на событие по его имени.
        /// При возникновении события будет вызван указанный делегат <paramref name="action"/>.
        /// Возвращаемый объект <see cref="IDisposable"/> позволяет отменить подписку
        /// и корректно отписаться от события.
        /// </remarks>
        /// <param name="source">Объект, содержащий событие.</param>
        /// <param name="eventName">Имя события, на которое необходимо подписаться.</param>
        /// <param name="action">
        /// Действие, которое будет вызвано при возникновении события.
        /// Первый параметр — отправитель события (<c>sender</c>),
        /// второй параметр — аргументы события.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отменить подписку
        /// и удалить обработчик события.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="source"/> или <paramref name="action"/> равны <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если <paramref name="eventName"/> равен <see langword="null"/> или пустой строке.
        /// </exception>
        public static IDisposable BindEventToAction(object source, string eventName, Action action)
        {
            return BindEventToAction(source, eventName, (s, e) => action());
        }

        /// <summary>
        /// Подписывает указанное действие на Click событие объекта.
        /// </summary>
        /// <remarks>
        /// Метод выполняет динамическую подписку на событие по его имени.
        /// При возникновении события будет вызван указанный делегат <paramref name="action"/>.
        /// Возвращаемый объект <see cref="IDisposable"/> позволяет отменить подписку
        /// и корректно отписаться от события.
        /// </remarks>
        /// <param name="source">Объект, содержащий событие.</param>
        /// <param name="action">
        /// Действие, которое будет вызвано при возникновении события.
        /// Первый параметр — отправитель события (<c>sender</c>),
        /// второй параметр — аргументы события.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отменить подписку
        /// и удалить обработчик события.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="source"/> или <paramref name="action"/> равны <see langword="null"/>.
        /// </exception>
        public static IDisposable BindClickToAction(object source, Action action)
        {
            return BindEventToAction(source, "Click", (s, e) => action());
        }

        /// <summary>
        /// Подписывает указанное действие на событие объекта.
        /// </summary>
        /// <remarks>
        /// Метод выполняет динамическую подписку на событие по его имени.
        /// При возникновении события будет вызван указанный делегат <paramref name="action"/>.
        /// Возвращаемый объект <see cref="IDisposable"/> позволяет отменить подписку
        /// и корректно отписаться от события.
        /// </remarks>
        /// <param name="source">Объект, содержащий событие.</param>
        /// <param name="eventName">Имя события, на которое необходимо подписаться.</param>
        /// <param name="action">
        /// Действие, которое будет вызвано при возникновении события.
        /// Первый параметр — отправитель события (<c>sender</c>),
        /// второй параметр — аргументы события.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отменить подписку
        /// и удалить обработчик события.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="source"/> или <paramref name="action"/> равны <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если <paramref name="eventName"/> равен <see langword="null"/> или пустой строке.
        /// </exception>
        public static IDisposable BindEventToAction(object source, string eventName, Action<object, EventArgs> action)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (string.IsNullOrEmpty(eventName))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(eventName));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var sourceTypeCache = MemberCache.Create(source.GetType());
            var sourceEvent = sourceTypeCache.GetEvent(x => x.Name == eventName);
            var binding = new EventBinding<object, EventArgs>(source, sourceEvent, action, (_, __) => true);
            var handler = CreateEventHandlerDelegate<object, EventArgs>(sourceEvent.EventHandlerType, binding.OnEvent);
            binding.ActionHandler = handler;
            sourceEvent.AddEventHandler(source, handler);

            return binding;
        }

        /// <summary>
        /// Создает привязку между свойствами двух объектов с возможностью синхронизации их значений
        /// через указанные события.
        /// </summary>
        /// <remarks>
        /// Метод позволяет организовать одностороннюю или двустороннюю синхронизацию свойств.
        /// Обновление происходит при возникновении указанных событий у исходного или целевого объекта.
        /// При необходимости можно указать функции преобразования значений.
        /// </remarks>
        /// <param name="source">Исходный объект, содержащий свойство-источник.</param>
        /// <param name="sourcePropertyName">Имя свойства исходного объекта.</param>
        /// <param name="onSourceEvent">
        /// Имя события исходного объекта, при возникновении которого значение
        /// будет копироваться из источника в целевой объект.
        /// </param>
        /// <param name="target">Целевой объект, содержащий свойство-приемник.</param>
        /// <param name="targetPropertyName">Имя свойства целевого объекта.</param>
        /// <param name="onTargetEvent">
        /// Имя события целевого объекта, при возникновении которого значение
        /// будет копироваться из целевого объекта обратно в источник.
        /// Если <see langword="null"/>, двусторонняя синхронизация не выполняется.
        /// </param>
        /// <param name="sourceToTargetValueConverter">
        /// Функция преобразования значения из свойства источника в значение
        /// свойства целевого объекта. Если <see langword="null"/>, используется
        /// прямое присваивание.
        /// </param>
        /// <param name="targetToSourceValueConverter">
        /// Функция преобразования значения из свойства целевого объекта
        /// в значение свойства источника. Используется при двусторонней привязке.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Делегат, вызываемый после изменения значения любого из связанных свойств.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отменить привязку
        /// и отписаться от всех подписанных событий.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="source"/> или <paramref name="target"/> равны <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если имена свойств или событий заданы пустой строкой или равны <see langword="null"/>.
        /// </exception>
        public static IDisposable BindProperties(object source, string sourcePropertyName, string onSourceEvent, object target, string targetPropertyName, string onTargetEvent = null, Func<object, object> sourceToTargetValueConverter = null, Func<object, object> targetToSourceValueConverter = null, Action onPropertyChanged = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (string.IsNullOrEmpty(sourcePropertyName))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(sourcePropertyName));
            }

            if (string.IsNullOrEmpty(targetPropertyName))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(targetPropertyName));
            }

            if (string.IsNullOrEmpty(onSourceEvent))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(onSourceEvent));
            }

            var sourceTypeCache = MemberCache.Create(source.GetType());
            var targetTypeCache = MemberCache.Create(target.GetType());
            var sourceEvent = sourceTypeCache.GetEvent(x => x.Name == onSourceEvent);
            var targetEvent = targetTypeCache.GetEvent(x => x.Name == onTargetEvent);
            var sourceProperty = sourceTypeCache.GetPath(sourcePropertyName, '.', false);
            var targetProperty = targetTypeCache.GetPath(targetPropertyName, '.', false);

            if (sourceProperty == null || sourceProperty.Length == 0)
            {
                throw new InvalidOperationException("Source property not found: " + sourcePropertyName);
            }

            var pb = new PropertiesBinding<object, object, EventArgs, object, object, EventArgs>(source, sourceProperty, sourceEvent, (_, __) => true, target, targetProperty, targetEvent, (_, __) => true, sourceToTargetValueConverter, targetToSourceValueConverter, (_, __) => onPropertyChanged?.Invoke());
            if (sourceEvent != null)
            {
                var eventHandlerType = sourceEvent.EventHandlerType;
                var eventHandler = CreateEventHandlerDelegate<object, object>(eventHandlerType, pb.OnSourceEvent);
                sourceEvent.AddEventHandler(source, eventHandler);
                pb.SrcEventHandler = eventHandler;
            }

            if (targetEvent != null)
            {
                var eventHandlerType = targetEvent.EventHandlerType;
                var eventHandler = CreateEventHandlerDelegate<object, object>(eventHandlerType, pb.OnTargetEvent);
                targetEvent.AddEventHandler(target, eventHandler);
                pb.DstEventHandler = eventHandler;
            }

            return pb;
        }

        private sealed class EventBinding<TSource, TEventArgs> : IDisposable
        {
            private readonly Action<TSource, TEventArgs> action;
            private readonly Func<TSource, TEventArgs, bool> canExecute;
            private readonly EventInfo eventInfo;
            private readonly object target;
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
                this.Dispose();
            }

            public Delegate ActionHandler { get; internal set; }

            public void Dispose()
            {
                if (this.disposed)
                {
                    return;
                }

                this.eventInfo.RemoveEventHandler(this.target, this.ActionHandler);
                this.disposed = true;

                // Предотвращает вызов финализатора для этого объекта
                GC.SuppressFinalize(this);
            }

            public void OnEvent(TSource source, TEventArgs args)
            {
                if (this.canExecute != null && !this.canExecute(source, args))
                {
                    return;
                }

                this.action(source, args);
            }
        }

        private sealed class PropertiesBinding<TSrc, TSrcValue, TSrcArgs, TTarget, TTargetValue, TTargetArgs> : IDisposable
            where TSrc : class
            where TTarget : class
            where TSrcArgs : EventArgs
            where TTargetArgs : EventArgs
        {
            private readonly Func<TSrc, TSrcArgs, bool> canAcceptSourceEvent;
            private readonly Func<TTarget, TTargetArgs, bool> canAcceptTargetEvent;
            private readonly Action<object, PropertyChangedEventArgs> onPropertyChanged;
            private bool disposed;
            private WeakReference source;
            private EventInfo sourceEvent;
            private MemberCache[] sourcePropertyInfo;
            private Func<TSrcValue, TTargetValue> sourceToTargetConverter;
            private WeakReference target;
            private EventInfo targetEvent;
            private MemberCache[] targetPropertyInfo;
            private Func<TTargetValue, TSrcValue> targetToSourceConverter;

            public PropertiesBinding(
                object src,
                MemberCache[] srcPropInfo,
                EventInfo sourceEvent,
                Func<TSrc, TSrcArgs, bool> canAcceptSourceEvent,
                object target,
                MemberCache[] targetPropInfo,
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
                if (this.disposed)
                {
                    return;
                }

                var src = this.source?.Target;
                var dst = this.target?.Target;

                if (src != null && this.sourceEvent != null && this.SrcEventHandler != null)
                {
                    EventHelper.UnBindActionFromEvent(this.source.Target, this.sourceEvent, this.SrcEventHandler);
                }

                if (dst != null && this.targetEvent != null && this.DstEventHandler != null)
                {
                    EventHelper.UnBindActionFromEvent(this.target.Target, this.targetEvent, this.DstEventHandler);
                }

                this.sourcePropertyInfo = null;
                this.targetPropertyInfo = null;
                this.sourceToTargetConverter = null;
                this.targetToSourceConverter = null;
                this.sourceEvent = null;
                this.targetEvent = null;
                this.source = null;
                this.target = null;
                this.disposed = true;
            }

            internal void OnSourceEvent(object sender, object args)
            {
                if (this.canAcceptSourceEvent == null && args is PropertyChangedEventArgs pc && pc.PropertyName != this.sourcePropertyInfo[this.sourcePropertyInfo.Length - 1].Name)
                {
                    return;
                }

                if (this.canAcceptSourceEvent != null && sender is TSrc src && args is TSrcArgs srcArgs && !this.canAcceptSourceEvent(src, srcArgs))
                {
                    return;
                }

                if (this.source.Target == null)
                {
                    this.Dispose();
                    return;
                }

                if (this.target.Target != null)
                {
                    var senderValue = MemberCache.GetValues(sender, this.sourcePropertyInfo);
                    var targetValue = MemberCache.GetValues(this.target.Target, this.targetPropertyInfo);
                    var convertedValue = this.sourceToTargetConverter != null
                        ? this.sourceToTargetConverter((TSrcValue)senderValue.LastOrDefault())
                        : senderValue.Last();
                    if (EqualityComparer<TTargetValue>.Default.Equals((TTargetValue)targetValue.LastOrDefault(), (TTargetValue)convertedValue))
                    {
                        return;
                    }

                    this.targetPropertyInfo[this.targetPropertyInfo.Length - 1].SetValue(this.targetPropertyInfo.Length == 1 ? this.target.Target : targetValue[targetValue.Length - 2], convertedValue);
                }

                this.onPropertyChanged?.Invoke(this.target.Target, new PropertyChangedEventArgs(this.targetPropertyInfo[this.targetPropertyInfo.Length - 1].Name));
            }

            internal void OnTargetEvent(object sender, object args)
            {
                if (this.canAcceptTargetEvent == null && args is PropertyChangedEventArgs pc && pc.PropertyName != this.sourcePropertyInfo[this.sourcePropertyInfo.Length - 1].Name)
                {
                    return;
                }

                if (this.canAcceptTargetEvent != null && sender is TTarget s && args is TTargetArgs a && !this.canAcceptTargetEvent(s, a))
                {
                    return;
                }

                if (this.source.Target == null || this.target.Target == null)
                {
                    this.Dispose();
                    return;
                }

                var sourceValue = MemberCache.GetValues(sender, this.targetPropertyInfo);
                var targetValue = MemberCache.GetValues(this.source.Target, this.sourcePropertyInfo);
                var convertedValue = this.targetToSourceConverter != null
                    ? this.targetToSourceConverter((TTargetValue)sourceValue.LastOrDefault())
                    : sourceValue.Last();
                if (EqualityComparer<TSrcValue>.Default.Equals((TSrcValue)targetValue.LastOrDefault(), (TSrcValue)convertedValue))
                {
                    return;
                }

                this.sourcePropertyInfo[this.sourcePropertyInfo.Length - 1].SetValue(this.sourcePropertyInfo.Length == 1 ? this.source.Target : sourceValue[sourceValue.Length - 2], convertedValue);
                this.onPropertyChanged?.Invoke(this.source.Target, new PropertyChangedEventArgs(this.sourcePropertyInfo[this.sourcePropertyInfo.Length - 1].Name));
            }
        }
    }
}