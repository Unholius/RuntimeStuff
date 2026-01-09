// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="OptionsBase{T}.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Options
{
    using System;

    /// <summary>
    /// Обобщённый базовый класс опций с поддержкой клонирования,
    /// объединения и построения через конфигурационный делегат.
    /// </summary>
    /// <typeparam name="T">Тип-наследник, реализующий шаблон CRTP
    /// (Curiously Recurring Template Pattern).</typeparam>
    public abstract class OptionsBase<T> : OptionsBase
        where T : OptionsBase<T>, new()
    {
        /// <summary>
        /// Gets создаёт экземпляр опций со значениями по умолчанию.
        /// </summary>
        /// <value>The default.</value>
        public static T Default => new T();

        /// <summary>
        /// Создаёт и конфигурирует экземпляр опций
        /// с помощью переданного делегата.
        /// </summary>
        /// <param name="configure">Делегат конфигурации.</param>
        /// <returns>Сконфигурированный экземпляр опций.</returns>
        public static T Build(Action<T> configure)
        {
            var instance = Default;
            configure(instance);
            return instance;
        }

        /// <summary>
        /// Создаёт поверхностную копию текущего объекта.
        /// </summary>
        /// <returns>Клонированный экземпляр опций.</returns>
        public T Clone() => (T)this.MemberwiseClone();

        /// <summary>
        /// Объединяет текущие опции с другими,
        /// копируя только ненулевые значения.
        /// </summary>
        /// <param name="other">Другой объект опций.</param>
        /// <returns>Текущий экземпляр после объединения.</returns>
        public T Merge(OptionsBase other)
        {
            foreach (var prop in this.PropertyMap)
            {
                var value = prop.Value.GetValue(other);
                if (value != null)
                {
                    prop.Value.SetValue(this, value);
                }
            }

            return (T)this;
        }
    }
}