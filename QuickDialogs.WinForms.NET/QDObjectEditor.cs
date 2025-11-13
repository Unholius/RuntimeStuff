using System.ComponentModel;
using System.Windows.Forms;
using QuickDialogs.Core.ObjectEditor;
using RuntimeStuff;

namespace QuickDialogs.WinForms.NET
{
    public partial class QDObjectEditor : UserControl, INotifyPropertyChanged
    {
        private readonly ObjectEditorViewModel _vm = new ObjectEditorViewModel();
        private readonly PropertyChangeNotifier _notifier = new PropertyChangeNotifier();

        public QDObjectEditor()
        {
            InitializeComponent();
            propertyGrid1.DataBindings.Add(nameof(PropertyGrid.SelectedObject), _vm, nameof(ObjectEditorViewModel.SelectedObject), true, DataSourceUpdateMode.OnPropertyChanged);
        }

        public object SelectedObject
        {
            get => _vm.SelectedObject;
            set
            {
                _vm.SelectedObject = value;
                _notifier.OnPropertyChanged();
            }
        }

        public ObjectEditorConfiguration Configuration => (ObjectEditorConfiguration)_vm.Configuration;

        public event PropertyChangedEventHandler PropertyChanged
        {
            add => _notifier.PropertyChanged += value;
            remove => _notifier.PropertyChanged -= value;
        }

    }
}
