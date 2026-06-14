using Wordle;
using Xunit;

namespace Wordle.Tests;

public class WordleGameTests
{
    [Fact]
    public void EvaluateGuess_ReturnsCorrectColorsForSimpleGuess()
    {
        var game = new WordleGame("PLANT");
        var result = game.EvaluateGuess("SLATE");

        Assert.Equal(WordleResultStatus.Ok, result.Status);
        Assert.Equal(new[] { TileColor.Gray, TileColor.Green, TileColor.Green, TileColor.Yellow, TileColor.Gray }, result.Colors);
    }

    [Fact]
    public void SubmitGuess_WinsWhenGuessMatchesAnswer()
    {
        var game = new WordleGame("PLANT");
        var result = game.SubmitGuess("PLANT");

        Assert.True(result.Won);
        Assert.Equal(1, game.GuessCount);
    }
}
