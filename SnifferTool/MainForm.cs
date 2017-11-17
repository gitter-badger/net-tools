﻿using HexToBinLib;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace SnifferTool
{
    public partial class MainForm : Form
    {
        private const int MTU = 65535;
        private Socket socket;
        private object [] protocolTypes = new []
        {
            new { id = ProtocolType.Icmp, text = "ICMP" },
            new { id = ProtocolType.IP, text = "IP" },
            new { id = ProtocolType.Udp, text = "UDP" }
        };

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            protocolType.DataSource = protocolTypes;
            protocolType.ValueMember = "id";
            protocolType.DisplayMember = "text";
            protocolType.SelectedValue = ProtocolType.IP;
            interfaceSelector.InterfaceDeleted += InterfaceSelector_InterfaceDeleted;
        }

        private void InterfaceSelector_InterfaceDeleted(string address)
        {
            interfaceSelector.SelectedIndex =
                interfaceSelector.SelectedIndex > 0 ? interfaceSelector.SelectedIndex - 1 : -1;
            if (socket != null)
            {
                CloseRawSocket();
                MessageBox.Show(this, "Socket closed.", this.Text);
            }
        }

        private void bind_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(interfaceSelector.Text))
            {
                MessageBox.Show("Please select an interface.", this.Text);
                interfaceSelector.Focus();
                return;
            }

            try
            {
                CreateRawSocket(new IPEndPoint(IPAddress.Parse(interfaceSelector.Text), 0));
                close.Enabled = true;
                bind.Enabled = false;
                interfaceSelector.Enabled = false;
                protocolType.Enabled = false;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, this.Text);
            }
        }

        private void CreateRawSocket(IPEndPoint endPoint)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, 
                (ProtocolType)protocolType.SelectedValue);
            socket.Bind(endPoint);
            PlatformID p = Environment.OSVersion.Platform;
            if (p == PlatformID.Win32NT && !endPoint.Address.Equals(IPAddress.Any))
            {
                socket.SetSocketOption(SocketOptionLevel.IP, 
                    SocketOptionName.HeaderIncluded, true);

                socket.IOControl(IOControlCode.ReceiveAll, 
                    new byte[] { 1, 0, 0, 0 }, 
                    new byte[] { 0, 0, 0, 0 });
            }
            BeginReceiveFrom();
        }

        private void BeginReceiveFrom()
        {
            byte[] data = new byte[MTU];
            EndPoint remoteEndPoint = new IPEndPoint(0, 0);
            try
            {
                socket.BeginReceiveFrom(data, 0, data.Length, SocketFlags.None,
                    ref remoteEndPoint,
                    delegate (IAsyncResult ar)
                    {
                        ReceiveCallback(ar, remoteEndPoint, data);
                    }, null);
            }
            catch
            {

            }
        }

        private void ReceiveCallback(IAsyncResult ar, EndPoint remoteEndPoint, byte[] data)
        {
            int length;
            try
            {
                length = socket.EndReceiveFrom(ar, ref remoteEndPoint);
            }
            catch
            {
                return;
            }
            ShowMessage(data, length, remoteEndPoint);
            BeginReceiveFrom();
        }

        private void ShowMessage(byte[] data, int length, EndPoint remoteEndPoint)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate ()
                {
                    ShowMessage(data, length, remoteEndPoint);
                });
                return;
            }
            string dateTime = string.Format("{0:yyyyMMddTHH:mm:ssZ}", DateTime.UtcNow);
            output.AppendText(string.Format("Received {0} bytes from {1} on {2}:{3}",
                length, remoteEndPoint.ToString(), dateTime, 
                Environment.NewLine));
            output.Append(data, length);
            output.AppendText(Environment.NewLine);
            output.AppendText(Environment.NewLine);
        }

        private void close_Click(object sender, EventArgs e)
        {
            CloseRawSocket();
        }

        private void CloseRawSocket()
        {
            if (socket == null)
                return;

            if (InvokeRequired)
            {
                Invoke((Action)delegate
                {
                    CloseRawSocket();
                });
                return;
            }
            socket.Close();
            socket = null;
            bind.Enabled = true;
            close.Enabled = false;
            interfaceSelector.Enabled = true;
            protocolType.Enabled = true;
        }

        // Experimental support to send IP header, protocol header and payload
        private void Send(string hexStream, IPEndPoint toEndPoint)
        {
            MemoryStream o = new MemoryStream();
            HexToBin.Convert(new StringReader(hexStream), o);
            Send(o.GetBuffer(), (int)o.Length, toEndPoint);
        }

        // Experimental support to send IP header, protocol header and payload
        private void Send(byte [] data, int length, IPEndPoint toEndPoint)
        {
            socket.SendTo(data, length, 0, toEndPoint);
        }
    }
}
