using CSCore.CoreAudioAPI;
using System.Windows.Forms;

namespace MicappReceiver
{
    public partial class FormSelectOutput : Form
    {
        public MMDevice SelectedDevice { get; set; }

        public FormSelectOutput()
        {
            InitializeComponent();

            var devices = MMDeviceEnumerator.EnumerateDevices(DataFlow.Render, DeviceState.Active);

            foreach (var d in devices)
            {
                var item = new ListViewItem(d.FriendlyName);
                item.Tag = d;
                listView1.Items.Add(item);
            }
        }

        private void buttonOk_Click(object sender, System.EventArgs e)
        {
            SelectedDevice = (MMDevice)listView1.SelectedItems[0].Tag;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            SelectedDevice = (MMDevice)listView1.SelectedItems[0].Tag;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
