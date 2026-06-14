using Wordle;
using Xunit;

namespace Wordle.Tests;

public class WordleGameTests
{
    [Fact]
    public void TestEvaluateGuess()
    {
        WordleGame game = new WordleGame("PLANT");
        WordleGuessResult result = game.EvaluateGuess("SLATE");

        Assert.Equal(WordleResultStatus.Ok, result.Status);
        Assert.Equal(TileColor.Gray, result.Colors[0]);
        Assert.Equal(TileColor.Green, result.Colors[1]);
        Assert.Equal(TileColor.Green, result.Colors[2]);
        Assert.Equal(TileColor.Yellow, result.Colors[3]);
        Assert.Equal(TileColor.Gray, result.Colors[4]);
    }

    [Fact]
    public void TestWin()
    {
        WordleGame game = new WordleGame("PLANT");
        WordleGuessResult result = game.SubmitGuess("PLANT");

        Assert.True(result.Won);
        Assert.Equal(1, game.GuessCount);
    }

    [Fact]
    public void TestKeyboardColors()
    {
        WordleGame game = new WordleGame("PLANT", new[] { "PLANT", "SLATE" });
        game.SubmitGuess("SLATE");

        var keyboard = game.GetKeyboardState();
        Assert.Equal(TileColor.Green, keyboard['L']);
        Assert.Equal(TileColor.Yellow, keyboard['T']);
    }
}
