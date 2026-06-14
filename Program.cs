using Silk.NET.Maths;
using Silk.NET.SDL;

namespace Wordle;

public static class Program
{
    private const int WindowWidth = 800;
    private const int WindowHeight = 700;
    private const int BoardLeft = 120;
    private const int BoardTop = 120;
    private const int TileSize = 60;
    private const int TileGap = 8;

    public static void Main()
    {
        var game = new WordleGame();
        var stats = new ScoreStore(GetStatsPath()).Load();
        var sdl = new Sdl(new WordleSdlContext());

        if (sdl.Init(Sdl.InitVideo | Sdl.InitEvents | Sdl.InitTimer) < 0)
        {
            throw new InvalidOperationException("Failed to initialize SDL.");
        }

        unsafe
        {
            var window = (IntPtr)sdl.CreateWindow(
                "Wordle",
                Sdl.WindowposUndefined,
                Sdl.WindowposUndefined,
                WindowWidth,
                WindowHeight,
                (uint)WindowFlags.Resizable | (uint)WindowFlags.AllowHighdpi);

            if (window == IntPtr.Zero)
            {
                Console.WriteLine("SDL window could not be created; running in terminal-only mode.");
                RunTerminalOnlyGame(game, stats);
                sdl.Quit();
                return;
            }

            var renderer = (IntPtr)sdl.CreateRenderer((Window*)window, -1, (uint)RendererFlags.Accelerated);
            if (renderer == IntPtr.Zero)
            {
                Console.WriteLine("SDL renderer could not be created; running in terminal-only mode.");
                sdl.DestroyWindow((Window*)window);
                RunTerminalOnlyGame(game, stats);
                sdl.Quit();
                return;
            }

            sdl.RenderSetVSync((Renderer*)renderer, 1);
            RunGameLoop(sdl, (Renderer*)renderer, ref game, stats);
            sdl.DestroyWindow((Window*)window);
        }

        sdl.Quit();
    }

    private static void RunTerminalOnlyGame(WordleGame game, WordleStats stats)
    {
        Console.WriteLine("Wordle is running in terminal-only mode because a graphical renderer is unavailable.");
        Console.WriteLine("Type a 5-letter word and press Enter to play.");

        var input = string.Empty;
        while (true)
        {
            Console.Write("Your guess: ");
            var guess = Console.ReadLine()?.Trim().ToUpperInvariant() ?? string.Empty;

            if (guess.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (guess.Length != 5)
            {
                Console.WriteLine("Need exactly 5 letters.");
                continue;
            }

            var result = game.SubmitGuess(guess);
            if (result.Status == WordleResultStatus.Ok)
            {
                Console.WriteLine($"Result: {result.Status}");
                stats.GamesPlayed++;
                if (game.Won)
                {
                    stats.Wins++;
                    stats.BestGuesses = Math.Min(stats.BestGuesses, game.GuessCount);
                    Console.WriteLine("You won!");
                    SaveStats(stats);
                    break;
                }

                if (game.IsOver)
                {
                    Console.WriteLine($"You lost. Answer: {game.Answer}");
                    SaveStats(stats);
                    break;
                }
            }
            else if (result.Status == WordleResultStatus.NotInWordList)
            {
                Console.WriteLine("That word is not in the list.");
            }
        }
    }

    private static unsafe void RunGameLoop(Sdl sdl, Renderer* renderer, ref WordleGame game, WordleStats stats)
    {
        var quit = false;
        var ev = new Event();
        var input = string.Empty;
        var message = "Type a 5-letter word and press Enter";
        var gameOver = false;
        var lastStatus = string.Empty;

        while (!quit)
        {
            while (sdl.PollEvent(ref ev) != 0)
            {
                if (ev.Type == (uint)EventType.Quit)
                {
                    quit = true;
                    break;
                }

                if (ev.Type != (uint)EventType.Keydown)
                {
                    continue;
                }

                var scancode = ev.Key.Keysym.Scancode;
                if (scancode == Scancode.ScancodeEscape)
                {
                    quit = true;
                }
                else if (scancode == Scancode.ScancodeBackspace && input.Length > 0)
                {
                    input = input[..^1];
                }
                else if (scancode == Scancode.ScancodeReturn && input.Length == 5)
                {
                    var result = game.SubmitGuess(input);
                    if (result.Status == WordleResultStatus.Ok)
                    {
                        input = string.Empty;
                        if (game.Won)
                        {
                            message = "You won! Press R for a new game";
                            gameOver = true;
                        }
                        else if (game.IsOver)
                        {
                            message = $"You lost. Answer: {game.Answer}. Press R for a new game";
                            gameOver = true;
                        }
                        else
                        {
                            message = "Good guess. Try another one";
                        }

                        stats.GamesPlayed++;
                        if (game.Won)
                        {
                            stats.Wins++;
                            stats.BestGuesses = Math.Min(stats.BestGuesses, game.GuessCount);
                        }

                        SaveStats(stats);
                    }
                    else if (result.Status == WordleResultStatus.InvalidLength)
                    {
                        message = "Need exactly 5 letters";
                    }
                    else if (result.Status == WordleResultStatus.NotInWordList)
                    {
                        message = "That word is not in the list";
                    }
                }
                else if (gameOver && scancode == Scancode.ScancodeR)
                {
                    game = new WordleGame();
                    input = string.Empty;
                    message = "Type a 5-letter word and press Enter";
                    gameOver = false;
                }
                else if (scancode is >= Scancode.ScancodeA and <= Scancode.ScancodeZ)
                {
                    if (input.Length < 5)
                    {
                        input += ((char)('A' + ((int)scancode - (int)Scancode.ScancodeA))).ToString();
                    }
                }
            }

            var statusText = $"{message}\nWins: {stats.Wins}, Games: {stats.GamesPlayed}, Best: {stats.BestGuesses}";
            if (statusText != lastStatus)
            {
                Console.WriteLine(statusText);
                lastStatus = statusText;
            }

            Draw(sdl, renderer, game, stats, input, message);
            System.Threading.Thread.Sleep(16);
        }
    }

    private static unsafe void Draw(Sdl sdl, Renderer* renderer, WordleGame game, WordleStats stats, string input, string message)
    {
        sdl.SetRenderDrawColor(renderer, 240, 232, 255, 255);
        sdl.RenderClear(renderer);

        DrawBoard(sdl, renderer, game, input);
        DrawStatus(sdl, renderer, stats, message);
        sdl.RenderPresent(renderer);
    }

    private static unsafe void DrawBoard(Sdl sdl, Renderer* renderer, WordleGame game, string input)
    {
        for (var row = 0; row < 6; row++)
        {
            for (var col = 0; col < 5; col++)
            {
                var x = BoardLeft + col * (TileSize + TileGap);
                var y = BoardTop + row * (TileSize + TileGap);
                var letter = string.Empty;
                var color = TileColor.Gray;

                if (row < game.Guesses.Count)
                {
                    letter = game.Guesses[row][col].ToString();
                    var guessResult = game.EvaluateGuess(game.Guesses[row]);
                    color = guessResult.Colors[col];
                }
                else if (row == game.GuessCount && input.Length > col)
                {
                    letter = input[col].ToString();
                }

                DrawTile(sdl, renderer, x, y, letter, color);
            }
        }
    }

    private static unsafe void DrawTile(Sdl sdl, Renderer* renderer, int x, int y, string letter, TileColor color)
    {
        var r = 180;
        var g = 180;
        var b = 180;

        switch (color)
        {
            case TileColor.Green:
                r = 110; g = 170; b = 120; break;
            case TileColor.Yellow:
                r = 210; g = 190; b = 100; break;
        }

        sdl.SetRenderDrawColor(renderer, (byte)r, (byte)g, (byte)b, 255);
        var rect = new Rectangle<int>(x, y, TileSize, TileSize);
        sdl.RenderFillRect(renderer, ref rect);

        sdl.SetRenderDrawColor(renderer, 60, 60, 60, 255);
        sdl.RenderDrawRect(renderer, ref rect);

        if (!string.IsNullOrEmpty(letter))
        {
            sdl.SetRenderDrawColor(renderer, 255, 255, 255, 255);
            sdl.RenderDrawLine(renderer, x + 20, y + 18, x + 40, y + 42);
            sdl.RenderDrawLine(renderer, x + 40, y + 18, x + 20, y + 42);
        }
    }

    private static unsafe void DrawStatus(Sdl sdl, Renderer* renderer, WordleStats stats, string message)
    {
        sdl.SetRenderDrawColor(renderer, 240, 232, 255, 255);
        sdl.RenderDrawLine(renderer, 110, 560, 690, 560);
        sdl.SetRenderDrawColor(renderer, 80, 60, 120, 255);
        var statusRect = new Rectangle<int>(100, 580, 600, 70);
        sdl.RenderDrawRect(renderer, ref statusRect);
        sdl.SetRenderDrawColor(renderer, 30, 20, 80, 255);
        sdl.RenderDrawLine(renderer, 110, 620, 690, 620);

    }

    private static void SaveStats(WordleStats stats)
    {
        new ScoreStore(GetStatsPath()).Save(stats);
    }

    private static string GetStatsPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "stats.json");
    }
}
