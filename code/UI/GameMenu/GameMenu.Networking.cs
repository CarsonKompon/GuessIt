using Sandbox;
using Sandbox.Menu;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GuessIt;

enum LOBBY_MESSAGE
{
    NONE,
    START_ROUND,
    CHOOSE_WORD,
    DRAW,
    REQUEST_CANVAS,
    SEND_CANVAS,
    CORRECT_GUESS,
    REVEAL_WORD,
    SHOW_RESULTS,
    END_GAME
}

public partial class GameMenu
{
    void OnNetworkMessage(ILobby.NetworkMessage msg)
    {
        ByteStream data = msg.Data;

        ushort messageId = data.Read<ushort>();

        Log.Info((LOBBY_MESSAGE)messageId);

        switch((LOBBY_MESSAGE)messageId)
        {
            case LOBBY_MESSAGE.START_ROUND:
                if(Lobby.Owner.Id != Game.SteamId)
                {
                    GetLobbyPlayerData();
                }
                long drawingId = data.Read<long>();
                Drawing = new Friend(drawingId);
                if(StartingPlayers.Contains(Drawing))
                {
                    StartingPlayers.Remove(Drawing);
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
                if(Lobby.Owner.Id == Game.SteamId)
                {
                    Lobby.SetData("guess", Guess);
                }
                GameTimer = 90f;
                StartDrawing();
                break;

            case LOBBY_MESSAGE.DRAW:
                if(LobbyState != LOBBY_STATE.PLAYING) break;
                if(Drawing.Id == Game.SteamId) break;
                if(Canvas is not Texture)
                {
                    ResetCanvas();
                }

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
                    if(!AllPlayers.Contains(friend)) break;
                    NetworkSendCanvas(friend);
                }
                break;
            
            case LOBBY_MESSAGE.SEND_CANVAS:
                if(LobbyState != LOBBY_STATE.PLAYING) break;

                ResetCanvas();
                int pixelCount = data.Read<int>();
                Color32[] pixels = new Color32[pixelCount];
                for(int i=0; i<pixelCount; i++)
                {
                    pixels[i] = data.Read<Color32>();
                }
                Canvas.Update(pixels);
                CanvasPanel.SetTexture(Canvas);
                StateHasChanged();

                GetLobbyPlayerData();
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

                CreateChatEntry(player.Name, " guessed correctly!", "guess-correct");
                Audio.Play("ui.guess.correct");
                UpdatePlayerClass(player, "correct");

                if(player.Id == Game.SteamId)
                {
                    Sandbox.Services.Stats.Increment( "correct-guesses", 1 );
                }

                if(!CorrectPlayers.Contains(player)) CorrectPlayers.Add(player);
                GivePlayerScore(player.Id, playerScore, false);
                GivePlayerScore(drawing.Id, drawingScore, false);
                UpdatePlayerOrder();

                float timer = GameTimer;
                if(Lobby.Data.ContainsKey("timer")) timer = float.Parse(Lobby.Data["timer"]);
                if(timer < GameTimer) GameTimer = timer;

                if(Lobby.Owner.Id != Game.SteamId)
                {
                    if(Lobby.Data.ContainsKey("timer")) GameTimer = float.Parse(Lobby.Data["timer"]);
                }

                break;

            case LOBBY_MESSAGE.REVEAL_WORD:
                if(LobbyState != LOBBY_STATE.PLAYING) break;

                CorrectPlayers.Clear();
                ushort correctCount = data.Read<ushort>();
                for(int i=0; i<correctCount; i++)
                {
                    long correctId = data.Read<long>();
                    CorrectPlayers.Add(new Friend(correctId));
                }

                byteLength = data.Read<int>();
                byte[] wordBytes = new byte[byteLength];
                for(int i=0; i<byteLength; i++)
                {
                    wordBytes[i] = data.Read<byte>();
                }
                Guess = System.Text.Encoding.Unicode.GetString(wordBytes);
                RevealAnswer();
                break;
            
            case LOBBY_MESSAGE.SHOW_RESULTS:
                ShowResults();
                break;
            
            case LOBBY_MESSAGE.END_GAME:
                EndGame();
                break;
        }
    }

    void NetworkStartRound()
    {
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

    void NetworkRevealAnswer()
    {
        if(Lobby.Owner.Id != Game.SteamId) return;
        if(LobbyState != LOBBY_STATE.PLAYING) return;
        GameTimer = 0f;

        byte[] wordBytes = System.Text.Encoding.Unicode.GetBytes(Guess);
        ByteStream data = ByteStream.Create(8 + (CorrectPlayers.Count * 8) + wordBytes.Length);
        data.Write((ushort)LOBBY_MESSAGE.REVEAL_WORD);
        data.Write((ushort)CorrectPlayers.Count);
        for(int i=0; i<CorrectPlayers.Count; i++)
        {
            data.Write((long)CorrectPlayers[i].Id);
        }
        data.Write((int)wordBytes.Length);
        for(int i=0; i<wordBytes.Length; i++)
        {
            data.Write(wordBytes[i]);
        }

        Lobby.BroadcastMessage(data);
    }

    void NetworkShowResults()
    {
        if(Lobby.Owner.Id != Game.SteamId) return;
        GameTimer = 10f;

        ByteStream data = ByteStream.Create(2);
        data.Write((ushort)LOBBY_MESSAGE.SHOW_RESULTS);

        Lobby.BroadcastMessage(data);
    }

    void NetworkEndGame()
    {
        if(Lobby.Owner.Id != Game.SteamId) return;

        ByteStream data = ByteStream.Create(2);
        data.Write((ushort)LOBBY_MESSAGE.END_GAME);

        Lobby.BroadcastMessage(data);
    }
}