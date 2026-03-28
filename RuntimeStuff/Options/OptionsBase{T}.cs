//// ***********************************************************************
//// Assembly         : RuntimeStuff
//// Author           : RS
//// Created          : 01-06-2026
////
//// Last Modified By : RS
//// Last Modified On : 01-07-2026
//// ***********************************************************************
//// <copyright file="OptionsBase{T}.cs" company="Rudnev Sergey">
//// Copyright (c) Rudnev Sergey. All rights reserved.
//// </copyright>
//// <summary></summary>
//// ***********************************************************************
//namespace System.Options
//{
//    using System;

//    /// <summary>
//    /// Базовый абстрактный класс для реализации паттерна конфигурационных параметров (Options),
//    /// поддерживающий самотипизацию (CRTP) и создание экземпляра по умолчанию.
//    /// </summary>
//    /// <typeparam name="T">
//    /// Конкретный тип параметров, наследующий <see cref="OptionsBase{T}"/>.
//    /// Ограничение <c>where T : OptionsBase&lt;T&gt;, new()</c> обеспечивает:
//    /// <list type="bullet">
//    /// <item>
//    /// <description>Наличие конструктора без параметров.</description>
//    /// </item>
//    /// <item>
//    /// <description>Корректное приведение текущего экземпляра к типу <typeparamref name="T"/>.</description>
//    /// </item>
//    /// </list>
//    /// </typeparam>
//    public abstract class OptionsBase<T>
//        where T : OptionsBase<T>, new()
//    {
//        /// <summary>
//        /// Инициализирует новый экземпляр класса <see cref="OptionsBase{T}"/>
//        /// и применяет к нему набор конфигурационных делегатов.
//        /// </summary>
//        /// <param name="configure">
//        /// Массив делегатов <see cref="Action{T}"/>, каждый из которых
//        /// выполняет настройку экземпляра типа <typeparamref name="T"/>.
//        /// </param>
//        /// <remarks>
//        /// Делегаты выполняются последовательно в порядке передачи.
//        /// Текущий экземпляр приводится к типу <typeparamref name="T"/>.
//        /// </remarks>
//        public OptionsBase(params Action<T>[] configure)
//            : this()
//        {
//            foreach (var c in configure)
//            {
//                c((T)this);
//            }
//        }

//        /// <summary>
//        /// Инициализирует новый экземпляр класса <see cref="OptionsBase{T}"/>.
//        /// Предназначен для использования в производных классах.
//        /// </summary>
//        protected OptionsBase()
//        {
//        }

//        /// <summary>
//        /// Возвращает статический экземпляр параметров по умолчанию.
//        /// </summary>
//        /// <remarks>
//        /// Экземпляр создаётся один раз при первой инициализации типа
//        /// посредством вызова конструктора без параметров.
//        /// Изменение состояния возвращённого объекта повлияет на все обращения
//        /// к <see cref="Default"/>, поэтому при необходимости рекомендуется
//        /// использовать <see cref="Clone"/>.
//        /// </remarks>
//        public static T Default { get; } = new T();

//        /// <summary>
//        /// Создаёт поверхностную копию текущего экземпляра.
//        /// </summary>
//        /// <returns>
//        /// Новый объект типа <typeparamref name="T"/>, содержащий копии
//        /// значений полей текущего экземпляра.
//        /// </returns>
//        /// <remarks>
//        /// Метод использует <see cref="object.MemberwiseClone"/>,
//        /// поэтому выполняется поверхностное копирование:
//        /// ссылочные поля копируются по ссылке.
//        /// </remarks>
//        public T Clone() => (T)this.MemberwiseClone();
//    }
//}