using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// PERF-11: the log viewer now reads only the end of the file, so the rules for which lines those are need to be
/// exact - a seek starts mid-line, and a trailing newline is not a line.
/// </summary>
public sealed class LogTailTests
{
    [Fact]
    public void The_last_lines_are_returned()
    {
        var result = LogTail.LastLines("one\ntwo\nthree\nfour", 2);

        Assert.Equal(["three", "four"], result);
    }

    [Fact]
    public void Fewer_lines_than_asked_for_returns_all_of_them()
    {
        var result = LogTail.LastLines("one\ntwo", 10);

        Assert.Equal(["one", "two"], result);
    }

    [Fact]
    public void A_trailing_newline_does_not_produce_an_empty_line()
    {
        var result = LogTail.LastLines("one\ntwo\n", 10);

        Assert.Equal(["one", "two"], result);
    }

    [Fact]
    public void Windows_line_endings_are_handled()
    {
        var result = LogTail.LastLines("one\r\ntwo\r\nthree", 2);

        Assert.Equal(["two", "three"], result);
    }

    /// <summary>A seek starts mid-file, so the first line is very likely cut in half.</summary>
    [Fact]
    public void A_partial_first_line_is_dropped_when_reading_from_a_seek()
    {
        var result = LogTail.LastLines("alf a line\ntwo\nthree", 10, dropPartialFirstLine: true);

        Assert.Equal(["two", "three"], result);
    }

    [Fact]
    public void A_partial_first_line_is_kept_when_the_file_was_read_from_the_start()
    {
        var result = LogTail.LastLines("whole line\ntwo", 10);

        Assert.Equal(["whole line", "two"], result);
    }

    [Fact]
    public void Dropping_the_partial_line_never_empties_a_single_line_read()
    {
        var result = LogTail.LastLines("only line", 10, dropPartialFirstLine: true);

        Assert.Equal(["only line"], result);
    }

    [Fact]
    public void A_zero_or_negative_line_count_is_empty()
    {
        Assert.Empty(LogTail.LastLines("one\ntwo", 0));
        Assert.Empty(LogTail.LastLines("one\ntwo", -5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void No_text_is_empty(string? text)
    {
        Assert.Empty(LogTail.LastLines(text, 10));
    }
}
