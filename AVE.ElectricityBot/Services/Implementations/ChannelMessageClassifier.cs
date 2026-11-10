using System.Text.RegularExpressions;

namespace AVE.ElectricityBot.Services.Implementations;

/// <summary>
/// Згадка групи в тексті повідомлення разом з провайдером секції, у якій вона знаходиться
/// </summary>
public sealed record ChannelGroupMention(string Group, int? DsoId);

/// <summary>
/// Результат розбору повідомлення з каналу
/// </summary>
public sealed record ChannelMessageInfo(
    IReadOnlyCollection<int> DsoIds,
    IReadOnlyList<ChannelGroupMention> Groups,
    bool IsOutageRelated,
    bool IsNationwideNotice,
    bool IsCityAlert = false,
    bool IsProviderInferred = false)
{
    /// <summary>
    /// Чи є в повідомленні щось, що може бути комусь цікаве
    /// </summary>
    public bool IsRelevant => Groups.Count > 0 || (IsOutageRelated && (DsoIds.Count > 0 || IsNationwideNotice || IsCityAlert));
}

/// <summary>
/// Розбір повідомлень з каналів (@dnipro_svet, @ddnepr_vse): провайдер, групи, тематика.
/// Не має залежностей, щоб його можна було перевіряти окремо.
/// </summary>
public static class ChannelMessageClassifier
{
    public const int DtekDsoId = 301;
    public const int CekDsoId = 303;

    private static readonly string[] AllGroups =
        ["1.1", "1.2", "2.1", "2.2", "3.1", "3.2", "4.1", "4.2", "5.1", "5.2", "6.1", "6.2"];

    // Група X.Y: 1-6 і підгрупа 1-2. Відсікаємо час і дати: 14.30, 5.10, 15.11, 1.12, 05.1
    private const string GroupPattern = @"(?<![\d.:])([1-6]\.[12])(?!\d|[.:]\d)";
    private static readonly Regex GroupRegex = new(GroupPattern, RegexOptions.Compiled);

    private static readonly Regex KeycapDigitRegex = new("([0-9])️?⃣", RegexOptions.Compiled);

    // Реклама сервісів сповіщень/ботів («→ [Підключити]», «підключити систему єКомунал», «Оберіть свою чергу…»)
    // і переманювання в інші канали («графіки будемо публікувати тут👇 https://t.me/+…»).
    // «Підписуйтесь та поділіться» — футер справжніх графіків, тому не є ознакою реклами
    private static readonly Regex AdvertRegex = new(
        @"підключити\s+систем|систем\w*\s+сповіщен|\[\s*підключити\s*\]|єкомунал|масово підключаються|встигніть і ви|оберіть свою черг|" +
        @"t\.me/(?:\+|joinchat)|публікув[^\n]{0,40}?тут\s*👇|" +
        // Збори донатів: банка, номер картки, «гривню на підтримку»
        @"monobank\.ua/jar|send\.monobank|privat24\.ua/send|номер (?:банки|картки)|(?<!\d)\d{4}\s?\d{4}\s?\d{4}\s?\d{4}(?!\d)|" +
        @"гривню на підтримку|підтримати наш|донат",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Діапазон груп: 1.1-6.2, 1.1 – 3.2, з 1.1 по 6.2
    private static readonly Regex GroupRangeRegex = new(
        GroupPattern + @"\s*(?:-|–|—|по)\s*" + GroupPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DtekRegex = new(@"дтек", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // ЦЕК, ЦЕКу, ЦЕКом, ЦЕКівський
    private static readonly Regex CekRegex = new(@"\bцек(?:у|ом|і|івськ\w*)?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // «Черга» у відмінках (але не «черговий/чергове»)
    private const string QueueWordPattern = @"\bчерг(?:а|и|у|ою|ам|ами|ах)?\b";

    // ЦЕК використовує «черги», а його офіційний шаблон — «Шановні споживачі! … НЕК "Укренерго"»
    // Лише «черга» з номером («🔹 Черга 1.1», «📌 1.1 черга», «5.1, 5.2 черги», «1 черга»):
    // «графіки погодинних відключень (від 2 до 4 черг)» — загальнонаціональне, не ЦЕК
    private static readonly Regex CekWordingRegex = new(
        @"[1-6]\.[12]\W{0,3}черг|черг\w*\W{0,3}[1-6]\.[12]|(?<![\d.])\d\s+черга\b|шановні споживачі",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Укренерго або загальнонаціональний масштаб («у частині областей», «в усіх регіонах», «по всій Україні»)
    private static readonly Regex NationwideRegex = new(
        @"укренерго|\bнек\b|(?:частин\w*|більшост\w*|усіх|всіх|кількох)\s+(?:областей|областях|регіон\w*)|по всій україні|" +
        // Обсяг у чергах («обсягом від 0,5 до 3 черг») буває лише в загальнонаціональних оголошеннях
        @"обсяг\w*\s+(?:від\s+)?\d",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Для загальнонаціональних повідомлень — лише явно про графіки/відключення, не будь-яка згадка «світла»
    private static readonly Regex NationwideOutageRegex = new(
        @"гпв|графік|відключ|вимика|вимкн|стабілізац|обмежен|знеструм", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Рішення Укренерго: застосовуються / діятимуть / скасовано / не буде / посилюють
    private static readonly Regex NationwideDecisionRegex = new(
        @"застосов|застосуван|діятим|діють|будуть\s+(?:діяти|відключ)|не прогноз|скасова|запроваджен|введен|вводят|задіян|не буде|відключатим|вимикатим|посилю|збільшено обсяг|нові команди",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Огляди ситуації та коментарі: «Найскладніша ситуація… — Укренерго», «є знеструмлені споживачі у … областях»
    private static readonly Regex NationwideCommentaryRegex = new(
        @"найскладніш|ситуація зі світлом|фейк|критичним для|\bчому\b|зафіксовано знеструм|зафіксували|є знеструмлені|голова\s+«?укренерго|ексочільник|прем'єр|прем’єр|міністр",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AllUkraineRegex = new(
        @"україн|усіх|всіх|більшост|частині регіон|по всій країні|областях", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool IsNationwideDecision(string text) =>
        NationwideRegex.IsMatch(text) &&
        NationwideOutageRegex.IsMatch(text) &&
        NationwideDecisionRegex.IsMatch(text) &&
        !NationwideCommentaryRegex.IsMatch(text) &&
        // Тільки про інший регіон (Київ, Львів) — не наше
        (!OtherRegionRegex.IsMatch(text) || DniproRegex.IsMatch(text) || AllUkraineRegex.IsMatch(text));

    // Конкретно про відключення. «Світло», «електроенергія», «електропостачання» самі по собі не рахуються:
    // вони є в кожній новині («…щоб у кожній домівці були світло й тепло»)
    // Екстрені/аварійні відключення. «Екстрені служби / поставки / допомога…» — інше значення (ДТП, імпорт електроенергії)
    private const string EmergencyPattern =
        // Лише форми множини («екстрені / екстренні / екстрених / екстреними»): «екстренки», «екстрену систему» — не про світло
        @"екстренн?(?:і|их|ими|им)\b(?!\s+(?:служб|поставк|допомог|медич|виклик|евакуац|рятувальн|ситуац|нарад|засідан))|" +
        @"аварійн\w*\s+відключ|графік\w*\s+аварійн|\bгав\b|аварійн\w*\s+схем";

    private const string OutageActionPattern =
        @"відключ|вимкн|вимик|ввімкн|увімкн|вмика|графік|гпв|" + QueueWordPattern + @"|\bгруп|знеструм|стабілізац|" + EmergencyPattern + @"|скасова|" +
        @"оновлен|коригув|\bзмін|обмежен|не прогноз|припинил\w*\s+подач|технологічн\w*\s+порушен|ремонтно-відновлювальн";

    private static readonly Regex OutageKeywordsRegex = new(OutageActionPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Новини про атаки на енергетику — пропускаємо, лише якщо в них є конкретика для споживача
    private static readonly Regex AttackNewsRegex = new(
        @"атакува|атак[аиуою]\b|\bудар|обстріл|влучан|влучив|дрон|шахед|ракет|бпла", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AttackConsumerActionRegex = new(
        @"графік|гпв|екстрен|аварійн\w*\s+відключ|стабілізац|скасова|відключення мереж|припинил\w*\s+подач|технологічн\w*\s+порушен",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool IsAttackNewsOnly(string text) =>
        AttackNewsRegex.IsMatch(text) && !AttackConsumerActionRegex.IsMatch(text);

    // Локальні звернення/знеструмлення за адресами — не загальна інформація
    private static readonly Regex AddressRegex = new(
        @"(?<!\w)(?:вул\.?|вулиц\w*|просп\.?|проспект\w*|пров\.?|провул\w*|пр-т|ж/м|буд\.|будин\w*|корп\.?|під'їзд\w*|під’їзд\w*)(?!\w)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ScheduleWordsRegex = new(
        @"графік|гпв|стабілізац|" + QueueWordPattern + @"|\bгруп", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Скарги/звернення підписників («ДТЕК зверніть увагу», «реагуйте на заявки», «Локальні аварійні знеструмлення»)
    private static readonly Regex ComplaintRegex = new(
        @"заявк|звернен|реагуй|зверніть увагу|#нампишуть|локальн\w* аварійн|профілактичн|зробіть людям|ігнор|де ви поділ|підтримати наш|донат",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Інший населений пункт: «М.Шахтарське», «🏙м. Павлоград», «с. Зоря» (але не «м. Дніпро»)
    private static readonly Regex OtherLocalityRegex = new(
        @"(?<!\w)(?:м|с|смт|с-ще)\.\s*(?!дніпр)[а-яіїєґ]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Що вважається самою інформацією в короткому повідомленні: статус графіка або конкретний час
    // Суттєва зміна: скасування, екстрені/аварійні, прогноз, конкретний час
    private const string ChangePattern =
        @"скасова|не діют|не прогноз|не буде|не відключ|не заплан|відключатим|вимикатим|введен|запроваджен|" + EmergencyPattern + @"|стабілізац|" +
        @"відключення мереж|припинил\w*\s+подач|технологічн\w*\s+порушен|орієнтовн|" +
        @"\b(?:з|до|о|від)\s+\d{1,2}[:.]\d{2}|\d{1,2}[:.]\d{2}\s*[-–—]\s*\d{1,2}[:.]\d{2}";

    private static readonly Regex ChangeRegex = new(ChangePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Що вважається інформацією в короткому повідомленні: статус графіка або суттєва зміна
    private static readonly Regex InformativeRegex = new(
        @"діють|діятим|застосов|застосуван|" + ChangePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NoChangeRegex = new(@"без змін", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const int ShortMessageLength = 80;
    private const int NoChangeNoticeMaxLength = 200;

    /// <summary>
    /// Повідомлення без змісту для користувача:
    /// короткий підпис до картинки («ДТЕК на 5 грудня», «ДТЕК ЗМІНИ», «Оновлення на 18:15 ДТЕК») або
    /// «без змін» без жодної іншої інформації («Станом на 7:00 графіки без змін | Графік ЦЕК дивимось тут»)
    /// </summary>
    private static bool IsContentless(string text)
    {
        // Загальне повідомлення без статусу графіка, скасування, екстрених чи часу — думка або підпис
        // («Я б на вашому місці не брав до уваги ті графіки…», «Графік ДТЕК на 12 грудня | В разі змін будемо інформувати»)
        var length = text.Trim().Length;
        if (!InformativeRegex.IsMatch(text) && !NationwideDecisionRegex.IsMatch(text))
            return true;

        return length < NoChangeNoticeMaxLength && NoChangeRegex.IsMatch(text) && !ChangeRegex.IsMatch(text);
    }

    private static bool IsLocalAddressReport(string text) =>
        ComplaintRegex.IsMatch(text) || (AddressRegex.Matches(text).Count >= 2 && !ScheduleWordsRegex.IsMatch(text));

    // Загальноміські алерти: екстрені/аварійні відключення, ГАВ
    private static readonly Regex EmergencyRegex = new(EmergencyPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex OtherRegionRegex = new(
        @"київ|києв|харків|франків|коломи|запоріж|одес|львів|житомир|полтав|сумськ|чернігів|донеч|херсон|миколаїв|вінниц|черкас|рівн|волин|тернопіл|кропивниц",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DniproRegex = new(@"дніпр|област|місті", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Інші міста області. Пост, що відкривається одним із них і не згадує Дніпро, — про інше місто
    private static readonly Regex OtherCityRegex = new(
        // Форми назв підібрані так, щоб не ловити вулиці Дніпра (вул. Кам'янська, Шахтарська, П'ятихатки)
        @"павлоград|кам.янськ(?:е|ому|ого)\b|новомосковськ|\bсамар(?:і|а|у)?\b|самарівськ|нікопол|марганц|марганец|\bпокров(?:і|а|у)?\b|" +
        @"кривий ріг|кривого рогу|кривому розі|криворіж|жовті води|жовтих вод|вільногірськ|синельников|першотравенськ|шахтарськ(?:е|ому|ого)\b|" +
        @"тернівк|верхньодніпровськ|апостолов|зеленодольськ|царичанк|петриківк|магдалинівк|\bмежов(?:а|ій|у)\b|васильківк|перещепин|губиних",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DniproCityRegex = new(@"(?<!верхньо)дніпр", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const int CityHeaderLength = 80;

    // Будь-який населений пункт з позначкою («м. Підгороднє», «с. Зоря», «с-ще Обухівка», «смт Слобожанське»), крім Дніпра,
    // і передмістя без позначки. «Слобожанськ(е|ому|ого)» — щоб не чіпати Слобожанський проспект
    // Позначка обов'язково з крапкою або «-ще» і назва з великої літери: «200 м. від…», «с ДТЕК» — не населені пункти
    private static readonly Regex OtherLocalityHeaderRegex = new(
        @"(?<!\w)(?:[мМсС]\.|смт\.?\s|СМТ\.?\s|[сС]-ще\.?\s)\s*(?!Дніпр|ДНІПР)[А-ЯІЇЄҐ]|(?i:підгородн|обухівк|слобожанськ(?:е|ому|ого)\b)",
        RegexOptions.Compiled);

    /// <summary>
    /// Пост про інший населений пункт: він стоїть у першому рядку («М.Тернівка | група 2.2…», «м. Підгороднє, с-ще Обухівка…»)
    /// або в перших 80 символах після рядка-заголовка без інформації («Електропостачання | 🏙с. Зоря…»), а Дніпро не згадано.
    /// «Нагадуємо графік ДТЕК СКАСОВАНО | ПО М.Підгороднє…» — не локальний: перший рядок сам є новиною
    /// </summary>
    private static bool IsOtherCityPost(string text)
    {
        if (DniproCityRegex.IsMatch(text))
            return false;

        var city = OtherCityRegex.Match(text);
        var locality = OtherLocalityHeaderRegex.Match(text);
        var first = new[] { city, locality }.Where(m => m.Success).OrderBy(m => m.Index).FirstOrDefault();
        if (first == null)
            return false;

        var firstLineEnd = text.IndexOf('\n');
        if (firstLineEnd < 0 || first.Index < firstLineEnd)
            return true;

        return first.Index < CityHeaderLength && !InformativeRegex.IsMatch(text[..firstLineEnd]);
    }

    private static readonly Regex WaterRegex = new(@"водопостач|водовод|водопровід", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool IsCityAlertText(string text)
    {
        if (!EmergencyRegex.IsMatch(text) || IsLocalAddressReport(text) || OtherLocalityRegex.IsMatch(text))
            return false;

        // Про воду, а не про світло
        if (WaterRegex.IsMatch(text) && !Regex.IsMatch(text, @"світл|електро", RegexOptions.IgnoreCase))
            return false;

        // Інший регіон — тільки якщо поруч не згадано Дніпро/область
        return !OtherRegionRegex.IsMatch(text) || DniproRegex.IsMatch(text);
    }

    private static readonly Regex ReferenceLineRegex = new(
        @"\bтут\b|за посиланням|👇", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static (int Start, int End) LineBounds(string text, int position)
    {
        var start = position == 0 ? 0 : text.LastIndexOf('\n', position - 1) + 1;
        var end = text.IndexOf('\n', position);
        return (start, end < 0 ? text.Length : end);
    }

    private static bool IsReferenceLine(string text, int position)
    {
        var (start, end) = LineBounds(text, position);
        var line = text[start..end];
        return ReferenceLineRegex.IsMatch(line) && !GroupRegex.IsMatch(line);
    }

    public static ChannelMessageInfo Parse(string? text)
    {
        // Реклама та пости про інше місто області («Павлоград: графік ЦЕК на завтра», «М.Тернівка | група 2.2…») — нікому
        if (string.IsNullOrWhiteSpace(text) || AdvertRegex.IsMatch(text) || IsOtherCityPost(text))
            return new ChannelMessageInfo([], [], false, false);

        // Емодзі-цифри «1️⃣.1️⃣ черга» -> «1.1 черга»
        text = KeycapDigitRegex.Replace(text, "$1");

        var allMarkers = new List<(int Position, int DsoId)>();
        allMarkers.AddRange(DtekRegex.Matches(text).Select(m => (m.Index, DtekDsoId)));
        allMarkers.AddRange(CekRegex.Matches(text).Select(m => (m.Index, CekDsoId)));
        allMarkers.Sort((a, b) => a.Position.CompareTo(b.Position));

        // Рядки-посилання («Для ЦЕК дивимось тут», «Графік ЦЕК тут 👇») не відкривають секцію провайдера
        var markers = allMarkers.Where(m => !IsReferenceLine(text, m.Position)).ToList();

        // Провайдери повідомлення: з «змістовних» рядків. Якщо провайдер згаданий лише як посилання
        // («Станом на 7:00 графіки без змін | Графік ЦЕК дивимось тут»), повідомлення — про іншого провайдера
        var dsoIds = markers.Select(m => m.DsoId).Distinct().ToList();
        if (dsoIds.Count == 0 && allMarkers.Count > 0)
        {
            var referenced = allMarkers.Select(m => m.DsoId).Distinct().ToList();
            var others = new[] { DtekDsoId, CekDsoId }.Except(referenced).ToList();
            // Обидва провайдери лише посиланнями («Графік ДТЕК тут / Графік ЦЕК тут») — стосується обох
            dsoIds = others.Count > 0 ? others : referenced;
        }
        // Провайдер не названий, але формулювання ЦЕК («Шановні споживачі», «Черга 1.1») — вгаданий, не явний
        var providerInferred = false;
        if (dsoIds.Count == 0 && CekWordingRegex.IsMatch(text))
        {
            dsoIds.Add(CekDsoId);
            providerInferred = true;
        }

        var groups = new List<ChannelGroupMention>();
        void AddGroup(string group, int position)
        {
            var dsoId = ResolveDso(text, position, markers, dsoIds);
            if (!groups.Any(g => g.Group == group && g.DsoId == dsoId))
            {
                groups.Add(new ChannelGroupMention(group, dsoId));
            }
        }

        foreach (Match range in GroupRangeRegex.Matches(text))
        {
            var from = Array.IndexOf(AllGroups, range.Groups[1].Value);
            var to = Array.IndexOf(AllGroups, range.Groups[2].Value);
            if (from < 0 || to < from) continue;
            for (var i = from; i <= to; i++)
            {
                AddGroup(AllGroups[i], range.Index);
            }
        }

        foreach (Match match in GroupRegex.Matches(text))
        {
            AddGroup(match.Groups[1].Value, match.Index);
        }

        return new ChannelMessageInfo(
            dsoIds,
            groups,
            OutageKeywordsRegex.IsMatch(text) && !IsLocalAddressReport(text) && !IsContentless(text) && !IsAttackNewsOnly(text),
            dsoIds.Count == 0 && IsNationwideDecision(text),
            IsCityAlertText(text),
            providerInferred);
    }

    // Відбиток графіка: групи та час, починаючи з першої групи
    private static readonly Regex ScheduleTokenRegex = new(
        // Час, але не дата: «07.02.2026» не є «07:02»
        @"(?<t>(?<![\d.])\d{1,2}[:.]\d{2}(?!\d|[.:]\d))|(?<g>" + GroupPattern + ")", RegexOptions.Compiled);

    private static readonly Regex DateWordRegex = new(
        @"(?<!\d)(\d{1,2})\s+(січ|лют|берез|квіт|трав|черв|лип|серп|верес|жовт|листоп|груд)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DateNumericRegex = new(
        @"(?<![\d.])(\d{1,2})\.(\d{2})\.(?:\d{2}|\d{4})(?!\d)", RegexOptions.Compiled);

    private static readonly string[] MonthStems =
        ["січ", "лют", "берез", "квіт", "трав", "черв", "лип", "серп", "верес", "жовт", "листоп", "груд"];

    private const int MinScheduleGroups = 2;
    private const int MinScheduleTimes = 4;

    /// <summary>
    /// Відбиток графіка для пошуку копій між каналами: той самий розклад з різним підписом і в різному форматі
    /// («📢 Шановні споживачі!…» / «Графік для ЦЕК…», «1.1 та 1.2 — 00:00–04:00» / «Група 1.1 • 00:00 — 04:00»).
    /// <see cref="Content"/> — провайдер і для кожної групи її інтервали (кінець доби «00:00» = «24:00»);
    /// <see cref="Date"/> — дата графіка «день.місяць» («8 листопада» і «08.11.2026» дають «8.11») або null, якщо її немає.
    /// </summary>
    public sealed record ScheduleFingerprint(string Content, string? Date);

    /// <summary>Однакова дата графіка — копія вважається дублем протягом доби</summary>
    public static readonly TimeSpan SameDateScheduleWindow = TimeSpan.FromHours(24);

    /// <summary>Дати немає хоча б в одному пості — лише 3 години, щоб графік «на завтра», ідентичний сьогоднішньому, не став дублем</summary>
    public static readonly TimeSpan UndatedScheduleWindow = TimeSpan.FromHours(3);

    /// <summary>
    /// Чи це той самий графік, що вже пересилали: однаковий зміст і дата (якщо вона є в обох) у межах відповідного вікна
    /// </summary>
    public static bool IsSameSchedule(ScheduleFingerprint current, DateTime currentPublishedUtc, ScheduleFingerprint previous, DateTime previousPublishedUtc)
    {
        if (current.Content != previous.Content)
            return false;

        var bothDated = current.Date != null && previous.Date != null;
        if (bothDated && current.Date != previous.Date)
            return false;

        var window = bothDated ? SameDateScheduleWindow : UndatedScheduleWindow;
        return (currentPublishedUtc - previousPublishedUtc).Duration() <= window;
    }

    /// <summary>
    /// null — у тексті немає графіка (менше 2 груп або 4 значень часу)
    /// </summary>
    public static ScheduleFingerprint? GetScheduleFingerprint(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var info = Parse(text);
        text = KeycapDigitRegex.Replace(text, "$1");

        var firstGroup = GroupRegex.Match(text);
        if (!firstGroup.Success)
            return null;

        // Йдемо по групах і часу: групи підряд без часу між ними («1.1 та 1.2 — …») ділять один набір інтервалів
        var intervals = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var rawTokens = new List<string>();
        var currentGroups = new List<string>();
        var currentTimes = new List<string>();
        var paired = true;
        int groupCount = 0, timeCount = 0;

        void Flush()
        {
            if (currentGroups.Count == 0 || currentTimes.Count == 0)
                return;
            if (currentTimes.Count % 2 != 0)
                paired = false;
            for (var i = 0; i + 1 < currentTimes.Count; i += 2)
            {
                var end = currentTimes[i + 1] == "00:00" ? "24:00" : currentTimes[i + 1];
                var interval = $"{currentTimes[i]}-{end}";
                foreach (var group in currentGroups)
                {
                    if (!intervals.TryGetValue(group, out var set))
                        intervals[group] = set = new SortedSet<string>(StringComparer.Ordinal);
                    set.Add(interval);
                }
            }
            currentGroups.Clear();
            currentTimes.Clear();
        }

        foreach (Match token in ScheduleTokenRegex.Matches(text, firstGroup.Index))
        {
            if (token.Groups["t"].Success)
            {
                var parts = token.Value.Split(':', '.');
                var time = $"{int.Parse(parts[0]):00}:{parts[1]}";
                currentTimes.Add(time);
                rawTokens.Add(time);
                timeCount++;
            }
            else
            {
                if (currentTimes.Count > 0)
                    Flush();
                currentGroups.Add(token.Value);
                rawTokens.Add(token.Value);
                groupCount++;
            }
        }
        Flush();

        if (groupCount < MinScheduleGroups || timeCount < MinScheduleTimes)
            return null;

        // Непарний час («1.1 до 19:00») — інтервали не відновити, порівнюємо послідовність як є
        var body = paired && intervals.Count > 0
            ? string.Join(";", intervals.Select(kv => $"{kv.Key}:{string.Join(",", kv.Value)}"))
            : "raw:" + string.Join(" ", rawTokens);

        return new ScheduleFingerprint($"{string.Join(",", info.DsoIds.OrderBy(d => d))}|{body}", ExtractDate(text));
    }

    private static string? ExtractDate(string text)
    {
        var dateWord = DateWordRegex.Match(text);
        if (dateWord.Success)
        {
            var month = Array.FindIndex(MonthStems, stem => dateWord.Groups[2].Value.StartsWith(stem, StringComparison.OrdinalIgnoreCase)) + 1;
            return $"{int.Parse(dateWord.Groups[1].Value)}.{month}";
        }

        var dateNumeric = DateNumericRegex.Match(text);
        if (dateNumeric.Success)
            return $"{int.Parse(dateNumeric.Groups[1].Value)}.{int.Parse(dateNumeric.Groups[2].Value)}";

        return null;
    }

    /// <summary>
    /// Провайдер для згадки групи. Пріоритет: маркер ДТЕК/ЦЕК перед групою в тому ж рядку,
    /// маркер після групи в тому ж рядку («5.2 ДТЕК група…»), найближчий маркер у попередніх рядках (секція),
    /// єдиний провайдер повідомлення, інакше невідомо
    /// </summary>
    private static int? ResolveDso(string text, int position, List<(int Position, int DsoId)> markers, List<int> dsoIds)
    {
        var (lineStart, lineEnd) = LineBounds(text, position);

        // default((int, int)) не може бути справжнім маркером: DsoId завжди 301/303
        var precedingInLine = markers.LastOrDefault(m => m.Position >= lineStart && m.Position < position);
        if (precedingInLine != default)
            return precedingInLine.DsoId;

        var followingInLine = markers.FirstOrDefault(m => m.Position > position && m.Position < lineEnd);
        if (followingInLine != default)
            return followingInLine.DsoId;

        var preceding = markers.LastOrDefault(m => m.Position < lineStart);
        if (preceding != default)
            return preceding.DsoId;

        return dsoIds.Count == 1 ? dsoIds[0] : null;
    }

    /// <summary>
    /// Чи надсилати повідомлення користувачу з провайдером <paramref name="userDsoId"/> і групою <paramref name="userGroup"/>
    /// </summary>
    public static bool MatchesUser(ChannelMessageInfo info, int? userDsoId, string? userGroup)
    {
        bool DsoFits(int? dsoId) => !dsoId.HasValue || !userDsoId.HasValue || dsoId.Value == userDsoId.Value;

        if (info.Groups.Count > 0)
        {
            // Інформація по конкретних групах — тільки якщо є згадка групи користувача у секції його провайдера
            if (string.IsNullOrEmpty(userGroup))
                return info.Groups.Any(g => DsoFits(g.DsoId));

            return info.Groups.Any(g =>
                string.Equals(g.Group, userGroup, StringComparison.OrdinalIgnoreCase) && DsoFits(g.DsoId));
        }

        // Загальні повідомлення — тільки про відключення
        if (!info.IsOutageRelated)
            return false;

        // Алерт про екстрені/аварійні відключення по місту без прив'язки до провайдера — всім
        // Провайдер лише вгаданий за формулюванням («Шановні споживачі м. ДНІПРО… скасування екстрених») — теж всім:
        // екстрені вводяться й скасовуються для всієї області
        if (info.IsCityAlert && (info.DsoIds.Count == 0 || info.IsProviderInferred))
            return true;

        if (info.DsoIds.Count > 0)
            return !userDsoId.HasValue || info.DsoIds.Contains(userDsoId.Value);

        return info.IsNationwideNotice;
    }
}
