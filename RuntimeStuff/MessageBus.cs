// <copyright file="MessageBus.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    /// <summary>
    /// Простейшая реализация шины сообщений (Message Bus)
    /// для обмена сообщениями внутри процесса.
    /// </summary>
    /// <remarks>
    /// Класс позволяет:
    /// <list type="bullet">
    /// <item><description>подписываться на сообщения определённого типа;</description></item>
    /// <item><description>публиковать сообщения и уведомлять всех подписчиков;</description></item>
    /// <item><description>использовать единый глобальный экземпляр через <see cref="Default"/>.</description></item>
    /// </list>
    ///
    /// Реализация потокобезопасна на уровне регистрации и доставки сообщений,
    /// однако обработчики выполняются синхронно в потоке вызова <see cref="Publish{T}"/>.
    /// </remarks>
    public sealed class MessageBus
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> handlers =
            new ConcurrentDictionary<Type, List<Delegate>>();

        /// <summary>
        /// Глобальный экземпляр шины сообщений.
        /// </summary>
        /// <remarks>
        /// Может использоваться как простой singleton
        /// для обмена сообщениями между компонентами приложения.
        /// </remarks>
        public static MessageBus Default { get; } = new MessageBus();

        /// <summary>
        /// Подписывает обработчик на сообщения указанного типа.
        /// </summary>
        /// <typeparam name="T">
        /// Тип сообщения.
        /// </typeparam>
        /// <param name="handler">
        /// Делегат, который будет вызван при публикации сообщения.
        /// </param>
        /// <remarks>
        /// Обработчики одного типа сообщений вызываются
        /// в порядке их регистрации.
        ///
        /// Метод потокобезопасен.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Может быть сгенерировано при передаче <c>null</c> в качестве <paramref name="handler"/>
        /// (проверка не выполняется явно).
        /// </exception>
        public void Subscribe<T>(Action<T> handler)
        {
            var list = handlers.GetOrAdd(typeof(T), _ => new List<Delegate>());
            lock (list) list.Add(handler);
        }

        /// <summary>
        /// Публикует сообщение указанного типа и уведомляет всех подписчиков.
        /// </summary>
        /// <typeparam name="T">
        /// Тип публикуемого сообщения.
        /// </typeparam>
        /// <param name="message">
        /// Экземпляр сообщения.
        /// </param>
        /// <remarks>
        /// Если для данного типа сообщений отсутствуют подписчики,
        /// метод завершается без каких-либо действий.
        ///
        /// Вызов обработчиков выполняется синхронно
        /// в потоке, из которого был вызван метод.
        ///
        /// Исключения, выброшенные обработчиками,
        /// не перехватываются и прерывают дальнейшую публикацию.
        /// </remarks>
        public void Publish<T>(T message)
        {
            if (!handlers.TryGetValue(typeof(T), out var list))
                return;

            Delegate[] snapshot;
            lock (list) snapshot = list.ToArray();

            for (int i = 0; i < snapshot.Length; i++)
                ((Action<T>)snapshot[i])(message);
        }
    }
}