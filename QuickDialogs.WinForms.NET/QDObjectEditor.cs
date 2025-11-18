using QuickDialogs.Core.ObjectEditor;
using RuntimeStuff;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace QuickDialogs.WinForms.NET
{
    public partial class QDObjectEditor : UserControl, INotifyPropertyChanged
    {
        private ObjectEditorViewModel<object> _vm = new ObjectEditorViewModel();
        private readonly PropertyChangeNotifier _notifier = new PropertyChangeNotifier();

        public QDObjectEditor()
        {
            InitializeComponent();
            //dataGridView1.AllowUserToAddRows = false;
            //dataGridView1.AllowUserToDeleteRows = false;

            //dataGridView1.DataBindings.Add(nameof(DataGridView.DataSource), _vm, nameof(ObjectEditorViewModel.Properties), true, DataSourceUpdateMode.OnPropertyChanged);
            //this.DataBindings.Add(nameof(SelectedObject), _vm, nameof(ObjectEditorViewModel.SelectedObject), true, DataSourceUpdateMode.OnPropertyChanged);
            //var dpo = new DynamicPropertyObject();
            //propertyGrid1.SelectedObject = dpo;
            //dpo.AddProperty("Date", typeof(DateTime), DateTime.Now);
            //dpo.ClearProperties();
            //dpo.SetValue("VM", 123);
            //dpo.SetValue("Date", DateTime.Now);
            //DynamicPropertyDescriptor.Editors[typeof(DateTime)] = typeof(DateTimePickerInlineEditor);
            //_vm.SelectedObject
        }

        object _selectedObject;

        [Browsable(false)]
        public object SelectedObject
        {
            get => _selectedObject;
            set
            {
                _notifier.SetProperty(ref _selectedObject, value);
                propertyGrid1.SelectedObject = _selectedObject;
            }
        }

        public ObjectEditorConfiguration<T> SetUp<T>(T selectedObject = default)
        {
            _vm = new ObjectEditorViewModel();
            if (_vm is ObjectEditorViewModel<T> vm)
            {
                vm.SelectedObject = selectedObject;
                return vm.Configuration;
            }

            throw new NullReferenceException(nameof(_vm));
        }

        public ObjectEditorConfiguration Configuration => ((ObjectEditorViewModel)_vm).Configuration;

        public event PropertyChangedEventHandler PropertyChanged
        {
            add => _notifier.PropertyChanged += value;
            remove => _notifier.PropertyChanged -= value;
        }
    }
}
