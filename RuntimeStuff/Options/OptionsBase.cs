// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="OptionsBase.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Options
{
    using System.Collections.Generic;
    using System.Reflection;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Базовый класс для работы с набором опций,
    /// доступ к которым осуществляется через отражение (Reflection).
    /// </summary>
    public abstract class OptionsBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OptionsBase"/> class.
        /// Инициализирует экземпляр класса и строит карту свойств
        /// для текущего типа.
        /// </summary>
        protected OptionsBase()
        {
            this.PropertyMap = Obj.GetPropertiesMap(this.GetType());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionsBase"/> class.
        /// Инициализирует экземпляр класса и устанавливает значения свойств
        /// из переданного словаря.
        /// </summary>
        /// <param name="paramValues">Словарь значений параметров, где ключ — имя свойства,
        /// значение — устанавливаемое значение.</param>
        protected OptionsBase(IDictionary<string, object> paramValues)
            : this()
        {
            foreach (var kvp in paramValues)
            {
                this.PropertyMap[kvp.Key].SetValue(this, kvp.Value);
            }
        }

        /// <summary>
        /// Gets карта свойств текущего типа: ключ — имя свойства, значение — информация о свойстве.
        /// </summary>
        protected Dictionary<string, PropertyInfo> PropertyMap { get; }

        /// <summary>
        /// Индексатор для получения или установки значения свойства по имени.
        /// </summary>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Значение свойства.</returns>
        public object this[string name]
        {
            get => this.Get<object>(name);
            set => this.Set(name, value);
        }

        /// <summary>
        /// Получает значение свойства по имени с приведением типа.
        /// </summary>
        /// <typeparam name="TValue">Ожидаемый тип значения.</typeparam>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Значение свойства, приведённое к указанному типу.</returns>
        public TValue Get<TValue>(string name) => typeof(TValue) == typeof(object)
                ? (TValue)this.PropertyMap[name].GetValue(this)
                : Obj.ChangeType<TValue>(this.PropertyMap[name].GetValue(this));

        /// <summary>
        /// Устанавливает значение свойства по имени.
        /// </summary>
        /// <typeparam name="TValue">Тип устанавливаемого значения.</typeparam>
        /// <param name="name">Имя свойства.</param>
        /// <param name="value">Новое значение.</param>
        /// <returns><c>true</c>, если значение успешно установлено;
        /// <c>false</c> — если свойство не найдено или произошла ошибка.</returns>
        public bool Set<TValue>(string name, TValue value)
        {
            if (!this.PropertyMap.TryGetValue(name, out var p))
            {
                return false;
            }

            try
            {
                p.SetValue(this, Obj.ChangeType(value, p.PropertyType));
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Преобразует все свойства объекта в словарь.
        /// </summary>
        /// <returns>Словарь, где ключ — имя свойства,
        /// значение — текущее значение свойства.</returns>
        public Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>();

            foreach (var prop in this.PropertyMap)
            {
                var value = prop.Value.GetValue(this);
                dict[prop.Key] = value;
            }

            return dict;
        }
    }
}