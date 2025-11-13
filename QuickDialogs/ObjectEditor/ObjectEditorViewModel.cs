using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using RuntimeStuff;
using RuntimeStuff.UI.Core;

namespace QuickDialogs.Core.ObjectEditor
{
    public class ObjectEditorViewModel : ObjectEditorViewModel<object>
    {
    }

    
    public class ObjectEditorViewModel<TObject> : QuickDialogsViewModelBase
    {
        private bool _canAccept = true;
        private MemberInfoEx _focusedPropertyInfo;
        private string _focusedPropertyName;
        private object _focusedPropertyValue;
        private bool _hasChanges;

        private BindingListView<ObjectPropertyInfo<TObject>> _properties =
            new BindingListView<ObjectPropertyInfo<TObject>>();

        private TObject _selectedObject;
        private Type _selectedObjectType;

        internal Dictionary<string, ObjectPropertyInfo<TObject>> PropertyMap =
            new Dictionary<string, ObjectPropertyInfo<TObject>>();

        public ObjectEditorViewModel()
        {
            Configuration = new ObjectEditorConfiguration<TObject>(this);
            RaiseCanAcceptChanged();
        }

        public BindingListView<ObjectPropertyInfo<TObject>> Properties
        {
            get => _properties;
            private set => SetProperty(ref _properties, value);
        }

        private List<string> Errors { get; set; } = new List<string>();

        public TObject SelectedObject
        {
            get => _selectedObject;
            set => SetProperty(ref _selectedObject, value, OnSelectedObjectChanged);
        }

        public bool CanAccept
        {
            get => _canAccept;
            private set => SetProperty(ref _canAccept, value, CanAcceptChanged);
        }

        public bool HasChanges
        {
            get => _hasChanges;
            private set => SetProperty(ref _hasChanges, value, ObjectChanged);
        }

        public Type SelectedObjectType
        {
            get => _selectedObjectType;
            private set => SetProperty(ref _selectedObjectType, value, OnSelectedObjectTypeChanged);
        }

        public MemberInfoEx FocusedProperty
        {
            get => _focusedPropertyInfo;
            set => SetProperty(ref _focusedPropertyInfo, value);
        }

        public string FocusedPropertyName
        {
            get => _focusedPropertyName;
            set => SetProperty(ref _focusedPropertyName, value);
        }

        public object FocusedPropertyValue
        {
            get => _focusedPropertyValue;
            set => SetProperty(ref _focusedPropertyValue, value);
        }

        public ObjectEditorConfiguration<TObject> Configuration { get; set; }

        public ObjectPropertyInfo<TObject> Property(string propertyName)
        {
            return PropertyMap.TryGetValue(propertyName, out var info) ? info : null;
        }

        public ObjectPropertyInfo<TObject> Property(Expression<Func<TObject, object>> propertySelector)
        {
            return PropertyMap[propertySelector.GetPropertyName()];
        }

        public event Action<bool> CanAcceptChanged;
        public event Action<bool> ObjectChanged;

        private INotifyPropertyChanged _subscribedNotifyPropertyChanged;

        private void OnSelectedObjectChanged()
        {
            if (_subscribedNotifyPropertyChanged != null)
                _subscribedNotifyPropertyChanged.PropertyChanged -= SelectedObjectPropertyChanged;

            if (_selectedObject is INotifyPropertyChanged propertyChanged)
            {
                propertyChanged.PropertyChanged += SelectedObjectPropertyChanged;
                _subscribedNotifyPropertyChanged = propertyChanged;
            }
            else
            {
                _subscribedNotifyPropertyChanged = null;
            }

            SelectedObjectType = _selectedObject?.GetType();
        }

        private void SelectedObjectPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var opi = PropertyMap[e.PropertyName];
            opi.Value = opi.GetValue(SelectedObject);
        }

        private void OnSelectedObjectTypeChanged()
        {
            if (_selectedObjectType == null)
                return;

            var mi = MemberInfoEx.Create(_selectedObjectType);
            PropertyMap =
                mi.PublicProperties.ToDictionary(x => x.Key,
                    m => new ObjectPropertyInfo<TObject>(this, SelectedObject, m.Value));

            Properties = new BindingListView<ObjectPropertyInfo<TObject>>(PropertyMap.Values);
        }

        public void RaiseCanAcceptChanged()
        {
            CanAccept = PropertyMap.Values.All(p => p.IsValueValid(p.Value));
            CanAcceptChanged?.Invoke(CanAccept);
            Errors.Clear();
            var errorMessages = Properties.Where(x => !string.IsNullOrWhiteSpace(x.ErrorMessage)).Select(x => $"[{x.Name}]: {x.ErrorMessage} Текущее значение: '{x.DisplayValue}'").ToArray();
            if (errorMessages.Any())
                Errors.AddRange(errorMessages);
        }

        public void RaiseHasChangesChanged()
        {
            HasChanges = PropertyMap.Values.Any(p => p.Changed);
        }
    }
}