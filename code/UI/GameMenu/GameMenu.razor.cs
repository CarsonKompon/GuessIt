using Sandbox;
using Sandbox.Menu;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;

namespace GuessIt;

enum LOBBY_STATE
{
    WAITING_FOR_PLAYERS,
    CHOOSING_WORD,
    PLAYING,
    RESULTS
}

enum LOBBY_MESSAGE
{
    NONE,
    START_ROUND,
    CHOOSE_WORD,
    DRAW,
    REQUEST_CANVAS,
    SEND_CANVAS,
    CORRECT_GUESS,
    REVEAL_WORD
}

public partial class GameMenu
{
    // Game Variables
    LOBBY_STATE LobbyState = LOBBY_STATE.WAITING_FOR_PLAYERS;
    List<Friend> StartingPlayers = new List<Friend>();
    List<Friend> FinishedPlayers = new List<Friend>();
    List<Friend> CorrectPlayers = new List<Friend>();
    Dictionary<long, long> PlayerScores = new Dictionary<long, long>();
    public Friend Drawing;
    Texture Canvas;
    string Guess = "";
    float GameTimer = 120f;

    // UI Variables
    GameHeader Header { get; set; }
    Panel PlayerList { get; set; }
    Panel CanvasContainer { get; set; }
    GameCanvas CanvasPanel { get; set; }
    TextEntry ChatEntry { get; set; }
    Panel ChatBox { get; set; }
    string ChatText { get; set; }
    int ChatIndex = 0;
    RealTimeSince LastUpdate = 0f;

    // Lobby Variables
    public ILobby Lobby { get; set; } = Game.Menu.Lobby;

    protected override void OnAfterTreeRender(bool firstTime)
    {
        base.OnAfterTreeRender(firstTime);

        if(firstTime)
        {
            Lobby.OnChatMessage = OnChatMessage;
            Lobby.OnMemberEnter = OnMemberEnter;
            Lobby.OnMemberLeave = OnMemberLeave;

            Header.SetOverride("Waiting for players...");

            if(Lobby.Owner.Id == Game.SteamId)
            {
                InitLobby();
            }
            else
            {
                JoinedLobby();
            }

            foreach(var friend in Lobby.Members)
            {
                PlayerListEntry entry = PlayerList.AddChild<PlayerListEntry>();
                entry.Player = friend;
                entry.Lobby = Lobby;
                entry.SetScore(GetPlayerScore(friend.Id));
            }
        }
    }

    void InitLobby()
    {
        Lobby.SetData("state", LOBBY_STATE.WAITING_FOR_PLAYERS.ToString());
        Lobby.SetData("drawing", "");
        Lobby.SetData("players", "");
        Lobby.SetData("played", "");
        Lobby.SetData("guess", "");
        Lobby.SetData("correct", "");
        Lobby.SetData("timer", "120");
    }

    void JoinedLobby()
    {
        LobbyState = (LOBBY_STATE)Enum.Parse(typeof(LOBBY_STATE), Lobby.Data["state"]);
        if(Lobby.Data.ContainsKey("players")) StartingPlayers = ListFromString(Lobby.Data["players"]);
        if(Lobby.Data.ContainsKey("played")) FinishedPlayers = ListFromString(Lobby.Data["played"]);
        if(Lobby.Data.ContainsKey("drawing")) Drawing = new Friend(long.Parse(Lobby.Data["drawing"]));
        if(Lobby.Data.ContainsKey("guess")) Guess = Lobby.Data["guess"];
        if(Lobby.Data.ContainsKey("timer")) GameTimer = float.Parse(Lobby.Data["timer"]);

        if(LobbyState == LOBBY_STATE.CHOOSING_WORD)
        {
            StartRound();
        }
        else if(LobbyState == LOBBY_STATE.PLAYING)
        {
            NetworkRequestCanvas();
        }
        else if(LobbyState == LOBBY_STATE.RESULTS)
        {
            ShowResults();
        }
    }

    void StartGame()
    {
        if(Lobby.Owner.Id != Game.SteamId) return;
        if(LobbyState != LOBBY_STATE.WAITING_FOR_PLAYERS) return;

        StartingPlayers.Clear();
        foreach(var friend in Lobby.Members)
        {
            StartingPlayers.Add(friend);
        }
        // Randomize the order of the list
        StartingPlayers.Sort((a, b) => (new Random()).Next(0, 2) == 0 ? -1 : 1);
        Lobby.SetData("players", ListString(StartingPlayers));
        Lobby.SetData("played", "");

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

        NetworkStartRound();
        
    }

    void StartRound()
    {
        LobbyState = LOBBY_STATE.CHOOSING_WORD;

        CorrectPlayers.Clear();
        Header.SetOverride(Drawing.Name + " is choosing a word...");
        GameTimer = 15f;

        if(Lobby.Owner.Id == Game.SteamId)
        {
            Lobby.SetData("drawing", Drawing.Id.ToString());
            Lobby.SetData("state", LOBBY_STATE.CHOOSING_WORD.ToString());
            Lobby.SetData("correct", "");
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
        Lobby.SetData("guess", Guess);

        ResetCanvas();

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

        if(Lobby.Owner.Id == Game.SteamId)
        {
            Lobby.SetData("state", LOBBY_STATE.PLAYING.ToString());
        }

    }

    void NextRound()
    {
        if(Lobby.Owner.Id != Game.SteamId) return;
        if(LobbyState == LOBBY_STATE.WAITING_FOR_PLAYERS || LobbyState == LOBBY_STATE.RESULTS) return;

        if(StartingPlayers.Count > 0)
        {
            Drawing = StartingPlayers[0];
            StartingPlayers.RemoveAt(0);
            LobbyState = LOBBY_STATE.CHOOSING_WORD;
            Header.SetOverride(Drawing.Name + " is choosing a word...");
            Lobby.SetData("played", ListString(FinishedPlayers));
            Lobby.SetData("drawing", Drawing.Id.ToString());
            StartRound();
        }
        else
        {
            ShowResults();
        }
    }

    void CorrectGuess(Friend friend)
    {
        if(Lobby.Owner.Id != Game.SteamId) return;
        if(CorrectPlayers.Contains(friend)) return;
        if(LobbyState != LOBBY_STATE.PLAYING) return;

        long DrawingScore = 200;
        long PlayerScore = 1000;


        CorrectPlayers.Add(friend);
        Lobby.SetData("correct", ListString(CorrectPlayers));

        if(CorrectPlayers.Count == Lobby.MemberCount - 1)
        {
            if(CorrectPlayers.Count > 1)
            {
                PlayerScore = (long)MathF.Floor(Utils.Map(GameTimer, 30, 0, 1000, 250));
                DrawingScore = 100;
            }
            RevealAnswer();
        }
        else if(CorrectPlayers.Count == 1)
        {
            GameTimer = 30;
        }
        else
        {
            PlayerScore = (long)MathF.Floor(Utils.Map(GameTimer, 30, 0, 1000, 200));
            DrawingScore = 100;
        }

        Lobby.SetData("timer", GameTimer.ToString());

        NetworkCorrectGuess(friend, PlayerScore, DrawingScore);
    }

    void RevealAnswer()
    {

    }

    void ShowResults()
    {

    }

    string ListString(List<Friend> list)
    {
        string str = "";
        for(int i=0; i<list.Count; i++)
        {
            str += list[i].Id.ToString();
            if(i < list.Count - 1)
            {
                str += ",";
            }
        }
        return str;
    }

    List<Friend> ListFromString(string str)
    {
        if(string.IsNullOrEmpty(str)) return new List<Friend>();
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
        Lobby.SendChat(ChatText);
        ChatEntry.Text = "";
        ChatEntry.Focus();
    }

    // PLAYER SCORE FUNCTIONS

    long GetPlayerScore(long id)
    {
        if(PlayerScores.ContainsKey(id))
        {
            return PlayerScores[id];
        }
        else
        {
            return 0;
        }
    }

    void SetPlayerScore(long id, long score, bool update = true)
    {
        if(PlayerScores.ContainsKey(id))
        {
            PlayerScores[id] = score;
        }
        else
        {
            PlayerScores.Add(id, score);
        }
        if(update) UpdatePlayerOrder();
    }

    void GivePlayerScore(long id, long score, bool update = true)
    {
        if(PlayerScores.ContainsKey(id))
        {
            PlayerScores[id] += score;
        }
        else
        {
            PlayerScores.Add(id, score);
        }
        if(update) UpdatePlayerOrder();
    }

    void TakePlayerScore(long id, long score, bool update = true)
    {
        if(PlayerScores.ContainsKey(id))
        {
            PlayerScores[id] -= score;
        }
        else
        {
            PlayerScores.Add(id, -score);
        }
        if(update) UpdatePlayerOrder();
    }

    void UpdatePlayerOrder()
    {
        for(int i=0; i<PlayerList.ChildrenCount; i++)
        {
            if(PlayerList.GetChild(i) is PlayerListEntry entry)
            {
                entry.SetScore(GetPlayerScore(entry.Player.Id));
            }
        }

        // Sort with SetChildIndex
        List<PlayerListEntry> entries = new List<PlayerListEntry>();
        foreach(var child in PlayerList.Children)
        {
            if(child is PlayerListEntry entry)
            {
                entries.Add(entry);
            }
        }
        entries.Sort((a, b) => (int)(GetPlayerScore(b.Player.Id) - GetPlayerScore(a.Player.Id)));
        for(int i=0; i<entries.Count; i++)
        {
            PlayerList.SetChildIndex(entries[i], i);
        }
    }

    // LOBBY FUNCTIONS

    void OnChatMessage(Friend friend, string message)
    {
        if(CorrectPlayers.Contains(friend) && CorrectPlayers.Contains(new Friend(Game.SteamId)))
        {
            CreateChatEntry(friend.Name + ":", message);
        }
        else if(Lobby.Data["state"] == LOBBY_STATE.PLAYING.ToString() && message.Contains(Lobby.Data["guess"]))
        {
            if(Lobby.Owner.Id == Game.SteamId)
            {
                CorrectGuess(friend);
            }
        }
        else
        {
            CreateChatEntry(friend.Name + ":", message);
        }
    }

    void OnMemberEnter(Friend friend)
    {
        CreateChatEntry(friend.Name, " has joined the game.");

        PlayerListEntry entry = PlayerList.AddChild<PlayerListEntry>();
        entry.Player = friend;
        entry.Lobby = Lobby;
        entry.SetScore(GetPlayerScore(friend.Id));
    }

    void OnMemberLeave(Friend friend)
    {
        CreateChatEntry(friend.Name, " has left the game.");

        if(Lobby.Owner.Id == Game.SteamId)
        {
            if(friend.Id == Drawing.Id && (LobbyState == LOBBY_STATE.PLAYING || LobbyState == LOBBY_STATE.CHOOSING_WORD))
            {
                NextRound();
            }
        }

        foreach(var child in PlayerList.Children)
        {
            if(child is PlayerListEntry entry)
            {
                if(entry.Player.Id == friend.Id)
                {
                    entry.Delete();
                    break;
                }
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

	// TICK FUNCTION
	public override void Tick()
	{
		Lobby.ReceiveMessages(OnNetworkMessage);

        if(LobbyState == LOBBY_STATE.PLAYING && GameTimer > 0f)
        {
            GameTimer -= Time.Delta;
            if(GameTimer < 0f){
                
                if(Lobby.Owner.Id == Game.SteamId)
                {
                    RevealAnswer();
                }

                GameTimer = 0f;
            }
        }

        if(LastUpdate > 2f)
        {
            if(Lobby.Owner.Id == Game.SteamId)
            {
                Lobby.SetData("timer", GameTimer.ToString());
            }
            LastUpdate = 0f;
        }
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
        if(Canvas is null) return;
        if(LobbyState != LOBBY_STATE.PLAYING) return;

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
        if(Canvas is null) return;
        if(LobbyState != LOBBY_STATE.PLAYING) return;

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
        return HashCode.Combine(LogoClass(), Lobby.MemberCount, MathF.Floor(GameTimer));
    }


    // NETWORKING SHIT

    void OnNetworkMessage(ILobby.NetworkMessage msg)
    {
        ByteStream data = msg.Data;

        Log.Info(msg.Source);

        ushort messageId = data.Read<ushort>();

        Log.Info((LOBBY_MESSAGE)messageId);

        switch((LOBBY_MESSAGE)messageId)
        {
            case LOBBY_MESSAGE.START_ROUND:
                long drawingId = data.Read<long>();
                Drawing = new Friend(drawingId);
                StartingPlayers = ListFromString(Lobby.Data["players"]);
                if(Lobby.Data.ContainsKey("played")) FinishedPlayers = ListFromString(Lobby.Data["played"]);
                else FinishedPlayers?.Clear();
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
                if(Lobby.Owner.Id == Game.SteamId)
                {
                    Lobby.SetData("guess", Guess);
                }
                StartDrawing();
                break;

            case LOBBY_MESSAGE.DRAW:
                Color32 color = data.Read<Color32>();
                int size = data.Read<ushort>();
                ushort pointCount = data.Read<ushort>();
                List<Vector2> points = new List<Vector2>();
                for(int i=0; i<pointCount; i++)
                {
                    ushort x = data.Read<ushort>();
                    ushort y = data.Read<ushort>();
                    points.Add(new Vector2(x, y));
                }
                Draw(points, color, size);
                break;
            
            case LOBBY_MESSAGE.REQUEST_CANVAS:
                if(Lobby.Owner.Id == Game.SteamId && LobbyState == LOBBY_STATE.PLAYING)
                {
                    long friendId = data.Read<long>();
                    Friend friend = new Friend(friendId);
                    if(!Lobby.Members.Contains(friend)) break;
                    NetworkSendCanvas(friend);
                }
                break;
            
            case LOBBY_MESSAGE.SEND_CANVAS:
                if(LobbyState != LOBBY_STATE.PLAYING) break;
                if(Canvas is not Texture)
                {
                    ResetCanvas();
                }

                int pixelCount = data.Read<int>();
                Color32[] pixels = new Color32[pixelCount];
                for(int i=0; i<pixelCount; i++)
                {
                    pixels[i] = data.Read<Color32>();
                }
                Canvas.Update(pixels);
                CanvasPanel.SetTexture(Canvas);
                StateHasChanged();

                Drawing = new Friend(long.Parse(Lobby.Data["drawing"]));
                StartingPlayers = ListFromString(Lobby.Data["players"]);
                if(Lobby.Data.ContainsKey("played")) FinishedPlayers = ListFromString(Lobby.Data["played"]);
                else FinishedPlayers?.Clear();
                Guess = Lobby.Data["guess"];
                StartDrawing();
                break;

            case LOBBY_MESSAGE.CORRECT_GUESS:
                if(LobbyState != LOBBY_STATE.PLAYING) break;

                long playerId = data.Read<long>();
                Friend player = new Friend(playerId);
                long playerScore = data.Read<long>();
                long drawerId = data.Read<long>();
                Friend drawing = new Friend(drawerId);
                long drawingScore = data.Read<long>();

                CreateChatEntry(player.Name, " guessed correctly!");

                GivePlayerScore(player.Id, playerScore, false);
                GivePlayerScore(drawing.Id, drawingScore, false);
                UpdatePlayerOrder();

                if(Lobby.Owner.Id != Game.SteamId)
                {
                    if(Lobby.Data.ContainsKey("timer")) GameTimer = float.Parse(Lobby.Data["timer"]);
                }

                break;
        }
    }

    void NetworkStartRound()
    {
        Log.Info("Starting on the network");
        ByteStream data = ByteStream.Create(10);
        data.Write((ushort)LOBBY_MESSAGE.START_ROUND);
        data.Write(Drawing.Id);

        Lobby.BroadcastMessage(data);
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

        Lobby.BroadcastMessage(data);
    }

    public void NetworkDraw(List<Vector2> points, Color color, int size)
    {
        ByteStream data = ByteStream.Create(10 + (2 * points.Count));
        data.Write((ushort)LOBBY_MESSAGE.DRAW);
        data.Write(color.ToColor32());
        data.Write((ushort)size);
        data.Write((ushort)points.Count);
        for(int i=0; i<points.Count; i++)
        {
            data.Write((ushort)points[i].x);
            data.Write((ushort)points[i].y);
        }

        Lobby.BroadcastMessage(data);
    }

    void NetworkRequestCanvas()
    {
        Header.SetOverride("Loading...");
        ByteStream data = ByteStream.Create(10);
        data.Write((ushort)LOBBY_MESSAGE.REQUEST_CANVAS);
        data.Write((long)Game.SteamId);

        Lobby.OwnerMessage(data);
    }

    void NetworkSendCanvas(Friend friend)
    {
        if(LobbyState != LOBBY_STATE.PLAYING) return;
        if(Lobby.Owner.Id != Game.SteamId) return;
        if(Canvas is not Texture) return;

        Color32[] pixels = Canvas.GetPixels();

        ByteStream data = ByteStream.Create(6 + (pixels.Length * 4));
        data.Write((ushort)LOBBY_MESSAGE.SEND_CANVAS);
        data.Write((int)pixels.Length);
        for(int i=0; i<pixels.Length; i++)
        {
            data.Write(pixels[i]);
        }

        Lobby.SendMessage(friend, data);
    }

    void NetworkCorrectGuess(Friend friend, long playerScore, long drawingScore)
    {
        if(Lobby.Owner.Id != Game.SteamId) return;
        if(LobbyState != LOBBY_STATE.PLAYING) return;

        ByteStream data = ByteStream.Create(34);
        data.Write((ushort)LOBBY_MESSAGE.CORRECT_GUESS);
        data.Write((long)friend.Id);
        data.Write((long)playerScore);
        data.Write((long)Drawing.Id);
        data.Write((long)drawingScore);

        Lobby.BroadcastMessage(data);
    }
}