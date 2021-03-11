using Micapp.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace MicappReceiver
{
    public partial class FormMain : Form
    {
        Socket controlSocket = new Socket(SocketType.Stream, ProtocolType.IP);
        IPEndPoint localControlEp = new IPEndPoint(IPAddress.Any, AppConfig.ControlPort);

        Socket dataSocket = new Socket(SocketType.Dgram, ProtocolType.IP);
        IPEndPoint localDataEp = new IPEndPoint(IPAddress.Any, AppConfig.DataPort);

        List<Client> clients = new List<Client>();

        public FormMain()
        {
            InitializeComponent();

            controlSocket.Bind(localControlEp);
            controlSocket.Listen(100);
            controlSocket.BeginAccept(OnAccept, null);

            dataSocket.Bind(localDataEp);

            Application.ApplicationExit += Application_ApplicationExit;
        }

        private void OnAccept(IAsyncResult ar)
        {
            Socket clientSocket;

            try
            {
                clientSocket = controlSocket.EndAccept(ar);
            }
            catch
            {
                return;
            }

            Debug.WriteLine($"Client {clientSocket.RemoteEndPoint} connected.");

            var client = new Client(clientSocket, dataSocket);
            client.Disconnected += Client_Disconnected;
            client.NameRegistered += Client_NameRegistered;
            clients.Add(client);

            UpdateListView();


            controlSocket.BeginAccept(OnAccept, null);
        }

        private void Client_NameRegistered(object sender, EventArgs e)
        {
            UpdateListView();
        }

        private void Client_Disconnected(object sender, EventArgs e)
        {
            var client = (Client)sender;

            try
            {
                Debug.WriteLine($"Client {client.ControlSocket.RemoteEndPoint} disconnected.");
            } catch (ObjectDisposedException)
            {

            }

            if (clients.Remove(client))
            {
                Debug.WriteLine("Client removed from list.");

                UpdateListView();
            }

            client.Dispose();
        }

        private void UpdateListView()
        {
            Invoke(new Action(() =>
            {
                listView1.Items.Clear();

                foreach (var c in clients)
                {
                    var item = new ListViewItem(new string[]
                    {
                    c.Name,
                    (c.ControlSocket != null ? ((IPEndPoint)c.ControlSocket.RemoteEndPoint).Address.ToString() : "-"),
                        (c.wasapiOut.Device != null ? c.wasapiOut.Device.FriendlyName : "-")
                    });
                    item.Tag = c;
                    listView1.Items.Add(item);
                }
            }));
        }

        private void Application_ApplicationExit(object sender, EventArgs e)
        {
            controlSocket.Close();
            dataSocket.Close();

            foreach(var c in clients)
            {
                c.Dispose();
            }
        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var clientInfo = (Client)listView1.SelectedItems[0].Tag;

            var formSelectOutput = new FormSelectOutput();
            var result = formSelectOutput.ShowDialog();

            if (result == DialogResult.OK)
            {
                clientInfo.SwitchDevice(formSelectOutput.SelectedDevice);
                UpdateListView();
            }
        }
    }
}
