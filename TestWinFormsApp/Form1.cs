using RuntimeStuff;
using RuntimeStuff.Extensions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;

namespace TestWinFormsApp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var sw = new Stopwatch();
            sw.Start();
            var formCache = MemberCache.Create(this.GetType());
            for (var i = 0; i < 1_000_000; i++)
            {
                formCache = MemberCache.Create(this.GetType());
                var p = formCache["BackgroundImageLayout"];
            }
            sw.Stop();
            var ms1 = sw.ElapsedMilliseconds;

            sw.Restart();
            var count1 = formCache.CachedMembersCount;
            formCache.CreateInternalCaches();
            formCache.CreateInternalCaches();
            var count2 = formCache.CachedMembersCount;
            sw.Stop();
            var ms2 = sw.ElapsedMilliseconds;
            btnMemberCacheAllMembers.BindToAction(nameof(Button.Click), BtnClick);
            textBox1.BindToAction(nameof(TextBox.EnabledChanged), TextBoxEnabledChanged, (box, args) => checkBox1.Checked);
            m.BindProperties(x => x.Text, "PropertyChanged", textBox1, x => x.Text, nameof(TextBox.TextChanged));
            propertyGrid1.Subscribe(m, propertyGrid1.Refresh);
            m.Text = "123";
            m.BindToAction(x => x.Text, M_PropertyChanged); //m.PropertyChanged += M_PropertyChanged;
            propertyGrid1.SelectedObject = m;
            var oc = new ObservableCollection<object>();
            oc.BindToAction(BindCollectionChangedToAction);
            oc.Add(new object());
            textBox1.BindProperties(x => x.Text, nameof(TextBox.TextChanged), checkBox1, x => x.Checked, nameof(CheckBox.CheckedChanged), BindingDirection.OneWay, s => s.IsNumber() && Convert.ToInt64(s) % 2 == 0);
            
            Obj.Set(dataGridView1, "DoubleBuffered", true);
            m.BindToProperty(x => x.IsFree, btnLoad, x => x.Enabled);
            m.BindToAction(x => x.Number, (s, e) => MessageBox.Show(e.PropertyName));
        }

        private void M_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
        }

        private void BindCollectionChangedToAction(object sender, object args)
        {
        }

        private readonly Model m = new();

        private void TextBoxEnabledChanged(object sender, object e)
        {
            MessageBox.Show("Changed!");
        }

        private void BtnClick(object sender, object e)
        {
            MessageBox.Show("Click");
            textBox1.Enabled = !textBox1.Enabled;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (m.Text != textBox1.Text)
            {
            }
        }

        private async void btnLoad_Click(object sender, EventArgs e)
        {
            m.IsFree = false;
            var dt = new DataTable();
            dt.AddCol("ID", typeof(int));
            dt.AddRow(1).AddRow(2).AddRow(3);
            //dataGridView1.DataSource = null;
            //using (var con = new SqlConnection().Server("serv40").Database("Tamuz").TrustCertificate(true).IntegratedSecurity(true))
            //{
            //    var dt = await con.ToDataTableAsync("select top 10000 * from products", valueConverter: (s, v, c) => v is string str ? str.Trim() : v);
            //    dataGridView1.DataSource = dt;
            //    dt = null;
            //}
            dataGridView1.DataSource = dt;
            m.IsFree = true;
        }

        private void dataGridView1_Click(object sender, EventArgs e)
        {
            btnLoad.Enabled = true;
        }
    }

    public class Model : PropertyObserver
    {
        public string Text
        {
            get => Get<string>();
            set => Set(value);
        }

        public int Number
        {
            get => Get<int>();
            set => Set(value);
        }

        public bool IsFree
        {
            get => Get<bool>();
            set => Set(value);
        }
    }
}