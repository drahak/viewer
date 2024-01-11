﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WeifenLuo.WinFormsUI.Docking;

namespace Viewer.Core.UI
{
    public abstract class Presenter<TView> : IDisposable where TView : class, IWindowView
    {
        public class AutoEventSubscription
        {
            public EventInfo Event { get; set; }
            public Delegate Handler { get; set; }
        }

        public class SubscriptionLifetime 
        {
            private readonly object _view;
            private readonly List<AutoEventSubscription> _subscriptions;

            public SubscriptionLifetime(object view, List<AutoEventSubscription> subscriptions)
            {
                _view = view;
                _subscriptions = subscriptions;
            }

            public void Unsubscribe()
            {
                foreach (var subscription in _subscriptions)
                {
                    subscription.Event.RemoveEventHandler(_view, subscription.Handler);
                }
                _subscriptions.Clear();
            }
        }

        /// <summary>
        /// Event subscriptions
        /// </summary>
        private readonly List<SubscriptionLifetime> _subscriptions = new List<SubscriptionLifetime>();

        /// <summary>
        /// Main view of the presenter
        /// </summary>
        public TView View { get; protected set; }

        /// <summary>
        /// Automatically subscribe to all view events. For each EventName in <paramref name="view"/>
        /// find method <paramref name="eventHandlerPrefix"/>_EventName in this presenter and
        /// subsribe this method to the event.
        /// </summary>
        /// <typeparam name="T">Type of the view</typeparam>
        /// <param name="view">View</param>
        /// <param name="eventHandlerPrefix">Prefix of event handler method names</param>
        /// <returns>
        /// Event subscription lifetime. It is automatically disposed when you <see cref="Dispose"/>
        /// this presenter.
        /// </returns>
        public SubscriptionLifetime SubscribeTo<T>(T view, string eventHandlerPrefix)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));
            if (eventHandlerPrefix == null)
                throw new ArgumentNullException(nameof(eventHandlerPrefix));

            var result = new List<AutoEventSubscription>();

            var presenterType = GetType();
            foreach (var eventInfo in view.GetType().GetEvents())
            {
                // find event handler method in the presenter
                var handlerName = eventHandlerPrefix + "_" + eventInfo.Name;
                var method = presenterType.GetMethod(handlerName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                { 
                    // subscribe to that event
                    var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, this, method);
                    eventInfo.AddEventHandler(view, handler);
                    result.Add(new AutoEventSubscription
                    {
                        Event = eventInfo,
                        Handler = handler
                    });
                }
            }

            var lifetime =  new SubscriptionLifetime(view, result);
            _subscriptions.Add(lifetime);
            return lifetime;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var subscription in _subscriptions)
                {
                    subscription.Unsubscribe();
                }
                View.Dispose();
                View = null;
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
