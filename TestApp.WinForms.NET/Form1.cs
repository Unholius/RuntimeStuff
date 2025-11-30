using RuntimeStuff;
using RuntimeStuff.MSTests.Models;

namespace TestApp.WinForms.NET
{
    public partial class Form1 : Form
    {
        BindingListView<TestClassWithBasicPropertiesWithNotifyPropertyChanged> ds = new BindingListView<TestClassWithBasicPropertiesWithNotifyPropertyChanged>();
        public Form1()
        {
            InitializeComponent();
            ds.SuspendListChangedEvents = true;
            for (var i = 0; i < 1_000; i++)
                ds.Add(new TestClassWithBasicPropertiesWithNotifyPropertyChanged(i));
            ds.SuspendListChangedEvents = false;
            dataGridView1.DataSource = ds;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            var obj = new TestClassWithBasicProperties();
            //qdObjectEditor1.SetUp(obj)
            //    .AddProperty("Int69", 123)
            //    .SetDisplayFormat("{0:0000}", x => x.Int32);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ds.SetRowVisible(0, false);
            //this.qdObjectEditor1.SelectedObject = new TestClassWithBasicProperties();
        }
        private bool visible = true;
        private void button1_Click(object sender, EventArgs e)
        {
            visible = !visible;
            ds.SetAllRowsVisible(visible);
            ds.RemoveSort();
            ds.Cleanup();
        }

        private void btnApplyFilter_Click(object sender, EventArgs e)
        {
            ds.Filter = textBox1.Text;
            ds.ApplyFilterAndSort();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            ds.Add(new TestClassWithBasicPropertiesWithNotifyPropertyChanged(DateTime.Now.Second));
        }
    }
}
