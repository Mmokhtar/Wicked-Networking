using System;
using System.Xml;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class WickedServer
{
    public delegate void DelStrSoc(string str, Socket soc);
    public delegate void DelSoc(Socket soc);
    public DelStrSoc _recvPacketUDP;
    public DelStrSoc _recvPacketTCP;
    public DelSoc _clientConnect;
    public DelSoc _clientUnidentified;
    public DelSoc _clientForceDisconnect;
    private Socket _SocketTCP;
    private Socket _SocketUDP;
    private ClientList Clients = new ClientList();
    private int BUFFER_SIZE;
    private byte[] _buffer;

	public enum ServerProtocol {TCP, UDP}
	public enum ServerType {TCP, TCP_AND_UDP}

	public List<Socket> getClients()
	{
		List <Socket> sockets = new List<Socket> ();
		foreach (ClientData c in Clients.getClients()) {sockets.Add (c.getSocket ());}
		return sockets;
	}

	public WickedServer (ServerType type ,int TCPBufferSize, int port, int TCPBackLog)
    {
        BUFFER_SIZE = TCPBufferSize + 73;
        _buffer = new byte[BUFFER_SIZE];

        if (type == ServerType.TCP)
		{
			Thread setupTCPServer = new Thread(() => SetupTCPServer (port, TCPBackLog));
			setupTCPServer.Start ();
		}
		else if (type == ServerType.TCP_AND_UDP)
		{
			Thread setupTCPServer = new Thread(() => SetupTCPServer (port, TCPBackLog));
            setupTCPServer.IsBackground = true;
			setupTCPServer.Start ();

            Thread setupUDPServer = new Thread(() => SetupUDPServer(port));
            setupUDPServer.IsBackground = true;
			setupUDPServer.Start ();
        }
    }

	public void SendString (ServerProtocol protocol, string text, Socket _clientSocket)
	{
		byte[] buffer = Encoding.ASCII.GetBytes(text);
		
		if (protocol == ServerProtocol.TCP)
		{
			_clientSocket.Send(buffer, 0, buffer.Length, SocketFlags.None);
		}
		else if (protocol == ServerProtocol.UDP)
		{
            EndPoint ep = null;

            foreach (ClientData c in Clients.getClients())
            {
                if (c.getSocket() == _clientSocket)
                {
                    ep = c.getEP();
                }
            }

            _SocketUDP.SendTo(buffer, ep);
		}
	}

	public void closeClientSocket(Socket socket)
	{
		socket.Shutdown(SocketShutdown.Both);
		socket.Close();

		for (int i = Clients.getClients().Count - 1; i >= 0; i--)
		{
			if (Clients.getClients()[i].getSocket () == socket)
			{
				Clients.removeClient(Clients.getClients()[i]);
			}
		}
	}
	
	public void KillServer()
	{
        for (int i = Clients.getClients().Count; i <= 0; i--)
		{
            Clients.getClients()[i].getSocket ().Shutdown(SocketShutdown.Both);
            Clients.getClients()[i].getSocket ().Close();
            Clients.removeClient(Clients.getClients()[i]);
		}
		
		_SocketTCP.Close();
        _SocketUDP.Close();
	}

    private void SetupTCPServer(int port, int backLog)
    {
		_SocketTCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try { _SocketTCP.Bind(new IPEndPoint(IPAddress.Any, port)); } catch (SocketException e) { _SocketTCP.Close(); throw e; }
        _SocketTCP.Listen(backLog);
        _SocketTCP.BeginAccept(AcceptCallback, null);
    }

	private void SetupUDPServer (int port)
	{
        _SocketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _SocketUDP.Bind(new IPEndPoint(IPAddress.Any, port));

        EndPoint recvEP = new IPEndPoint(IPAddress.Any, port) as EndPoint;

        while (true)
		{
            bool hacker = false;
            
            string text;
            try { text = ReceiveResponseUDP(ref recvEP); } catch { continue; }

            XmlDocument xDoc = new XmlDocument ();
            try { xDoc.LoadXml(text); } catch { hacker = true; }
            
            if (hacker == false)
            {
                if (xDoc.DocumentElement.SelectSingleNode("Purpose").InnerXml == "MES")
                {
                    foreach (ClientData c in Clients.getClients())
                    {
                        try
                        {
                            if (xDoc.DocumentElement.SelectSingleNode("UID").InnerXml == c.getID())
                            {
                                try { _recvPacketUDP(System.Web.HttpUtility.HtmlDecode(xDoc.SelectSingleNode("Data/MES").InnerXml), c.getSocket()); } catch (NullReferenceException) { }
                                break;
                            }
                        }
                        catch (NullReferenceException) { hacker = true; break; }
                    }
                }
                else if (xDoc.DocumentElement.SelectSingleNode("Purpose").InnerXml == "UID")
                {
                    foreach (ClientData c in Clients.getClients())
                    {
                        try
                        {
                            if (xDoc.DocumentElement.SelectSingleNode("UID").InnerXml == c.getID())
                            {
                                byte[] buffer = new byte[1];
                                c.getSocket().Send(buffer, 0, buffer.Length, SocketFlags.None);

                                if (c.VerifiedUDP() == false)
                                {
                                    c.verifyUDP();
                                    c.setEP(recvEP);
                                    try { _clientConnect(c.getSocket()); } catch (NullReferenceException) { }
                                }

                                break;
                            }
                        }
                        catch (NullReferenceException) { hacker = true; break; }
                    }
                }
            }
            if (hacker == true)
            {
                bool uni = true;

                foreach (ClientData c in Clients.getClients())
                {
                    try
                    {
                        if (xDoc.SelectSingleNode("Data/UID").InnerText == c.getID())
                        {
                            try { _clientUnidentified(c.getSocket()); } catch (NullReferenceException) { uni = false; }
                        }
                    }
                    catch (NullReferenceException) {}
                }
                if (uni == false)
                {
                    foreach (ClientData c in Clients.getClients())
                    {
                        if (c.getEP() == recvEP)
                        {
                            try { _clientUnidentified(c.getSocket()); } catch (NullReferenceException) { break; }
                        }
                    }
                }
            }
        }
	}
	
    private void AcceptCallback(IAsyncResult AR)
    {
        Socket socket;

        try
        {
            socket = _SocketTCP.EndAccept(AR);
        }
        catch (ObjectDisposedException) // I cannot seem to avoid this (on exit when properly closing sockets)
        {
            return;
        }

		ClientData client = new ClientData (socket);

        Clients.addClient(client);

        socket.BeginReceive(_buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, socket);
        _SocketTCP.BeginAccept(AcceptCallback, null);
    }

    private void ReceiveCallback(IAsyncResult AR)
    {
        Socket current = (Socket)AR.AsyncState;
        int received = 0;

        try
        {
            received = current.EndReceive(AR);
        }
        catch (SocketException)
        {
            foreach (ClientData client in Clients.getClients())
            {
                if (client.getSocket() == current)
                {
                    try { _clientForceDisconnect(current); } catch (NullReferenceException) { }
                    current.Close(); // Dont shutdown because the socket may be disposed and its disconnected anyway
                    try { current.Shutdown(SocketShutdown.Both); } catch { }

                    ClientData clientD = null;

                    foreach (ClientData c in Clients.getClients())
                    {
                        if (c.getSocket() == current)
                        {
                            clientD = c;
                        }
                    }
                    Clients.removeClient(clientD);

                    return;
                }
            }
        }
        catch (ObjectDisposedException)
        { return; }

        byte[] recBuf = new byte[received];
        Array.Copy(_buffer, recBuf, received);
        string text = Encoding.ASCII.GetString(recBuf);

        bool xmlLoaded = true;
		XmlDocument xDoc = new XmlDocument ();
        try {xDoc.LoadXml(text);}catch {xmlLoaded = false;}

        ClientData currentCD = null;
        bool clientIdentified = true;

        foreach (ClientData c in Clients.getClients())
        {
            if (c.getSocket() == current)
            {
                currentCD = c;
            }
        }

        if (xmlLoaded == true)
        {
            if (xDoc.DocumentElement.SelectSingleNode("Purpose").InnerText == "UID")
            {
                _buffer = new byte[BUFFER_SIZE];

                foreach (ClientData c in Clients.getClients())
                {
                    if (c.getSocket() == current)
                    {
                        c.setID(xDoc.SelectSingleNode("Data/UID").InnerText);
                        byte[] b = Encoding.ASCII.GetBytes((current.RemoteEndPoint as IPEndPoint).Port.ToString());
                        current.Send(b);
                    }
                }
            }
            else if (xDoc.SelectSingleNode("Data/Purpose").InnerText == "MES" && xDoc.SelectSingleNode("Data/UID").InnerText == currentCD.getID())
            {
                try { _recvPacketTCP(System.Web.HttpUtility.HtmlDecode(xDoc.SelectSingleNode("Data/MES").InnerXml), current); } catch (NullReferenceException) { }
            }
            else
            {
                clientIdentified = false;
                try { _clientUnidentified(current); } catch (NullReferenceException) { }
                closeClientSocket(current);
            }

            if (clientIdentified == true)
            {
                foreach (ClientData c in Clients.getClients())
                {
                    if (c.getSocket() == current)
                    {
                        try
                        {
                            current.BeginReceive(_buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
                        }
                        catch (SocketException)
                        {
                            foreach (ClientData client in Clients.getClients())
                            {
                                if (client.getSocket() == current)
                                {
                                    try { _clientForceDisconnect(current); } catch (NullReferenceException) { }

                                    current.Close(); // Dont shutdown because the socket may be disposed and its disconnected anyway
                                    try { current.Shutdown(SocketShutdown.Both); } catch { }

                                    ClientData clientD = null;

                                    foreach (ClientData cd in Clients.getClients())
                                    {
                                        if (c.getSocket() == current)
                                        {
                                            clientD = cd;
                                        }
                                    }
                                    Clients.removeClient(clientD);

                                    return;
                                }
                            }
                        }
                        catch (ObjectDisposedException)
                        { return; }
                    }
                }
            }
        }
        else
        {
            try { _clientUnidentified(current); } catch (NullReferenceException) { }
            closeClientSocket(current);
        }
	}

    private string ReceiveResponseUDP(ref EndPoint ep)
    {
        byte[] buffer = new byte[BUFFER_SIZE]; 

        int received = _SocketUDP.ReceiveFrom(buffer, ref ep);
        if (received == 0) return null;
        byte[] recvBuf = new byte[received];
        Array.Copy(buffer, recvBuf, received);
        string text = Encoding.ASCII.GetString(recvBuf);
        return text;
    }
}