// <copyright file="EventHelperExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Extensions
{
    using System;
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
    /// <item><description>подписываться на события по имени или <see cref="System.Reflection.EventInfo"/>;</description></item>
    /// <item><description>адаптировать события с произвольной сигнатурой
    /// к унифицированному обработчику вида <c>Action&lt;object, object&gt;</c>;</description></item>
    /// <item><description>управлять временем жизни подписки через <see cref="IDisposable"/>.</description></item>
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
        /// Привязывает обработчик к событию объекта по имени события.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, содержащего событие.
        /// </typeparam>
        /// <typeparam name="TArgs">
        /// Тип аргумента событя.
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
        public static IDisposable BindEventToAction<T, TArgs>(
            this T obj,
            string eventName,
            Action<T, TArgs> action)
            where T : class
        {
            var eventInfo = obj.GetType().GetEvent(eventName);
            return eventInfo == null
                ? throw new ArgumentException($"Событие '{eventName}' не найдено в типе '{obj.GetType().Name}'", nameof(eventName))
                : EventHelper.BindEventToAction<T, TArgs>(obj, eventInfo, action);
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
        public static IDisposable BindEventToAction<T>(
            this T obj,
            string eventName,
            Action<T, EventArgs> action)
            where T : class
        {
            var eventInfo = obj.GetType().GetEvent(eventName);
            return eventInfo == null
                ? throw new ArgumentException($"Событие '{eventName}' не найдено в типе '{obj.GetType().Name}'", nameof(eventName))
                : EventHelper.BindEventToAction(obj, eventInfo, action);
        }

        /// <summary>
        /// Привязывает указанное действие без аргументов к событию объекта по имени события.
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
        /// Действие, которое будет выполнено при срабатывании события.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отписаться от события
        /// и освободить связанные ресурсы.
        /// </returns>
        /// <remarks>
        /// Метод использует рефлексию для поиска события по имени и привязывает его
        /// к указанному действию, игнорируя аргументы события. Является удобной
        /// перегрузкой для случаев, когда обработчик события не использует параметры
        /// <see cref="EventArgs"/> или источник события.
        /// </remarks>
        public static IDisposable BindEventToAction<T>(
            this T obj,
            string eventName,
            Action action)
            where T : class
        {
            var eventInfo = obj.GetType().GetEvent(eventName);
            return eventInfo == null
                ? throw new ArgumentException($"Событие '{eventName}' не найдено в типе '{obj.GetType().Name}'", nameof(eventName))
                : EventHelper.BindEventToAction<T, object>(obj, eventInfo, (s, e) => action());
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
        /// <paramref name="eventInfo"/> или <paramref name="action"/> равны <c>null</c>.
        /// </exception>
        public static IDisposable BindEventToAction<T>(
            this T obj,
            EventInfo eventInfo,
            Action<object, object> action)
            where T : class
        {
            return EventHelper.BindEventToAction<T, EventArgs>(obj, eventInfo, action);
        }

        /// <summary>
        /// Связывает указанное свойство объекта-источника со свойством объекта-приёмника,
        /// синхронизируя их значения при изменении.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника, реализующего <see cref="INotifyPropertyChanged"/>.
        /// </typeparam>
        /// <typeparam name="TDest">
        /// Тип объекта-приёмника, реализующего <see cref="INotifyPropertyChanged"/>.
        /// </typeparam>
        /// <param name="source">
        /// Объект-источник, изменения свойства которого будут отслеживаться.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Выражение, указывающее связываемое свойство объекта-источника.
        /// </param>
        /// <param name="dest">
        /// Объект-приёмник, свойство которого будет обновляться при изменении источника.
        /// </param>
        /// <param name="destPropertySelector">
        /// Выражение, указывающее связываемое свойство объекта-приёмника.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий разорвать связь и отписаться
        /// от событий изменения свойств.
        /// </returns>
        /// <remarks>
        /// Метод автоматически использует событие <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// у обоих объектов и делегирует фактическую логику связывания перегруженному
        /// методу <c>BindProperties</c> с явным указанием типов аргументов события.
        /// </remarks>
        public static IDisposable BindProperties<TSource, TDest>(this TSource source, Expression<Func<TSource, object>> sourcePropertySelector, TDest dest, Expression<Func<TDest, object>> destPropertySelector)
            where TSource : class, INotifyPropertyChanged
            where TDest : class, INotifyPropertyChanged
        {
            var srcEvent = source.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            var destEvent = dest.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            return BindProperties<TSource, PropertyChangedEventArgs, TDest, PropertyChangedEventArgs>(source, sourcePropertySelector, srcEvent, dest, destPropertySelector, destEvent);
        }

        /// <summary>
        /// Связывает свойства объекта-источника и объекта-приёмника,
        /// используя указанные события для отслеживания изменений.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника, реализующего <see cref="INotifyPropertyChanged"/>.
        /// </typeparam>
        /// <typeparam name="TDest">
        /// Тип объекта-приёмника, реализующего <see cref="INotifyPropertyChanged"/>.
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
        /// <param name="dest">
        /// Объект-приёмник, свойство которого будет обновляться.
        /// </param>
        /// <param name="destPropertySelector">
        /// Выражение, указывающее связываемое свойство объекта-приёмника.
        /// </param>
        /// <param name="destEventName">
        /// Имя события объекта-приёмника, при срабатывании которого выполняется обновление свойства.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий разорвать связь и отписаться
        /// от событий источника и приёмника.
        /// </returns>
        /// <remarks>
        /// Метод ищет события по их именам с помощью рефлексии, после чего делегирует
        /// фактическую логику связывания перегруженному методу <c>BindProperties</c>
        /// с явным указанием типов аргументов событий (<see cref="ProgressChangedEventArgs"/>).
        /// </remarks>
        public static IDisposable BindProperties<TSource, TDest>(this TSource source, Expression<Func<TSource, object>> sourcePropertySelector, string sourceEventName, TDest dest, Expression<Func<TDest, object>> destPropertySelector, string destEventName)
            where TSource : class
            where TDest : class
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (dest == null)
                throw new ArgumentNullException(nameof(dest));
            if (string.IsNullOrWhiteSpace(sourceEventName))
                throw new ArgumentException("Имя события источника не может быть пустым", nameof(sourceEventName));
            if (string.IsNullOrWhiteSpace(destEventName))
                throw new ArgumentException("Имя события приёмника не может быть пустым", nameof(destEventName));

            var srcEvent = source.GetType().GetEvent(sourceEventName) ?? throw new ArgumentException($"Событие '{sourceEventName}' не найдено в типе '{source.GetType().Name}'", nameof(sourceEventName));
            var destEvent = dest.GetType().GetEvent(destEventName);
            return destEvent == null
                ? throw new ArgumentException($"Событие '{destEventName}' не найдено в типе '{dest.GetType().Name}'", nameof(destEventName))
                : BindProperties<TSource, EventArgs, TDest, EventArgs>(source, sourcePropertySelector, srcEvent, dest, destPropertySelector, destEvent);
        }

        /// <summary>
        /// Связывает указанное свойство объекта-источника со свойством объекта-приёмника,
        /// используя стандартное событие <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// у источника и пользовательское событие у объекта-приёмника.
        /// </summary>
        /// <typeparam name="TSource">
        /// Тип объекта-источника, реализующего <see cref="INotifyPropertyChanged"/>.
        /// </typeparam>
        /// <typeparam name="TDest">
        /// Тип объекта-приёмника.
        /// </typeparam>
        /// <param name="source">
        /// Объект-источник, изменения свойства которого будут отслеживаться.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Выражение, указывающее связываемое свойство объекта-источника.
        /// </param>
        /// <param name="dest">
        /// Объект-приёмник, свойство которого будет обновляться.
        /// </param>
        /// <param name="destPropertySelector">
        /// Выражение, указывающее связываемое свойство объекта-приёмника.
        /// </param>
        /// <param name="destEventName">
        /// Имя события объекта-приёмника, при срабатывании которого выполняется обновление свойства.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий разорвать связь и отписаться
        /// от используемых событий.
        /// </returns>
        /// <remarks>
        /// Метод предназначен для сценариев, в которых объект-приёмник не реализует
        /// <see cref="INotifyPropertyChanged"/>, но предоставляет собственное событие,
        /// сигнализирующее о необходимости синхронизации свойства.
        /// Фактическая логика связывания делегируется перегруженному методу
        /// <c>BindProperties</c> с явным указанием типов аргументов событий.
        /// </remarks>
        public static IDisposable BindProperties<TSource, TDest>(this TSource source, Expression<Func<TSource, object>> sourcePropertySelector, TDest dest, Expression<Func<TDest, object>> destPropertySelector, string destEventName)
    where TSource : class, INotifyPropertyChanged
    where TDest : class
        {
            var srcEventName = nameof(INotifyPropertyChanged.PropertyChanged);
            return BindProperties<TSource, PropertyChangedEventArgs, TDest, EventArgs>(source, sourcePropertySelector, srcEventName, dest, destPropertySelector, destEventName);
        }

        /// <summary>
        /// Связывает указанное свойство объекта-источника со свойством объекта-приёмника,
        /// используя заданные имена событий у обоих объектов для отслеживания изменений.
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
        /// Объект-источник, изменения свойства которого будут отслеживаться.
        /// </param>
        /// <param name="sourcePropertySelector">
        /// Выражение, указывающее связываемое свойство объекта-источника.
        /// </param>
        /// <param name="sourceEventName">
        /// Имя события объекта-источника, при срабатывании которого выполняется синхронизация свойства.
        /// </param>
        /// <param name="dest">
        /// Объект-приёмник, свойство которого будет обновляться.
        /// </param>
        /// <param name="destPropertySelector">
        /// Выражение, указывающее связываемое свойство объекта-приёмника.
        /// </param>
        /// <param name="destEventName">
        /// Имя события объекта-приёмника, при срабатывании которого выполняется синхронизация свойства.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий разорвать связь и отписаться
        /// от событий источника и приёмника.
        /// </returns>
        /// <remarks>
        /// Метод выполняет поиск событий по их именам с использованием рефлексии,
        /// после чего делегирует фактическую логику связывания перегруженному методу
        /// <c>BindProperties</c>, принимающему объекты <see cref="EventInfo"/>.
        /// Ожидается, что указанные события соответствуют стандартному .NET-паттерну
        /// и используют аргументы, производные от <see cref="EventArgs"/>.
        /// </remarks>
        public static IDisposable BindProperties<TSource, TSourceEventArgs, TDest, TDestEventArgs>(this TSource source, Expression<Func<TSource, object>> sourcePropertySelector, string sourceEventName, TDest dest, Expression<Func<TDest, object>> destPropertySelector, string destEventName)
            where TSource : class
            where TSourceEventArgs : EventArgs
            where TDest : class
            where TDestEventArgs : EventArgs
        {
            var srcEvent = source.GetType().GetEvent(sourceEventName) ?? throw new ArgumentException($"Событие '{sourceEventName}' не найдено в типе '{source.GetType().Name}'", nameof(sourceEventName));
            var dstEvent = dest.GetType().GetEvent(destEventName);
            return dstEvent == null
                ? throw new ArgumentException($"Событие '{destEventName}' не найдено в типе '{dest.GetType().Name}'", nameof(destEventName))
                : BindProperties<TSource, TSourceEventArgs, TDest, TDestEventArgs>(source, sourcePropertySelector, srcEvent, dest, destPropertySelector, dstEvent);
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
        public static IDisposable BindProperties<TSource, TSourceEventArgs, TDest, TDestEventArgs>(this TSource source, Expression<Func<TSource, object>> sourcePropertySelector, EventInfo sourceEvent, TDest dest, Expression<Func<TDest, object>> destPropertySelector, EventInfo destEvent)
            where TSource : class
            where TSourceEventArgs : EventArgs
            where TDest : class
            where TDestEventArgs : EventArgs
        {
            return EventHelper.BindProperties<TSource, TSourceEventArgs, TDest, TDestEventArgs>(source, sourcePropertySelector, sourceEvent, dest, destPropertySelector, destEvent);
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
        public static IDisposable BindPropertyChangedToAction<T>(
            this T obj,
            Action<T, PropertyChangedEventArgs> action)
            where T : class
        {
            var eventName = nameof(INotifyPropertyChanged.PropertyChanged);
            return BindEventToAction<T, PropertyChangedEventArgs>(obj, eventName, action);
        }

        /// <summary>
        /// Привязывает указанное действие к событию <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// объекта, выполняя действие при любом изменении свойства.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, реализующего <see cref="INotifyPropertyChanged"/>.
        /// </typeparam>
        /// <param name="obj">
        /// Объект, у которого будет отслеживаться изменение свойств.
        /// </param>
        /// <param name="action">
        /// Действие, которое будет выполнено при срабатывании события <c>PropertyChanged</c>.
        /// </param>
        /// <returns>
        /// Объект <see cref="IDisposable"/>, позволяющий отписаться от события
        /// и прекратить выполнение действия.
        /// </returns>
        /// <remarks>
        /// Метод использует перегрузку <see cref="BindEventToAction{T,TEventArgs}"/>
        /// для привязки события <c>PropertyChanged</c> к указанному действию,
        /// игнорируя аргументы события.
        /// </remarks>
        public static IDisposable BindPropertyChangedToAction<T>(
            this T obj,
            Action action)
            where T : class
        {
            var eventName = nameof(INotifyPropertyChanged.PropertyChanged);
            return BindEventToAction<T, PropertyChangedEventArgs>(obj, eventName, (s, e) => action());
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
        /// Метод использует <see cref="BindEventToAction{TSource,TEventArgs}"/> для привязки
        /// события к действию и обеспечивает удобный способ связывать события с конкретными
        /// подписчиками без ручной реализации обработчиков.
        /// </remarks>
        public static void Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, TSource eventSource, EventInfo sourceEvent, Action<TSubscriber, TSource> action)
            where TSubscriber : class
            where TSource : class
        {
            EventHelper.Subscribe(subscriber, eventSource, sourceEvent, action);
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
        /// Метод использует <see cref="BindEventToAction{TSource,TEventArgs}"/> для привязки
        /// события к действию и обеспечивает удобный способ связывать события с конкретными
        /// подписчиками без ручной реализации обработчиков.
        /// </remarks>
        public static void Subscribe<TSubscriber, TSource>(this TSubscriber subscriber, TSource eventSource, string sourceEventName, Action<TSubscriber, TSource> action)
    where TSubscriber : class
    where TSource : class
        {
            var sourceEvent = eventSource.GetType().GetEvent(sourceEventName);
            EventHelper.Subscribe(subscriber, eventSource, sourceEvent, action);
        }
    }
}