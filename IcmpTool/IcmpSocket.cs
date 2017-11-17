﻿using System;
using System.Net;
using System.Net.Sockets;

namespace IcmpTool
{
    class IcmpSocket
    {
        private Socket icmpSocket;
        private byte[] receiveBuffer = new byte[256];
        private EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        public delegate void MessageReceivedHandler(IPEndPoint from, byte[] message, int length);
        public event MessageReceivedHandler MessageReceived;
        public IPEndPoint LocalEndPoint { get; private set; }

        /// <summary>
        /// Creates a raw ICMP socket.
        /// </summary>
        public IcmpSocket()
        {
            LocalEndPoint = new IPEndPoint(IPAddress.Any, 0);
            CreateIcmpSocket();
        }

        /// <summary>
        /// Creates a raw ICMP socket and binds it to the specified end point.
        /// </summary>
        /// <param name="endPoint"></param>
        public IcmpSocket(IPEndPoint endPoint)
        {
            LocalEndPoint = endPoint;
            CreateIcmpSocket();
        }

        private void CreateIcmpSocket()
        {
            icmpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
            icmpSocket.Bind(LocalEndPoint);
            PlatformID p = Environment.OSVersion.Platform;
            if (p == PlatformID.Win32NT && !LocalEndPoint.Address.Equals(IPAddress.Any))
            {
                icmpSocket.IOControl(IOControlCode.ReceiveAll, new byte[] { 1, 0, 0, 0 }, new byte[] { 1, 0, 0, 0 });
            }
            BeginReceiveFrom();
        }

        private void BeginReceiveFrom()
        {
            icmpSocket.BeginReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None,
                ref remoteEndPoint, ReceiveCallback, null);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            int len;
            try
            {
                len = icmpSocket.EndReceiveFrom(ar, ref remoteEndPoint);
            }
            catch
            {
                return;
            }
            IPAddress remoteIPAddress = ((IPEndPoint)remoteEndPoint).Address;
            if (!LocalEndPoint.Address.Equals(remoteIPAddress))
            {
                // Handle messages not sent by us
                MessageReceived.Invoke((IPEndPoint)remoteEndPoint, receiveBuffer, len);
            }
            BeginReceiveFrom();
        }

        internal void Close()
        {
            icmpSocket.Close();
        }

        internal void Send(byte[] data, int length, IPEndPoint remoteEndPoint)
        {
            icmpSocket.SendTo(data, length, SocketFlags.None, remoteEndPoint);
        }
    }
}
