// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="MemberInfoExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Extensions
{
    using System.Reflection;

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
    }
}