// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="EventHandlers.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Internal
{
    using System.ComponentModel;

    /// <summary>
    /// Class EventHandlers.
    /// </summary>
    internal class EventHandlers
    {
        /// <summary>
        /// Gets or sets the changed.
        /// </summary>
        public PropertyChangedEventHandler Changed { get; set; }

        /// <summary>
        /// Gets or sets the changing.
        /// </summary>
        public PropertyChangingEventHandler Changing { get; set; }
    }
}