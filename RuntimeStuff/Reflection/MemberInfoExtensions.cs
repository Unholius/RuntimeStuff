// <copyright file="MemberInfoExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Reflection
{
    /// <summary>
    /// Расширения для <see cref="MemberInfo"/>.
    /// </summary>
    public static class MemberInfoExtensions
    {
        /// <summary>
        /// Получить расширенную информацию о члене класса.
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса.</param>
        /// <returns>Расширенная информация о члене класса.</returns>
        public static MemberCache GetMemberCache(this MemberInfo memberInfo) => MemberCache.Create(memberInfo);

        /// <summary>
        /// Получить имя колонки из метаданных свойства.
        /// </summary>
        /// <param name="memberInfo">Свойство.</param>
        /// <returns>Имя колонки.</returns>
        public static string GetColumnName(this MemberInfo memberInfo) => GetMemberCache(memberInfo)?.ColumnName;
    }
}