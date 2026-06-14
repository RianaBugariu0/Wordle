using System.Text.Json;

namespace Wordle;

public enum TileColor
{
    Gray,
    Yellow,
    Green,
}

public enum WordleResultStatus
{
    Ok,
    InvalidLength,
    NotInWordList,
    GameOver,
}

public sealed record WordleGuessResult(IReadOnlyList<TileColor> Colors, WordleResultStatus Status, bool Won);

public sealed class WordleInputException : Exception
{
    public WordleInputException(string message) : base(message)
    {
    }
}

public sealed class WordleStats
{
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int BestGuesses { get; set; } = 999;
}

public sealed class ScoreStore
{
    private readonly string _path;

    public ScoreStore(string path)
    {
        _path = path;
    }

    public WordleStats Load()
    {
        if (!File.Exists(_path))
        {
            return new WordleStats();
        }

        var text = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<WordleStats>(text) ?? new WordleStats();
    }

    public void Save(WordleStats stats)
    {
        var text = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, text);
    }
}

public sealed class WordleGame
{
    private readonly string _answer;
    private readonly HashSet<string> _wordList;
    private readonly List<string> _guesses = new();

    public WordleGame(string? answer = null, IEnumerable<string>? words = null)
    {
        _wordList = (words ?? LoadWords()).Select(x => x.ToUpperInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _answer = (answer ?? PickAnswer()).ToUpperInvariant();

        if (_answer.Length != 5)
        {
            throw new WordleInputException("The answer must be 5 letters long.");
        }
    }

    public string Answer => _answer;
    public string CurrentGuess { get; private set; } = string.Empty;
    public int GuessCount => _guesses.Count;
    public bool Won { get; private set; }
    public bool IsOver => Won || GuessCount >= 6;
    public IReadOnlyList<string> Guesses => _guesses;

    public void AddLetter(char letter)
    {
        if (IsOver || CurrentGuess.Length >= 5)
        {
            return;
        }

        if (char.IsLetter(letter))
        {
            CurrentGuess += char.ToUpperInvariant(letter);
        }
    }

    public void RemoveLetter()
    {
        if (CurrentGuess.Length > 0)
        {
            CurrentGuess = CurrentGuess[..^1];
        }
    }

    public WordleGuessResult SubmitGuess(string? guess)
    {
        if (IsOver)
        {
            return new WordleGuessResult(Array.Empty<TileColor>(), WordleResultStatus.GameOver, Won);
        }

        var text = (guess ?? CurrentGuess).Trim().ToUpperInvariant();
        if (text.Length != 5)
        {
            return new WordleGuessResult(Array.Empty<TileColor>(), WordleResultStatus.InvalidLength, false);
        }

        if (!_wordList.Contains(text))
        {
            return new WordleGuessResult(Array.Empty<TileColor>(), WordleResultStatus.NotInWordList, false);
        }

        var result = EvaluateGuess(text);
        _guesses.Add(text);
        CurrentGuess = string.Empty;

        if (text == _answer)
        {
            Won = true;
        }

        return new WordleGuessResult(result.Colors, WordleResultStatus.Ok, Won);
    }

    public WordleGuessResult EvaluateGuess(string guess)
    {
        var result = new List<TileColor>(5);
        var answerChars = _answer.ToCharArray();
        var guessChars = guess.ToUpperInvariant().ToCharArray();
        var answerCounts = new Dictionary<char, int>();
        var used = new bool[5];

        foreach (var letter in answerChars)
        {
            if (!answerCounts.ContainsKey(letter))
            {
                answerCounts[letter] = 0;
            }

            answerCounts[letter]++;
        }

        for (var i = 0; i < 5; i++)
        {
            if (guessChars[i] == answerChars[i])
            {
                result.Add(TileColor.Green);
                used[i] = true;
                answerCounts[guessChars[i]]--;
            }
            else
            {
                result.Add(TileColor.Gray);
            }
        }

        for (var i = 0; i < 5; i++)
        {
            if (result[i] == TileColor.Green)
            {
                continue;
            }

            if (answerCounts.ContainsKey(guessChars[i]) && answerCounts[guessChars[i]] > 0)
            {
                result[i] = TileColor.Yellow;
                answerCounts[guessChars[i]]--;
            }
        }

        return new WordleGuessResult(result, WordleResultStatus.Ok, false);
    }

    private static string PickAnswer()
    {
        var words = LoadWords();
        var random = new Random();
        return words[random.Next(words.Count)].ToUpperInvariant();
    }

    private static List<string> LoadWords()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "Words.txt");
        if (!File.Exists(filePath))
        {
            return new List<string> { "PLANT", "CRANE", "SMILE", "STONE", "BRAVE", "GHOST", "MUSIC", "CLOUD", "GRAPE", "SHINE" };
        }

        return File.ReadAllLines(filePath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Where(x => x.Length == 5)
            .ToList();
    }
}
