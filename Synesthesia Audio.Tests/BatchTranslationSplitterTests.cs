using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// Batch translation joins inputs with newlines and splits the reply back; a reply that cannot be aligned
/// one-to-one must be rejected, or one work's tag lands on another.
/// </summary>
public sealed class BatchTranslationSplitterTests
{
    [Fact]
    public void An_aligned_reply_splits_one_to_one()
    {
        var lines = BatchTranslationSplitter.TrySplit("School\nFantasy\nRomance", 3);

        Assert.NotNull(lines);
        Assert.Equal(new[] { "School", "Fantasy", "Romance" }, lines);
    }

    [Fact]
    public void Fewer_lines_than_inputs_is_rejected()
    {
        Assert.Null(BatchTranslationSplitter.TrySplit("School\nFantasy", 3));
    }

    [Fact]
    public void More_lines_than_inputs_is_rejected()
    {
        Assert.Null(BatchTranslationSplitter.TrySplit("School\nFantasy\nRomance\nDrama", 3));
    }

    /// <summary>
    /// A blank line means the service dropped or split an item; skipping it would make the count match and shift
    /// every later translation onto the wrong input.
    /// </summary>
    [Fact]
    public void A_blank_line_in_the_middle_is_rejected()
    {
        Assert.Null(BatchTranslationSplitter.TrySplit("School\n\nRomance", 3));
    }

    [Fact]
    public void Windows_line_endings_are_handled()
    {
        var lines = BatchTranslationSplitter.TrySplit("School\r\nFantasy\r\nRomance", 3);

        Assert.NotNull(lines);
        Assert.Equal(new[] { "School", "Fantasy", "Romance" }, lines);
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed_per_item()
    {
        var lines = BatchTranslationSplitter.TrySplit("  School \n\tFantasy", 2);

        Assert.NotNull(lines);
        Assert.Equal(new[] { "School", "Fantasy" }, lines);
    }

    [Fact]
    public void Empty_or_missing_reply_is_rejected()
    {
        Assert.Null(BatchTranslationSplitter.TrySplit(null, 2));
        Assert.Null(BatchTranslationSplitter.TrySplit("", 2));
    }

    [Fact]
    public void Zero_expected_items_returns_an_empty_result()
    {
        Assert.Empty(BatchTranslationSplitter.TrySplit("anything", 0)!);
    }

    /// <summary>A newline inside an input would otherwise fake an extra batch item.</summary>
    [Theory]
    [InlineData("Line one\nLine two", "Line one Line two")]
    [InlineData("Line one\r\nLine two", "Line one Line two")]
    [InlineData("Line one\rLine two", "Line one Line two")]
    [InlineData("  padded  ", "padded")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void Inputs_are_flattened_to_one_line(string? input, string expected)
    {
        Assert.Equal(expected, BatchTranslationSplitter.SanitizeInput(input));
    }

    [Fact]
    public void Flattened_inputs_join_to_the_expected_line_count()
    {
        var inputs = new[] { "first\nnested", "second" };

        var joined = string.Join("\n", inputs.Select(BatchTranslationSplitter.SanitizeInput));

        Assert.Equal(2, joined.Split('\n').Length);
    }
}
