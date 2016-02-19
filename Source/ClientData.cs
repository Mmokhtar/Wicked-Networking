using System.Net;
using System.Net.Sockets;

public class ClientData
{
	private Socket Socket;
    	private EndPoint ep;
    	private string ID;

	public Socket getSocket () { return Socket; }

	public void setID (string id) {ID = id;}
	public string getID () {return ID;}

    	public void setEP(EndPoint EP) { ep = EP; }
    	public EndPoint getEP() { return ep; }

	public ClientData (Socket s) {Socket = s;}
}


