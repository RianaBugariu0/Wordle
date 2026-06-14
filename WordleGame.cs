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

public class WordleStats
{
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int BestGuesses { get; set; } = 999;
}

public class ScoreStore
{
    private string filePath;

    public ScoreStore(string path)
    {
        filePath = path;
    }

    public WordleStats Load()
    {
        if (File.Exists(filePath) == false)
        {
            return new WordleStats();
        }

        string text = File.ReadAllText(filePath);
        WordleStats? loaded = JsonSerializer.Deserialize<WordleStats>(text);
        if (loaded == null)
        {
            return new WordleStats();
        }

        return loaded;
    }

    public void Save(WordleStats stats)
    {
        string json = JsonSerializer.Serialize(stats);
        File.WriteAllText(filePath, json);
    }
}

public class WordleGame
{
    private string answer;
    private HashSet<string> wordList;
    private List<string> guesses = new List<string>();

    public WordleGame(string? answer = null, IEnumerable<string>? words = null)
    {
        if (words == null)
        {
            wordList = LoadWords().ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            wordList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var w in words)
            {
                wordList.Add(w.ToUpperInvariant());
            }
        }

        if (answer == null)
        {
            this.answer = PickAnswer();
        }
        else
        {
            this.answer = answer.ToUpperInvariant();
        }

        if (this.answer.Length != 5)
        {
            throw new WordleInputException("The answer must be 5 letters long.");
        }
    }

    public string Answer => answer;
    public string CurrentGuess { get; private set; } = "";
    public int GuessCount => guesses.Count;
    public bool Won { get; private set; }
    public bool IsOver => Won || GuessCount >= 6;
    public IReadOnlyList<string> Guesses => guesses;

    public IReadOnlyDictionary<char, TileColor> GetKeyboardState()
    {
        var states = new Dictionary<char, TileColor>();

        foreach (var guess in guesses)
        {
            var result = EvaluateGuess(guess);
            for (var i = 0; i < guess.Length; i++)
            {
                var letter = guess[i];
                var existing = states.ContainsKey(letter) ? states[letter] : TileColor.Gray;
                states[letter] = MergeKeyboardColor(existing, result.Colors[i]);
            }
        }

        return states;
    }

    private static TileColor MergeKeyboardColor(TileColor existing, TileColor incoming)
    {
        if (existing == TileColor.Green || incoming == TileColor.Green)
        {
            return TileColor.Green;
        }

        if (existing == TileColor.Yellow || incoming == TileColor.Yellow)
        {
            return TileColor.Yellow;
        }

        return TileColor.Gray;
    }

    public void AddLetter(char letter)
    {
        if (IsOver)
        {
            return;
        }

        if (CurrentGuess.Length >= 5)
        {
            return;
        }

        if (char.IsLetter(letter))
        {
            CurrentGuess = CurrentGuess + char.ToUpper(letter);
        }
    }

    public void RemoveLetter()
    {
        if (CurrentGuess.Length > 0)
        {
            CurrentGuess = CurrentGuess.Substring(0, CurrentGuess.Length - 1);
        }
    }

    public WordleGuessResult SubmitGuess(string? guess)
    {
        if (IsOver)
        {
            return new WordleGuessResult(new List<TileColor>(), WordleResultStatus.GameOver, Won);
        }

        string text;
        if (guess == null)
        {
            text = CurrentGuess.Trim().ToUpperInvariant();
        }
        else
        {
            text = guess.Trim().ToUpperInvariant();
        }

        if (text.Length != 5)
        {
            return new WordleGuessResult(new List<TileColor>(), WordleResultStatus.InvalidLength, false);
        }

        if (wordList.Contains(text) == false)
        {
            return new WordleGuessResult(new List<TileColor>(), WordleResultStatus.NotInWordList, false);
        }

        WordleGuessResult result = EvaluateGuess(text);
        guesses.Add(text);
        CurrentGuess = "";

        if (text == answer)
        {
            Won = true;
        }

        return new WordleGuessResult(result.Colors, WordleResultStatus.Ok, Won);
    }

    public WordleGuessResult EvaluateGuess(string guess)
    {
        var result = new List<TileColor>(5);
        var answerChars = answer.ToCharArray();
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

    private string PickAnswer()
    {
        List<string> words = LoadWords();
        Random random = new Random();
        int index = random.Next(words.Count);
        return words[index].ToUpperInvariant();
    }

    private static List<string> LoadWords()
    {
        string filePath = Path.Combine(AppContext.BaseDirectory, "Words.txt");
        List<string> words = new List<string>();

        if (!File.Exists(filePath))
        {
            words.Add("PLANT");
            words.Add("CRANE");
            words.Add("SMILE");
            words.Add("STONE");
            words.Add("BRAVE");
            words.Add("GHOST");
            words.Add("MUSIC");
            words.Add("CLOUD");
            words.Add("GRAPE");
            words.Add("SHINE");
            return words;
        }

        string[] lines = File.ReadAllLines(filePath);
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string word = line.Trim().ToUpperInvariant();
            if (word.Length == 5)
            {
                words.Add(word);
            }
        }

        return words;
    }
}
