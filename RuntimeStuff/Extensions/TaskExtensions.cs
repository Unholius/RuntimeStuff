// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 10-13-2025
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="TaskExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Extensions
{
    using System;
    using System.Threading.Tasks;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Предоставляет вспомогательные методы для безопасного запуска задач в фоновом режиме без ожидания завершения
    /// (fire-and-forget).
    /// Позволяет централизованно обрабатывать исключения, возникающие в задачах.
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// Выполняет задачу в фоновом режиме без ожидания завершения, с обработкой исключений общего типа
        /// <see cref="Exception" />.
        /// </summary>
        /// <param name="task">Задача, которую нужно запустить.</param>
        /// <param name="onException">Действие, выполняемое при возникновении исключения.</param>
        /// <param name="continueOnCapturedContext">Если <c>true</c>, продолжение выполняется в исходном контексте синхронизации.</param>
        public static void RunAndForget(this Task task, Action<Exception> onException, bool continueOnCapturedContext = false) => TaskHelper.RunAndForget(task, in onException, in continueOnCapturedContext);

        /// <summary>
        /// Выполняет задачу в фоновом режиме без ожидания завершения, с обработкой исключений определённого типа.
        /// </summary>
        /// <typeparam name="TException">Тип обрабатываемого исключения.</typeparam>
        /// <param name="task">Задача, которую нужно запустить.</param>
        /// <param name="onException">Действие, выполняемое при возникновении исключения указанного типа.</param>
        /// <param name="continueOnCapturedContext">Если <c>true</c>, продолжение выполняется в исходном контексте синхронизации.</param>
        public static void RunAndForget<TException>(this Task task, Action<TException> onException, bool continueOnCapturedContext = false)
            where TException : Exception => TaskHelper.RunAndForget(task, in onException, in continueOnCapturedContext);

        /// <summary>
        /// Выполняет задачу в фоновом режиме без ожидания завершения и с необязательной обработкой исключений.
        /// </summary>
        /// <param name="task">Задача, которую нужно запустить.</param>
        /// <param name="onException">Действие, выполняемое при возникновении исключения (необязательно).</param>
        /// <param name="continueOnCapturedContext">Если <c>true</c>, продолжение выполняется в исходном контексте синхронизации.</param>
        public static void RunAndForget(this Task task, in Action<Exception> onException = null, in bool continueOnCapturedContext = false) => TaskHelper.RunAndForget(task, onException, continueOnCapturedContext);

        /// <summary>
        /// Выполняет задачу в фоновом режиме без ожидания завершения и с необязательной обработкой исключений указанного типа.
        /// </summary>
        /// <typeparam name="TException">Тип обрабатываемого исключения.</typeparam>
        /// <param name="task">Задача, которую нужно запустить.</param>
        /// <param name="onException">Действие, выполняемое при возникновении исключения (необязательно).</param>
        /// <param name="continueOnCapturedContext">Если <c>true</c>, продолжение выполняется в исходном контексте синхронизации.</param>
        public static void RunAndForget<TException>(Task task, in Action<TException> onException = null, in bool continueOnCapturedContext = false)
            where TException : Exception => TaskHelper.RunAndForget(task, onException, continueOnCapturedContext);
    }
}