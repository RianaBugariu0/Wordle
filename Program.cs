using Silk.NET.Maths;
using Silk.NET.SDL;

namespace Wordle;

public static class Program
{
    private const int LogicalWidth = 920;
    private const int LogicalHeight = 900;
    private const int WindowMargin = 20;
    private const int CardPadding = 28;
    private const int BoardCols = 5;
    private const int BoardRows = 6;

    private static string[] keyboardRow1 = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" };
    private static string[] keyboardRow2 = { "A", "S", "D", "F", "G", "H", "J", "K", "L" };
    private static string[] keyboardRow3 = { "Z", "X", "C", "V", "B", "N", "M" };

    public static void Main()
    {
        WordleGame game = new WordleGame();
        ScoreStore store = new ScoreStore(GetStatsPath());
        WordleStats stats = store.Load();

        WordleSdlContext context = new WordleSdlContext();
        Sdl sdl = new Sdl(context);

        if (sdl.Init(Sdl.InitVideo | Sdl.InitEvents | Sdl.InitTimer) < 0)
        {
            throw new InvalidOperationException("SDL init failed");
        }

        unsafe
        {
            IntPtr windowPtr = (IntPtr)sdl.CreateWindow(
                "Wordle",
                Sdl.WindowposCentered,
                Sdl.WindowposCentered,
                LogicalWidth,
                LogicalHeight,
                (uint)WindowFlags.Resizable | (uint)WindowFlags.AllowHighdpi);

            if (windowPtr == IntPtr.Zero)
            {
                Console.WriteLine("No window, using console mode");
                RunConsoleGame(game, stats);
                sdl.Quit();
                return;
            }

            Renderer* renderer = (Renderer*)sdl.CreateRenderer((Window*)windowPtr, -1, (uint)RendererFlags.Accelerated);
            if (renderer == null)
            {
                Console.WriteLine("No renderer, using console mode");
                sdl.DestroyWindow((Window*)windowPtr);
                RunConsoleGame(game, stats);
                sdl.Quit();
                return;
            }

            sdl.RenderSetLogicalSize(renderer, LogicalWidth, LogicalHeight);
            sdl.RenderSetVSync(renderer, 1);
            RunGameLoop(sdl, renderer, ref game, stats);
            sdl.DestroyWindow((Window*)windowPtr);
        }

        sdl.Quit();
    }

    // fallback if SDL does not work on the pc
    private static void RunConsoleGame(WordleGame game, WordleStats stats)
    {
        Console.WriteLine("Wordle console mode");
        Console.WriteLine("Type a 5 letter word. Type QUIT to stop.");

        while (true)
        {
            Console.Write("Guess: ");
            string? line = Console.ReadLine();
            if (line == null)
            {
                break;
            }

            string guess = line.Trim().ToUpperInvariant();
            if (guess == "QUIT")
            {
                break;
            }

            if (guess.Length != 5)
            {
                Console.WriteLine("Need 5 letters");
                continue;
            }

            WordleGuessResult result = game.SubmitGuess(guess);
            if (result.Status == WordleResultStatus.NotInWordList)
            {
                Console.WriteLine("Not in word list");
                continue;
            }

            if (result.Status == WordleResultStatus.Ok)
            {
                if (game.Won)
                {
                    stats.GamesPlayed++;
                    stats.Wins++;
                    if (game.GuessCount < stats.BestGuesses)
                    {
                        stats.BestGuesses = game.GuessCount;
                    }

                    SaveStats(stats);
                    Console.WriteLine("You won!");
                    break;
                }

                if (game.IsOver)
                {
                    stats.GamesPlayed++;
                    SaveStats(stats);
                    Console.WriteLine("You lost. Answer was: " + game.Answer);
                    break;
                }
            }
        }
    }

    private static unsafe void RunGameLoop(Sdl sdl, Renderer* renderer, ref WordleGame game, WordleStats stats)
    {
        bool running = true;
        Event ev = new Event();
        string typedWord = "";
        string statusMessage = "Type a 5-letter word and press Enter";
        bool finished = false;

        while (running)
        {
            while (sdl.PollEvent(ref ev) != 0)
            {
                if (ev.Type == (uint)EventType.Quit)
                {
                    running = false;
                    break;
                }

                if (ev.Type != (uint)EventType.Keydown)
                {
                    continue;
                }

                Scancode key = ev.Key.Keysym.Scancode;

                if (key == Scancode.ScancodeEscape)
                {
                    running = false;
                }
                else if (finished == false && key == Scancode.ScancodeBackspace)
                {
                    if (typedWord.Length > 0)
                    {
                        typedWord = typedWord.Substring(0, typedWord.Length - 1);
                    }
                }
                else if (finished == false && key == Scancode.ScancodeReturn)
                {
                    if (typedWord.Length == 5)
                    {
                        WordleGuessResult result = game.SubmitGuess(typedWord);
                        if (result.Status == WordleResultStatus.Ok)
                        {
                            typedWord = "";
                            if (game.Won)
                            {
                                statusMessage = "You won! Press R for a new game";
                                finished = true;
                                UpdateStatsAfterGame(stats, game);
                            }
                            else if (game.IsOver)
                            {
                                statusMessage = "You lost. Answer: " + game.Answer + ". Press R";
                                finished = true;
                                UpdateStatsAfterGame(stats, game);
                            }
                            else
                            {
                                statusMessage = "Good guess. Try another one";
                            }
                        }
                        else if (result.Status == WordleResultStatus.NotInWordList)
                        {
                            statusMessage = "That word is not in the list";
                            typedWord = "";
                        }
                    }
                    else
                    {
                        statusMessage = "Need exactly 5 letters";
                    }
                }
                else if (finished && key == Scancode.ScancodeR)
                {
                    game = new WordleGame();
                    typedWord = "";
                    statusMessage = "Type a 5-letter word and press Enter";
                    finished = false;
                }
                else if (finished == false)
                {
                    if (key >= Scancode.ScancodeA && key <= Scancode.ScancodeZ)
                    {
                        if (typedWord.Length < 5)
                        {
                            int offset = (int)key - (int)Scancode.ScancodeA;
                            char letter = (char)('A' + offset);
                            typedWord = typedWord + letter;
                        }
                    }
                }
            }

            DrawEverything(sdl, renderer, game, stats, typedWord, statusMessage);
            System.Threading.Thread.Sleep(16);
        }
    }

    private static void UpdateStatsAfterGame(WordleStats stats, WordleGame game)
    {
        stats.GamesPlayed = stats.GamesPlayed + 1;
        if (game.Won)
        {
            stats.Wins = stats.Wins + 1;
            if (game.GuessCount < stats.BestGuesses)
            {
                stats.BestGuesses = game.GuessCount;
            }
        }

        SaveStats(stats);
    }

    private static unsafe void DrawEverything(Sdl sdl, Renderer* renderer, WordleGame game, WordleStats stats, string typedWord, string message)
    {
        Layout layout = CreateLayout();

        sdl.SetRenderDrawColor(renderer, 255, 236, 244, 255);
        sdl.RenderClear(renderer);

        DrawBackgroundCard(sdl, renderer, layout);
        DrawTitleText(sdl, renderer, layout);
        DrawGuessBoard(sdl, renderer, layout, game, typedWord);
        DrawKeyboardKeys(sdl, renderer, layout, game);
        DrawBottomStatus(sdl, renderer, layout, stats, message);

        sdl.RenderPresent(renderer);
    }

    private static unsafe void DrawBackgroundCard(Sdl sdl, Renderer* renderer, Layout layout)
    {
        sdl.SetRenderDrawColor(renderer, 230, 180, 200, 255);
        Rectangle<int> shadow = new Rectangle<int>(layout.CardLeft + 4, layout.CardTop + 6, layout.CardWidth, layout.CardHeight);
        sdl.RenderFillRect(renderer, ref shadow);

        sdl.SetRenderDrawColor(renderer, 255, 210, 228, 255);
        Rectangle<int> border = new Rectangle<int>(layout.CardLeft, layout.CardTop, layout.CardWidth, layout.CardHeight);
        sdl.RenderFillRect(renderer, ref border);

        sdl.SetRenderDrawColor(renderer, 255, 250, 252, 255);
        Rectangle<int> fill = new Rectangle<int>(layout.CardLeft + 3, layout.CardTop + 3, layout.CardWidth - 6, layout.CardHeight - 6);
        sdl.RenderFillRect(renderer, ref fill);
    }

    private static unsafe void DrawGuessBoard(Sdl sdl, Renderer* renderer, Layout layout, WordleGame game, string typedWord)
    {
        for (int row = 0; row < BoardRows; row++)
        {
            for (int col = 0; col < BoardCols; col++)
            {
                int x = layout.BoardLeft + col * (layout.TileSize + layout.TileGap);
                int y = layout.BoardTop + row * (layout.TileSize + layout.TileGap);
                string letter = "";
                TileColor tileColor = TileColor.Gray;
                bool typingNow = false;

                if (row < game.Guesses.Count)
                {
                    letter = game.Guesses[row][col].ToString();
                    WordleGuessResult rowResult = game.EvaluateGuess(game.Guesses[row]);
                    tileColor = rowResult.Colors[col];
                }
                else if (row == game.GuessCount)
                {
                    if (typedWord.Length > col)
                    {
                        letter = typedWord[col].ToString();
                        typingNow = true;
                    }
                }

                DrawOneTile(sdl, renderer, x, y, layout.TileSize, layout.TileSize, letter, tileColor, typingNow, false, layout.TileFontScale);
            }
        }
    }

    private static unsafe void DrawKeyboardKeys(Sdl sdl, Renderer* renderer, Layout layout, WordleGame game)
    {
        var keyboardState = game.GetKeyboardState();
        string[][] rows = { keyboardRow1, keyboardRow2, keyboardRow3 };

        for (int row = 0; row < rows.Length; row++)
        {
            string[] keys = rows[row];
            int rowWidth = keys.Length * layout.KeyWidth + (keys.Length - 1) * layout.KeyGap;
            int rowLeft = layout.BoardLeft + (layout.BoardWidth - rowWidth) / 2;
            int y = layout.KeyboardTop + row * (layout.KeyHeight + layout.KeyGap);

            for (int i = 0; i < keys.Length; i++)
            {
                int x = rowLeft + i * (layout.KeyWidth + layout.KeyGap);
                char keyChar = keys[i][0];
                TileColor color = TileColor.Gray;
                bool tried = keyboardState.ContainsKey(keyChar);
                if (tried)
                {
                    color = keyboardState[keyChar];
                }

                DrawOneTile(sdl, renderer, x, y, layout.KeyWidth, layout.KeyHeight, keys[i], color, false, !tried, layout.KeyFontScale);
            }
        }
    }

    private static unsafe void DrawOneTile(
        Sdl sdl,
        Renderer* renderer,
        int x,
        int y,
        int width,
        int height,
        string letter,
        TileColor color,
        bool isTyping,
        bool notUsedYet,
        int fontScale)
    {
        byte fillR = 248;
        byte fillG = 232;
        byte fillB = 242;
        byte borderR = 230;
        byte borderG = 190;
        byte borderB = 210;
        byte textR = 90;
        byte textG = 55;
        byte textB = 85;

        if (isTyping)
        {
            fillR = 255;
            fillG = 236;
            fillB = 246;
            borderR = 240;
            borderG = 150;
            borderB = 185;
        }
        else if (color == TileColor.Green)
        {
            fillR = 134;
            fillG = 207;
            fillB = 154;
            borderR = 96;
            borderG = 175;
            borderB = 118;
            textR = 255;
            textG = 255;
            textB = 255;
        }
        else if (color == TileColor.Yellow)
        {
            fillR = 255;
            fillG = 214;
            fillB = 120;
            borderR = 230;
            borderG = 180;
            borderB = 70;
            textR = 110;
            textG = 75;
            textB = 20;
        }
        else if (color == TileColor.Gray && letter != "" && notUsedYet == false)
        {
            fillR = 188;
            fillG = 170;
            fillB = 198;
            borderR = 150;
            borderG = 135;
            borderB = 165;
            textR = 255;
            textG = 255;
            textB = 255;
        }

        sdl.SetRenderDrawColor(renderer, borderR, borderG, borderB, 255);
        Rectangle<int> outer = new Rectangle<int>(x, y, width, height);
        sdl.RenderFillRect(renderer, ref outer);

        sdl.SetRenderDrawColor(renderer, fillR, fillG, fillB, 255);
        Rectangle<int> inner = new Rectangle<int>(x + 2, y + 2, width - 4, height - 4);
        sdl.RenderFillRect(renderer, ref inner);

        if (letter != "")
        {
            int textWidth = SimpleFont.CharWidth(fontScale);
            int textHeight = SimpleFont.CharHeight(fontScale);
            int textX = x + (width - textWidth) / 2;
            int textY = y + (height - textHeight) / 2;
            SimpleFont.DrawChar(sdl, renderer, letter[0], textX, textY, fontScale, textR, textG, textB);
        }
    }

    private static unsafe void DrawTitleText(Sdl sdl, Renderer* renderer, Layout layout)
    {
        string title = "WORDLE";
        int titleWidth = SimpleFont.MeasureString(title, layout.TitleScale);
        int titleX = layout.CardLeft + (layout.CardWidth - titleWidth) / 2;
        SimpleFont.DrawString(sdl, renderer, title, titleX, layout.TitleTop, layout.TitleScale, 236, 90, 130);

        string subtitle = "GUESS THE WORD";
        int subtitleWidth = SimpleFont.MeasureString(subtitle, layout.SubtitleScale);
        int subtitleX = layout.CardLeft + (layout.CardWidth - subtitleWidth) / 2;
        SimpleFont.DrawString(sdl, renderer, subtitle, subtitleX, layout.SubtitleTop, layout.SubtitleScale, 200, 120, 150);
    }

    private static unsafe void DrawBottomStatus(Sdl sdl, Renderer* renderer, Layout layout, WordleStats stats, string message)
    {
        int boxLeft = layout.CardLeft + CardPadding;
        int boxWidth = layout.CardWidth - CardPadding * 2;
        int boxTop = layout.StatusTop;

        sdl.SetRenderDrawColor(renderer, 255, 210, 228, 255);
        Rectangle<int> outer = new Rectangle<int>(boxLeft, boxTop, boxWidth, layout.StatusHeight);
        sdl.RenderFillRect(renderer, ref outer);

        sdl.SetRenderDrawColor(renderer, 255, 245, 250, 255);
        Rectangle<int> inner = new Rectangle<int>(boxLeft + 2, boxTop + 2, boxWidth - 4, layout.StatusHeight - 4);
        sdl.RenderFillRect(renderer, ref inner);

        int messageWidth = SimpleFont.MeasureString(message, layout.StatusFontScale);
        int messageX = boxLeft + (boxWidth - messageWidth) / 2;
        SimpleFont.DrawString(sdl, renderer, message, messageX, boxTop + 14, layout.StatusFontScale, 110, 70, 95);

        string best = "-";
        if (stats.BestGuesses != 999)
        {
            best = stats.BestGuesses.ToString();
        }

        string statsLine = "WINS " + stats.Wins + "   GAMES " + stats.GamesPlayed + "   BEST " + best;
        int statsWidth = SimpleFont.MeasureString(statsLine, layout.StatusFontScale);
        int statsX = boxLeft + (boxWidth - statsWidth) / 2;
        SimpleFont.DrawString(sdl, renderer, statsLine, statsX, boxTop + 50, layout.StatusFontScale, 180, 110, 145);
    }

    private static void SaveStats(WordleStats stats)
    {
        ScoreStore store = new ScoreStore(GetStatsPath());
        store.Save(stats);
    }

    private static string GetStatsPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "stats.json");
    }

    private readonly struct Layout
    {
        public int CardLeft { get; init; }
        public int CardTop { get; init; }
        public int CardWidth { get; init; }
        public int CardHeight { get; init; }
        public int TileSize { get; init; }
        public int TileGap { get; init; }
        public int KeyWidth { get; init; }
        public int KeyHeight { get; init; }
        public int KeyGap { get; init; }
        public int BoardLeft { get; init; }
        public int BoardTop { get; init; }
        public int KeyboardTop { get; init; }
        public int StatusTop { get; init; }
        public int StatusHeight { get; init; }
        public int TitleTop { get; init; }
        public int SubtitleTop { get; init; }
        public int TitleScale { get; init; }
        public int SubtitleScale { get; init; }
        public int TileFontScale { get; init; }
        public int KeyFontScale { get; init; }
        public int StatusFontScale { get; init; }
        public int BoardWidth { get; init; }
    }

    private static Layout CreateLayout()
    {
        var cardLeft = WindowMargin;
        var cardTop = WindowMargin;
        var cardWidth = LogicalWidth - WindowMargin * 2;
        var cardHeight = LogicalHeight - WindowMargin * 2;
        var innerWidth = cardWidth - CardPadding * 2;
        var innerHeight = cardHeight - CardPadding * 2;

        var headerHeight = innerHeight * 11 / 100;
        var statusHeight = innerHeight * 13 / 100;
        var sectionGap = innerHeight * 3 / 100;
        var playHeight = innerHeight - headerHeight - statusHeight - sectionGap * 2;
        var keyboardHeight = playHeight * 34 / 100;
        var boardHeight = playHeight - keyboardHeight;

        var tileGap = Math.Max(5, boardHeight / 55);
        var tileSizeByHeight = (boardHeight - tileGap * (BoardRows - 1)) / BoardRows;
        var tileSizeByWidth = (innerWidth - tileGap * (BoardCols - 1)) / BoardCols;
        var tileSize = Math.Clamp(Math.Min(tileSizeByHeight, tileSizeByWidth), 48, 110);
        tileGap = Math.Max(5, tileSize / 10);

        var boardWidth = BoardCols * tileSize + (BoardCols - 1) * tileGap;
        var boardLeft = cardLeft + CardPadding + (innerWidth - boardWidth) / 2;
        var boardTop = cardTop + CardPadding + headerHeight;

        var keyGap = Math.Max(4, tileGap * 2 / 3);
        var keyHeight = (keyboardHeight - keyGap * 2) / 3;
        var keyWidth = (innerWidth - keyGap * 9) / 10;
        keyWidth = Math.Clamp(Math.Min(keyWidth, keyHeight + 8), 30, 72);
        keyHeight = Math.Clamp(keyHeight, 34, 64);

        var keyboardTop = boardTop + BoardRows * tileSize + (BoardRows - 1) * tileGap + sectionGap;
        var statusTop = keyboardTop + 3 * keyHeight + 2 * keyGap + sectionGap;

        return new Layout
        {
            CardLeft = cardLeft,
            CardTop = cardTop,
            CardWidth = cardWidth,
            CardHeight = cardHeight,
            TileSize = tileSize,
            TileGap = tileGap,
            KeyWidth = keyWidth,
            KeyHeight = keyHeight,
            KeyGap = keyGap,
            BoardLeft = boardLeft,
            BoardTop = boardTop,
            KeyboardTop = keyboardTop,
            StatusTop = statusTop,
            StatusHeight = statusHeight,
            TitleTop = cardTop + CardPadding + headerHeight / 6,
            SubtitleTop = cardTop + CardPadding + headerHeight * 5 / 10,
            TitleScale = Math.Clamp(cardWidth / 200, 4, 6),
            SubtitleScale = Math.Clamp(cardWidth / 380, 2, 3),
            TileFontScale = Math.Clamp(tileSize / 17, 3, 6),
            KeyFontScale = Math.Clamp(keyHeight / 22, 2, 4),
            StatusFontScale = Math.Clamp(cardWidth / 400, 2, 3),
            BoardWidth = boardWidth,
        };
    }
}
