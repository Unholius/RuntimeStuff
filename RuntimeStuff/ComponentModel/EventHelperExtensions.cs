// <copyright file="EventHelperExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.ComponentModel
{
    using System;
    using System.Collections.Specialized;
    using System.Helpers;
    using System.Linq.Expressions;

    /// <summary>
    /// Class EventHelperExtensions.
    /// </summary>
    public static class EventHelperExtensions
    {
        /// <summary>
        /// Привязывает действие к событию изменения коллекции
        /// (<see cref="INotifyCollectionChanged"/>).
        /// </summary>
        /// <typeparam name="TSource">Тип источника.</typeparam>
        /// <param name="source">Коллекция-источник.</param>
        /// <param name="action">
        /// Действие, выполняемое при изменении коллекции.
        /// </param>
        /// <returns>
        /// <see cref="IDisposable"/> для отмены подписки.
        /// </returns>
        public static IDisposable BindCollectionChangedToAction<TSource>(
            this TSource source,
            Action<TSource, NotifyCollectionChangedEventArgs> action)
            where TSource : class, INotifyCollectionChanged
        {
            var srcEvent = source.GetType().GetEvent(nameof(INotifyCollectionChanged.CollectionChanged));

            return EventHelper.BindEventToAction<TSource, NotifyCollectionChangedEventArgs>(source, srcEvent, action);
        }

        /// <summary>
        /// Привязывает действие к событию изменения коллекции
        /// (<see cref="INotifyCollectionChanged"/>).
        /// </summary>
        /// <typeparam name="TSource">Тип источника.</typeparam>
        /// <param name="source">Коллекция-источник.</param>
        /// <param name="action">
        /// Действие, выполняемое при изменении коллекции.
        /// </param>
        /// <returns>
        /// <see cref="IDisposable"/> для отмены подписки.
        /// </returns>
        public static IDisposable BindCollectionChangedToAction<TSource>(
            this TSource source,
            Action action)
            where TSource : class, INotifyCollectionChanged
        {
            var srcEvent = source.GetType().GetEvent(nameof(INotifyCollectionChanged.CollectionChanged));

            return EventHelper.BindEventToAction<TSource, NotifyCollectionChangedEventArgs>(source, srcEvent, (_, __) => action());
        }

        /// <summary>
        /// Привязывает выполнение действия к событию источника.
        /// </summary>
        /// <typeparam name="TSource">Тип объекта-источника.</typeparam>
        /// <param name="source">Объект-источник.</param>
        /// <param name="onSourceEventName">Имя события.</param>
        /// <param name="action">
        /// Действие, выполняемое при возникновении события.
        /// </param>
        /// <param name="canExecuteAction">
        /// Условие, определяющее возможность выполнения действия.
        /// </param>
        /// <returns>
        /// <see cref="IDisposable"/> для отмены подписки.
        /// </returns>
        public static IDisposable BindEventToAction<TSource>(
            this TSource source,
            string onSourceEventName,
            Action<object, object> action,
            Func<bool> canExecuteAction = null)
            where TSource : class
        {
            var srcEvent = source.GetType().GetEvent(onSourceEventName) ?? throw new ArgumentException($@"Событие '{onSourceEventName}' не найдено в типе '{source.GetType().Name}'", nameof(onSourceEventName));
            if (canExecuteAction == null)
            {
                canExecuteAction = () => true;
            }

            return EventHelper.BindEventToAction(source, srcEvent, action, (s, e) => canExecuteAction());
        }

        /// <summary>
        /// Создает типобезопасную привязку между свойствами двух объектов с использованием выражений,
        /// позволяя синхронизировать значения свойств при возникновении указанных событий.
        /// </summary>
        /// <remarks>
        /// Метод поддерживает двустороннюю синхронизацию значений свойств.
        /// При возникновении события у исходного объекта значение его свойства
        /// передается в свойство целевого объекта. Аналогично, при возникновении
        /// события у целевого объекта значение может быть передано обратно
        /// в свойство источника.
        ///
        /// Для выбора свойств используются выражения (<see cref="Expression{TDelegate}"/>),
        /// что обеспечивает проверку корректности свойств на этапе компиляции.
        /// </remarks>
        /// <typeparam name="TSource">Тип исходного объекта.</typeparam>
        /// <typeparam name="TSourceProp">Тип свойства исходного объекта.</typeparam>
        /// <typeparam name="TTarget">Тип целевого объекта.</typeparam>
        /// <typeparam name="TTargetProp">Тип свойства целевого объекта.</typeparam>
        /// <param name="source">Исходный объект.</param>
        /// <param name="sourcePropertySelector">
        /// Выражение, указывающее свойство исходного объекта,
        /// значение которого будет синхронизироваться.
        /// </param>
        /// <param name="sourceEventName">
        /// Имя события исходного объекта, при возникновении которого
        /// значение свойства источника будет передано целевому объекту.
        /// </param>
        /// <param name="target">Целевой объект.</param>
        /// <param name="targetPropertySelector">
        /// Выражение, указывающее свойство целевого объекта,
        /// значение которого будет синхронизироваться.
        /// </param>
        /// <param name="targetEventName">
        /// Имя события целевого объекта, при возникновении которого
        /// значение свойства целевого объекта будет передано обратно источнику.
        /// </param>
        /// <param name="sourcePropertyValueToTargetPropertyValueConverter">
        /// Функция преобразования значения свойства источника в значение
        /// свойства целевого объекта.
        /// </param>
        /// <param name="targetPropertyValueToSourcePropertyValueConverter">
        /// Функция преобразования значения свойства целевого объекта
        /// в значение свойства источника.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Делегат, вызываемый после изменения одного из связанных свойств.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отменить привязку
        /// и отписаться от всех подписанных событий.
        /// </returns>
        public static IDisposable BindProperties<TSource, TSourceProp, TTarget, TTargetProp>(
            this TSource source,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            string sourceEventName,
            TTarget target,
            Expression<Func<TTarget, TTargetProp>> targetPropertySelector,
            string targetEventName,
            Func<TSourceProp, TTargetProp> sourcePropertyValueToTargetPropertyValueConverter,
            Func<TTargetProp, TSourceProp> targetPropertyValueToSourcePropertyValueConverter,
            Action<object, PropertyChangedEventArgs> onPropertyChanged = null)
            where TSource : class
            where TTarget : class
        {
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var srcEvent = source.GetType().GetEvent(sourceEventName);
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(targetEventName);

            return EventHelper.BindProperties<TSource, TSourceProp, PropertyChangedEventArgs, TTarget, TTargetProp, PropertyChangedEventArgs>(
                source,
                srcProp,
                srcEvent,
                (s, e) => true,
                target,
                targetProp,
                targetEvent,
                (s, e) => true,
                sourcePropertyValueToTargetPropertyValueConverter,
                targetPropertyValueToSourcePropertyValueConverter,
                onPropertyChanged);
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
        /// <typeparam name="TSource">Тип объекта-источника.</typeparam>
        /// <typeparam name="TTarget">Тип объекта-приёмника.</typeparam>
        /// <param name="source">Исходный объект, содержащий свойство-источник.</param>
        /// <param name="sourcePropertySelector">Свойство исходного объекта.</param>
        /// <param name="target">Целевой объект, содержащий свойство-приемник.</param>
        /// <param name="targetPropertySelector">Свойство целевого объекта.</param>
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
        public static IDisposable BindProperties<TSource, TTarget>(this TSource source, Expression<Func<TSource, object>> sourcePropertySelector, TTarget target, Expression<Func<TSource, object>> targetPropertySelector, string onTargetEvent = null, Func<object, object> sourceToTargetValueConverter = null, Func<object, object> targetToSourceValueConverter = null, Action onPropertyChanged = null)
            where TSource : INotifyPropertyChanged
        {
            return EventHelper.BindProperties(source, sourcePropertySelector, target, targetPropertySelector, onTargetEvent, sourceToTargetValueConverter, targetToSourceValueConverter, onPropertyChanged);
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
        /// <typeparam name="TSource">Тип объекта-источника.</typeparam>
        /// <typeparam name="TTarget">Тип объекта-приёмника.</typeparam>
        /// <param name="source">Исходный объект, содержащий свойство-источник.</param>
        /// <param name="sourcePropertySelector">Свойство исходного объекта.</param>
        /// <param name="onSourceEvent">
        /// Имя события исходного объекта, при возникновении которого значение
        /// будет копироваться из исходного объекта в целевой.
        /// </param>
        /// <param name="target">Целевой объект, содержащий свойство-приемник.</param>
        /// <param name="targetPropertySelector">Свойство целевого объекта.</param>
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
        public static IDisposable BindProperties<TSource, TTarget>(this TSource source, Expression<Func<TSource, object>> sourcePropertySelector, string onSourceEvent, TTarget target, Expression<Func<TSource, object>> targetPropertySelector, string onTargetEvent = null, Func<object, object> sourceToTargetValueConverter = null, Func<object, object> targetToSourceValueConverter = null, Action onPropertyChanged = null)
            where TSource : class
        {
            return EventHelper.BindProperties(source, sourcePropertySelector, onSourceEvent, target, targetPropertySelector, onTargetEvent, sourceToTargetValueConverter, targetToSourceValueConverter, onPropertyChanged);
        }

        /// <summary>
        /// Связывает свойства двух объектов с поддержкой уведомлений об изменении,
        /// обеспечивая синхронизацию значений между источником и целевым объектом.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника.
        /// </typeparam>
        /// <typeparam name="TSourceProp">
        /// Тип свойства объекта-источника.
        /// </typeparam>
        /// <typeparam name="TTarget">
        /// Тип целевого объекта.
        /// </typeparam>
        /// <typeparam name="TTargetProp">
        /// Тип свойства целевого объекта.
        /// </typeparam>
        /// <param name="source">
        /// Объект-источник, реализующий <see cref="INotifyPropertyChanged"/>.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Лямбда-выражение, указывающее связываемое свойство объекта-источника.
        /// </param>
        /// <param name="target">
        /// Целевой объект, реализующий <see cref="INotifyPropertyChanged"/>.
        /// </param>
        /// <param name="targetPropertySelector">
        /// Лямбда-выражение, указывающее связываемое свойство целевого объекта.
        /// </param>
        /// <param name="sourcePropertyValueToTargetPropertyValueConverter">
        /// Функция преобразования значения свойства источника в значение свойства целевого объекта.
        /// </param>
        /// <param name="targetPropertyValueToSourcePropertyValueConverter">
        /// Функция преобразования значения свойства целевого объекта в значение свойства источника.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Необязательный колбэк, вызываемый при изменении связанного свойства.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий разорвать связь между свойствами.
        /// </returns>
        public static IDisposable BindProperties<TSource, TSourceProp, TTarget, TTargetProp>(
            this TSource source,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            TTarget target,
            Expression<Func<TTarget, TTargetProp>> targetPropertySelector,
            Func<TSourceProp, TTargetProp> sourcePropertyValueToTargetPropertyValueConverter,
            Func<TTargetProp, TSourceProp> targetPropertyValueToSourcePropertyValueConverter,
            Action<object, PropertyChangedEventArgs> onPropertyChanged = null)
            where TSource : class, INotifyPropertyChanged
            where TTarget : class, INotifyPropertyChanged
        {
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var srcEvent = source.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));

            return EventHelper.BindProperties<TSource, TSourceProp, PropertyChangedEventArgs, TTarget, TTargetProp, PropertyChangedEventArgs>(source, srcProp, srcEvent, (s, e) => e.PropertyName == srcProp.Name, target, targetProp, targetEvent, (s, e) => e.PropertyName == targetProp.Name, sourcePropertyValueToTargetPropertyValueConverter, targetPropertyValueToSourcePropertyValueConverter, onPropertyChanged);
        }

        /// <summary>
        /// Связывает свойство объекта-источника с свойством целевого объекта,
        /// используя указанное событие целевого объекта для отслеживания изменений.
        /// Поддерживается преобразование значений между типами свойств.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника.
        /// </typeparam>
        /// <typeparam name="TSourceProp">
        /// Тип свойства объекта-источника.
        /// </typeparam>
        /// <typeparam name="TTarget">
        /// Тип целевого объекта.
        /// </typeparam>
        /// <typeparam name="TTargetProp">
        /// Тип свойства целевого объекта.
        /// </typeparam>
        /// <param name="source">
        /// Объект-источник, реализующий <see cref="INotifyPropertyChanged"/>.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Лямбда-выражение, указывающее связываемое свойство объекта-источника.
        /// </param>
        /// <param name="target">
        /// Целевой объект.
        /// </param>
        /// <param name="onTargetEventName">
        /// Имя события целевого объекта, при возникновении которого
        /// выполняется синхронизация значения свойства целевого объекта
        /// с объектом-источником.
        /// </param>
        /// <param name="targetPropertySelector">
        /// Лямбда-выражение, указывающее связываемое свойство целевого объекта.
        /// </param>
        /// <param name="sourcePropertyValueToTargetPropertyValueConverter">
        /// Функция преобразования значения свойства источника
        /// в значение свойства целевого объекта.
        /// </param>
        /// <param name="targetPropertyValueToSourcePropertyValueConverter">
        /// Функция преобразования значения свойства целевого объекта
        /// в значение свойства источника.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Необязательный обработчик, вызываемый при синхронизации свойств.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий разорвать связь между свойствами.
        /// </returns>
        public static IDisposable BindProperties<TSource, TSourceProp, TTarget, TTargetProp>(
            this TSource source,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            TTarget target,
            string onTargetEventName,
            Expression<Func<TTarget, TTargetProp>> targetPropertySelector,
            Func<TSourceProp, TTargetProp> sourcePropertyValueToTargetPropertyValueConverter,
            Func<TTargetProp, TSourceProp> targetPropertyValueToSourcePropertyValueConverter,
            Action<object, PropertyChangedEventArgs> onPropertyChanged = null)
            where TSource : class, INotifyPropertyChanged
            where TTarget : class
        {
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var srcEvent = source.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(onTargetEventName);

            return EventHelper.BindProperties<TSource, TSourceProp, PropertyChangedEventArgs, TTarget, TTargetProp, EventArgs>(source, srcProp, srcEvent, (s, e) => e.PropertyName == srcProp.Name, target, targetProp, targetEvent, (s, e) => true, sourcePropertyValueToTargetPropertyValueConverter, targetPropertyValueToSourcePropertyValueConverter, onPropertyChanged);
        }

        /// <summary>
        /// Связывает свойства двух объектов одинакового типа,
        /// используя указанное событие целевого объекта
        /// для синхронизации значений.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника.
        /// </typeparam>
        /// <typeparam name="TTarget">
        /// Тип целевого объекта.
        /// </typeparam>
        /// <typeparam name="T">
        /// Тип связываемых свойств.
        /// </typeparam>
        /// <param name="source">
        /// Объект-источник, реализующий <see cref="INotifyPropertyChanged"/>.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Лямбда-выражение, указывающее связываемое свойство объекта-источника.
        /// </param>
        /// <param name="target">
        /// Целевой объект.
        /// </param>
        /// <param name="onTargetEventName">
        /// Имя события целевого объекта, при возникновении которого
        /// выполняется синхронизация значения свойства.
        /// </param>
        /// <param name="targetPropertySelector">
        /// Лямбда-выражение, указывающее связываемое свойство целевого объекта.
        /// </param>
        /// <param name="valueConverter">Конвертер значения. Например, x => !x.</param>
        /// <param name="onPropertyChanged">
        /// Необязательный обработчик, вызываемый при изменении связанного свойства.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отменить связывание свойств.
        /// </returns>
        public static IDisposable BindProperties<TSource, TTarget, T>(
            this TSource source,
            Expression<Func<TSource, T>> sourcePropertySelector,
            TTarget target,
            string onTargetEventName,
            Expression<Func<TTarget, T>> targetPropertySelector,
            Func<T, T> valueConverter,
            Action<object, PropertyChangedEventArgs> onPropertyChanged = null)
            where TSource : class, INotifyPropertyChanged
            where TTarget : class
        {
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var srcEvent = source.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(onTargetEventName);

            return EventHelper.BindProperties<TSource, T, PropertyChangedEventArgs, TTarget, T, EventArgs>(source, srcProp, srcEvent, (s, e) => e.PropertyName == srcProp.Name, target, targetProp, targetEvent, (s, e) => true, valueConverter, valueConverter, onPropertyChanged);
        }

        /// <summary>
        /// Связывает свойства двух объектов одинакового типа,
        /// используя указанное событие целевого объекта
        /// для синхронизации значений.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника.
        /// </typeparam>
        /// <typeparam name="TTarget">
        /// Тип целевого объекта.
        /// </typeparam>
        /// <typeparam name="T">
        /// Тип связываемых свойств.
        /// </typeparam>
        /// <param name="source">
        /// Объект-источник, реализующий <see cref="INotifyPropertyChanged"/>.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Лямбда-выражение, указывающее связываемое свойство объекта-источника.
        /// </param>
        /// <param name="target">
        /// Целевой объект.
        /// </param>
        /// <param name="onTargetEventName">
        /// Имя события целевого объекта, при возникновении которого
        /// выполняется синхронизация значения свойства.
        /// </param>
        /// <param name="targetPropertySelector">
        /// Лямбда-выражение, указывающее связываемое свойство целевого объекта.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Необязательный обработчик, вызываемый при изменении связанного свойства.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отменить связывание свойств.
        /// </returns>
        public static IDisposable BindProperties<TSource, TTarget, T>(
            this TSource source,
            Expression<Func<TSource, T>> sourcePropertySelector,
            TTarget target,
            string onTargetEventName,
            Expression<Func<TTarget, T>> targetPropertySelector,
            Action<object, PropertyChangedEventArgs> onPropertyChanged = null)
            where TSource : class, INotifyPropertyChanged
            where TTarget : class
        {
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var srcEvent = source.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(onTargetEventName);

            return EventHelper.BindProperties<TSource, T, PropertyChangedEventArgs, TTarget, T, EventArgs>(source, srcProp, srcEvent, (s, e) => e.PropertyName == srcProp.Name, target, targetProp, targetEvent, (s, e) => true, null, null, onPropertyChanged);
        }

        /// <summary>
        /// Связывает одноимённые по типу свойства двух объектов,
        /// обеспечивая двустороннюю синхронизацию их значений
        /// при изменении свойств.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника.
        /// </typeparam>
        /// <typeparam name="TTarget">
        /// Тип целевого объекта.
        /// </typeparam>
        /// <typeparam name="T">
        /// Тип связываемых свойств.
        /// </typeparam>
        /// <param name="source">
        /// Объект-источник, реализующий <see cref="INotifyPropertyChanged"/>.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Лямбда-выражение, указывающее связываемое свойство объекта-источника.
        /// </param>
        /// <param name="target">
        /// Целевой объект, реализующий <see cref="INotifyPropertyChanged"/>.
        /// </param>
        /// <param name="targetPropertySelector">
        /// Лямбда-выражение, указывающее связываемое свойство целевого объекта.
        /// </param>
        /// <param name="valueConverter">Конвертер значения. Например, x => !x.</param>
        /// <param name="onPropertyChanged">
        /// Необязательный обработчик, вызываемый при изменении связанного свойства
        /// у любого из объектов.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отменить связывание свойств.
        /// </returns>
        public static IDisposable BindProperties<TSource, TTarget, T>(
            this TSource source,
            Expression<Func<TSource, T>> sourcePropertySelector,
            TTarget target,
            Expression<Func<TTarget, T>> targetPropertySelector,
            Func<T, T> valueConverter,
            Action<object, PropertyChangedEventArgs> onPropertyChanged = null)
            where TSource : class, INotifyPropertyChanged
            where TTarget : class, INotifyPropertyChanged
        {
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var srcEvent = source.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));

            return EventHelper.BindProperties<TSource, T, PropertyChangedEventArgs, TTarget, T, PropertyChangedEventArgs>(source, srcProp, srcEvent, (s, e) => e.PropertyName == srcProp.Name, target, targetProp, targetEvent, (s, e) => e.PropertyName == targetProp.Name, valueConverter, valueConverter, onPropertyChanged);
        }

        /// <summary>
        /// Связывает одноимённые по типу свойства двух объектов,
        /// обеспечивая двустороннюю синхронизацию их значений
        /// при изменении свойств.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника.
        /// </typeparam>
        /// <typeparam name="TTarget">
        /// Тип целевого объекта.
        /// </typeparam>
        /// <typeparam name="T">
        /// Тип связываемых свойств.
        /// </typeparam>
        /// <param name="source">
        /// Объект-источник, реализующий <see cref="INotifyPropertyChanged"/>.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Лямбда-выражение, указывающее связываемое свойство объекта-источника.
        /// </param>
        /// <param name="target">
        /// Целевой объект, реализующий <see cref="INotifyPropertyChanged"/>.
        /// </param>
        /// <param name="targetPropertySelector">
        /// Лямбда-выражение, указывающее связываемое свойство целевого объекта.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Необязательный обработчик, вызываемый при изменении связанного свойства
        /// у любого из объектов.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отменить связывание свойств.
        /// </returns>
        public static IDisposable BindProperties<TSource, TTarget, T>(
            this TSource source,
            Expression<Func<TSource, T>> sourcePropertySelector,
            TTarget target,
            Expression<Func<TTarget, T>> targetPropertySelector,
            Action<object, PropertyChangedEventArgs> onPropertyChanged = null)
            where TSource : class, INotifyPropertyChanged
            where TTarget : class, INotifyPropertyChanged
        {
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var srcEvent = source.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));

            return EventHelper.BindProperties<TSource, T, PropertyChangedEventArgs, TTarget, T, PropertyChangedEventArgs>(source, srcProp, srcEvent, (s, e) => e.PropertyName == srcProp.Name, target, targetProp, targetEvent, (s, e) => e.PropertyName == targetProp.Name, null, null, onPropertyChanged);
        }

        /// <summary>
        /// Устанавливает двустороннюю или одностороннюю привязку между свойствами
        /// объекта-источника и объекта-приёмника на основе указанных событий.
        /// </summary>
        /// <typeparam name="TSource">Тип объекта-источника.</typeparam>
        /// <typeparam name="TSourceProp">Тип свойства источника.</typeparam>
        /// <typeparam name="TSourceEventArgs">Тип аргументов события источника.</typeparam>
        /// <typeparam name="TTarget">Тип объекта-приёмника.</typeparam>
        /// <typeparam name="TTargetProp">Тип свойства приёмника.</typeparam>
        /// <typeparam name="TTargetEventArgs">Тип аргументов события приёмника.</typeparam>
        /// <param name="source">Объект-источник.</param>
        /// <param name="onSourceEventName">Имя события источника, инициирующего обновление.</param>
        /// <param name="sourcePropertySelector">
        /// Лямбда-выражение, указывающее привязываемое свойство источника.
        /// </param>
        /// <param name="canAcceptSourceEvent">
        /// Фильтр, определяющий, следует ли обрабатывать событие источника.
        /// </param>
        /// <param name="target">Объект-приёмник.</param>
        /// <param name="onTargetEventName">Имя события приёмника.</param>
        /// <param name="targetPropertySelector">
        /// Лямбда-выражение, указывающее привязываемое свойство приёмника.
        /// </param>
        /// <param name="canAcceptTargetEvent">
        /// Фильтр, определяющий, следует ли обрабатывать событие приёмника.
        /// </param>
        /// <param name="sourcePropertyValueToTargetPropertyValueConverter">
        /// Конвертер значения свойства источника в значение свойства приёмника.
        /// </param>
        /// <param name="targetPropertyValueToSourcePropertyValueConverter">
        /// Конвертер значения свойства приёмника в значение свойства источника.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Дополнительный колбэк, вызываемый при обновлении свойства.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий разорвать привязку.
        /// </returns>
        /// <remarks>
        /// Метод является базовым низкоуровневым API и позволяет явно управлять
        /// событиями, фильтрацией и конвертацией значений.
        /// </remarks>
        public static IDisposable BindPropertiesOnEvents<TSource, TSourceProp, TSourceEventArgs, TTarget, TTargetProp, TTargetEventArgs>(
            this TSource source,
            string onSourceEventName,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            Func<TSource, TSourceEventArgs, bool> canAcceptSourceEvent,
            TTarget target,
            string onTargetEventName,
            Expression<Func<TTarget, TTargetProp>> targetPropertySelector,
            Func<TTarget, TTargetEventArgs, bool> canAcceptTargetEvent,
            Func<TSourceProp, TTargetProp> sourcePropertyValueToTargetPropertyValueConverter = null,
            Func<TTargetProp, TSourceProp> targetPropertyValueToSourcePropertyValueConverter = null,
            Action<object, PropertyChangedEventArgs> onPropertyChanged = null)
            where TSource : class
            where TTarget : class
            where TSourceEventArgs : EventArgs
            where TTargetEventArgs : EventArgs
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (sourcePropertySelector == null)
            {
                throw new ArgumentException(@"Свойство источника не может быть пустым", nameof(sourcePropertySelector));
            }

            if (targetPropertySelector == null)
            {
                throw new ArgumentException(@"Свойство приемника не может быть пустым", nameof(targetPropertySelector));
            }

            if (string.IsNullOrWhiteSpace(onSourceEventName))
            {
                throw new ArgumentException(@"Имя события источника не может быть пустым", nameof(onSourceEventName));
            }

            if (string.IsNullOrWhiteSpace(onTargetEventName))
            {
                throw new ArgumentException(@"Имя приемника источника не может быть пустым", nameof(onTargetEventName));
            }

            var srcEvent = source.GetType().GetEvent(onSourceEventName) ?? throw new ArgumentException($@"Событие '{onSourceEventName}' не найдено в типе '{source.GetType().Name}'", nameof(onSourceEventName));
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(onTargetEventName);

            if (canAcceptSourceEvent == null)
            {
                canAcceptSourceEvent = (s, e) => true;
            }

            if (canAcceptTargetEvent == null)
            {
                canAcceptTargetEvent = (s, e) => true;
            }

            if (sourcePropertyValueToTargetPropertyValueConverter == null && !typeof(TTargetProp).IsAssignableFrom(typeof(TSourceProp)))
            {
                sourcePropertyValueToTargetPropertyValueConverter = (v) => Obj.ChangeType<TTargetProp>(v);
            }

            if (targetPropertyValueToSourcePropertyValueConverter == null && !typeof(TSourceProp).IsAssignableFrom(typeof(TTargetProp)))
            {
                targetPropertyValueToSourcePropertyValueConverter = (v) => Obj.ChangeType<TSourceProp>(v);
            }

            return EventHelper.BindProperties(source, srcProp, srcEvent, canAcceptSourceEvent, target, targetProp, targetEvent, canAcceptTargetEvent, sourcePropertyValueToTargetPropertyValueConverter, targetPropertyValueToSourcePropertyValueConverter, onPropertyChanged);
        }

        /// <summary>
        /// Устанавливает привязку между свойствами источника и приёмника
        /// на основе явно указанных событий.
        /// </summary>
        /// <typeparam name="TSource">Тип источника.</typeparam>
        /// <typeparam name="TTarget">Тип приёмника.</typeparam>
        /// <typeparam name="T">Тип свойства.</typeparam>
        /// <param name="source">Объект-источник.</param>
        /// <param name="onSourceEvent">Событие источника.</param>
        /// <param name="sourcePropertySelector">Свойство источника.</param>
        /// <param name="target">Объект-приёмник.</param>
        /// <param name="onTargetEventName">Событие приёмника.</param>
        /// <param name="targetPropertySelector">Свойство приёмника.</param>
        /// <param name="valueConverter">Конвертер значения. Например, x => !x.</param>
        /// <param name="onPropertyChanged">
        /// Колбэк при изменении свойств.
        /// </param>
        /// <returns>
        /// <see cref="IDisposable"/> для отмены привязки.
        /// </returns>
        public static IDisposable BindPropertiesOnEvents<TSource, TTarget, T>(
            this TSource source,
            string onSourceEvent,
            Expression<Func<TSource, T>> sourcePropertySelector,
            TTarget target,
            string onTargetEventName,
            Expression<Func<TTarget, T>> targetPropertySelector,
            Func<T, T> valueConverter,
            Action<object, EventArgs> onPropertyChanged = null)
            where TSource : class
            where TTarget : class
        {
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var srcEvent = source.GetType().GetEvent(onSourceEvent) ?? throw new ArgumentException($@"Событие '{onSourceEvent}' не найдено в типе '{source.GetType().Name}'", nameof(onSourceEvent));
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(onTargetEventName) ?? throw new ArgumentException($@"Событие '{onTargetEventName}' не найдено в типе '{target.GetType().Name}'", nameof(onTargetEventName));
            return EventHelper.BindProperties<TSource, T, EventArgs, TTarget, T, EventArgs>(source, srcProp, srcEvent, null, target, targetProp, targetEvent, null, valueConverter, valueConverter, onPropertyChanged);
        }

        /// <summary>
        /// Устанавливает привязку между свойствами источника и приёмника
        /// на основе явно указанных событий.
        /// </summary>
        /// <typeparam name="TSource">Тип источника.</typeparam>
        /// <typeparam name="TTarget">Тип приёмника.</typeparam>
        /// <typeparam name="T">Тип свойства.</typeparam>
        /// <param name="source">Объект-источник.</param>
        /// <param name="onSourceEvent">Событие источника.</param>
        /// <param name="sourcePropertySelector">Свойство источника.</param>
        /// <param name="target">Объект-приёмник.</param>
        /// <param name="onTargetEventName">Событие приёмника.</param>
        /// <param name="targetPropertySelector">Свойство приёмника.</param>
        /// <param name="onPropertyChanged">
        /// Колбэк при изменении свойств.
        /// </param>
        /// <returns>
        /// <see cref="IDisposable"/> для отмены привязки.
        /// </returns>
        public static IDisposable BindPropertiesOnEvents<TSource, TTarget, T>(
            this TSource source,
            string onSourceEvent,
            Expression<Func<TSource, T>> sourcePropertySelector,
            TTarget target,
            string onTargetEventName,
            Expression<Func<TTarget, T>> targetPropertySelector,
            Action<object, EventArgs> onPropertyChanged = null)
            where TSource : class
            where TTarget : class
        {
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var srcEvent = source.GetType().GetEvent(onSourceEvent) ?? throw new ArgumentException($@"Событие '{onSourceEvent}' не найдено в типе '{source.GetType().Name}'", nameof(onSourceEvent));
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(onTargetEventName) ?? throw new ArgumentException($@"Событие '{onTargetEventName}' не найдено в типе '{target.GetType().Name}'", nameof(onTargetEventName));
            return EventHelper.BindProperties<TSource, T, EventArgs, TTarget, T, EventArgs>(source, srcProp, srcEvent, null, target, targetProp, targetEvent, null, null, null, onPropertyChanged);
        }

        /// <summary>
        /// Выполняет действие при изменении указанного свойства источника.
        /// </summary>
        /// <typeparam name="TSource">Тип источника.</typeparam>
        /// <typeparam name="TSourceProp">Тип свойства.</typeparam>
        /// <param name="source">Объект-источник.</param>
        /// <param name="sourcePropertySelector">Отслеживаемое свойство.</param>
        /// <param name="action">Действие, выполняемое при изменении.</param>
        /// <returns>
        /// <see cref="IDisposable"/> для управления подпиской.
        /// </returns>
        public static IDisposable BindPropertyChangeToAction<TSource, TSourceProp>(
            this TSource source,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            Action action)
            where TSource : class, INotifyPropertyChanged
        {
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var srcEvent = source.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));

            return EventHelper.BindEventToAction<TSource, PropertyChangedEventArgs>(source, srcEvent, (s, e) => action(), (s, e) => e.PropertyName == srcProp.Name);
        }

        /// <summary>
        /// Устанавливает одностороннюю привязку свойства источника на основе <see cref="INotifyPropertyChanged"/>
        /// к свойству приёмника.
        /// </summary>
        /// <typeparam name="TSource">Тип объекта-источника.</typeparam>
        /// <typeparam name="TSourceProp">Тип свойства источника.</typeparam>
        /// <typeparam name="TTarget">Тип объекта-приёмника.</typeparam>
        /// <typeparam name="TTargetProp">Тип свойства приёмника.</typeparam>
        /// <param name="source">Объект-источник.</param>
        /// <param name="sourcePropertySelector">Привязываемое свойство источника.</param>
        /// <param name="target">Объект-приёмник.</param>
        /// <param name="targetPropertySelector">Привязываемое свойство приёмника.</param>
        /// <param name="sourcePropertyValueToTargetPropertyValueConverter">
        /// Конвертер значения свойства источника.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Колбэк, вызываемый при изменении свойства.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/> для управления жизненным циклом привязки.
        /// </returns>
        public static IDisposable BindToProperty<TSource, TSourceProp, TTarget, TTargetProp>(
            this TSource source,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            TTarget target,
            Expression<Func<TTarget, TTargetProp>> targetPropertySelector,
            Func<TSourceProp, TTargetProp> sourcePropertyValueToTargetPropertyValueConverter,
            Action<object, PropertyChangedEventArgs> onPropertyChanged = null)
            where TSource : class, INotifyPropertyChanged
            where TTarget : class
        {
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var srcEvent = source.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            var targetProp = targetPropertySelector.GetPropertyInfo();

            return EventHelper.BindProperties<TSource, TSourceProp, PropertyChangedEventArgs, TTarget, TTargetProp, EventArgs>(source, srcProp, srcEvent, (s, e) => e.PropertyName == srcProp.Name, target, targetProp, null, null, sourcePropertyValueToTargetPropertyValueConverter, null, onPropertyChanged);
        }

        /// <summary>
        /// Устанавливает двустороннюю привязку между свойствами источника
        /// и приёмника, использующими <see cref="INotifyPropertyChanged"/>.
        /// </summary>
        /// <typeparam name="TSource">Тип объекта-источника.</typeparam>
        /// <typeparam name="TTarget">Тип объекта-приёмника.</typeparam>
        /// <typeparam name="T">Тип привязываемого свойства.</typeparam>
        /// <param name="source">Объект-источник.</param>
        /// <param name="sourcePropertySelector">Свойство источника.</param>
        /// <param name="target">Объект-приёмник.</param>
        /// <param name="targetPropertySelector">Свойство приёмника.</param>
        /// <param name="onPropertyChanged">
        /// Колбэк, вызываемый при синхронизации свойств.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отменить привязку.
        /// </returns>
        /// <remarks>
        /// Изменения любого из свойств автоматически синхронизируются
        /// с противоположной стороной.
        /// </remarks>
        public static IDisposable BindToProperty<TSource, TTarget, T>(
            this TSource source,
            Expression<Func<TSource, T>> sourcePropertySelector,
            TTarget target,
            Expression<Func<TTarget, T>> targetPropertySelector,
            Action<object, PropertyChangedEventArgs> onPropertyChanged = null)
            where TSource : class, INotifyPropertyChanged
            where TTarget : class
        {
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var srcEvent = source.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            var targetProp = targetPropertySelector.GetPropertyInfo();
            return EventHelper.BindProperties<TSource, T, PropertyChangedEventArgs, TTarget, T, EventArgs>(source, srcProp, srcEvent, (s, e) => e.PropertyName == srcProp.Name, target, targetProp, null, null, null, null, onPropertyChanged);
        }

        /// <summary>
        /// Устанавливает одностороннюю привязку свойства источника
        /// к свойству приёмника на основе указанного события источника.
        /// </summary>
        /// <typeparam name="TSource">Тип источника.</typeparam>
        /// <typeparam name="TSourceProp">Тип свойства источника.</typeparam>
        /// <typeparam name="TTarget">Тип приёмника.</typeparam>
        /// <typeparam name="TTargetProp">Тип свойства приёмника.</typeparam>
        /// <param name="source">Объект-источник.</param>
        /// <param name="onSourceEventName">Имя события источника.</param>
        /// <param name="sourcePropertySelector">Свойство источника.</param>
        /// <param name="target">Объект-приёмник.</param>
        /// <param name="targetPropertySelector">Свойство приёмника.</param>
        /// <param name="sourcePropertyValueToTargetPropertyValueConverter">
        /// Конвертер значения свойства.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Колбэк при обновлении свойства.
        /// </param>
        /// <returns>
        /// <see cref="IDisposable"/> для управления подпиской.
        /// </returns>
        public static IDisposable BindToPropertyOnEvent<TSource, TSourceProp, TTarget, TTargetProp>(
            this TSource source,
            string onSourceEventName,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            TTarget target,
            Expression<Func<TTarget, TTargetProp>> targetPropertySelector,
            Func<TSourceProp, TTargetProp> sourcePropertyValueToTargetPropertyValueConverter = null,
            Action<object, EventArgs> onPropertyChanged = null)
            where TSource : class
            where TTarget : class
        {
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var srcEvent = source.GetType().GetEvent(onSourceEventName) ?? throw new ArgumentException($@"Событие '{onSourceEventName}' не найдено в типе '{source.GetType().Name}'", nameof(onSourceEventName));
            var targetProp = targetPropertySelector.GetPropertyInfo();

            return EventHelper.BindProperties<TSource, TSourceProp, EventArgs, TTarget, TTargetProp, EventArgs>(source, srcProp, srcEvent, (s, e) => true, target, targetProp, null, null, sourcePropertyValueToTargetPropertyValueConverter, null, onPropertyChanged);
        }
    }
}