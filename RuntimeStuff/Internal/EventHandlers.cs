// <copyright file="EventHandlers.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Internal
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