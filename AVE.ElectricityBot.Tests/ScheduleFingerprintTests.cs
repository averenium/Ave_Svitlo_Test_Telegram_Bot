using AVE.ElectricityBot.Services.Implementations;

namespace AVE.ElectricityBot.Tests;

/// <summary>
/// Пошук копій графіка між каналами: той самий розклад з іншим підписом або в іншому форматі пересилається один раз
/// </summary>
public class ScheduleFingerprintTests
{
    private static readonly DateTime Published = new(2026, 11, 7, 21, 0, 0, DateTimeKind.Utc);

    private const string CekBody =
        "\n🔹 Черга 1.1\n10:30-17:30\n21:00-00:00\n\n🔹 Черга 1.2\n10:30-17:30\n21:00-00:00\n\n🔹 Черга 5.1\n00:00-03:30\n09:00-14:00";

    private const string DtekFourGroups =
        "\nГрупа 1.1\n• 00:00 — 01:00\n• 07:30 — 14:30\n• 18:00 — 22:00\n\nГрупа 1.2\n• 00:00 — 01:00\n• 07:30 — 14:30\n• 18:00 — 22:00\n\n" +
        "Група 2.1\n• 00:00 — 01:00\n• 07:30 — 11:30\n• 18:00 — 22:00\n\nГрупа 2.2\n• 00:00 — 01:00\n• 07:30 — 11:30\n• 18:00 — 22:00";

    private const string DtekFourGroupsUpdated =
        "\nГрупа 1.1\nОновлений графік:\n• 00:00 — 01:00\n• 07:30 — 14:30\n• 18:00 — 22:00\n\nГрупа 1.2\nОновлений графік:\n• 00:00 — 01:00\n• 07:30 — 14:30\n• 18:00 — 22:00\n\n" +
        "Група 2.1\nОновлений графік:\n• 00:00 — 01:00\n• 07:30 — 11:30\n• 18:00 — 22:00\n\nГрупа 2.2\nОновлений графік:\n• 00:00 — 01:00\n• 07:30 — 11:30\n• 18:00 — 22:00";

    private const string PairedGroups =
        "\n\n▪️1.1 та 1.2 — 00:00–04:00, 10:30–14:30, 21:00–00:00;\n\n▪️2.1 та 2.2 — 00:00–04:00, 10:30–17:30, 21:00–00:00;\n\n▪️5.1 та 5.2 — 07:00–11:00, 17:30–22:00;";

    private const string PerGroup =
        "\n\nГрупа 1.1\nГрафік на завтра:\n• 00:00 — 04:00\n• 10:30 — 14:30\n• 21:00 — 24:00\n\nГрупа 1.2\nГрафік на завтра:\n• 00:00 — 04:00\n• 10:30 — 14:30\n• 21:00 — 24:00\n\n" +
        "Група 2.1\nГрафік на завтра:\n• 00:00 — 04:00\n• 10:30 — 17:30\n• 21:00 — 24:00\n\nГрупа 2.2\nГрафік на завтра:\n• 00:00 — 04:00\n• 10:30 — 17:30\n• 21:00 — 24:00\n\n" +
        "Група 5.1\nГрафік на завтра:\n• 07:00 — 11:00\n• 17:30 — 22:00\n\nГрупа 5.2\nГрафік на завтра:\n• 07:00 — 11:00\n• 17:30 — 22:00";

    public static TheoryData<string, string, string, int, bool> Pairs => new()
    {
        {
            "ЦЕК: «Шановні споживачі!» і «Графік для ЦЕК» з тим самим розкладом",
            "📢 Шановні споживачі! Повідомляємо, що за розпорядженням диспетчерського центру НЕК «Укренерго», 08 ЛИСТОПАДА з 00:00 до 24:00 застосовуватимуться відключення наступних черг:" + CekBody,
            "Графік для ЦЕК \n 08 ЛИСТОПАДА з 00:00 до 24:00 застосовуватимуться відключення наступних черг:" + CekBody,
            2, true
        },
        { "Той самий розклад на іншу дату", "Графік для ЦЕК 08 ЛИСТОПАДА" + CekBody, "Графік для ЦЕК 09 ЛИСТОПАДА" + CekBody, 600, false },
        { "«на сьогодні» і «на завтра» без дат через 10 годин", "Графік для ЦЕК на сьогодні" + CekBody, "Графік для ЦЕК на завтра" + CekBody, 600, false },
        {
            "Різний час у заголовку («станом на 7:00 / 7:20»)",
            "ДТЕК станом на 7:00 ВІДБУЛИСЯ ЗМІНИ\n1.1 група\nз 09:00 - 16:00\n1.2 група\nз 09:00 - 16:00",
            "ДТЕК станом на 7:20 ВІДБУЛИСЯ ЗМІНИ\n1.1 група\nз 09:00 - 16:00\n1.2 група\nз 09:00 - 16:00",
            20, true
        },
        {
            "Змінився один інтервал",
            "ДТЕК\n1.1 група\nз 09:00 - 16:00\n1.2 група\nз 09:00 - 16:00",
            "ДТЕК\n1.1 група\nз 09:00 - 16:00\n1.2 група\nз 09:00 - 17:00",
            20, false
        },
        {
            "Однаковий розклад у ДТЕК і ЦЕК",
            "Графік ДТЕК 08 ЛИСТОПАДА\n1.1 група\nз 09:00 - 16:00\n1.2 група\nз 09:00 - 16:00",
            "Графік ЦЕК 08 ЛИСТОПАДА\n1.1 черга\nз 09:00 - 16:00\n1.2 черга\nз 09:00 - 16:00",
            5, false
        },
        {
            "«Коригування…» без дати і «графік на 09.11.2026» з «Оновлений графік:»",
            "Коригування графіку для наступних груп ДТЕК " + DtekFourGroups,
            "⚠️ ДТЕК: графік відключень на 09.11.2026\n" + DtekFourGroupsUpdated,
            15, true
        },
        {
            "Пари груп «1.1 та 1.2 — …», «21:00–00:00», «8 листопада» проти «Група 1.1 • …», «21:00 — 24:00», «08.11.2026»",
            "Дніпропетровщина: графіки відключень на 8 листопада для споживачів ДТЕК" + PairedGroups,
            "Графік ДТЕК , 08.11.2026" + PerGroup,
            3, true
        },
        {
            "Той самий розклад без дати через 10 годин — інший день",
            "Коригування графіку для наступних груп ДТЕК " + DtekFourGroups,
            "Графік ДТЕК на завтра " + DtekFourGroups,
            600, false
        },
        {
            "Дата в тілі («до 12:04 07.02.2026») не є часом",
            "Група ДТЕК 1.1 (до 12:04 07.02.2026) 10:00-12:00 14:00-16:00\nГрупа 1.2 10:00-12:00 14:00-16:00",
            "Група ДТЕК 1.1 (до 12:04 07.02.2026) 10:00-12:00 14:00-16:00\nГрупа 1.2 10:00-12:00 14:00-16:00",
            30, true
        },
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void SameScheduleIsDetectedAcrossCaptionsAndFormats(string description, string previous, string current, int minutesApart, bool expectedDuplicate)
    {
        _ = description;
        var previousFingerprint = ChannelMessageClassifier.GetScheduleFingerprint(previous);
        var currentFingerprint = ChannelMessageClassifier.GetScheduleFingerprint(current);

        Assert.NotNull(previousFingerprint);
        Assert.NotNull(currentFingerprint);
        Assert.Equal(expectedDuplicate, ChannelMessageClassifier.IsSameSchedule(
            currentFingerprint, Published.AddMinutes(minutesApart), previousFingerprint, Published));
    }

    [Fact]
    public void DateFormatsAreNormalized()
    {
        Assert.Equal("8.11", ChannelMessageClassifier.GetScheduleFingerprint("Графік ДТЕК на 8 листопада" + PairedGroups)?.Date);
        Assert.Equal("8.11", ChannelMessageClassifier.GetScheduleFingerprint("Графік ДТЕК , 08.11.2026" + PerGroup)?.Date);
    }

    [Theory]
    [InlineData("З 12:00 відключення груп ДТЕК 1.1 до 19:00")]
    [InlineData("Група ДТЕК 5.1 де ще немає світла")]
    [InlineData("")]
    public void TextWithoutScheduleHasNoFingerprint(string text) =>
        Assert.Null(ChannelMessageClassifier.GetScheduleFingerprint(text));
}
