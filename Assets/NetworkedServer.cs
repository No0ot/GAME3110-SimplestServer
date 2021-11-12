using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;
    LinkedList<PlayerAccount> playerAccounts;

    const int PlayerAccountRecord = 1;

    string playerAccountDataPath;

    int playerWaitinginQueueID = -1;

    LinkedList<GameRoom> gameRooms;

    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

        playerAccounts = new LinkedList<PlayerAccount>();

        playerAccountDataPath = Application.dataPath + Path.DirectorySeparatorChar + "PlayerAccounts.txt";

        LoadPlayerAccount();

        gameRooms = new LinkedList<GameRoom>();
    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                break;
        }

    }
  
    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }
    
    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');

        int signifier = int.Parse(csv[0]);

        bool errorFound = false;
        GameRoom gr;
        switch (signifier)
        {
            case ClientToServerSignifiers.CreateAccount:

                foreach (PlayerAccount account in playerAccounts)
                {
                    if (csv[1] == account.name)
                    {
                        SendMessageToClient(ServertoClientSignifiers.AccountCreationFailed + "", id);
                        errorFound = true;
                    }
                }
                if (!errorFound)
                {
                    PlayerAccount pa = new PlayerAccount(csv[1], csv[2]);
                    playerAccounts.AddLast(pa);
                    SendMessageToClient(ServertoClientSignifiers.AccountCreationComplete + "", id);

                    SavePlayerAccount();
                }
                break;
            case ClientToServerSignifiers.LoginAccount:

                foreach (PlayerAccount account in playerAccounts)
                {
                    if (csv[1] == account.name)
                    {
                        if (csv[2] == account.password)
                        {
                            SendMessageToClient(ServertoClientSignifiers.LoginComplete + "", id);
                            errorFound = true;
                        }
                    }
                }
                if (!errorFound)
                {
                    SendMessageToClient(ServertoClientSignifiers.LoginFailed + "", id);
                }
                break;
            case ClientToServerSignifiers.JoinQueue:

                if(playerWaitinginQueueID == -1)
                    playerWaitinginQueueID = id;
                else
                {
                    gr = new GameRoom(playerWaitinginQueueID, id);
                    gameRooms.AddLast(gr);
                    SendMessageToClient(ServertoClientSignifiers.GameStart + "", gr.player1ID);
                    SendMessageToClient(ServertoClientSignifiers.GameStart + "", gr.player2ID);
                    playerWaitinginQueueID = -1;
                }
                break;
            case ClientToServerSignifiers.GameButtonPressed:
                gr = GetGameRoomWithClientID(id);

                if(gr != null)
                {
                    if (gr.player1ID == id)
                        SendMessageToClient(ServertoClientSignifiers.OpponenetPlay + "", gr.player2ID);
                    else
                        SendMessageToClient(ServertoClientSignifiers.OpponenetPlay + "", gr.player1ID);
                }
                break;
        }

    }
    private void SavePlayerAccount()
    {
        StreamWriter sw = new StreamWriter(playerAccountDataPath);

        foreach(PlayerAccount pa in playerAccounts)
        {
            sw.WriteLine(PlayerAccountRecord + "," + pa.name + "," + pa.password);
        }

        sw.Close();
    }

    private void LoadPlayerAccount()
    {

        if (File.Exists(playerAccountDataPath))
        {

            StreamReader sr = new StreamReader(playerAccountDataPath);

            string line;

            while ((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');

                int signifier = int.Parse(csv[0]);

                if (signifier == PlayerAccountRecord)
                {
                    PlayerAccount pa = new PlayerAccount(csv[1], csv[2]);
                    playerAccounts.AddLast(pa);
                }
            }

            sr.Close();
        }
    }

    private GameRoom GetGameRoomWithClientID(int id)
    {
        foreach(GameRoom gr in gameRooms)
        {
            if (gr.player1ID == id || gr.player2ID == id)
                return gr;
        }

        return null;
    }

}




public class PlayerAccount
{
    public string name, password;
    public PlayerAccount(string Name, string Password)
    {
        name = Name;
        password = Password;
    }
}

public class GameRoom
{
    public int player1ID, player2ID;

    public GameRoom(int Player1ID, int Player2ID)
    {
        player1ID = Player1ID;
        player2ID = Player2ID;
    }

}

public static class ClientToServerSignifiers
{
    public const int CreateAccount = 1;

    public const int LoginAccount = 2;

    public const int JoinQueue = 3;

    public const int GameButtonPressed = 4;
}

public static class ServertoClientSignifiers
{
    public const int LoginComplete = 1;

    public const int LoginFailed = 2;

    public const int AccountCreationComplete = 3;

    public const int AccountCreationFailed = 4;

    public const int OpponenetPlay = 5;

    public const int GameStart = 6;
}
