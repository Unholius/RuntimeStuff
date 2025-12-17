using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RuntimeStuff;

namespace QuickDialogs.Core.ObjectEditor
{
    public class ObjectPropertyInfo : MemberInfoEx, INotifyPropertyChanged
    {
        private bool _changed;
        private string _displayValue;
        private bool _editable = true;
        private string _errorMessage;
        private Func<object, bool> _isValueValid;
        private bool _readOnly;
        private object _value;
        private readonly PropertyChangeNotifier _notifier = new PropertyChangeNotifier();

        public ObjectPropertyInfo(object source, MemberInfoEx memberInfoEx) : base(memberInfoEx)
        {
            IsValueValid = _ => true;
            EditFormat = string.Empty;
            InvalidValueErrorMessage = string.Empty;
            OriginalValue = memberInfoEx.GetValue(source);
            _value = memberInfoEx.GetValue(source);
            _displayValue = string.Format(DisplayFormat, Value);
        }

        public object Value
        {
            get => _value;
            set
            {
                if (!Editable)
                    return;
                _notifier.SetProperty(ref _value, value, OnValueChanged);
            }
        }

        public object OriginalValue { get; internal set; }

        public string DisplayValue
        {
            get => _displayValue;
            internal set => _notifier.SetProperty(ref _displayValue, value);
        }

        public Func<object, bool> IsValueValid
        {
            get => _isValueValid;
            internal set => _notifier.SetProperty(ref _isValueValid, value, OnValueChanged);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            internal set => _notifier.SetProperty(ref _errorMessage, value, OnValueChanged);
        }

        public string InvalidValueErrorMessage { get; internal set; }
        public string DisplayFormat { get; internal set; } = "{0}";
        public string EditFormat { get; internal set; }

        public object MinValue { get; internal set; }
        public object MaxValue { get; internal set; }
        public object[] AllowedValues { get; internal set; }

        //public bool Visible => _viewModel.Properties.Contains(this);

        public bool Changed
        {
            get => _changed;
            internal set => _notifier.SetProperty(ref _changed, value);
        }

        public bool Editable
        {
            get => _editable;
            set
            {
                _readOnly = !value;
                _notifier.SetProperty(ref _editable, value);
            }
        }

        public bool ReadOnly
        {
            get => _readOnly;
            internal set
            {
                _editable = !value;
                _notifier.SetProperty(ref _readOnly, value);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnValueChanged()
        {
            Changed = !Equals(Value, OriginalValue);
            ErrorMessage = IsValueValid(Value) ? string.Empty : InvalidValueErrorMessage;
            DisplayValue = string.Format(DisplayFormat, Value);
            //_viewModel.RaiseCanAcceptChanged();
            //_viewModel.RaiseHasChangesChanged();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public sealed override string ToString()
        {
            return
                $"{string.Format(DisplayFormat, Value)}{(string.IsNullOrWhiteSpace(ErrorMessage) ? "" : $" ({ErrorMessage})")}";
        }
    }
}