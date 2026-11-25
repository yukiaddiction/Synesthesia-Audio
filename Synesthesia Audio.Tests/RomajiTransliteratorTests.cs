using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// BUG-m-20: the kana romaniser dropped the sokuon, the prolonged sound mark and small-kana combos.
/// Voice actor and circle names are romanised locally and never sent to a translation service, so these
/// readings are what the user actually sees in the library.
/// </summary>
public sealed class RomajiTransliteratorTests
{
    /// <summary>The audit's own acceptance case.</summary>
    [Fact]
    public void Sokuon_doubles_the_next_consonant()
    {
        Assert.Equal("Nitta", RomajiTransliterator.ToRomaji("ニッタ"));
    }

    [Theory]
    [InlineData("ニッタ", "Nitta")]
    [InlineData("サッカ", "Sakka")]
    [InlineData("キップ", "Kippu")]
    [InlineData("ロック", "Rokku")]
    [InlineData("マッチ", "Matchi")]
    public void Sokuon_in_katakana(string input, string expected)
    {
        Assert.Equal(expected, RomajiTransliterator.ToRomaji(input));
    }

    [Fact]
    public void Sokuon_in_hiragana()
    {
        Assert.Equal("Kitte", RomajiTransliterator.ToRomaji("きって"));
    }

    /// <summary>Before the fix the mark was simply dropped, so コーヒー became "Kohi".</summary>
    [Fact]
    public void Prolonged_sound_mark_repeats_the_previous_vowel()
    {
        // コ + ー = "koo", ヒ + ー = "hii".
        Assert.Equal("Koohii", RomajiTransliterator.ToRomaji("コーヒー"));
    }

    [Theory]
    [InlineData("コー", "Koo")]
    [InlineData("ハー", "Haa")]
    [InlineData("ルー", "Ruu")]
    [InlineData("レー", "Ree")]
    public void Prolonged_mark_after_each_vowel(string input, string expected)
    {
        Assert.Equal(expected, RomajiTransliterator.ToRomaji(input));
    }

    /// <summary>A mark with no preceding vowel has nothing to lengthen, so it must not emit anything.</summary>
    [Fact]
    public void A_leading_prolonged_mark_is_ignored()
    {
        Assert.Equal("A", RomajiTransliterator.ToRomaji("ーア"));
    }

    [Theory]
    [InlineData("ファ", "Fa")]
    [InlineData("ティ", "Ti")]
    [InlineData("ディ", "Di")]
    [InlineData("シェ", "She")]
    [InlineData("チェ", "Che")]
    [InlineData("ジェ", "Je")]
    [InlineData("ウィ", "Wi")]
    [InlineData("ヴァ", "Va")]
    [InlineData("ツォ", "Tso")]
    [InlineData("クォ", "Kwo")]
    public void Small_vowel_combos_are_one_syllable(string input, string expected)
    {
        Assert.Equal(expected, RomajiTransliterator.ToRomaji(input));
    }

    [Fact]
    public void Small_kana_alone_still_has_a_reading()
    {
        Assert.Equal("Ya", RomajiTransliterator.ToRomaji("ャ"));
        Assert.Equal("E", RomajiTransliterator.ToRomaji("ェ"));
    }

    [Fact]
    public void A_realistic_voice_actor_name_is_readable()
    {
        // ミナト・リョウ. The interpunct is kept (it separates the halves of a joint credit) and, as before,
        // only the first letter of the whole result is capitalised.
        Assert.Equal("Minato・ryou", RomajiTransliterator.ToRomaji("ミナト・リョウ"));
    }

    [Fact]
    public void Latin_text_is_returned_unchanged()
    {
        Assert.Equal("Already Roman", RomajiTransliterator.ToRomaji("Already Roman"));
    }

    [Fact]
    public void Empty_input_returns_empty()
    {
        Assert.Equal(string.Empty, RomajiTransliterator.ToRomaji(null));
        Assert.Equal(string.Empty, RomajiTransliterator.ToRomaji("   "));
    }

    /// <summary>Kanji have no reading here, so they are dropped rather than guessed — unchanged behaviour.</summary>
    [Fact]
    public void Kanji_are_dropped_and_the_rest_is_romanised()
    {
        // Text that is entirely kanji reduces to nothing; kana around it is still romanised.
        Assert.Equal(string.Empty, RomajiTransliterator.ToRomaji("凛"));
        Assert.Equal("Rin", RomajiTransliterator.ToRomaji("凛リン"));
    }

    [Fact]
    public void The_first_letter_is_capitalised()
    {
        Assert.Equal("Kana", RomajiTransliterator.ToRomaji("かな"));
    }
}
