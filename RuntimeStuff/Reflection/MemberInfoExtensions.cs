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
        /// <param name="type">Тип.</param>
        /// <returns>Расширенная информация о типе.</returns>
        public static MemberCache GetMemberCache(this Type type) => MemberCache.Get(type);

        /// <summary>
        /// Получить расширенную информацию о члене класса.
        /// </summary>
        /// <param name="typeMember">Член типа.</param>
        /// <returns>Расширенная информация о типе.</returns>
        public static MemberCache GetMemberCache<T>(this MemberInfo typeMember) => MemberCache.Get<T>(typeMember);

        /// <summary>
        /// Получить расширенную информацию о члене класса.
        /// </summary>
        /// <param name="typeMember">Член типа.</param>
        /// <param name="type">Тип которому принадлежит член.</param>
        /// <returns>Расширенная информация о типе.</returns>
        public static MemberCache GetMemberCache(this MemberInfo typeMember, Type type) => MemberCache.Get(type, typeMember);

        /// <summary>
        /// Получить имя колонки из метаданных свойства.
        /// </summary>
        /// <param name="propertyInfo">Свойство.</param>
        /// <returns>Имя колонки.</returns>
        public static string GetColumnName(this MemberInfo propertyInfo) => MemberCache.Get(propertyInfo.DeclaringType, propertyInfo)?.ColumnName;
    }
}