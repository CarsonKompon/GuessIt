using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;

namespace GuessIt;

public partial class GameMenu
{
    // Game Variables
    LOBBY_STATE LobbyState = LOBBY_STATE.WAITING_FOR_PLAYERS;
    List<Friend> StartingPlayers = new List<Friend>();
    List<Friend> FinishedPlayers = new List<Friend>();
    Friend Drawing;
    Texture Canvas;
    string Guess = "";

    // UI Variables
    GameHeader Header { get; set; }
    Panel CanvasContainer { get; set; }
    GameCanvas CanvasPanel { get; set; }
    TextEntry ChatEntry { get; set; }
    Panel ChatBox { get; set; }
    string ChatText { get; set; }
    int ChatIndex = 0;

    protected override void OnAfterTreeRender(bool firstTime)
    {
        base.OnAfterTreeRender(firstTime);

        if(firstTime)
        {
            Game.Menu.Lobby.OnChatMessage = OnChatMessage;
            Game.Menu.Lobby.OnMemberEnter = OnMemberEnter;
            Game.Menu.Lobby.OnMemberLeave = OnMemberLeave;
            Game.Menu.Lobby.ReceiveMessages(OnNetworkMessage);

            Header.SetOverride("Waiting for players...");

            if(Game.Menu.Lobby.Owner.Id == Game.SteamId)
            {
                InitLobby();
            }
        }
    }

    void InitLobby()
    {
        Game.Menu.Lobby.SetData("state", LOBBY_STATE.WAITING_FOR_PLAYERS.ToString());
        Game.Menu.Lobby.SetData("drawing", "");
        Game.Menu.Lobby.SetData("players", "");
        Game.Menu.Lobby.SetData("played", "");
        Game.Menu.Lobby.SetData("guess", "");
        Game.Menu.Lobby.SetData("correct", "");
    }

    void StartGame()
    {
        if(Game.Menu.Lobby.Owner.Id != Game.SteamId) return;
        if(LobbyState != LOBBY_STATE.WAITING_FOR_PLAYERS) return;

        StartingPlayers.Clear();
        foreach(var friend in Game.Menu.Lobby.Members)
        {
            StartingPlayers.Add(friend);
        }
        // Randomize the order of the list
        StartingPlayers.Sort((a, b) => (new Random()).Next(0, 2) == 0 ? -1 : 1);
        Game.Menu.Lobby.SetData("players", ListString(StartingPlayers));
        Game.Menu.Lobby.SetData("played", "");

        // Get the first player
        Drawing = StartingPlayers[0];
        StartingPlayers.RemoveAt(0);
        

        string playerString = "";
        for(int i=0; i<StartingPlayers.Count; i++)
        {
            playerString += StartingPlayers[i].Id;
            if(i < StartingPlayers.Count - 1)
            {
                playerString += ",";
            }
        }

        StartRound();
        NetworkStartRound();
        
    }

    void StartRound()
    {
        LobbyState = LOBBY_STATE.CHOOSING_WORD;

        Header.SetOverride(Drawing.Name + " is choosing a word...");

        if(Game.Menu.Lobby.Owner.Id == Game.SteamId)
        {
            Game.Menu.Lobby.SetData("state", LOBBY_STATE.CHOOSING_WORD.ToString());
        }

        // Reset the canvas to a plain white texture
        ResetCanvas();

        // Show word choice popup if your turn
        if(Drawing.Id == Game.SteamId)
        {
            Header.SetOverride("Choose a word!");
            CanvasContainer.AddChild<WordSelection>();
        }
    }

    public void ChooseWord(string word)
    {
        if(Drawing.Id != Game.SteamId) return;
        if(LobbyState != LOBBY_STATE.CHOOSING_WORD) return;
        
        Guess = word;
        Game.Menu.Lobby.SetData("guess", Guess);

        ResetCanvas();

        StartDrawing();
        NetworkChooseWord();
    }

    void StartDrawing()
    {
        LobbyState = LOBBY_STATE.PLAYING;

        if(Drawing.Id == Game.SteamId)
        {
            Header.SetWord(Guess, "You are drawing:", true);
        }
        else
        {
            Header.SetWord(Guess);

        }

    }

    void RequestCanvas()
    {

    }

    void NextRound()
    {
        if(Game.Menu.Lobby.Owner.Id != Game.SteamId) return;
        if(LobbyState == LOBBY_STATE.WAITING_FOR_PLAYERS || LobbyState == LOBBY_STATE.RESULTS) return;

        if(StartingPlayers.Count > 0)
        {
            Drawing = StartingPlayers[0];
            StartingPlayers.RemoveAt(0);
            LobbyState = LOBBY_STATE.CHOOSING_WORD;
            Header.SetOverride(Drawing.Name + " is choosing a word...");
            Game.Menu.Lobby.SetData("played", ListString(FinishedPlayers));
            CanvasContainer.AddChild<WordSelection>();
        }
        else
        {
            ShowResults();
        }
    }

    void ShowResults()
    {

    }

    string ListString(List<Friend> list)
    {
        string str = "";
        for(int i=0; i<list.Count; i++)
        {
            str += list[i].Id;
            if(i < list.Count - 1)
            {
                str += ",";
            }
        }
        return str;
    }

    List<Friend> ListFromString(string str)
    {
        List<Friend> list = new List<Friend>();
        string[] ids = str.Split(',');
        foreach(var id in ids)
        {
            list.Add(new Friend(long.Parse(id)));
        }
        return list;
    }

    void SendChat()
    {
        Game.Menu.Lobby.SendChat(ChatText);
        ChatEntry.Text = "";
        ChatEntry.Focus();
    }

    void OnChatMessage(Friend friend, string message)
    {
        if(Game.Menu.Lobby.Data["state"] == LOBBY_STATE.PLAYING.ToString() && message.Contains(Game.Menu.Lobby.Data["guess"]))
        {
            CreateChatEntry(friend.Name, " guessed correctly!");
        }
        else
        {
            CreateChatEntry(friend.Name + ":", message);
        }
    }

    void OnMemberEnter(Friend friend)
    {
        CreateChatEntry(friend.Name, " has joined the game.");

        if(Game.Menu.Lobby.Owner.Id != Game.SteamId)
        {
            LobbyState = (LOBBY_STATE)Enum.Parse(typeof(LOBBY_STATE), Game.Menu.Lobby.Data["state"]);
            StartingPlayers = ListFromString(Game.Menu.Lobby.Data["players"]);
            FinishedPlayers = ListFromString(Game.Menu.Lobby.Data["played"]);
            Drawing = new Friend(long.Parse(Game.Menu.Lobby.Data["drawing"]));
            Guess = Game.Menu.Lobby.Data["guess"];

            if(LobbyState == LOBBY_STATE.CHOOSING_WORD)
            {
                StartRound();
            }
            else if(LobbyState == LOBBY_STATE.PLAYING)
            {
                RequestCanvas();
            }
            else if(LobbyState == LOBBY_STATE.RESULTS)
            {
                ShowResults();
            }
        }
    }

    void OnMemberLeave(Friend friend)
    {
        CreateChatEntry(friend.Name, " has left the game.");

        if(Game.Menu.Lobby.Owner.Id == Game.SteamId)
        {
            if(friend.Id == Drawing.Id && (LobbyState == LOBBY_STATE.PLAYING || LobbyState == LOBBY_STATE.CHOOSING_WORD))
            {
                NextRound();
            }
        }
    }

    void CreateChatEntry(string name, string message)
    {
        var entry = ChatBox.AddChild<ChatEntry>();
        entry.SetMessage(name, message);

        if(ChatBox.ChildrenCount > 256)
        {
            ChatBox.GetChild(ChatBox.ChildrenCount - 1).Delete();
        }

        Audio.Play("ui.chat.message" + (ChatIndex + 1));

        ChatIndex = (ChatIndex + 1) % 2;
    }

    // DRAWING FUNCTIONS

    void ResetCanvas()
    {
        if(Canvas is not null)
        {
            Canvas.Dispose();
        }
        Texture2DBuilder canvasBuilder = Texture.Create(320, 240);
        byte[] textureData = new byte[320 * 240 * 4];
        for(int i=0; i<textureData.Length; i++)
        {
            textureData[i] = 255;
        }
        canvasBuilder.WithData(textureData);
        Canvas = canvasBuilder.Finish();
        CanvasPanel.SetTexture(Canvas);
        StateHasChanged();
    }
    
    public void Draw(Vector2 point, Color color, int size, bool send = true)
    {
        // Draw a circle of radius size at point with color color
        Canvas.Update(color.ToColor32(), new Rect(point.x - size, point.y - size, size * 2, size * 2));
        
        if(send)
        {
            CanvasPanel.SetTexture(Canvas);
            StateHasChanged();
        }
    }

    public void Draw(List<Vector2> points, Color color, int size)
    {
        // Draw lines of radius size between each point with color color
        for(int i=0; i<points.Count - 1; i++)
        {
            Vector2 point1 = points[i];
            Vector2 point2 = points[i + 1];
            Vector2 diff = point2 - point1;
            float length = diff.Length;
            for(int j=0; j<length; j++)
            {
                Vector2 point = point1 + (diff.Normal * j);
                Draw(point, color, size, false);
            }
        }

        CanvasPanel.SetTexture(Canvas);
        StateHasChanged();
    }


    // STYLE SHIT

    string LogoClass()
    {
        if(LobbyState == LOBBY_STATE.PLAYING) return "tilting";
        return "";
    }

    protected override int BuildHash()
    {
        return HashCode.Combine(LogoClass());
    }


    // NETWORKING SHIT

    void OnNetworkMessage(Sandbox.Menu.ILobby.NetworkMessage msg)
    {
        ByteStream data = msg.Data;

        ushort messageId = data.Read<ushort>();

        switch((LOBBY_MESSAGE)messageId)
        {
            case LOBBY_MESSAGE.START_ROUND:
                ushort playerCount = data.Read<ushort>();
                long drawingId = data.Read<long>();
                Drawing = new Friend(drawingId);
                StartingPlayers.Clear();
                FinishedPlayers.Clear();
                for(var i=1; i<playerCount; i++)
                {
                    long id = data.Read<long>();
                    Friend friend = new Friend(id);
                    StartingPlayers.Add(friend);
                }
                StartRound();
                break;
            
            case LOBBY_MESSAGE.CHOOSE_WORD:
                int byteLength = data.Read<int>();
                byte[] guessBytes = new byte[byteLength];
                for(int i=0; i<byteLength; i++)
                {
                    guessBytes[i] = data.Read<byte>();
                }
                Guess = System.Text.Encoding.Unicode.GetString(guessBytes);
                StartDrawing();
                break;
        }
    }

    void NetworkStartRound()
    {
        ushort playerCount = (ushort)(StartingPlayers.Count + 1);
        ByteStream data = ByteStream.Create(4 + (8 * playerCount));
        data.Write((ushort)LOBBY_MESSAGE.START_ROUND);
        data.Write(playerCount);
        data.Write(Drawing.Id);
        for(int i=0; i<StartingPlayers.Count; i++)
        {
            data.Write(StartingPlayers[i].Id);
        }

        Game.Menu.Lobby.BroadcastMessage(data);
    }

    void NetworkChooseWord()
    {
        byte[] guessBytes = System.Text.Encoding.Unicode.GetBytes(Guess);

        ByteStream data = ByteStream.Create(6 + guessBytes.Length);
        data.Write((ushort)LOBBY_MESSAGE.CHOOSE_WORD);
        data.Write((int)guessBytes.Length);
        for(int i=0; i<guessBytes.Length; i++)
        {
            data.Write(guessBytes[i]);
        }

        Game.Menu.Lobby.BroadcastMessage(data);
    }

    public void NetworkDraw(List<Vector2> points, Color color, int size)
    {

    }
}