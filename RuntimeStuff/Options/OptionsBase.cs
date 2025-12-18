using System;
using System.Collections.Generic;
using System.Reflection;
using RuntimeStuff.Helpers;

namespace RuntimeStuff.Options
{
    /// <summary>
    /// Базовый класс для работы с набором опций,
    /// доступ к которым осуществляется через отражение (Reflection).
    /// </summary>
    public abstract class OptionsBase
    {
        /// <summary>
        /// Карта свойств текущего типа:
        /// ключ — имя свойства, значение — информация о свойстве.
        /// </summary>
        protected readonly Dictionary<string, PropertyInfo> PropertyMap;

        /// <summary>
        /// Инициализирует экземпляр класса и строит карту свойств
        /// для текущего типа.
        /// </summary>
        protected OptionsBase()
        {
            PropertyMap = TypeHelper.GetPropertiesMap(this.GetType());
        }

        /// <summary>
        /// Инициализирует экземпляр класса и устанавливает значения свойств
        /// из переданного словаря.
        /// </summary>
        /// <param name="paramValues">
        /// Словарь значений параметров, где ключ — имя свойства,
        /// значение — устанавливаемое значение.
        /// </param>
        protected OptionsBase(IDictionary<string, object> paramValues) : this()
        {
            foreach (var kvp in paramValues)
                PropertyMap[kvp.Key].SetValue(this, kvp.Value);
        }

        /// <summary>
        /// Индексатор для получения или установки значения свойства по имени.
        /// </summary>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Значение свойства.</returns>
        public object this[string name]
        {
            get => Get<object>(name);
            set => Set(name, value);
        }

        /// <summary>
        /// Получает значение свойства по имени с приведением типа.
        /// </summary>
        /// <typeparam name="TValue">Ожидаемый тип значения.</typeparam>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Значение свойства, приведённое к указанному типу.</returns>
        public TValue Get<TValue>(string name)
        {
            return typeof(TValue) == typeof(object)
                ? (TValue)PropertyMap[name].GetValue(this)
                : TypeHelper.ChangeType<TValue>(PropertyMap[name].GetValue(this));
        }

        /// <summary>
        /// Устанавливает значение свойства по имени.
        /// </summary>
        /// <typeparam name="TValue">Тип устанавливаемого значения.</typeparam>
        /// <param name="name">Имя свойства.</param>
        /// <param name="value">Новое значение.</param>
        /// <returns>
        /// <c>true</c>, если значение успешно установлено;
        /// <c>false</c> — если свойство не найдено или произошла ошибка.
        /// </returns>
        public bool Set<TValue>(string name, TValue value)
        {
            if (!PropertyMap.TryGetValue(name, out var p))
                return false;

            try
            {
                p.SetValue(this, TypeHelper.ChangeType(value, p.PropertyType));
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
        /// <returns>
        /// Словарь, где ключ — имя свойства,
        /// значение — текущее значение свойства.
        /// </returns>
        public Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>();

            foreach (var prop in PropertyMap)
            {
                var value = prop.Value.GetValue(this);
                dict[prop.Key] = value;
            }

            return dict;
        }
    }

    /// <summary>
    /// Обобщённый базовый класс опций с поддержкой клонирования,
    /// объединения и построения через конфигурационный делегат.
    /// </summary>
    /// <typeparam name="T">
    /// Тип-наследник, реализующий шаблон CRTP
    /// (Curiously Recurring Template Pattern).
    /// </typeparam>
    public abstract class OptionsBase<T> : OptionsBase
        where T : OptionsBase<T>, new()
    {
        /// <summary>
        /// Создаёт экземпляр опций со значениями по умолчанию.
        /// </summary>
        public static T Default => new T();

        /// <summary>
        /// Создаёт поверхностную копию текущего объекта.
        /// </summary>
        /// <returns>Клонированный экземпляр опций.</returns>
        public T Clone()
        {
            return (T)MemberwiseClone();
        }

        /// <summary>
        /// Объединяет текущие опции с другими,
        /// копируя только ненулевые значения.
        /// </summary>
        /// <param name="other">Другой объект опций.</param>
        /// <returns>Текущий экземпляр после объединения.</returns>
        public T Merge(OptionsBase other)
        {
            foreach (var prop in PropertyMap)
            {
                var value = prop.Value.GetValue(other);
                if (value != null)
                    prop.Value.SetValue(this, value);
            }

            return (T)this;
        }

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
    }
}
