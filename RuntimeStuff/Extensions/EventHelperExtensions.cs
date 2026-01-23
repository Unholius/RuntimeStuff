// <copyright file="EventHelperExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Extensions
{
    using System;
    using System.Collections.Generic;
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
        /// <param name="actionSenderAndArgs">
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
            Action<object, PropertyChangedEventArgs> actionSenderAndArgs)
            where T : class
        {
            var eventName = nameof(INotifyPropertyChanged.PropertyChanged);
            return BindEventToAction<T, PropertyChangedEventArgs>(obj, eventName, actionSenderAndArgs);
        }

        public static IDisposable BindProperties<TSource, TDest>(this TSource source, Expression<Func<TSource, object>> sourcePropertySelector, TDest dest, Expression<Func<TDest, object>> destPropertySelector)
            where TSource : class, INotifyPropertyChanged
            where TDest : class, INotifyPropertyChanged
        {
            var srcEvent = source.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            var destEvent = dest.GetType().GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            return BindProperties<TSource, ProgressChangedEventArgs, TDest, ProgressChangedEventArgs>(source, sourcePropertySelector, srcEvent, dest, destPropertySelector, destEvent);
        }

        public static IDisposable BindProperties<TSource, TDest>(this TSource source, Expression<Func<TSource, object>> sourcePropertySelector, TDest dest, Expression<Func<TDest, object>> destPropertySelector, string destEventName)
    where TSource : class, INotifyPropertyChanged
    where TDest : class
        {
            var srcEventName = nameof(INotifyPropertyChanged.PropertyChanged);
            return BindProperties<TSource, PropertyChangedEventArgs, TDest, EventArgs>(source, sourcePropertySelector, srcEventName, dest, destPropertySelector, destEventName);
        }

        public static IDisposable BindProperties<TSource, TSourceEventArgs, TDest, TDestEventArgs>(this TSource source, Expression<Func<TSource, object>> sourcePropertySelector, string sourceEventName, TDest dest, Expression<Func<TDest, object>> destPropertySelector, string destEventName)
            where TSource : class
            where TSourceEventArgs : EventArgs
            where TDest : class
            where TDestEventArgs : EventArgs
        {
            var srcEvent = source.GetType().GetEvent(sourceEventName);
            var dstEvent = dest.GetType().GetEvent(destEventName);
            return BindProperties<TSource, TSourceEventArgs, TDest, TDestEventArgs>(source, sourcePropertySelector, srcEvent, dest, destPropertySelector, dstEvent);
        }

        public static IDisposable BindProperties<TSource, TSourceEventArgs, TDest, TDestEventArgs>(this TSource source, Expression<Func<TSource, object>> sourcePropertySelector, EventInfo sourceEvent, TDest dest, Expression<Func<TDest, object>> destPropertySelector, EventInfo destEvent)
            where TSource : class
            where TSourceEventArgs : EventArgs
            where TDest : class
            where TDestEventArgs : EventArgs
        {
            var binding = new PropertiesBinding(source, sourcePropertySelector.GetPropertyInfo(), dest, destPropertySelector.GetPropertyInfo());
            EventHelper.BindEventToAction<TSource, TSourceEventArgs>(source, sourceEvent, binding.SrcPropChanged);
            EventHelper.BindEventToAction<TDest, TDestEventArgs>(dest, destEvent, binding.DstPropChanged);
            return binding;
        }

        private sealed class PropertiesBinding : IDisposable
        {
            PropertyInfo sourcePropertyInfo;
            PropertyInfo destPropertyInfo;
            object source;
            object dest;
            public PropertiesBinding(object src, PropertyInfo srcPropInfo, object dest, PropertyInfo destPropInfo)
            {
                sourcePropertyInfo = srcPropInfo;
                destPropertyInfo = destPropInfo;
                source = src;
                this.dest = dest;
            }

            public void SrcPropChanged(object sender, object args)
            {
                if (args is PropertyChangedEventArgs pc && pc.PropertyName != sourcePropertyInfo.Name)
                    return;

                var srcValue = sourcePropertyInfo.GetValue(sender);
                var destValue = destPropertyInfo.GetValue(dest);
                if (EqualityComparer<object>.Default.Equals(srcValue, destValue))
                    return;

                destPropertyInfo.SetValue(dest, srcValue);
            }

            public void DstPropChanged(object sender, object args)
            {
                if (args is PropertyChangedEventArgs pc && pc.PropertyName != sourcePropertyInfo.Name)
                    return;

                var senderValue = destPropertyInfo.GetValue(sender);
                var destValue = sourcePropertyInfo.GetValue(source);
                if (EqualityComparer<object>.Default.Equals(senderValue, destValue))
                    return;

                sourcePropertyInfo.SetValue(source, senderValue);
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }
        }

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
            this T obj,
            string eventName,
            Action<T, TArgs> actionSenderAndArgs)
            where T : class
        {
            return EventHelper.BindEventToAction<T, TArgs>(obj, eventName, actionSenderAndArgs);
        }

        public static IDisposable BindEventToAction<T>(
            this T obj,
            string eventName,
            Action<T, EventArgs> actionSenderAndArgs)
            where T : class
        {
            return EventHelper.BindEventToAction(obj, eventName, actionSenderAndArgs);
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
        public static IDisposable BindEventToAction<T>(
            this T obj,
            EventInfo eventInfo,
            Action<object, object> actionSenderAndArgs)
            where T : class
        {
            return EventHelper.BindEventToAction<T, EventArgs>(obj, eventInfo, actionSenderAndArgs);
        }
    }
}
