using System;


[Serializable]
public  class Transmit
{
    public char t = 't';
    public string ip; //IP of Client
    public long time;
}

[Serializable]
public class Ping : Transmit
{
    public new char t = 'P';
    public int port; //Listening Peer port
    public string nm;
}


[Serializable]
public class Request : Transmit
{
    public new char t = 'R';
    public int port; //Listening Peer port
    public string nm;
    public Command c = Command.ping;
    public long length = 0;

    public enum Command : byte
    {
        ping, details, fileSendReq, fileAccept
    }
}

[Serializable]
public class Response : Transmit
{
    public new char t = 'r';
    public int port; //Listening Peer port
    public string nm;

    public Status stat = Status.none;
    public enum Status : byte
    {
        none, ok, retry, pingReq, failed, reconnect
    }
}