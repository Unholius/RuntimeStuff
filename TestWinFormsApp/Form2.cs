using RuntimeStuff;

namespace TestWinFormsApp
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }

        private void btnSendMessageToForm1_Click(object sender, EventArgs e)
        {
            MessageBus.SingleThreaded["my_form"].Publish("123");
        }
    }
}
