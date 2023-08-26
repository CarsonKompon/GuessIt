using Sandbox;
using Sandbox.Menu;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GuessIt;

enum LOBBY_STATE
{
    WAITING_FOR_PLAYERS,
    CHOOSING_WORD,
    PLAYING,
    RESULTS
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
    int ClockIndex = 0;
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
            UpdatePlayerOrder();
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
        if(Lobby.Data.ContainsKey("scores"))
        {
            PlayerScores.Clear();
            string[] scores = Lobby.Data["scores"].Split(',');
            foreach(var score in scores)
            {
                string[] scoreSplit = score.Split(':');
                PlayerScores.Add(long.Parse(scoreSplit[0]), long.Parse(scoreSplit[1]));
            }
        }

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
        GameTimer = 120f;
        
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

    void CorrectGuess(Friend friend)
    {
        if(Lobby.Owner.Id != Game.SteamId) return;
        if(CorrectPlayers.Contains(friend)) return;
        if(LobbyState != LOBBY_STATE.PLAYING) return;

        long DrawingScore = 200;
        long PlayerScore = (long)MathF.Floor(Utils.Map(GameTimer, 120, 0, 1000, 800));

        CorrectPlayers.Add(friend);
        Lobby.SetData("correct", ListString(CorrectPlayers));

        if(CorrectPlayers.Count == Lobby.MemberCount - 1)
        {
            if(CorrectPlayers.Count > 1)
            {
                PlayerScore = (long)MathF.Floor(Utils.Map(GameTimer, 30, 0, 800, 250));
                DrawingScore = 100;
            }
            NetworkRevealAnswer();
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
        Header.SetWord(Guess, "The word was:", true);

        CreateChatEntry("The word was: " + Guess, "");

        if(Drawing.Id == Game.SteamId)
        {
            if(CorrectPlayers.Count > 0)
            {
                Audio.Play("ui.reveal.correct");
            }
            else
            {
                Audio.Play("ui.reveal.wrong");
            }
        }
        if(CorrectPlayers.Contains(new Friend(Game.SteamId)))
        {
            Audio.Play("ui.reveal.correct");
        }
        else
        {
            Audio.Play("ui.reveal.wrong");
        }

        GameTimer = 0;
    }

    void NextRound()
    {
        if(Lobby.Owner.Id != Game.SteamId) return;
        if(LobbyState == LOBBY_STATE.WAITING_FOR_PLAYERS || LobbyState == LOBBY_STATE.RESULTS) return;

        if(StartingPlayers.Count > 0)
        {
            Drawing = StartingPlayers[0];
            StartingPlayers.RemoveAt(0);
            FinishedPlayers.Add(Drawing);
            LobbyState = LOBBY_STATE.CHOOSING_WORD;
            Header.SetOverride(Drawing.Name + " is choosing a word...");
            Lobby.SetData("played", ListString(FinishedPlayers));
            Lobby.SetData("drawing", Drawing.Id.ToString());
            NetworkStartRound();
        }
        else
        {
            NetworkShowResults();
        }
    }

    void ShowResults()
    {
        if(Lobby.Owner.Id != Game.SteamId) return;
        if(LobbyState == LOBBY_STATE.RESULTS) return;

        LobbyState = LOBBY_STATE.RESULTS;
        Header.SetOverride("Results");
        
        GameTimer = 10f;
        
        if(Lobby.Owner.Id == Game.SteamId)
        {
            Lobby.SetData("state", LOBBY_STATE.RESULTS.ToString());
            Lobby.SetData("timer", GameTimer.ToString());
        }
    }

    void EndGame()
    {
        LobbyState = LOBBY_STATE.WAITING_FOR_PLAYERS;
        Header.SetOverride("Waiting for players...");

        if(Lobby.Owner.Id == Game.SteamId)
        {
            InitLobby();
        }
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
        if(Lobby.Owner.Id == Game.SteamId)
        {
            // Turn dictionary into string as "key:val,key:val"
            string scoreString = "";
            for(int i=0; i<PlayerScores.Count; i++)
            {
                scoreString += PlayerScores.ElementAt(i).Key + ":" + PlayerScores.ElementAt(i).Value;
                if(i < PlayerScores.Count - 1)
                {
                    scoreString += ",";
                }
            }
            Lobby.SetData("scores", scoreString);
        }

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
        int rank = 1;
        long previousScore = 0;
        for(int i=0; i<entries.Count; i++)
        {
            long score = GetPlayerScore(entries[i].Player.Id);
            if(i == 0) previousScore = score;
            if(score != previousScore) rank++;
            PlayerList.SetChildIndex(entries[i], i);
            entries[i].SetRank(rank);
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
        UpdatePlayerOrder();
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

        if(LobbyState == LOBBY_STATE.PLAYING || LobbyState == LOBBY_STATE.CHOOSING_WORD)
        {
            if(GameTimer > 0f)
            {
                float previous = MathF.Floor(MathF.Max(GameTimer, 0));
                GameTimer -= Time.Delta;

                if(MathF.Floor(MathF.Max(GameTimer, 0)) != previous)
                {
                    Audio.Play("ui.clock.tick" + (ClockIndex + 1));
                    ClockIndex = (ClockIndex + 1) % 2;
                }

                if(GameTimer <= 0f){
                    
                    if(Lobby.Owner.Id == Game.SteamId)
                    {
                        NetworkRevealAnswer();
                    }

                    GameTimer = 0f;
                }
            }
            else if(Lobby.Owner.Id == Game.SteamId && GameTimer > -5f)
            {
                GameTimer -= Time.Delta;
                if(GameTimer <= -5f)
                {
                    NextRound();
                    GameTimer = -5f;
                }
            }
        }
        else if(LobbyState == LOBBY_STATE.RESULTS)
        {
            if(GameTimer > 0f)
            {
                GameTimer -= Time.Delta;
                if(GameTimer < 0f)
                {
                    if(Lobby.Owner.Id == Game.SteamId)
                    {
                        EndGame();
                    }
                    GameTimer = 0f;
                }
            }
        }

        if(LastUpdate > 2f)
        {
            if(Lobby.Owner.Id == Game.SteamId && GameTimer >= 0f)
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
}