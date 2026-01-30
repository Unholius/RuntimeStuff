// <copyright file="EventHelperExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq.Expressions;
    using System.Reflection;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Набор extension-методов для упрощённой привязки действий
    /// к событиям объектов во время выполнения.
    /// </summary>
    /// <remarks>
    /// Класс предоставляет расширения, позволяющие:
    /// <list type="bullet">
    /// <item><description>Подписываться на события по имени или <see cref="System.Reflection.EventInfo"/>;</description></item>
    /// <item><description>адаптировать события с произвольной сигнатурой
    /// к унифицированному обработчику вида <c>Action&lt;object, object&gt;</c>;</description></item>
    /// <item><description>Управлять временем жизни подписки через <see cref="IDisposable"/>.</description></item>
    /// </list>
    ///
    /// Предназначен для инфраструктурного и runtime-кода:
    /// динамического связывания, логирования, проксирования событий,
    /// low-coupling сценариев и tooling-логики.
    ///
    /// Реализация не предполагает knowledge о конкретных типах событий
    /// на этапе компиляции.
    /// </remarks>
    public static class EventHelperExtensions
    {
        /// <summary>
        /// Связывает свойства объекта-источника и объекта-приёмника,
        /// используя указанные события для отслеживания изменений.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника.
        /// </typeparam>
        /// <typeparam name="TSourceProp">
        /// Тип свойства-источника.
        /// </typeparam>
        /// <typeparam name="TSourceEventArgs">
        /// Тип аргумента в событии-источника.
        /// </typeparam>
        /// <typeparam name="TTarget">
        /// Тип объекта-приёмника.
        /// </typeparam>
        /// <typeparam name="TTargetProp">
        /// Тип свойства-назначения.
        /// </typeparam>
        /// <typeparam name="TTargetEventArgs">
        /// Тип аргумента в событии-приемника.
        /// </typeparam>
        /// <param name="source">
        /// Объект-источник, изменения свойства которого будут отслеживаться.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Выражение, указывающее связываемое свойство объекта-источника.
        /// </param>
        /// <param name="sourceEventName">
        /// Имя события объекта-источника, при срабатывании которого выполняется обновление свойства.
        /// </param>
        /// <param name="target">
        /// Объект-приёмник, свойство которого будет обновляться.
        /// </param>
        /// <param name="targetPropertySelector">
        /// Выражение, указывающее связываемое свойство объекта-приёмника.
        /// </param>
        /// <param name="targetEventName">
        /// Имя события объекта-приёмника, при срабатывании которого выполняется обновление свойства.
        /// </param>
        /// <param name="sourceToTargetConverter">Конвертор значения свойства источника в тип свойства назначения.</param>
        /// <param name="targetToSourceConverter">Конвертор значения свойства назначения в тип свойства источника.</param>
        /// <param name="canAcceptSourceEvent">Условие принятия события в источнике.</param>
        /// <param name="canAcceptTargetEvent">Условие принятия события-приемника.</param>
        /// <param name="onPropertyChanged">Событие при успешном изменении значения свойства.</param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий разорвать связь и отписаться
        /// от событий источника и приёмника.
        /// </returns>
        /// <remarks>
        /// Метод ищет события по их именам с помощью рефлексии, после чего делегирует
        /// фактическую логику связывания перегруженному методу <c>BindProperties</c>
        /// с явным указанием типов аргументов событий (<see cref="ProgressChangedEventArgs"/>).
        /// </remarks>
        public static IDisposable Bind<TSource, TSourceProp, TSourceEventArgs, TTarget, TTargetProp, TTargetEventArgs>(
            this TSource source,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            string sourceEventName,
            Func<TSource, TSourceEventArgs, bool> canAcceptSourceEvent,
            TTarget target,
            Expression<Func<TTarget, TTargetProp>> targetPropertySelector,
            string targetEventName,
            Func<TTarget, TTargetEventArgs, bool> canAcceptTargetEvent,
            Func<TSourceProp, TTargetProp> sourceToTargetConverter = null,
            Func<TTargetProp, TSourceProp> targetToSourceConverter = null,
            Action onPropertyChanged = null)
            where TSource : class
            where TTarget : class
            where TSourceEventArgs : EventArgs
            where TTargetEventArgs : EventArgs
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrWhiteSpace(sourceEventName))
                throw new ArgumentException(@"Имя события источника не может быть пустым", nameof(sourceEventName));
            if (string.IsNullOrWhiteSpace(targetEventName))
                throw new ArgumentException(@"Имя события приёмника не может быть пустым", nameof(targetEventName));

            var srcEvent = source.GetType().GetEvent(sourceEventName) ?? throw new ArgumentException($@"Событие '{sourceEventName}' не найдено в типе '{source.GetType().Name}'", nameof(sourceEventName));
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(targetEventName);
            return EventHelper.BindProperties(source, srcProp, srcEvent, canAcceptSourceEvent, target, targetProp, targetEvent, canAcceptTargetEvent, sourceToTargetConverter, targetToSourceConverter);
        }

        public static IDisposable Bind<TSource, TSourceProp, TTarget, TTargetProp>(
            this TSource source,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            string sourceEventName,
            TTarget target,
            Expression<Func<TTarget, TTargetProp>> targetPropertySelector,
            string targetEventName,
            Func<TSourceProp, TTargetProp> sourceToTargetConverter = null,
            Func<TTargetProp, TSourceProp> targetToSourceConverter = null,
            Func<TSource, EventArgs, bool> canAcceptSourceEvent = null,
            Func<TTarget, EventArgs, bool> canAcceptTargetEvent = null,
            Action onPropertyChanged = null)
            where TSource : class
            where TTarget : class
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrWhiteSpace(sourceEventName))
                throw new ArgumentException(@"Имя события источника не может быть пустым", nameof(sourceEventName));
            if (string.IsNullOrWhiteSpace(targetEventName))
                throw new ArgumentException(@"Имя события приёмника не может быть пустым", nameof(targetEventName));

            var srcEvent = source.GetType().GetEvent(sourceEventName) ?? throw new ArgumentException($@"Событие '{sourceEventName}' не найдено в типе '{source.GetType().Name}'", nameof(sourceEventName));
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(targetEventName);
            return EventHelper.BindProperties(source, srcProp, srcEvent, canAcceptSourceEvent, target, targetProp, targetEvent, canAcceptTargetEvent, sourceToTargetConverter, targetToSourceConverter);
        }

        public static IDisposable Bind<TSource, TSourceProp, TTarget, TTargetProp>(
            this TSource source,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            string sourceEventName,
            TTarget target,
            Expression<Func<TTarget, TTargetProp>> targetPropertySelector,
            string targetEventName,
            Func<TSourceProp, TTargetProp> sourceToTargetConverter = null,
            Func<TTargetProp, TSourceProp> targetToSourceConverter = null,
            Func<TSource, PropertyChangedEventArgs, bool> canAcceptSourceEvent = null,
            Func<TTarget, PropertyChangedEventArgs, bool> canAcceptTargetEvent = null,
            Action onPropertyChanged = null)
            where TSource : class, INotifyPropertyChanged
            where TTarget : class, INotifyPropertyChanged
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrWhiteSpace(sourceEventName))
                throw new ArgumentException(@"Имя события источника не может быть пустым", nameof(sourceEventName));
            if (string.IsNullOrWhiteSpace(targetEventName))
                throw new ArgumentException(@"Имя события приёмника не может быть пустым", nameof(targetEventName));

            var srcEvent = source.GetType().GetEvent(sourceEventName) ?? throw new ArgumentException($@"Событие '{sourceEventName}' не найдено в типе '{source.GetType().Name}'", nameof(sourceEventName));
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(targetEventName);
            return EventHelper.BindProperties(source, srcProp, srcEvent, canAcceptSourceEvent, target, targetProp, targetEvent, canAcceptTargetEvent, sourceToTargetConverter, targetToSourceConverter);
        }

        /// <summary>
        /// Устанавливает одностороннюю привязку свойства источника к свойству назначения.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника, реализующего <see cref="INotifyPropertyChanged"/>.
        /// </typeparam>
        /// <typeparam name="TTarget">
        /// Тип объекта-приёмника.
        /// </typeparam>
        /// <typeparam name="T">
        /// Тип значения свойств.
        /// </typeparam>
        /// <param name="source">
        /// Объект-источник, изменения свойств которого отслеживаются.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Лямбда-выражение, указывающее привязываемое свойство источника.
        /// </param>
        /// <param name="target">
        /// Объект-приёмник, свойство которого будет обновляться.
        /// </param>
        /// <param name="targetPropertySelector">
        /// Лямбда-выражение, указывающее привязываемое свойство приёмника.
        /// </param>
        /// <param name="onPropertyChanged">Событие вызываемое при успешном изменении значения свойства.</param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий разорвать привязку
        /// и освободить связанные ресурсы.
        /// </returns>
        /// <remarks>
        /// Привязка является односторонней (OneWay):
        /// изменения свойства источника автоматически обновляют свойство приёмника.
        /// Обратная синхронизация не выполняется.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="source"/>,
        /// <paramref name="sourcePropertySelector"/>,
        /// <paramref name="target"/> или <paramref name="targetPropertySelector"/> равны <c>null</c>.
        /// </exception>
        public static IDisposable Bind<TSource, TTarget, T>(
            this TSource source,
            Expression<Func<TSource, T>> sourcePropertySelector,
            TTarget target,
            Expression<Func<TTarget, T>> targetPropertySelector,
            Action<object, PropertyChangedEventArgs> onPropertyChanged = null)
            where TSource : class, INotifyPropertyChanged
            where TTarget : class
        {
            var srcEvent = source.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var targetProp = targetPropertySelector.GetPropertyInfo();
            return EventHelper.BindProperties<TSource, T, PropertyChangedEventArgs, TTarget, T, EventArgs>(source, srcProp, srcEvent, (s, e) => e.PropertyName == targetProp.Name, target, targetProp, null, null, null, null, onPropertyChanged);
        }

        /// <summary>
        /// Привязывает действие без аргументов к указанному событию
        /// для каждого элемента коллекции.
        /// </summary>
        /// <typeparam name="T">
        /// Тип элементов коллекции.
        /// </typeparam>
        /// <param name="list">
        /// Коллекция элементов, для которых будет выполнена подписка на событие.
        /// </param>
        /// <param name="eventName">
        /// Имя события, к которому необходимо выполнить привязку.
        /// Событие должно существовать у типа <typeparamref name="T"/>.
        /// </param>
        /// <param name="action">
        /// Действие, выполняемое при возникновении события у любого элемента коллекции.
        /// </param>
        /// <returns>
        /// Последовательность объектов <see cref="IDisposable"/>,
        /// позволяющих отписаться от событий каждого элемента коллекции.
        /// </returns>
        /// <remarks>
        /// Метод последовательно подписывается на событие с указанным именем
        /// каждого элемента коллекции и возвращает набор объектов,
        /// управляющих временем жизни соответствующих подписок.
        /// Аргументы события игнорируются.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Может быть выброшено, если событие с указанным именем
        /// не найдено у типа <typeparamref name="T"/>.
        /// </exception>
        public static IEnumerable<IDisposable> Bind<T>(
            this IEnumerable<T> list,
            string eventName,
            Action action)
            where T : class
        {
            foreach (var item in list)
            {
                yield return Bind<object, object>(item, eventName, (s, e) => action());
            }
        }

        /// <summary>
        /// Подписывает действие на указанное событие каждого элемента коллекции.
        /// </summary>
        /// <typeparam name="T">
        /// Тип элементов коллекции, содержащих событие.
        /// </typeparam>
        /// <param name="list">
        /// Коллекция объектов, для каждого из которых будет выполнена подписка на событие.
        /// </param>
        /// <param name="eventName">
        /// Имя события, на которое необходимо подписаться.
        /// </param>
        /// <param name="action">
        /// Действие, вызываемое при срабатывании события.
        /// Первый параметр — объект, у которого произошло событие,
        /// второй параметр — аргументы события.
        /// </param>
        /// <returns>
        /// Перечисление объектов <see cref="IDisposable"/>, каждый из которых отвечает
        /// за отмену подписки на событие соответствующего элемента коллекции.
        /// </returns>
        /// <remarks>
        /// Метод предназначен для работы с событиями стандартного вида
        /// (<see cref="EventHandler"/> или <see cref="EventHandler{TEventArgs}"/>).
        /// Отписка от всех событий выполняется посредством вызова <see cref="IDisposable.Dispose"/>
        /// для каждого возвращаемого элемента.
        /// </remarks>
        public static IEnumerable<IDisposable> Bind<T>(
            this IEnumerable<T> list,
            string eventName,
            Action<T, EventArgs> action)
            where T : class
        {
            foreach (var item in list)
            {
                yield return Bind<T, EventArgs>(item, eventName, action);
            }
        }

        /// <summary>
        /// Привязывает указанное действие к событию изменения коллекции
        /// <see cref="INotifyCollectionChanged.CollectionChanged"/>.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, реализующего <see cref="INotifyCollectionChanged"/>.
        /// </typeparam>
        /// <param name="obj">
        /// Объект, коллекция которого будет отслеживаться.
        /// </param>
        /// <param name="action">
        /// Действие, выполняемое при изменении коллекции.
        /// Получает объект-источник события и аргументы изменения коллекции.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отписаться от события
        /// изменения коллекции.
        /// </returns>
        /// <remarks>
        /// Метод является специализированной обёрткой над <c>BindEventToAction</c>
        /// для события <c>CollectionChanged</c> и упрощает подписку на изменения
        /// коллекций без ручной реализации обработчиков событий.
        /// </remarks>
        public static IDisposable Bind<T>(
            this T obj,
            Action<T, NotifyCollectionChangedEventArgs> action)
            where T : class
        {
            var eventName = nameof(INotifyCollectionChanged.CollectionChanged);
            return Bind(obj, eventName, action);
        }

        /// <summary>
        /// Привязывает обработчик к событию объекта по имени события.
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
        /// <param name="eventName">
        /// Имя события, к которому необходимо привязать обработчик.
        /// </param>
        /// <param name="action">
        /// Делегат, который будет вызван при возникновении события.
        ///
        /// Первый параметр — объект-источник события (<c>sender</c>),
        /// второй параметр — аргументы события (<c>EventArgs</c> или производный тип).
        /// </param>
        /// <param name="canExecuteAction">Условие для выполнения делегата.</param>
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
        /// <paramref name="eventName"/> или <paramref name="action"/> равны <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Может быть сгенерировано, если событие с указанным именем не найдено.
        /// </exception>
        public static IDisposable Bind<T, TArgs>(
            this T obj,
            string eventName,
            Action<T, TArgs> action,
            Func<T, TArgs, bool> canExecuteAction = null)
        {
            var eventInfo = obj.GetType().GetEvent(eventName);
            return eventInfo == null
                ? throw new ArgumentException($@"Событие '{eventName}' не найдено в типе '{obj.GetType().Name}'", nameof(eventName))
                : EventHelper.BindEventToAction(obj, eventInfo, action, canExecuteAction);
        }

        /// <summary>
        /// Привязывает указанное действие к событию объекта по имени события.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, содержащего событие.
        /// </typeparam>
        /// <param name="obj">
        /// Экземпляр объекта, событие которого будет использовано.
        /// </param>
        /// <param name="eventName">
        /// Имя события, к которому необходимо привязать действие.
        /// </param>
        /// <param name="action">
        /// Действие, выполняемое при срабатывании события; получает объект-источник события
        /// и его аргументы.
        /// </param>
        /// <param name="canExecuteAction">Условие для выполнения делегата.</param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отписаться от события
        /// и освободить связанные ресурсы.
        /// </returns>
        /// <remarks>
        /// Метод является удобной обёрткой над <c>EventHelper.BindEventToAction</c>
        /// и использует рефлексию для поиска события по его имени.
        /// Ожидается, что указанное событие соответствует стандартному .NET-паттерну
        /// и использует аргументы, производные от <see cref="EventArgs"/>.
        /// </remarks>
        public static IDisposable Bind<T>(
            this T obj,
            string eventName,
            Action<T, EventArgs> action,
            Func<T, EventArgs, bool> canExecuteAction = null)
        {
            var eventInfo = obj.GetType().GetEvent(eventName);
            return eventInfo == null
                ? throw new ArgumentException($@"Событие '{eventName}' не найдено в типе '{obj.GetType().Name}'", nameof(eventName))
                : EventHelper.BindEventToAction(obj, eventInfo, action, canExecuteAction);
        }

        /// <summary>
        /// Привязывает обработчик к событию <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// объекта и перенаправляет вызовы в унифицированное действие.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, реализующего <see cref="INotifyPropertyChanged"/>.
        /// </typeparam>
        /// <param name="obj">
        /// Экземпляр объекта, для которого выполняется подписка на событие
        /// <see cref="INotifyPropertyChanged.PropertyChanged"/>.
        /// </param>
        /// <param name="propertySelector">Выбор свойства.</param>
        /// <param name="action">
        /// Делегат, принимающий отправителя события и аргументы события
        /// (<see cref="PropertyChangedEventArgs"/>), переданные как <see cref="object"/>.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, при уничтожении которого
        /// выполняется отписка от события.
        /// </returns>
        /// <remarks>
        /// Метод является thin-wrapper над <c>BindEventToAction</c> и предназначен
        /// для упрощения сценариев наблюдения за изменениями свойств:
        /// логирования, трассировки, data-binding инфраструктуры и runtime-инструментов.
        ///
        /// Не выполняет проверку реализации <see cref="INotifyPropertyChanged"/>
        /// на этапе компиляции — ошибка возможна во время выполнения,
        /// если событие отсутствует.
        /// </remarks>
        public static IDisposable Bind<T>(
            this T obj,
            Expression<Func<T, object>> propertySelector,
            Action<T, PropertyChangedEventArgs> action,
            Func<T, PropertyChangedEventArgs, bool> canExecuteAction = null)
            where T : class
        {
            var eventName = nameof(INotifyPropertyChanged.PropertyChanged);
            return Bind<T, PropertyChangedEventArgs>(obj, eventName, action, (sender, args) => args.PropertyName == propertySelector.GetPropertyName(), canExecuteAction);
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
        /// Метод использует <see cref="Bind{TSource,TEventArgs}"/> для привязки
        /// события к действию и обеспечивает удобный способ связывать события с конкретными
        /// подписчиками без ручной реализации обработчиков.
        /// </remarks>
        public static void Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, TSource eventSource, EventInfo sourceEvent, Action<TSubscriber, TSource> action)
            where TSubscriber : class
            where TSource : class
        {
            EventHelper.BindEventToAction<TSource, object>(eventSource, sourceEvent, (s, e) => action(subscriber, s));
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
        /// <param name="sourceEventName">
        /// Имя события источника, на которое необходимо подписаться.
        /// </param>
        /// <param name="action">
        /// Действие, которое будет вызвано при срабатывании события. Получает объект-подписчик
        /// и объект-источник события.
        /// </param>
        /// <remarks>
        /// Метод использует <see cref="Bind{TSource,TEventArgs}"/> для привязки
        /// события к действию и обеспечивает удобный способ связывать события с конкретными
        /// подписчиками без ручной реализации обработчиков.
        /// </remarks>
        /// <returns>Информация о подписке на событие.</returns>
        public static IDisposable Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, TSource eventSource, string sourceEventName, Action<TSubscriber, TSource, EventArgs> action)
            where TSubscriber : class
            where TSource : class
        {
            var sourceEvent = eventSource.GetType().GetEvent(sourceEventName);
            return EventHelper.BindEventToAction<TSource, EventArgs>(eventSource, sourceEvent, (s, e) => action(subscriber, s, e));
        }

        /// <summary>
        /// Подписывает объект на указанное событие источника и выполняет заданное действие
        /// при каждом возникновении этого события.
        /// </summary>
        /// <typeparam name="TSubscriber">
        /// Тип объекта-подписчика. Используется для семантической привязки,
        /// но напрямую в обработчике не участвует.
        /// </typeparam>
        /// <typeparam name="TSource">
        /// Тип объекта-источника события.
        /// </typeparam>
        /// <param name="subscriber">
        /// Объект-подписчик, инициирующий подписку.
        /// Обычно используется для контроля жизненного цикла подписки.
        /// </param>
        /// <param name="eventSource">
        /// Объект, содержащий событие, на которое выполняется подписка.
        /// </param>
        /// <param name="sourceEventName">
        /// Имя события источника, на которое необходимо подписаться.
        /// </param>
        /// <param name="action">
        /// Действие, выполняемое при возникновении события.
        /// Параметры события (sender и args) игнорируются.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отписаться от события
        /// и освободить связанные ресурсы.
        /// </returns>
        /// <remarks>
        /// Подписка выполняется с использованием отражения для получения события
        /// по его имени и вспомогательного метода
        /// <c>EventHelper.BindEventToAction</c>.
        /// Метод удобен в случаях, когда требуется простая реакция на событие
        /// без анализа источника и аргументов.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Может быть выброшено, если событие с указанным именем не найдено
        /// или если вспомогательный метод обработки событий не допускает
        /// передачу null-значений.
        /// </exception>
        public static IDisposable Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, TSource eventSource, string sourceEventName, Action action)
            where TSubscriber : class
            where TSource : class
        {
            var sourceEvent = eventSource.GetType().GetEvent(sourceEventName);
            return EventHelper.BindEventToAction<TSource, EventArgs>(eventSource, sourceEvent, (s, e) => action());
        }

        /// <summary>
        /// Подписывает подписчика на событие <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// указанного источника и вызывает заданное действие при каждом срабатывании события.
        /// </summary>
        /// <typeparam name="TSubscriber">
        /// Тип объекта-подписчика, для которого выполняется привязка события.
        /// </typeparam>
        /// <typeparam name="TSource">
        /// Тип источника события, реализующий <see cref="INotifyPropertyChanged"/>.
        /// </typeparam>
        /// <param name="subscriber">
        /// Объект-подписчик, который будет передан в действие при срабатывании события.
        /// </param>
        /// <param name="eventSource">
        /// Объект-источник события <see cref="INotifyPropertyChanged.PropertyChanged"/>.
        /// </param>
        /// <param name="action">
        /// Действие, вызываемое при возникновении события.
        /// В качестве параметров передаются подписчик и источник события.
        /// </param>
        /// <remarks>
        /// Метод использует отражение для получения события
        /// <see cref="INotifyPropertyChanged.PropertyChanged"/> и выполняет подписку
        /// через вспомогательный метод <c>EventHelper.BindEventToAction</c>.
        /// </remarks>
        /// <returns>Информация о подписке на событие.</returns>
        public static IDisposable Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, TSource eventSource, Action<TSubscriber, TSource, PropertyChangedEventArgs> action)
            where TSubscriber : class
            where TSource : INotifyPropertyChanged
        {
            var sourceEvent = eventSource.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            return EventHelper.BindEventToAction<TSource, PropertyChangedEventArgs>(eventSource, sourceEvent, (s, e) => action(subscriber, s, e));
        }

        /// <summary>
        /// Подписывает объект на событие <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// источника и выполняет заданное действие при любом изменении свойства.
        /// </summary>
        /// <typeparam name="TSubscriber">
        /// Тип объекта-подписчика. Используется для логической привязки подписки
        /// и контроля её жизненного цикла.
        /// </typeparam>
        /// <typeparam name="TSource">
        /// Тип объекта-источника, реализующего <see cref="INotifyPropertyChanged"/>.
        /// </typeparam>
        /// <param name="subscriber">
        /// Объект-подписчик, инициирующий подписку.
        /// </param>
        /// <param name="eventSource">
        /// Объект, изменения свойств которого необходимо отслеживать.
        /// </param>
        /// <param name="action">
        /// Действие, выполняемое при возникновении события
        /// <see cref="INotifyPropertyChanged.PropertyChanged"/>.
        /// Аргументы события (sender и <see cref="PropertyChangedEventArgs"/>)
        /// игнорируются.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отписаться от события
        /// и освободить связанные ресурсы.
        /// </returns>
        /// <remarks>
        /// Метод является упрощённой обёрткой для подписки на событие
        /// <see cref="INotifyPropertyChanged.PropertyChanged"/> и предназначен
        /// для сценариев, где требуется лишь факт изменения свойства,
        /// без анализа имени изменённого свойства или источника события.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Может быть выброшено, если событие <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// не найдено или при ошибке в процессе привязки обработчика.
        /// </exception>
        public static IDisposable Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, TSource eventSource, Action action)
            where TSubscriber : class
            where TSource : INotifyPropertyChanged
        {
            var sourceEvent = eventSource.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            return EventHelper.BindEventToAction<TSource, PropertyChangedEventArgs>(eventSource, sourceEvent, (s, e) => action());
        }

        /// <summary>
        /// Подписывает объект-подписчик на событие <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// каждого элемента в коллекции источников и выполняет указанное действие
        /// при любом изменении свойства.
        /// </summary>
        /// <typeparam name="TSubscriber">
        /// Тип объекта-подписчика, инициирующего подписку.
        /// </typeparam>
        /// <typeparam name="TSource">
        /// Тип объектов-источников, реализующих <see cref="INotifyPropertyChanged"/>.
        /// </typeparam>
        /// <param name="subscriber">
        /// Объект-подписчик, для которого создаются подписки.
        /// </param>
        /// <param name="eventSources">
        /// Коллекция объектов, изменения свойств которых необходимо отслеживать.
        /// </param>
        /// <param name="action">
        /// Действие, выполняемое при возникновении события
        /// <see cref="INotifyPropertyChanged.PropertyChanged"/> у любого элемента коллекции.
        /// Аргументы события игнорируются.
        /// </param>
        /// <returns>
        /// Последовательность объектов <see cref="IDisposable"/>, каждый из которых
        /// позволяет отписаться от соответствующего источника события.
        /// </returns>
        /// <remarks>
        /// Метод является удобной обёрткой для массовой подписки на изменения свойств
        /// нескольких объектов. Управление временем жизни подписок осуществляется
        /// через возвращаемые объекты <see cref="IDisposable"/>.
        /// </remarks>
        public static IEnumerable<IDisposable> Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, IEnumerable<TSource> eventSources, Action action)
            where TSubscriber : class
            where TSource : INotifyPropertyChanged
        {
            foreach (var eventSource in eventSources)
            {
                yield return Subscribe(subscriber, eventSource, action);
            }
        }

        /// <summary>
        /// Подписывает объект-подписчик на событие <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// каждого элемента в коллекции источников и вызывает заданное действие
        /// с передачей контекста подписчика, источника и аргументов события.
        /// </summary>
        /// <typeparam name="TSubscriber">
        /// Тип объекта-подписчика.
        /// </typeparam>
        /// <typeparam name="TSource">
        /// Тип объектов-источников, реализующих <see cref="INotifyPropertyChanged"/>.
        /// </typeparam>
        /// <param name="subscriber">
        /// Объект-подписчик, передаваемый в действие при срабатывании события.
        /// </param>
        /// <param name="eventSources">
        /// Коллекция объектов, изменения свойств которых необходимо отслеживать.
        /// </param>
        /// <param name="action">
        /// Действие, вызываемое при возникновении события.
        /// В параметры передаются подписчик, источник события и аргументы
        /// <see cref="PropertyChangedEventArgs"/>.
        /// </param>
        /// <returns>
        /// Последовательность объектов <see cref="IDisposable"/>, позволяющих
        /// управлять подписками на события каждого источника.
        /// </returns>
        /// <remarks>
        /// Данный метод предназначен для сценариев, где требуется анализировать
        /// контекст события, включая конкретный источник и имя изменённого свойства.
        /// </remarks>
        public static IEnumerable<IDisposable> Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, IEnumerable<TSource> eventSources, Action<TSubscriber, TSource, PropertyChangedEventArgs> action)
            where TSubscriber : class
            where TSource : INotifyPropertyChanged
        {
            foreach (var eventSource in eventSources)
            {
                yield return Subscribe(subscriber, eventSource, action);
            }
        }
    }
}