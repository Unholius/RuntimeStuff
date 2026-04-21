// <copyright file="JoinType.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Data
{
    /// <summary>
    /// Тип соединения для SQL JOIN.
    /// </summary>
    public enum JoinType
    {
        /// <summary>INNER JOIN.</summary>
        Inner,

        /// <summary>LEFT JOIN.</summary>
        Left,

        /// <summary>RIGHT JOIN.</summary>
        Right,

        /// <summary>FULL JOIN.</summary>
        Full,

        /// <summary>OUTER JOIN.</summary>
        Outer,

        /// <summary>CROSS APPLY.</summary>
        CrossApply,
    }
}