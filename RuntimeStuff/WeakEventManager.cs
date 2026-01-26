// <copyright file="WeakEventManager.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
namespace RuntimeStuff
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using RuntimeStuff.Internal;

    public class WeakEventManager
    {
        private readonly Dictionary<IWeakEventListener, Delegate> listeners = new Dictionary<IWeakEventListener, Delegate>();

        public void AddWeakEventListener<T, TArgs>(T source, string eventName, Action<T, TArgs> handler)
            where T : class
            where TArgs : EventArgs
        {
            listeners.Add(new WeakEventListener<T, TArgs>(source, eventName, handler), handler);
        }

        public void AddWeakEventListener<T>(T source, Action<T, PropertyChangedEventArgs> handler)
            where T : class, INotifyPropertyChanged
        {
            listeners.Add(new PropertyChangedWeakEventListener<T>(source, handler), handler);
        }

        public void AddWeakEventListener<T>(T source, Action<T, NotifyCollectionChangedEventArgs> handler)
            where T : class, INotifyCollectionChanged
        {
            listeners.Add(new CollectionChangedWeakEventListener<T>(source, handler), handler);
        }

        public void AddWeakEventListener<T, TArgs>(T source, Action<T, EventHandler<TArgs>> register, Action<T, EventHandler<TArgs>> unregister, Action<T, TArgs> handler)
            where T : class
            where TArgs : EventArgs
        {
            listeners.Add(new TypedWeakEventListener<T, TArgs>(source, register, unregister, handler), handler);
        }

        public void AddWeakEventListener<T, TArgs, THandler>(T source, Action<T, THandler> register, Action<T, THandler> unregister, Action<T, TArgs> handler)
            where T : class
            where TArgs : EventArgs
            where THandler : Delegate
        {
            listeners.Add(new TypedWeakEventListener<T, TArgs, THandler>(source, register, unregister, handler), handler);
        }

        public void RemoveWeakEventListener<T>(T source)
            where T : class
        {
            var toRemove = new List<IWeakEventListener>();
            foreach (var listener in listeners.Keys)
            {
                if (!listener.IsAlive)
                {
                    toRemove.Add(listener);
                }
                else if (listener.Source == source)
                {
                    listener.StopListening();
                    toRemove.Add(listener);
                }
            }

            foreach (var item in toRemove)
            {
                listeners.Remove(item);
            }
        }

        public void ClearWeakEventListeners()
        {
            foreach (var listener in listeners.Keys)
            {
                if (listener.IsAlive)
                {
                    listener.StopListening();
                }
            }

            listeners.Clear();
        }
    }
}
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
