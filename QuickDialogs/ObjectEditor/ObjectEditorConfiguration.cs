using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using RuntimeStuff;
using RuntimeStuff.Extensions;

namespace QuickDialogs.Core.ObjectEditor
{
    public class ObjectEditorConfiguration : ObjectEditorConfiguration<object>
    {
        public ObjectEditorConfiguration(ObjectEditorViewModel<object> viewModel) : base(viewModel)
        {
        }
    }

    public interface IObjectEditorConfiguration<TObject>
    {

    }

    public class ObjectEditorConfiguration<TObject>
    {
        private readonly ObjectEditorViewModel<TObject> _viewModel;

        public ObjectEditorConfiguration(ObjectEditorViewModel<TObject> viewModel)
        {
            _viewModel = viewModel;
        }

        public ObjectEditorConfiguration<TObject> SetRule(Expression<Func<TObject, object>> propertySelector,
            Func<TObject, bool> rule, string errorMessage)
        {
            return SetRule(propertySelector.GetPropertyName(), rule, errorMessage);
        }

        public ObjectEditorConfiguration<TObject> SetRule(string propertyName, Func<TObject, bool> rule, string errorMessage)
        {
            var opi = _viewModel.PropertyMap[propertyName];
            opi.IsValueValid = rule.ConvertFunc();
            opi.InvalidValueErrorMessage = errorMessage;
            return this;
        }

        public ObjectEditorConfiguration<TObject> SetRule<TProp>(Expression<Func<TObject, TProp>> propertySelector, TProp minValue, TProp maxValue, string errorMessage = null)
        {
            return SetRule(propertySelector.GetPropertyName(), minValue, maxValue, errorMessage);
        }

        public ObjectEditorConfiguration<TObject> SetRule<TProp>(string propertyName, TProp minValue, TProp maxValue, string errorMessage = null)
        {
            errorMessage = errorMessage ?? $"Значение должно быть от {minValue} до {maxValue} включительно!";
            var opi = _viewModel.PropertyMap[propertyName];
            opi.InvalidValueErrorMessage = errorMessage;
            opi.MinValue = minValue;
            opi.MaxValue = maxValue;
            opi.IsValueValid = x =>
            {
                if (x == null) return false;
                var comparer = Comparer<TProp>.Default;
                try
                {
                    var value = (TProp)x;
                    return comparer.Compare(value, (TProp)opi.MinValue) >= 0 &&
                           comparer.Compare(value, (TProp)opi.MaxValue) <= 0;
                }
                catch
                {
                    return false;
                }
            };

            return this;
        }

        public ObjectEditorConfiguration<TObject> SetRule<TProp>(Expression<Func<TObject, TProp>> propertySelector, IEnumerable<TProp> allowedValues, string errorMessage = null)
        {
            return SetRule(propertySelector.GetPropertyName(), allowedValues, errorMessage);
        }

        public ObjectEditorConfiguration<TObject> SetRule<TProp>(string propertyName, IEnumerable<TProp> allowedValues, string errorMessage)
        {
            errorMessage = errorMessage ?? "Значение должно быть одним из допустимых!";
            var opi = _viewModel.PropertyMap[propertyName];
            opi.AllowedValues = allowedValues.Cast<object>().ToArray();
            opi.InvalidValueErrorMessage = errorMessage;
            opi.IsValueValid = x => x.In(opi.AllowedValues);

            return this;
        }

        public ObjectEditorConfiguration<TObject> SetVisible(bool visibility, params Expression<Func<TObject, object>>[] propertySelectors)
        {
            return SetVisible(visibility, propertySelectors.Select(x => x.GetPropertyName()).ToArray());
        }

        public ObjectEditorConfiguration<TObject> SetVisible(bool visibility, params string[] propertyNames)
        {
            var propertyFilter = "";
            foreach (var p in propertyNames)
                propertyFilter += $"[{nameof(ObjectPropertyInfo.Name)}] {(visibility ? "==" : "!=")} '{p}' &&";

            propertyFilter = propertyFilter.TrimEnd(' ', '&');

            _viewModel.Properties.Filter = propertyFilter;
            return this;
        }

        public ObjectEditorConfiguration<TObject> SetDisplayFormat(string displayFormat, params Expression<Func<TObject, object>>[] propertySelector)
        {
            return SetDisplayFormat(displayFormat, propertySelector.Select(x => x.GetPropertyName()).ToArray());
        }

        public ObjectEditorConfiguration<TObject> SetDisplayFormat(string displayFormat, params string[] propertyName)
        {
            foreach (var pn in propertyName)
            {
                var opi = _viewModel.PropertyMap[pn];
                opi.DisplayFormat = displayFormat;
            }

            return this;
        }

        public ObjectEditorConfiguration<TObject> SetEditFormat(string editFormat, params Expression<Func<TObject, object>>[] propertySelectors)
        {
            return SetEditFormat(editFormat, propertySelectors.Select(x => x.GetPropertyName()).ToArray());
        }

        public ObjectEditorConfiguration<TObject> SetEditFormat(string editFormat, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                var opi = _viewModel.PropertyMap[propertyName];
                opi.EditFormat = editFormat;
            }
            return this;
        }

        public ObjectEditorConfiguration<TObject> SetEditable(bool editable, params Expression<Func<TObject, object>>[] propertySelectors)
        {
            return SetEditable(editable, propertySelectors.Select(x => x.GetPropertyName()).ToArray());
        }

        public ObjectEditorConfiguration<TObject> SetEditable(bool editable, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                var opi = _viewModel.PropertyMap[propertyName];
                opi.Editable = editable;
            }

            return this;
        }

        public ObjectEditorConfiguration<TObject> SetReadOnly(bool readOnly, params Expression<Func<TObject, object>>[] propertySelectors)
        {
            return SetReadOnly(readOnly, propertySelectors.Select(x => x.GetPropertyName()).ToArray());
        }

        public ObjectEditorConfiguration<TObject> SetReadOnly(bool readOnly, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                var opi = _viewModel.PropertyMap[propertyName];
                opi.ReadOnly = readOnly;
            }

            return this;
        }

        public ObjectEditorConfiguration<TObject> AddProperty<TValue>(string propertyName, TValue value)
        {
            _viewModel._do.AddProperty(propertyName, value?.GetType() ?? typeof(TValue));
            //_viewModel.SelectedObject = _do;
            return this;
        }

        public ObjectEditorConfiguration<TObject> AddProperty(string propertyName, object value)
        {
            _viewModel._do.AddProperty(propertyName, value?.GetType());
            return this;
        }
    }
}