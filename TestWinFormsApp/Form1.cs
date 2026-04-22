using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Runtime.Versioning;
using WinFormsExtensions;

namespace TestWinFormsApp
{
    [SupportedOSPlatform("windows")]
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public BindingCollection<FileItem> FileItems { get; set; } = new BindingCollection<FileItem>();

        private async void Form1_Load(object sender, EventArgs e)
        {
            //var sw = new Stopwatch();
            //sw.Start();
            //var formCache = MemberCache.Get(this.GetType());
            //for (var i = 0; i < 1_000_000; i++)
            //{
            //    formCache = MemberCache.Get(this.GetType());
            //    var p = formCache["BackgroundImageLayout"];
            //}
            //sw.Stop();
            //var ms1 = sw.ElapsedMilliseconds;

            //sw.Restart();
            //var count1 = formCache.CachedMembersCount;
            //formCache.CreateInternalCaches();
            //formCache.CreateInternalCaches();
            //var count2 = formCache.CachedMembersCount;
            //sw.Stop();
            //var ms2 = sw.ElapsedMilliseconds;
            //btnMemberCacheAllMembers.BindEventToAction(nameof(Button.Click), BtnClick);
            //textBox1.BindEventToAction(nameof(TextBox.EnabledChanged), TextBoxEnabledChanged, () => checkBox1.Checked);
            //m.BindPropertiesOnEvents("PropertyChanged", x => x.Text, textBox1, nameof(TextBox.TextChanged), x => x.Text, (s, e) => propertyGrid1.Refresh());
            //m.Text = "123";
            //propertyGrid1.SelectedObject = m;
            //var oc = new ObservableCollection<object>();
            //oc.BindCollectionChangedToAction(BindCollectionChangedToAction);
            //textBox1.BindToPropertyOnEvent(nameof(TextBox.TextChanged), x => x.Text, checkBox1, x => x.Checked, s => s.IsNumber() && Convert.ToInt64(s) % 2 == 0);

            Obj.Set(dgv, "DoubleBuffered", true);
            //_ = m.BindToProperty(x => x.IsFree, btnLoad, x => x.Enabled, x => !x);
            //m.BindPropertyChangeToAction(x => x.Number, () => MessageBox.Show(@"Number is Changed!"));
            //m.BindProperties(x => x.Number, m, x => x.Number);
            //MessageBus.SingleThreaded.Subscribe<ServerMessage>(OnServerMessage, SynchronizationContext.Current);

            ////EventHelper.BindProperties(textBox2, "Text", "TextChanged", label1, "Text");
            //EventHelper.BindEventToAction(textBox2, "TextChanged", () => label1.Text = textBox2.Text);
            //EventHelper.BindProperties(dataGridView1, "SelectedCells.Count", "SelectionChanged", btnLoad, "Text");
            dgv.ShowRowNumbers(true);
            dgv.SetRowColors(Color.LightGray);
            dgv.SetColumnMenu(true);
            //var gantt = new BindingList<GanttItem>();
            //var fromDate = DateTime.Now;
            //var endDate = DateTime.Now.AddDays(30);
            //dgv.AddGantt(fromDate, endDate, "From", "To", "dd/MM", "Childs");

            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            //await Task.Run(() =>
            //{
            //    var gi = new GanttItem() { From = fromDate, To = fromDate};
            //    gi.Childs.Add(new GanttItem() { From = fromDate.AddDays(2), To = fromDate.AddDays(4) });
            //    gi.Childs.Add(new GanttItem() { From = fromDate.AddDays(7), To = fromDate.AddDays(12) });
            //    gi.Childs.Add(new GanttItem() { From = fromDate.AddDays(14), To = fromDate.AddDays(15) });
            //    gi.Childs.Add(new GanttItem() { From = fromDate.AddDays(18), To = fromDate.AddDays(30) });
            //    gantt.Add(gi);
            //    for (int i = 0; i < 100_000; i++)
            //    {
            //        var rnd1 = DateTimeHelper.Random(fromDate, endDate);
            //        var ganttItem = new GanttItem() { From = rnd1, To = DateTimeHelper.Random(rnd1, endDate.AddDays(-10)) };
            //        gantt.Add(ganttItem);
            //        for (var j= 0; j < 4; j++)
            //        {
            //            ganttItem.Childs.Add(new GanttItem() { From = ganttItem.From.AddDays(2), To = DateTimeHelper.Random(rnd1, endDate) });
            //        }
            //    }
            //});
            //dgv.DataSource = gantt;
            //sw.Stop();
            //MessageBox.Show(sw.Elapsed.TotalSeconds.ToString());
            var ds4 = new ObservableCollectionEx<Dgv4View>();
            var item = new Dgv4View();
            item["Name"] = "Name";
            item["Id"] = 123;
            item[$"Property_{DateTime.Now.Ticks}"] = DateTime.Now.Ticks;
            ds4.Add(item);
            ds4.Add(new Dgv4View());
            dgvPage4.DataSource = ds4;
        }

        public class Dgv4View : ObservableObject
        {
            public int Id { get => Get<int>(); set => Set(value); }
        }

        private class ServerMessage
        {
            private string message;

            public ServerMessage()
            {
            }

            public ServerMessage(string message, bool offline)
            {
                Message = message;
                SenderId = Environment.ProcessId;
                Offline = offline;
            }

            public Guid Id { get; } = Guid.NewGuid();
            public DateTime Timestamp { get; set; } = DateTime.Now.ExactNow();

            public string Message
            {
                get => Offline ? throw new AccessViolationException(message) : message;
                set => message = value;
            }

            public int SenderId { get; }
            public bool Offline { get; }
        }

        private void OnServerMessage(ServerMessage message)
        {
            listBox1.Items.Add($"{message.Timestamp:HH:mm:ss.fff} (SenderId: {message.SenderId}, Id: {message.Id})");
        }

        private void OnMessage(string message)
        {
            btnOpenForm2.Text = message;
        }

        private void TextBoxEnabledChanged(object sender, object e)
        {
            MessageBox.Show("Changed because is Checked!");
        }

        private void BtnClick(object sender, object e)
        {
            MessageBox.Show("Click");
            textBox1.Enabled = !textBox1.Enabled;
        }

        private void dataGridView1_Click(object sender, EventArgs e)
        {
            btnLoad.Enabled = true;
        }

        private async void btnOpenForm2_Click(object sender, EventArgs e)
        {
            btnOpenForm2.Text = "Open Form 2";
            var f2 = new Form2();
            f2.ShowDialog();
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            await MessageBus.SingleThreaded.StartServer(12345);
            listBox1.Items.Add("Server started on port 12345");
        }

        private async void btnSendMessage_Click(object sender, EventArgs e)
        {
            await MessageBus.PublishAsync(new Uri("http://localhost:12345/"), new ServerMessage($"Msg_{Guid.NewGuid()}", chkOffline.Checked));
        }

        private void btnStopServer_Click(object sender, EventArgs e)
        {
            MessageBus.SingleThreaded.StopServer(12345);
            listBox1.Items.Add("Server stopped");
        }

        private void dgv_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            using (var con = new SqlConnection().Server("serv40").Database("Tamuz").IntegratedSecurity(true))
            {
                dgv.DataSource = con.ToDataTable("select top 1000 * from products");
            }

        }

        private void button1_Click(object sender, EventArgs e)
        {
            var x = dgvPage4.DataSource as IList<Dgv4View>;
            x[0][$"Property_{DateTime.Now.Ticks}"] = DateTime.Now.Ticks;
            dgvPage4.Columns.Clear();
            dgvPage4.DataSource = null;
            dgvPage4.DataSource = x;
        }
    }

    public class FileItem
    {
        public string FileName { get; set; }
        public long Size { get; set; }
        public DateTime Created { get; set; }
        public string Ext { get; set; }
    }

    public class GanttItem : INotifyPropertyChanged
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public List<GanttItem> Childs { get; set; } = new List<GanttItem>();
    }
}