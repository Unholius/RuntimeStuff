using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RuntimeStuff
{
    /// <summary>
    /// Базовый класс, предоставляющий реализацию интерфейса <see cref="INotifyPropertyChanged"/> и
    /// вспомогательные методы для уведомления об изменении свойств, а также автоматического управления подписками на
    /// изменения во вложенных объектах.
    /// </summary>
    /// <remarks>Класс предназначен для упрощения реализации паттерна "наблюдатель" в моделях данных, поддерживающих
    /// привязку свойств (например, в MVVM). Реализует автоматическую отписку и повторную подписку на события <see
    /// cref="PropertyChanged"/> у вложенных объектов, что предотвращает утечки памяти и облегчает управление зависимостями
    /// между объектами. Является потокобезопасным для операций подписки и отписки. Для корректного освобождения ресурсов
    /// рекомендуется явно вызывать <see cref="Dispose()"/> при уничтожении экземпляра.</remarks>
    public class PropertyChangeNotifier : INotifyPropertyChanged, INotifyPropertyChanging, IDisposable
    {
        /// <summary>
        /// Сопоставление вложенных объектов (<see cref="INotifyPropertyChanged"/>) с их обработчиками
        /// <see cref="PropertyChangedEventHandler"/>. Используется для автоматической отписки
        /// при замене вложенного объекта.
        /// </summary>
        private readonly Cache<object, EventHandlers> _subscriptions = new Cache<object, EventHandlers>(x => new EventHandlers());

        private readonly object _syncRoot = new object();

        /// <summary>
        /// Событие <see cref="PropertyChanged"/> для внешних подписчиков.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Событие <see cref="PropertyChanging"/> для внешних подписчиков.
        /// </summary>
        public event PropertyChangingEventHandler PropertyChanging;

        /// <summary>
        /// Вызывает событие <see cref="PropertyChanging"/> для указанного свойства.
        /// </summary>
        /// <param name="propertyName">Имя свойства. Если не задано, используется имя вызывающего члена.</param>
        /// <exception cref="ArgumentNullException">Если <paramref name="propertyName"/> равен <c>null</c>.</exception>
        public virtual void OnPropertyChanging([CallerMemberName] string propertyName = null) => PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));


        /// <summary>
        /// Вызывает событие <see cref="PropertyChanged"/> для указанного свойства.
        /// </summary>
        /// <param name="propertyName">Имя свойства. Если не задано, используется имя вызывающего члена.</param>
        /// <exception cref="ArgumentNullException">Если <paramref name="propertyName"/> равен <c>null</c>.</exception>
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Устанавливает значение backing field и вызывает уведомление об изменении свойства,
        /// если значение фактически изменилось.
        /// </summary>
        /// <typeparam name="T">Тип свойства.</typeparam>
        /// <param name="field">Ссылка на backing field свойства.</param>
        /// <param name="value">Новое значение для поля.</param>
        /// <param name="onChanged">Действие после изменения свойства</param>
        /// <param name="propertyName">Имя свойства. Если не задано, используется имя вызывающего члена.</param>
        /// <returns><c>true</c>, если значение было изменено и было вызвано событие; иначе <c>false</c>.</returns>
        protected bool SetProperty<T>(ref T field, T value, Action onChanged = null, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            OnPropertyChanging(propertyName);
            field = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Устанавливает значение backing field и вызывает уведомление об изменении свойства,
        /// если значение фактически изменилось.
        /// </summary>
        /// <typeparam name="T">Тип свойства.</typeparam>
        /// <param name="field">Ссылка на backing field свойства.</param>
        /// <param name="value">Новое значение для поля.</param>
        /// <param name="onChanged">Действие после изменения свойства</param>
        /// <param name="propertyName">Имя свойства. Если не задано, используется имя вызывающего члена.</param>
        /// <returns><c>true</c>, если значение было изменено и было вызвано событие; иначе <c>false</c>.</returns>
        protected bool SetProperty<T>(ref T field, T value, Action<T> onChanged, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            OnPropertyChanging(propertyName);
            field = value;
            onChanged?.Invoke(value);
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Комбинированная операция: сначала выполняет привязку обработчика изменения свойства у вложенного объекта,
        /// затем устанавливает новое значение для свойства текущего объекта. Если значение изменилось и
        /// <paramref name="thisPropertyChangeHandler"/> не равен <c>null</c>, он будет вызван.
        /// </summary>
        /// <typeparam name="T">Тип вложенного объекта, реализующего <see cref="INotifyPropertyChanged"/>.</typeparam>
        /// <param name="oldValue">Ссылка на текущее (старое) значение свойства (backing field).</param>
        /// <param name="newValue">Новое значение, которое будет присвоено.</param>
        /// <param name="childPropertyName">Имя свойства вложенного объекта, за изменением которого нужно следить.</param>
        /// <param name="childPropertyChangeHandler">Действие, вызываемое при изменении <paramref name="childPropertyName"/> у вложенного объекта.</param>
        /// <param name="thisPropertyChangeHandler">Действие, вызываемое после успешного изменения свойства текущего объекта.</param>
        /// <param name="propertyName">Имя свойства текущего объекта. Если не задано, используется имя вызывающего члена.</param>
        protected void SetAndBindPropertyChange<T>(ref T oldValue, T newValue, string childPropertyName, Action childPropertyChangeHandler, Action<string> thisPropertyChangeHandler = null, [CallerMemberName] string propertyName = null) where T : class, INotifyPropertyChanged
        {
            BindPropertyChange(ref oldValue, newValue, childPropertyName, childPropertyChangeHandler);
            if (SetProperty(ref oldValue, newValue, (Action)null, propertyName))
                thisPropertyChangeHandler?.Invoke(propertyName);
        }

        /// <summary>
        /// Выполняет подписку на изменение указанного свойства вложенного объекта и обеспечивает
        /// автоматическую отписку от старого объекта (если он был). Вызывать до присвоения нового значения
        /// в backing field.
        /// </summary>
        /// <typeparam name="T">Тип вложенного объекта, реализующий <see cref="INotifyPropertyChanged"/>.</typeparam>
        /// <param name="oldValue">Ссылка на текущее (старое) значение свойства (backing field).</param>
        /// <param name="newValue">Новое значение вложенного объекта, для которого нужно установить подписку.</param>
        /// <param name="childPropertyName">Имя свойства во вложенном объекте, изменение которого должно вызывать <paramref name="childPropertyChangeHandler"/>.</param>
        /// <param name="childPropertyChangeHandler">Действие, выполняемое при изменении <paramref name="childPropertyName"/> во вложенном объекте.</param>
        /// <remarks>
        /// Этот метод отписывает обработчик у старого объекта (если он присутствует) и подписывает новый обработчик
        /// к <paramref name="newValue"/>; обработчики хранятся в словаре <see cref="_subscriptions"/> для корректной последующей отписки.
        /// </remarks>
        protected void BindPropertyChange<T>(ref T oldValue, T newValue, string childPropertyName, Action childPropertyChangeHandler) where T : class, INotifyPropertyChanged
        {
            lock (_syncRoot)
            {
                // Отписываем старый объект, если он был
                if (oldValue != null && _subscriptions.TryGetValue(oldValue, out var oldHandler))
                {
                    oldValue.PropertyChanged -= oldHandler.Changed;
                    _subscriptions.Remove(oldValue);
                }

                if (newValue != null && childPropertyChangeHandler != null)
                {
                    var newPropertyChangedEventHandler = (PropertyChangedEventHandler)((sender, args) =>
                    {
                        if (childPropertyName == null || args.PropertyName == childPropertyName)
                            childPropertyChangeHandler();
                    });

                    newValue.PropertyChanged += newPropertyChangedEventHandler;
                    var s = _subscriptions.Get(newValue);
                    s.Changed = newPropertyChangedEventHandler;

                    if (typeof(T).IsImplements<INotifyPropertyChanging>())
                    {
                        var newPropertyChangingEventHandler = (PropertyChangingEventHandler)((sender, args) =>
                        {
                            if (childPropertyName == null || args.PropertyName == childPropertyName)
                                OnPropertyChanging(childPropertyName);
                        });
                        ((INotifyPropertyChanging)newValue).PropertyChanging += newPropertyChangingEventHandler;
                        s.Changing = newPropertyChangingEventHandler;
                    }
                }
            }
        }

        /// <summary>
        /// Упрощённая перегрузка <see cref="BindPropertyChange{T}(ref T, T, string, Action)"/> для подписки
        /// на любые изменения у вложенного объекта (без фильтрации по имени свойства).
        /// </summary>
        /// <typeparam name="T">Тип вложенного объекта, реализующий <see cref="INotifyPropertyChanged"/>.</typeparam>
        /// <param name="oldValue">Ссылка на текущее (старое) значение свойства (backing field).</param>
        /// <param name="newValue">Новое значение вложенного объекта, для которого нужно установить подписку.</param>
        /// <param name="handler">Действие, выполняемое при любом изменении во вложенном объекте.</param>
        protected void BindPropertyChange<T>(ref T oldValue, T newValue, Action handler) where T : class, INotifyPropertyChanged
        {
            BindPropertyChange(ref oldValue, newValue, null, handler);
        }

        /// <summary>
        /// Очистка управляемых ресурсов. Отписывает все внутренние подписки и очищает словарь подписок.
        /// </summary>
        /// <param name="disposing"><c>true</c> при вызове из <see cref="Dispose()"/>; <c>false</c> при вызове из финализатора.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            lock (_syncRoot)
            {
                foreach (var kvp in _subscriptions)
                {
                    if (kvp.Key is INotifyPropertyChanged npc1)
                        npc1.PropertyChanged -= kvp.Value.Changed;

                    if (kvp.Key is INotifyPropertyChanging npc2)
                        npc2.PropertyChanging -= kvp.Value.Changing;
                }

                _subscriptions.Clear();
            }
        }

        /// <summary>
        /// Выполняет освобождение ресурсов, вызывает <see cref="Dispose(bool)"/> и подавляет финализацию.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    class EventHandlers
    {
        public PropertyChangedEventHandler Changed;
        public PropertyChangingEventHandler Changing;
    }
}