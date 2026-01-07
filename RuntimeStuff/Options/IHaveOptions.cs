// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 11-19-2025
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="IHaveOptions.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Options
{
    /// <summary>
    /// Определяет контракт для объектов, содержащих набор опций
    /// строго типизированного типа.
    /// </summary>
    /// <typeparam name="T">Тип опций, производный от <see cref="OptionsBase{T}" />.</typeparam>
    /// <remarks>Интерфейс предназначен для использования в публичных API,
    /// где требуется доступ к опциям без потери типовой безопасности.</remarks>
    public interface IHaveOptions<out T> : IHaveOptions
        where T : OptionsBase<T>, new()
    {
        /// <summary>
        /// Gets возвращает набор опций, ассоциированный с объектом.
        /// </summary>
        /// <value>The options.</value>
        /// <remarks>Свойство является ковариантным (<c>out T</c>) и предназначено
        /// только для чтения. Для изменения опций рекомендуется использовать
        /// методы самого объекта опций или создавать новый экземпляр.</remarks>
        new T Options { get; }
    }

    /// <summary>
    /// Определяет базовый контракт для объектов,
    /// содержащих набор опций.
    /// </summary>
    /// <remarks>Используется как нетипизированная версия интерфейса
    /// <see cref="IHaveOptions{T}" /> для сценариев,
    /// где конкретный тип опций неизвестен во время компиляции.</remarks>
    public interface IHaveOptions
    {
        /// <summary>
        /// Gets or sets получает или задаёт набор опций, ассоциированный с объектом.
        /// </summary>
        /// <value>The options.</value>
        /// <remarks>В типизированных сценариях рекомендуется использовать
        /// интерфейс <see cref="IHaveOptions{T}" /> вместо данного.</remarks>
        OptionsBase Options { get; set; }
    }
}