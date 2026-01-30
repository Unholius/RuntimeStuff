namespace RuntimeStuff.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq.Expressions;
    using System.Reflection;
    using RuntimeStuff.Helpers;

    public static class EventHelperExtensions
    {
        public static IDisposable Bind<TSource, TSourceProp, TSourceEventArgs, TTarget, TTargetProp, TTargetEventArgs>(
            this TSource source,
            string changeTargetPropertyOnSourceEventName,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            Func<TSource, TSourceEventArgs, bool> canAcceptSourceEvent,
            TTarget target,
            string changeSourcePropertyOnTargetEventName,
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

            if (string.IsNullOrWhiteSpace(changeTargetPropertyOnSourceEventName))
                throw new ArgumentException(@"Имя события источника не может быть пустым", nameof(changeTargetPropertyOnSourceEventName));

            if (string.IsNullOrWhiteSpace(changeSourcePropertyOnTargetEventName))
                throw new ArgumentException(@"Имя приемника источника не может быть пустым", nameof(changeSourcePropertyOnTargetEventName));

            var srcEvent = source.GetType().GetEvent(changeTargetPropertyOnSourceEventName) ?? throw new ArgumentException($@"Событие '{changeTargetPropertyOnSourceEventName}' не найдено в типе '{source.GetType().Name}'", nameof(changeTargetPropertyOnSourceEventName));
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var targetProp = targetPropertySelector.GetPropertyInfo();
            var targetEvent = target.GetType().GetEvent(changeSourcePropertyOnTargetEventName);

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

        public static IDisposable Bind<TSource, TSourceProp, TTarget, TTargetProp>(
            this TSource source,
            string changeTargetPropertyOnSourceEventName,
            Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
            TTarget target,
            Expression<Func<TTarget, TTargetProp>> targetPropertySelector,
            Func<TSourceProp, TTargetProp> sourcePropertyValueToTargetPropertyValueConverter = null,
            Action<object, EventArgs> onPropertyChanged = null)
            where TSource : class
            where TTarget : class
        {
            var srcProp = sourcePropertySelector.GetPropertyInfo();
            var srcEvent = source.GetType().GetEvent(changeTargetPropertyOnSourceEventName) ?? throw new ArgumentException($@"Событие '{changeTargetPropertyOnSourceEventName}' не найдено в типе '{source.GetType().Name}'", nameof(changeTargetPropertyOnSourceEventName));
            var targetProp = targetPropertySelector.GetPropertyInfo();

            return EventHelper.BindProperties<TSource, TSourceProp, EventArgs, TTarget, TTargetProp, EventArgs>(source, srcProp, srcEvent, (s, e) => true, target, targetProp, null, null, sourcePropertyValueToTargetPropertyValueConverter, null, onPropertyChanged);
        }

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

        public static IDisposable Bind<TSource>(
            this TSource source,
            Action<object, object> action)
        where TSource : class, INotifyCollectionChanged
        {
            var srcEvent = source.GetType().GetEvent(nameof(INotifyCollectionChanged.CollectionChanged));

            return EventHelper.BindEventToAction<TSource, NotifyCollectionChangedEventArgs>(source, srcEvent, action);
        }

        //public static IDisposable Bind<TSource, TSourceProp, TTarget, TTargetProp>(
        //    this TSource source,
        //    Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
        //    string sourceEventName,
        //    TTarget target,
        //    Expression<Func<TTarget, TTargetProp>> targetPropertySelector,
        //    string targetEventName,
        //    Func<TSourceProp, TTargetProp> sourceToTargetConverter = null,
        //    Func<TTargetProp, TSourceProp> targetToSourceConverter = null,
        //    Func<TSource, EventArgs, bool> canAcceptSourceEvent = null,
        //    Func<TTarget, EventArgs, bool> canAcceptTargetEvent = null,
        //    Action onPropertyChanged = null)
        //    where TSource : class
        //    where TTarget : class
        //{
        //    if (source == null)
        //        throw new ArgumentNullException(nameof(source));
        //    if (target == null)
        //        throw new ArgumentNullException(nameof(target));
        //    if (string.IsNullOrWhiteSpace(sourceEventName))
        //        throw new ArgumentException(@"Имя события источника не может быть пустым", nameof(sourceEventName));

        //    var srcEvent = source.GetType().GetEvent(sourceEventName) ?? throw new ArgumentException($@"Событие '{sourceEventName}' не найдено в типе '{source.GetType().Name}'", nameof(sourceEventName));
        //    var srcProp = sourcePropertySelector.GetPropertyInfo();
        //    var targetProp = targetPropertySelector?.GetPropertyInfo();
        //    var targetEvent = target.GetType().GetEvent(targetEventName);
        //    return EventHelper.BindProperties(source, srcProp, srcEvent, canAcceptSourceEvent, target, targetProp, targetEvent, canAcceptTargetEvent, sourceToTargetConverter, targetToSourceConverter);
        //}

        //public static IDisposable Bind<TSource, TSourceProp, TTarget, TTargetProp>(
        //    this TSource source,
        //    Expression<Func<TSource, TSourceProp>> sourcePropertySelector,
        //    string sourceEventName,
        //    TTarget target,
        //    Expression<Func<TTarget, TTargetProp>> targetPropertySelector,
        //    string targetEventName,
        //    Func<TSourceProp, TTargetProp> sourceToTargetConverter = null,
        //    Func<TTargetProp, TSourceProp> targetToSourceConverter = null,
        //    Func<TSource, PropertyChangedEventArgs, bool> canAcceptSourceEvent = null,
        //    Func<TTarget, PropertyChangedEventArgs, bool> canAcceptTargetEvent = null,
        //    Action onPropertyChanged = null)
        //    where TSource : class, INotifyPropertyChanged
        //    where TTarget : class, INotifyPropertyChanged
        //{
        //    if (source == null)
        //        throw new ArgumentNullException(nameof(source));
        //    if (target == null)
        //        throw new ArgumentNullException(nameof(target));
        //    if (string.IsNullOrWhiteSpace(sourceEventName))
        //        throw new ArgumentException(@"Имя события источника не может быть пустым", nameof(sourceEventName));
        //    if (string.IsNullOrWhiteSpace(targetEventName))
        //        throw new ArgumentException(@"Имя события приёмника не может быть пустым", nameof(targetEventName));

        //    var srcEvent = source.GetType().GetEvent(sourceEventName) ?? throw new ArgumentException($@"Событие '{sourceEventName}' не найдено в типе '{source.GetType().Name}'", nameof(sourceEventName));
        //    var srcProp = sourcePropertySelector.GetPropertyInfo();
        //    var targetProp = targetPropertySelector.GetPropertyInfo();
        //    var targetEvent = target.GetType().GetEvent(targetEventName);
        //    return EventHelper.BindProperties(source, srcProp, srcEvent, canAcceptSourceEvent, target, targetProp, targetEvent, canAcceptTargetEvent, sourceToTargetConverter, targetToSourceConverter);
        //}

        //public static IDisposable Bind<TSource, TTarget, T>(
        //    this TSource source,
        //    Expression<Func<TSource, T>> sourcePropertySelector,
        //    TTarget target,
        //    Expression<Func<TTarget, T>> targetPropertySelector,
        //    Action<object, PropertyChangedEventArgs> onPropertyChanged = null)
        //    where TSource : class, INotifyPropertyChanged
        //    where TTarget : class
        //{
        //    var srcEvent = source.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
        //    var srcProp = sourcePropertySelector.GetPropertyInfo();
        //    var targetProp = targetPropertySelector.GetPropertyInfo();
        //    return EventHelper.BindProperties<TSource, T, PropertyChangedEventArgs, TTarget, T, EventArgs>(source, srcProp, srcEvent, (s, e) => e.PropertyName == targetProp.Name, target, targetProp, null, null, null, null, onPropertyChanged);
        //}

        //public static IEnumerable<IDisposable> Bind<T>(
        //    this IEnumerable<T> list,
        //    string eventName,
        //    Action action)
        //    where T : class
        //{
        //    foreach (var item in list)
        //    {
        //        yield return Bind<object, object>(item, eventName, (s, e) => action());
        //    }
        //}

        //public static IEnumerable<IDisposable> Bind<T>(
        //    this IEnumerable<T> list,
        //    string eventName,
        //    Action<T, EventArgs> action)
        //    where T : class
        //{
        //    foreach (var item in list)
        //    {
        //        yield return Bind<T, EventArgs>(item, eventName, action);
        //    }
        //}

        //public static IDisposable Bind<T>(
        //    this T obj,
        //    Action<T, NotifyCollectionChangedEventArgs> action)
        //    where T : class
        //{
        //    var eventName = nameof(INotifyCollectionChanged.CollectionChanged);
        //    return Bind(obj, eventName, action);
        //}

        //public static IDisposable Bind<T, TArgs>(
        //    this T obj,
        //    string eventName,
        //    Action<T, TArgs> action,
        //    Func<T, TArgs, bool> canExecuteAction = null)
        //{
        //    var eventInfo = obj.GetType().GetEvent(eventName);
        //    return eventInfo == null
        //        ? throw new ArgumentException($@"Событие '{eventName}' не найдено в типе '{obj.GetType().Name}'", nameof(eventName))
        //        : EventHelper.BindEventToAction(obj, eventInfo, action, canExecuteAction);
        //}

        //public static IDisposable Bind<T>(
        //    this T obj,
        //    string eventName,
        //    Action<T, EventArgs> action,
        //    Func<T, EventArgs, bool> canExecuteAction = null)
        //{
        //    var eventInfo = obj.GetType().GetEvent(eventName);
        //    return eventInfo == null
        //        ? throw new ArgumentException($@"Событие '{eventName}' не найдено в типе '{obj.GetType().Name}'", nameof(eventName))
        //        : EventHelper.BindEventToAction(obj, eventInfo, action, canExecuteAction);
        //}

        //public static IDisposable Bind<T>(
        //    this T obj,
        //    Expression<Func<T, object>> propertySelector,
        //    Action<T, PropertyChangedEventArgs> action,
        //    Func<T, PropertyChangedEventArgs, bool> canExecuteAction = null)
        //    where T : class
        //{
        //    var eventName = nameof(INotifyPropertyChanged.PropertyChanged);
        //    var propertyInfo = propertySelector.GetPropertyInfo();
        //    return Bind<T, object, PropertyChangedEventArgs, object, object, EventArgs>(obj, propertySelector, eventName, canExecuteAction, null, null, null, null, null, null, () => action(obj, new PropertyChangedEventArgs(propertyInfo.Name)));
        //}

        public static void Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, TSource eventSource, EventInfo sourceEvent, Action<TSubscriber, TSource> action)
            where TSubscriber : class
            where TSource : class
        {
            EventHelper.BindEventToAction<TSource, object>(eventSource, sourceEvent, (s, e) => action(subscriber, s));
        }

        public static IDisposable Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, TSource eventSource, string sourceEventName, Action<TSubscriber, TSource, EventArgs> action)
            where TSubscriber : class
            where TSource : class
        {
            var sourceEvent = eventSource.GetType().GetEvent(sourceEventName);
            return EventHelper.BindEventToAction<TSource, EventArgs>(eventSource, sourceEvent, (s, e) => action(subscriber, s, e));
        }

        public static IDisposable Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, TSource eventSource, string sourceEventName, Action action)
            where TSubscriber : class
            where TSource : class
        {
            var sourceEvent = eventSource.GetType().GetEvent(sourceEventName);
            return EventHelper.BindEventToAction<TSource, EventArgs>(eventSource, sourceEvent, (s, e) => action());
        }

        public static IDisposable Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, TSource eventSource, Action<TSubscriber, TSource, PropertyChangedEventArgs> action)
            where TSubscriber : class
            where TSource : INotifyPropertyChanged
        {
            var sourceEvent = eventSource.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            return EventHelper.BindEventToAction<TSource, PropertyChangedEventArgs>(eventSource, sourceEvent, (s, e) => action(subscriber, s, e));
        }

        public static IDisposable Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, TSource eventSource, Action action)
            where TSubscriber : class
            where TSource : INotifyPropertyChanged
        {
            var sourceEvent = eventSource.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            return EventHelper.BindEventToAction<TSource, PropertyChangedEventArgs>(eventSource, sourceEvent, (s, e) => action());
        }

        public static IEnumerable<IDisposable> Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, IEnumerable<TSource> eventSources, Action action)
            where TSubscriber : class
            where TSource : INotifyPropertyChanged
        {
            foreach (var eventSource in eventSources)
            {
                yield return Subscribe(subscriber, eventSource, action);
            }
        }

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