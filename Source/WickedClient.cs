using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

public class WickedClient
{
    private IPEndPoint EP;
    private bool stopListenBool = false;
    private string P_MAC_UID;
	private dynamic ClientEventClass;
	private byte[] _bufferTCP;
    private byte[] _bufferUDP;
    Thread listenTCP;
    Thread listenUDP;

    private readonly Socket _clientSocketUDP = new Socket
        (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    private readonly Socket _clientSocketTCP = new Socket
		(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

	public enum ClientProtocol {TCP, UDP}
	public enum ClientType {TCP, TCP_AND_UDP}

    public Socket getSocket () { return _clientSocketTCP; }

    public void KillClient()
    {
        stopListenBool = true;
        _clientSocketTCP.Shutdown(SocketShutdown.Both);
        _clientSocketTCP.Close();
        _clientSocketUDP.Shutdown(SocketShutdown.Both);
        _clientSocketUDP.Close();
    }

    public WickedClient (dynamic Events, ClientType protocol, string IP, int BufferSize, int port)
	{
		_bufferTCP = new byte[BufferSize];
        _bufferUDP = new byte[BufferSize];
        ClientEventClass = Events;
        IPAddress IPA;

        if (IP == "localhost") { IPA = IPAddress.Loopback; } else { IPA = IPAddress.Parse(IP); }
        EP = new IPEndPoint (IPA, port);

        if (protocol == ClientType.TCP)
		{
			ConnectToServerTCP (IPA, port);
			listenTCP = new Thread (BeginListenTCP);
			listenTCP.Start ();
		}
		else if (protocol == ClientType.TCP_AND_UDP)
		{
			ConnectToServerTCP (IPA, port);

			ConnectToServerUDP (IPA, port);

			string PID = Process.GetCurrentProcess().Id.ToString ();
			string MAC_ID = GetMacAddress ();

			P_MAC_UID = PID + "-" + MAC_ID;

			string xml =  ("<Data>" +
                           "<Purpose>UID</Purpose>" +
                           "<UID>" + P_MAC_UID + "</UID>" +
                           "</Data>");

            byte[] buffer = Encoding.ASCII.GetBytes(xml);
            _clientSocketTCP.Send(buffer, 0, buffer.Length, SocketFlags.None);

            _clientSocketTCP.Receive(_bufferTCP);

            SendUDP:
            {
                byte[] _buffer = Encoding.ASCII.GetBytes("<Data><Purpose>UID<Purpose><UID>" + P_MAC_UID + "</UID></Data>");
                _clientSocketUDP.Send(buffer, 0, buffer.Length, SocketFlags.None);

                var tokenSource = new CancellationTokenSource();
                CancellationToken token = tokenSource.Token;
                int timeOut = 5000; // 1000 ms

                string answer;
                var task = Task.Factory.StartNew(() => answer = listenIteration(), token);

                if (!task.Wait(timeOut, token))
                {
                    goto SendUDP;
                }
            }
            listenTCP = new Thread (BeginListenTCP);
            listenTCP.IsBackground = true;
            listenTCP.Start ();
            
			listenUDP = new Thread (BeginListenUDP);
            listenUDP.IsBackground = true;
            listenUDP.Start ();
		}
	}

	public void SendString(ClientProtocol protocol ,string text)
	{
		XmlDocument xDoc = new XmlDocument ();
		xDoc.LoadXml ("<Data>" +
					  "<Purpose>MES</Purpose>" +
		              "</Data>");
		XmlNode mes = xDoc.CreateElement ("MES");
		mes.InnerText = text;
		xDoc.DocumentElement.AppendChild (mes);

		XmlNode UID = xDoc.CreateElement ("UID");
		UID.InnerXml = P_MAC_UID;
		xDoc.DocumentElement.AppendChild (UID);
        
		byte[] buffer = Encoding.ASCII.GetBytes(xDoc.DocumentElement.OuterXml);

		if (protocol == ClientProtocol.TCP)
		{
			_clientSocketTCP.Send(buffer, 0, buffer.Length, SocketFlags.None);
		}
		else if (protocol == ClientProtocol.UDP) 
		{
            _clientSocketUDP.Send(buffer, 0, buffer.Length, SocketFlags.None);
        }
	}

	private void BeginListenTCP ()
	{
		while (stopListenBool == false)
		{
			string text = ReceiveResponseTCP ();

			if (text != null && stopListenBool == false)
			{
				try{ClientEventClass.recvPacketTCP (text);}catch{}
			}
			else if (stopListenBool == false)
			{
				try{ClientEventClass.clientForceDisconnect ();}catch{}
                KillClient();
            }
		}
	}

	private void BeginListenUDP ()
	{
        while (stopListenBool == false)
        {
            string text = listenIteration();
            if (text != null){ ClientEventClass.recvPacketUDP(text); }
        }
    }

    private string listenIteration()
    {
        EndPoint ep = EP as EndPoint;
        int received = 0;
        try { received = _clientSocketUDP.ReceiveFrom(_bufferUDP, ref ep); } catch (SocketException) { return null; }
        if (received == 0) return null;
        byte[] recvBuff = new byte[received];
        Array.Copy(_bufferUDP, recvBuff, received);
        _bufferUDP = recvBuff;
        string text = Encoding.ASCII.GetString(recvBuff);
        return text;
    }

	private void ConnectToServerTCP (IPAddress IP, int port)
	{
		while (!_clientSocketTCP.Connected)
		{
			try{_clientSocketTCP.Connect(IP, port);}catch(SocketException){}
		}
	}

	private void ConnectToServerUDP (IPAddress IP, int port)
	{
        while (!_clientSocketUDP.Connected)
        {
            try
            {
                _clientSocketUDP.Connect(IP, port);
            }
            catch (SocketException) {}
        }
    }

	private string ReceiveResponseTCP()
	{
		try
		{
			int received = _clientSocketTCP.Receive(_bufferTCP);

			var data = new byte[received];
			Array.Copy(_bufferTCP, data, received);
			string text = Encoding.ASCII.GetString(data);

            if (text == "")
            {
                return null;
            }
            else
            {
                return text;
            }
		}
		catch
		{
			return null;
		}
	}

	private string GetMacAddress()
	{
		const int MIN_MAC_ADDR_LENGTH = 12;
		string macAddress = string.Empty;
		long maxSpeed = -1;
		
		foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
		{
			string tempMac = nic.GetPhysicalAddress().ToString();
			if (nic.Speed > maxSpeed &&
			    !string.IsNullOrEmpty(tempMac) &&
			    tempMac.Length >= MIN_MAC_ADDR_LENGTH)
			{
				maxSpeed = nic.Speed;
				macAddress = tempMac;
			}
		}
		
		return macAddress;
	}
}