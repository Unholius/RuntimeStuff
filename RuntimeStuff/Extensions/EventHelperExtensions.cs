// <copyright file="EventHelperExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Extensions
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq.Expressions;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Class EventHelperExtensions.
    /// </summary>
    public static class EventHelperExtensions
    {
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
        public static IDisposable Bind<TSource, TSourceProp, TSourceEventArgs, TTarget, TTargetProp, TTargetEventArgs>(
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
                throw new ArgumentNullException(nameof(source));

            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (sourcePropertySelector == null)
                throw new ArgumentException(@"Свойство источника не может быть пустым", nameof(sourcePropertySelector));

            if (targetPropertySelector == null)
                throw new ArgumentException(@"Свойство приемника не может быть пустым", nameof(targetPropertySelector));

            if (string.IsNullOrWhiteSpace(onSourceEventName))
                throw new ArgumentException(@"Имя события источника не может быть пустым", nameof(onSourceEventName));

            if (string.IsNullOrWhiteSpace(onTargetEventName))
                throw new ArgumentException(@"Имя приемника источника не может быть пустым", nameof(onTargetEventName));

            var srcEvent = source.GetType().GetEvent(onSourceEventName) ?? throw new ArgumentException($@"Событие '{onSourceEventName}' не найдено в типе '{source.GetType().Name}'", nameof(onSourceEventName));
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(onTargetEventName);

            if (canAcceptSourceEvent == null)
                canAcceptSourceEvent = (s, e) => true;

            if (canAcceptTargetEvent == null)
                canAcceptTargetEvent = (s, e) => true;

            if (sourcePropertyValueToTargetPropertyValueConverter == null && !typeof(TTargetProp).IsAssignableFrom(typeof(TSourceProp)))
                sourcePropertyValueToTargetPropertyValueConverter = (v) => Obj.ChangeType<TTargetProp>(v);

            if (targetPropertyValueToSourcePropertyValueConverter == null && !typeof(TSourceProp).IsAssignableFrom(typeof(TTargetProp)))
                targetPropertyValueToSourcePropertyValueConverter = (v) => Obj.ChangeType<TSourceProp>(v);

            return EventHelper.BindProperties(source, srcProp, srcEvent, canAcceptSourceEvent, target, targetProp, targetEvent, canAcceptTargetEvent, sourcePropertyValueToTargetPropertyValueConverter, targetPropertyValueToSourcePropertyValueConverter, onPropertyChanged);
        }

        /// <summary>
        /// Устанавливает привязку между свойством источника и свойствами
        /// нескольких объектов-приёмников на основе указанных событий.
        /// </summary>
        /// <typeparam name="TSource">Тип объекта-источника.</typeparam>
        /// <typeparam name="TSourceProp">Тип свойства источника.</typeparam>
        /// <typeparam name="TSourceEventArgs">Тип аргументов события источника.</typeparam>
        /// <typeparam name="TTarget">Тип объектов-приёмников.</typeparam>
        /// <typeparam name="TTargetProp">Тип свойства приёмников.</typeparam>
        /// <typeparam name="TTargetEventArgs">Тип аргументов события приёмников.</typeparam>
        /// <param name="source">Объект-источник.</param>
        /// <param name="onSourceEventName">
        /// Имя события источника, инициирующего синхронизацию.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Лямбда-выражение, указывающее привязываемое свойство источника.
        /// </param>
        /// <param name="canAcceptSourceEvent">
        /// Фильтр, определяющий, следует ли обрабатывать событие источника.
        /// </param>
        /// <param name="targets">
        /// Коллекция объектов-приёмников, свойства которых синхронизируются с источником.
        /// </param>
        /// <param name="onTargetEventName">
        /// Имя события приёмников.
        /// </param>
        /// <param name="targetPropertySelector">
        /// Лямбда-выражение, указывающее привязываемое свойство приёмников.
        /// </param>
        /// <param name="canAcceptTargetEvent">
        /// Фильтр, определяющий, следует ли обрабатывать событие приёмников.
        /// </param>
        /// <param name="sourcePropertyValueToTargetPropertyValueConverter">
        /// Конвертер значения свойства источника в значение свойства приёмников.
        /// </param>
        /// <param name="targetPropertyValueToSourcePropertyValueConverter">
        /// Конвертер значения свойства приёмников в значение свойства источника.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Дополнительный колбэк, вызываемый при обновлении свойств.
        /// </param>
        /// <returns>
        /// Последовательность объектов <see cref="IDisposable"/>,
        /// каждый из которых управляет жизненным циклом отдельной привязки.
        /// </returns>
        /// <remarks>
        /// Метод создаёт отдельную привязку для каждого объекта-приёмника.
        /// Последовательность формируется лениво с использованием
        /// <c>yield return</c>.
        /// </remarks>
        public static IEnumerable<IDisposable> Bind<TSource, TSourceProp, TSourceEventArgs, TTarget, TTargetProp, TTargetEventArgs>(
            this TSource source,
            string onSourceEventName,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            Func<TSource, TSourceEventArgs, bool> canAcceptSourceEvent,
            IEnumerable<TTarget> targets,
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
            foreach (var target in targets)
            {
                yield return source.Bind(
                    onSourceEventName,
                    sourcePropertySelector,
                    canAcceptSourceEvent,
                    target,
                    onTargetEventName,
                    targetPropertySelector,
                    canAcceptTargetEvent,
                    sourcePropertyValueToTargetPropertyValueConverter,
                    targetPropertyValueToSourcePropertyValueConverter,
                    onPropertyChanged);
            }
        }

        /// <summary>
        /// Устанавливает одностороннюю привязку свойства источника
        /// к свойству приёмника на основе <see cref="INotifyPropertyChanged"/>.
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
        public static IDisposable Bind<TSource, TSourceProp, TTarget, TTargetProp>(
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
        /// Устанавливает одностороннюю привязку свойства источника
        /// к одному и тому же свойству нескольких объектов-приёмников
        /// на основе <see cref="INotifyPropertyChanged"/>.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника, реализующего <see cref="INotifyPropertyChanged"/>.
        /// </typeparam>
        /// <typeparam name="TSourceProp">
        /// Тип свойства источника.
        /// </typeparam>
        /// <typeparam name="TTarget">
        /// Тип объектов-приёмников.
        /// </typeparam>
        /// <typeparam name="TTargetProp">
        /// Тип свойства приёмников.
        /// </typeparam>
        /// <param name="source">
        /// Объект-источник, изменения свойства которого отслеживаются.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Лямбда-выражение, указывающее привязываемое свойство источника.
        /// </param>
        /// <param name="targets">
        /// Коллекция объектов-приёмников, свойства которых будут синхронизированы
        /// со свойством источника.
        /// </param>
        /// <param name="targetPropertySelector">
        /// Лямбда-выражение, указывающее привязываемое свойство приёмников.
        /// </param>
        /// <param name="sourcePropertyValueToTargetPropertyValueConverter">
        /// Конвертер значения свойства источника в значение свойства приёмников.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Дополнительный колбэк, вызываемый при обновлении свойства.
        /// </param>
        /// <returns>
        /// Последовательность объектов <see cref="IDisposable"/>,
        /// каждый из которых управляет жизненным циклом отдельной привязки
        /// между источником и соответствующим приёмником.
        /// </returns>
        /// <remarks>
        /// Для каждого объекта из коллекции <paramref name="targets"/> создаётся
        /// независимая односторонняя привязка.
        /// Последовательность формируется лениво с использованием
        /// <c>yield return</c>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="source"/>,
        /// <paramref name="sourcePropertySelector"/>,
        /// <paramref name="targets"/> или
        /// <paramref name="targetPropertySelector"/> равны <c>null</c>.
        /// </exception>
        public static IEnumerable<IDisposable> Bind<TSource, TSourceProp, TTarget, TTargetProp>(
            this TSource source,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            IEnumerable<TTarget> targets,
            Expression<Func<TTarget, TTargetProp>> targetPropertySelector,
            Func<TSourceProp, TTargetProp> sourcePropertyValueToTargetPropertyValueConverter,
            Action<object, PropertyChangedEventArgs> onPropertyChanged = null)
            where TSource : class, INotifyPropertyChanged
            where TTarget : class
        {
            foreach (var target in targets)
            {
                yield return source.Bind(
                    sourcePropertySelector,
                    target,
                    targetPropertySelector,
                    sourcePropertyValueToTargetPropertyValueConverter,
                    onPropertyChanged);
            }
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
        public static IDisposable Bind<TSource, TTarget, T>(
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
            var targetEvent = target.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));

            return EventHelper.BindProperties<TSource, T, PropertyChangedEventArgs, TTarget, T, PropertyChangedEventArgs>(source, srcProp, srcEvent, (s, e) => e.PropertyName == srcProp.Name, target, targetProp, targetEvent, (s, e) => e.PropertyName == targetProp.Name, null, null, onPropertyChanged);
        }

        /// <summary>
        /// Устанавливает двустороннюю привязку одного и того же свойства
        /// объекта-источника к соответствующему свойству нескольких объектов-приёмников
        /// на основе <see cref="INotifyPropertyChanged"/>.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника, реализующего <see cref="INotifyPropertyChanged"/>.
        /// </typeparam>
        /// <typeparam name="TTarget">
        /// Тип объектов-приёмников.
        /// </typeparam>
        /// <typeparam name="T">
        /// Тип привязываемого свойства.
        /// </typeparam>
        /// <param name="source">
        /// Объект-источник, свойства которого отслеживаются.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Лямбда-выражение, указывающее привязываемое свойство источника.
        /// </param>
        /// <param name="targets">
        /// Коллекция объектов-приёмников, свойства которых участвуют в привязке.
        /// </param>
        /// <param name="targetPropertySelector">
        /// Лямбда-выражение, указывающее привязываемое свойство приёмников.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Дополнительный колбэк, вызываемый при синхронизации свойств.
        /// </param>
        /// <returns>
        /// Последовательность объектов <see cref="IDisposable"/>,
        /// каждый из которых управляет жизненным циклом отдельной привязки.
        /// </returns>
        /// <remarks>
        /// Для каждого объекта из коллекции <paramref name="targets"/> создаётся
        /// независимая двусторонняя привязка.
        /// Последовательность формируется лениво с использованием
        /// <c>yield return</c>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="source"/>,
        /// <paramref name="sourcePropertySelector"/>,
        /// <paramref name="targets"/> или
        /// <paramref name="targetPropertySelector"/> равны <c>null</c>.
        /// </exception>
        public static IEnumerable<IDisposable> Bind<TSource, TTarget, T>(
            this TSource source,
            Expression<Func<TSource, T>> sourcePropertySelector,
            IEnumerable<TTarget> targets,
            Expression<Func<TTarget, T>> targetPropertySelector,
            Action<object, PropertyChangedEventArgs> onPropertyChanged = null)
            where TSource : class, INotifyPropertyChanged
            where TTarget : class
        {
            foreach (var target in targets)
            {
                yield return source.Bind(
                    sourcePropertySelector,
                    target,
                    targetPropertySelector,
                    onPropertyChanged);
            }
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
        public static IDisposable Bind<TSource, TSourceProp, TTarget, TTargetProp>(
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

        /// <summary>
        /// Устанавливает одностороннюю привязку свойства источника
        /// к одному и тому же свойству нескольких объектов-приёмников
        /// на основе указанного события источника.
        /// </summary>
        /// <typeparam name="TSource">Тип объекта-источника.</typeparam>
        /// <typeparam name="TSourceProp">Тип свойства источника.</typeparam>
        /// <typeparam name="TTarget">Тип объектов-приёмников.</typeparam>
        /// <typeparam name="TTargetProp">Тип свойства приёмников.</typeparam>
        /// <param name="source">
        /// Объект-источник, изменения которого инициируют синхронизацию.
        /// </param>
        /// <param name="onSourceEventName">
        /// Имя события источника, при возникновении которого выполняется обновление.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Лямбда-выражение, указывающее привязываемое свойство источника.
        /// </param>
        /// <param name="targets">
        /// Коллекция объектов-приёмников, свойства которых синхронизируются
        /// со свойством источника.
        /// </param>
        /// <param name="targetPropertySelector">
        /// Лямбда-выражение, указывающее привязываемое свойство приёмников.
        /// </param>
        /// <param name="sourcePropertyValueToTargetPropertyValueConverter">
        /// Конвертер значения свойства источника в значение свойства приёмников.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Дополнительный колбэк, вызываемый после обновления свойства.
        /// </param>
        /// <returns>
        /// Последовательность объектов <see cref="IDisposable"/>,
        /// каждый из которых управляет жизненным циклом отдельной привязки.
        /// </returns>
        /// <remarks>
        /// Для каждого объекта из коллекции <paramref name="targets"/> создаётся
        /// независимая односторонняя привязка.
        /// Последовательность формируется лениво с использованием
        /// <c>yield return</c>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="source"/>,
        /// <paramref name="onSourceEventName"/>,
        /// <paramref name="sourcePropertySelector"/>,
        /// <paramref name="targets"/> или
        /// <paramref name="targetPropertySelector"/> равны <c>null</c>.
        /// </exception>
        public static IEnumerable<IDisposable> Bind<TSource, TSourceProp, TTarget, TTargetProp>(
            this TSource source,
            string onSourceEventName,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            IEnumerable<TTarget> targets,
            Expression<Func<TTarget, TTargetProp>> targetPropertySelector,
            Func<TSourceProp, TTargetProp> sourcePropertyValueToTargetPropertyValueConverter = null,
            Action<object, EventArgs> onPropertyChanged = null)
            where TSource : class
            where TTarget : class
        {
            foreach (var target in targets)
            {
                yield return source.Bind(
                    onSourceEventName,
                    sourcePropertySelector,
                    target,
                    targetPropertySelector,
                    sourcePropertyValueToTargetPropertyValueConverter,
                    onPropertyChanged);
            }
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
        public static IDisposable Bind<TSource, TTarget, T>(
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
        /// Устанавливает двустороннюю привязку одинаковых по типу свойств
        /// между источником и несколькими объектами-приёмниками
        /// на основе указанных событий источника и приёмников.
        /// </summary>
        /// <typeparam name="TSource">Тип объекта-источника.</typeparam>
        /// <typeparam name="TTarget">Тип объектов-приёмников.</typeparam>
        /// <typeparam name="T">Тип привязываемого свойства.</typeparam>
        /// <param name="source">
        /// Объект-источник, участвующий в двусторонней синхронизации.
        /// </param>
        /// <param name="onSourceEvent">
        /// Имя события источника, инициирующего обновление приёмников.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Лямбда-выражение, указывающее привязываемое свойство источника.
        /// </param>
        /// <param name="targets">
        /// Коллекция объектов-приёмников, участвующих в синхронизации.
        /// </param>
        /// <param name="onTargetEventName">
        /// Имя события приёмников, инициирующего обновление источника.
        /// </param>
        /// <param name="targetPropertySelector">
        /// Лямбда-выражение, указывающее привязываемое свойство приёмников.
        /// </param>
        /// <param name="onPropertyChanged">
        /// Дополнительный колбэк, вызываемый после изменения любого
        /// из привязанных свойств.
        /// </param>
        /// <returns>
        /// Последовательность объектов <see cref="IDisposable"/>,
        /// каждый из которых управляет жизненным циклом отдельной двусторонней привязки.
        /// </returns>
        /// <remarks>
        /// Для каждого объекта из коллекции <paramref name="targets"/> создаётся
        /// независимая двусторонняя привязка.
        /// Последовательность формируется лениво с использованием
        /// <c>yield return</c>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="source"/>,
        /// <paramref name="onSourceEvent"/>,
        /// <paramref name="sourcePropertySelector"/>,
        /// <paramref name="targets"/>,
        /// <paramref name="onTargetEventName"/> или
        /// <paramref name="targetPropertySelector"/> равны <c>null</c>.
        /// </exception>
        public static IEnumerable<IDisposable> Bind<TSource, TTarget, T>(
            this TSource source,
            string onSourceEvent,
            Expression<Func<TSource, T>> sourcePropertySelector,
            IEnumerable<TTarget> targets,
            string onTargetEventName,
            Expression<Func<TTarget, T>> targetPropertySelector,
            Action<object, EventArgs> onPropertyChanged = null)
            where TSource : class
            where TTarget : class
        {
            foreach (var target in targets)
            {
                yield return source.Bind(
                    onSourceEvent,
                    sourcePropertySelector,
                    target,
                    onTargetEventName,
                    targetPropertySelector,
                    onPropertyChanged);
            }
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
        public static IDisposable Bind<TSource>(
            this TSource source,
            string onSourceEventName,
            Action<object, object> action,
            Func<bool> canExecuteAction = null)
            where TSource : class
        {
            var srcEvent = source.GetType().GetEvent(onSourceEventName) ?? throw new ArgumentException($@"Событие '{onSourceEventName}' не найдено в типе '{source.GetType().Name}'", nameof(onSourceEventName));
            if (canExecuteAction == null)
                canExecuteAction = () => true;
            return EventHelper.BindEventToAction(source, srcEvent, action, (s, e) => canExecuteAction());
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
        public static IDisposable Bind<TSource, TSourceProp>(
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
        public static IDisposable Bind<TSource>(
            this TSource source,
            Action<object, object> action)
        where TSource : class, INotifyCollectionChanged
        {
            var srcEvent = source.GetType().GetEvent(nameof(INotifyCollectionChanged.CollectionChanged));

            return EventHelper.BindEventToAction<TSource, NotifyCollectionChangedEventArgs>(source, srcEvent, action);
        }
    }
}