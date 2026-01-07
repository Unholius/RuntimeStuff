// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="MemberCache.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary>
    /// Class MemberCache.
    /// Implements the <see cref="RuntimeStuff.MemberCache" />.
    /// </summary>
    /// <typeparam name="T">Type.</typeparam>
    /// <seealso cref="RuntimeStuff.MemberCache" />
    public class MemberCache<T> : MemberCache
    {
        /// <summary>
        /// The member information cache t.
        /// </summary>
        protected static readonly ConcurrentDictionary<Type, MemberCache<T>> MemberInfoCacheT =
            new ConcurrentDictionary<Type, MemberCache<T>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberCache{T}" /> class.
        /// </summary>
        public MemberCache()
            : base(typeof(T))
        {
            this.DefaultConstructor = Expression.Lambda<Func<T>>(Expression.New(typeof(T))).Compile();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberCache{T}" /> class.
        /// </summary>
        /// <param name="memberInfo">The member information.</param>
        public MemberCache(MemberInfo memberInfo)
            : base(memberInfo)
        {
            this.DefaultConstructor = Expression.Lambda<Func<T>>(Expression.New(typeof(T))).Compile();
        }

        /// <summary>
        /// Gets the default constructor.
        /// </summary>
        /// <value>The default constructor.</value>
        public new Func<T> DefaultConstructor { get; }

        /// <summary>
        /// Creates this instance.
        /// </summary>
        /// <returns>MemberCache&lt;T&gt;.</returns>
        public static MemberCache<T> Create()
        {
            var memberCache = MemberInfoCache.GetOrAdd(typeof(T), x => new MemberCache(typeof(T)));
            var result = MemberInfoCacheT.GetOrAdd(typeof(T), x => new MemberCache<T>(memberCache));

            return result;
        }
    }
}