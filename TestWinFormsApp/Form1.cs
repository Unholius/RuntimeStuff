using System.ComponentModel;
using System.Diagnostics;
using RuntimeStuff;
using RuntimeStuff.Extensions;

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
            btnMemberCacheAllMembers.BindEventToAction(nameof(Button.Click), BtnClick);
            textBox1.BindEventToAction(nameof(TextBox.EnabledChanged), TextBoxEnabledChanged);
            m.BindProperties(x => x.Text, "PropertyChanged", textBox1, x => x.Text, nameof(TextBox.TextChanged));
            propertyGrid1.Subscribe(m, nameof(INotifyPropertyChanged.PropertyChanged), (s, e) => s.Refresh());
            m.Text = "123";
            m.BindPropertyChangedToAction(M_PropertyChanged); //m.PropertyChanged += M_PropertyChanged;
            propertyGrid1.SelectedObject = m;
        }

        private void M_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {

        }

        Model m = new Model();

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
    }

    public class Model : PropertyObserver
    {
        public string Text
        {
            get => Get<string>();
            set => Set(value);
        }

        public Model? Child { get; set; }
    }
}
