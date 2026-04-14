// <copyright file="TreeList.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Collections
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Представляет нетипизированную версию узла древовидной структуры.
    /// </summary>
    /// <remarks>
    /// Используется как упрощённый вариант <see cref="TreeList{T}"/> с типом значения <see cref="object"/>.
    /// </remarks>
    public class TreeList : TreeList<object>
    {
        /// <summary>
        /// Инициализирует новый экземпляр узла дерева с указанным значением.
        /// </summary>
        /// <param name="item">Значение узла.</param>
        public TreeList(object item)
            : base(item)
        {
        }

        /// <summary>
        /// Инициализирует новый экземпляр нетипизированного узла дерева.
        /// </summary>
        public TreeList()
            : base(null)
        {
        }
    }
}