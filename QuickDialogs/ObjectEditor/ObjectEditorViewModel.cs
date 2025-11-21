using RuntimeStuff;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using RuntimeStuff.Extensions;

namespace QuickDialogs.Core.ObjectEditor
{
    public interface IObjectEditorViewModel<TObject>
    {
        TObject SelectedObject { get; }
        BindingListView<ObjectPropertyInfo> Properties { get; }
        Dictionary<string, ObjectPropertyInfo> PropertyMap { get; }
        ObjectEditorConfiguration<TObject> Configuration { get; }
    }

    public class ObjectEditorViewModel : ObjectEditorViewModel<object>
    {
        public new ObjectEditorConfiguration Configuration { get; set; }
    }

    
    public class ObjectEditorViewModel<TObject> : QuickDialogsViewModelBase
    {
        private bool _canAccept = true;
        private MemberInfoEx _focusedPropertyInfo;
        private string _focusedPropertyName;
        private object _focusedPropertyValue;
        private bool _hasChanges;
        internal DynamicPropertyObject _do = new DynamicPropertyObject();

        private BindingListView<ObjectPropertyInfo> _properties =
            new BindingListView<ObjectPropertyInfo>();

        private object _selectedObject;
        private Type _selectedObjectType;

        public Dictionary<string, ObjectPropertyInfo> PropertyMap =
            new Dictionary<string, ObjectPropertyInfo>();

        public ObjectEditorViewModel()
        {
            Configuration = new ObjectEditorConfiguration<TObject>(this);
            RaiseCanAcceptChanged();
        }

        public BindingListView<ObjectPropertyInfo> Properties
        {
            get => _properties;
            private set => SetProperty(ref _properties, value);
        }

        private List<string> Errors { get; set; } = new List<string>();

        public object SelectedObject
        {
            get => _selectedObject;
            set
            {
                SetProperty(ref _selectedObject, value, OnSelectedObjectChanged);
                _do = new DynamicPropertyObject(_selectedObject);
            }
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

        public ObjectPropertyInfo Property(string propertyName)
        {
            return PropertyMap.TryGetValue(propertyName, out var info) ? info : null;
        }

        public ObjectPropertyInfo Property(Expression<Func<TObject, object>> propertySelector)
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
            opi.Value = opi.Getter(SelectedObject);
        }

        private void OnSelectedObjectTypeChanged()
        {
            if (_selectedObjectType == null)
                return;

            var mi = MemberInfoEx.Create(_selectedObjectType);
            PropertyMap =
                mi.PublicProperties.ToDictionary(x => x.Key,
                    m => new ObjectPropertyInfo(SelectedObject, m.Value));

            Properties = new BindingListView<ObjectPropertyInfo>(PropertyMap.Values);
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