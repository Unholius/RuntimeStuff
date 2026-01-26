// <copyright file="WeakEventListener.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1402 // File may only contain a single type
namespace RuntimeStuff.Internal
{
    using System;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Reflection;

    internal interface IWeakEventListener
    {
        bool IsAlive { get; }

        object Source { get; }

        Delegate Handler { get; }

        void StopListening();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:Elements should be documented", Justification = "1")]
    internal abstract class WeakEventListenerBase<T, TArgs> : IWeakEventListener
        where T : class
        where TArgs : EventArgs
    {
        private readonly WeakReference<T> source;
        private readonly WeakReference<Action<T, TArgs>> handler;

        protected WeakEventListenerBase(T source, Action<T, TArgs> handler)
        {
            this.source = new WeakReference<T>(source ?? throw new ArgumentNullException(nameof(source)));
            this.handler = new WeakReference<Action<T, TArgs>>(handler ?? throw new ArgumentNullException(nameof(handler)));
        }

        public bool IsAlive => handler.TryGetTarget(out var _) && source.TryGetTarget(out var _);

        public object Source
        {
            get
            {
                if (source.TryGetTarget(out var src))
                {
                    return src;
                }

                return null;
            }
        }

        public Delegate Handler
        {
            get
            {
                if (handler.TryGetTarget(out var h))
                {
                    return h;
                }

                return null;
            }
        }

        public void StopListening()
        {
            if (source.TryGetTarget(out var s))
            {
                StopListening(s);
            }
        }

        protected void HandleEvent(object sender, TArgs e)
        {
            if (handler.TryGetTarget(out var h))
            {
                h(sender as T, e);
            }
            else
            {
                StopListening();
            }
        }

        protected abstract void StopListening(T source);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "2")]
    internal class TypedWeakEventListener<T, TArgs> : WeakEventListenerBase<T, TArgs>
        where T : class
        where TArgs : EventArgs
    {
        private readonly Action<T, EventHandler<TArgs>> unregister;

        public TypedWeakEventListener(T source, Action<T, EventHandler<TArgs>> register, Action<T, EventHandler<TArgs>> unregister, Action<T, TArgs> handler)
            : base(source, handler)
        {
            if (register == null)
            {
                throw new ArgumentNullException(nameof(register));
            }

            this.unregister = unregister ?? throw new ArgumentNullException(nameof(unregister));
            register(source, HandleEvent);
        }

        protected override void StopListening(T source) => unregister(source, HandleEvent);
    }

    internal class PropertyChangedWeakEventListener<T> : WeakEventListenerBase<T, PropertyChangedEventArgs>
        where T : class, INotifyPropertyChanged
    {
        public PropertyChangedWeakEventListener(T source, Action<T, PropertyChangedEventArgs> handler)
            : base(source, handler)
        {
            source.PropertyChanged += HandleEvent;
        }

        protected override void StopListening(T source) => source.PropertyChanged -= HandleEvent;
    }

    internal class CollectionChangedWeakEventListener<T> : WeakEventListenerBase<T, NotifyCollectionChangedEventArgs>
        where T : class, INotifyCollectionChanged
    {
        public CollectionChangedWeakEventListener(T source, Action<T, NotifyCollectionChangedEventArgs> handler)
            : base(source, handler)
        {
            source.CollectionChanged += HandleEvent;
        }

        protected override void StopListening(T source) => source.CollectionChanged -= HandleEvent;
    }

    internal class TypedWeakEventListener<T, TArgs, THandler> : WeakEventListenerBase<T, TArgs>
        where T : class
        where TArgs : EventArgs
        where THandler : Delegate
    {
        private readonly Action<T, THandler> unregister;

        public TypedWeakEventListener(T source, Action<T, THandler> register, Action<T, THandler> unregister, Action<T, TArgs> handler)
            : base(source, handler)
        {
            if (register == null)
            {
                throw new ArgumentNullException(nameof(register));
            }

            this.unregister = unregister ?? throw new ArgumentNullException(nameof(unregister));
            register(source, (THandler)Delegate.CreateDelegate(typeof(THandler), this, nameof(HandleEvent)));
        }

        protected override void StopListening(T source)
        {
            unregister(source, (THandler)Delegate.CreateDelegate(typeof(THandler), this, nameof(HandleEvent)));
        }
    }

    internal class WeakEventListener<T, TArgs> : WeakEventListenerBase<T, TArgs>

        where T : class
        where TArgs : EventArgs
    {
        private readonly EventInfo eventInfo;

        public WeakEventListener(T source, string eventName, Action<T, TArgs> handler)
            : base(source, handler)
        {
            eventInfo = source.GetType().GetEvent(eventName) ?? throw new ArgumentException("Unknown Event Name", nameof(eventName));
            if (eventInfo.EventHandlerType == typeof(EventHandler<TArgs>))
            {
                eventInfo.AddEventHandler(source, new EventHandler<TArgs>(HandleEvent));
            }
            else
            {
                eventInfo.AddEventHandler(source, Delegate.CreateDelegate(eventInfo.EventHandlerType, this, nameof(HandleEvent)));
            }
        }

        protected override void StopListening(T source)
        {
            if (eventInfo.EventHandlerType == typeof(EventHandler<TArgs>))
            {
                eventInfo.RemoveEventHandler(source, new EventHandler<TArgs>(HandleEvent));
            }
            else
            {
                eventInfo.RemoveEventHandler(source, Delegate.CreateDelegate(eventInfo.EventHandlerType, this, nameof(HandleEvent)));
            }
        }
    }
}

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore SA1402 // File may only contain a single type
