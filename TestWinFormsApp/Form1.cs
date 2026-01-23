using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
            btnMemberCacheAllMembers.BindEventToAction(nameof(Button.EnabledChanged), PChanged);
            m.BindProperties(x => x.Text, textBox1, x => x.Text, nameof(TextBox.TextChanged));
            m.Text = "123";
            m.PropertyChanged += M_PropertyChanged;
        }

        private void M_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            
        }

        Model m = new Model();

        private void PChanged(object sender, object e)
        {
            MessageBox.Show("Changed!");
        }

        private void BtnClick(object sender, object e)
        {
            MessageBox.Show("Click");
            btnMemberCacheAllMembers.Enabled = false;
        }
    }

    public class Model : PropertyChangeNotifier
    {
        public string Text
        {
            get => GetProperty<string>();
            set => SetProperty(value);
        }
    }
}
