using System;
using System.Collections.Generic;

class ClientList
{
    private List<ClientData> Clients = new List<ClientData>();

    public List<ClientData> getClients() {return Clients;}
    public void addClient(ClientData c) {Clients.Add(c);}
    public void removeClient(ClientData c) {Clients.Remove(c);}
}