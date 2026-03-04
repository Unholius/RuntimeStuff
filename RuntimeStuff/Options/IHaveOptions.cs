// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 11-19-2025
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="IHaveOptions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
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
    public interface IHaveOptions<out T>
        where T : OptionsBase<T>, new()
    {
        /// <summary>
        /// Gets возвращает набор опций, ассоциированный с объектом.
        /// </summary>
        /// <value>The options.</value>
        /// <remarks>Свойство является ковариантным (<c>out T</c>) и предназначено
        /// только для чтения. Для изменения опций рекомендуется использовать
        /// методы самого объекта опций или создавать новый экземпляр.</remarks>
        T Options { get; }
    }
}