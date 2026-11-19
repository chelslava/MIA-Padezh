using System;
using MIA.Padezh.Generated;

namespace MIA.Padezh;

/// <summary>
/// Ядро склонения. Портировано с детерминированной логики NPetrovich (Petrovich).
/// Правила подставлены на этапе сборки (сгенерированный код в Generated/RulesTable.g.cs),
/// поэтому в рантайме нет ни парсинга JSON, ни зависимостей.
/// </summary>
internal static class DeclensionEngine
{
    private const string FirstWordTag = "first_word";

    private const string Consonants = "бвгджзклмнпрстфхцчшщ";

    /// <summary>Склоняет слово (или дефисное составное) в заданный падеж.</summary>
    public static string Inflect(string name, Case @case, NamePart part, Gender gender, VocativeStyle style = VocativeStyle.Colloquial)
    {
        if (@case == Case.Vocative)
            return part == NamePart.FirstName ? Vocative(name, gender, style) : name;

        if (@case == Case.Nominative || string.IsNullOrEmpty(name))
            return name;

        var chunks = name.Split('-');
        if (chunks.Length == 1)
            return FindAndApply(chunks[0], @case, part, gender, firstWord: false);

        var result = new string[chunks.Length];
        for (int i = 0; i < chunks.Length; i++)
        {
            bool firstWord = i == 0;
            result[i] = FindAndApply(chunks[i], @case, part, gender, firstWord);
        }

        return string.Join("-", result);
    }

    private static string FindAndApply(string chunk, Case @case, NamePart part, Gender gender, bool firstWord)
    {
        var rule = FindRule(chunk, RulesTable.Exceptions(part), gender, firstWord, wholeWord: true)
                ?? FindRule(chunk, RulesTable.Suffixes(part), gender, firstWord, wholeWord: false);

        if (rule is null)
            return chunk;

        return Apply(chunk, rule.Value.Mods[CaseIndex(@case)]);
    }

    /// <summary>Порядок правил значим: первое совпадение применяется (как в upstream).</summary>
    private static RuleEntry? FindRule(string chunk, RuleEntry[] rules, Gender gender, bool firstWord, bool wholeWord)
    {
        for (int i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];
            if (!MatchesGender(rule, gender) || !MatchesTags(rule, firstWord))
                continue;

            if (MatchesTest(chunk, rule.Test, wholeWord))
                return rule;
        }

        return null;
    }

    private static bool MatchesGender(RuleEntry rule, Gender gender)
    {
        return !(rule.Gender == Gender.Male && gender == Gender.Female)
            && !(rule.Gender == Gender.Female && gender != Gender.Female);
    }

    private static bool MatchesTags(RuleEntry rule, bool firstWord)
    {
        if (rule.Tags is null || rule.Tags.Length == 0)
            return true;

        return firstWord;
    }

    private static bool MatchesTest(string chunk, string[] tests, bool wholeWord)
    {
        var lowered = chunk.ToLowerInvariant();
        for (int i = 0; i < tests.Length; i++)
        {
            var test = tests[i];
            if (wholeWord)
            {
                if (lowered == test)
                    return true;
            }
            else
            {
                int start = lowered.Length - test.Length;
                if (start >= 0 && string.CompareOrdinal(lowered, start, test, 0, test.Length) == 0)
                    return true;
            }
        }

        return false;
    }

    /// <summary>Портированная семантика модификаторов: '-' — отрезать букву с конца, '.' — пропустить, остальное — дописать.</summary>
    private static string Apply(string chunk, string mod)
    {
        for (int i = 0; i < mod.Length; i++)
        {
            char c = mod[i];
            if (c == '.')
                continue;
            if (c == '-')
            {
                if (chunk.Length > 0)
                    chunk = chunk.Remove(chunk.Length - 1);
                continue;
            }

            chunk += c;
        }

        return chunk;
    }

    private static int CaseIndex(Case @case)
    {
        switch (@case)
        {
            case Case.Genitive: return 0;
            case Case.Dative: return 1;
            case Case.Accusative: return 2;
            case Case.Instrumental: return 3;
            case Case.Prepositional: return 4;
            default:
                throw new ArgumentOutOfRangeException(nameof(@case), @case, "Неизвестный падеж.");
        }
    }

    /// <summary>
    /// Множественное число: фамилии («Иванов» → «Ивановых»), имён («Иван» → «Иванов»)
    /// и отчеств («Иванович» → «Ивановичей»). Для звательного падежа возвращается
    /// форма именительного множественного.
    /// </summary>
    public static string InflectPlural(string name, Case @case, NamePart part, Gender gender)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var effectiveCase = @case == Case.Vocative || @case == Case.Nominative ? Case.Nominative : @case;
        var rules = part switch
        {
            NamePart.FirstName => RulesTable.FirstNamePlural,
            NamePart.MiddleName => RulesTable.MiddleNamePlural,
            _ => RulesTable.LastNamePlural,
        };

        var chunks = name.Split('-');
        var result = new string[chunks.Length];
        for (int i = 0; i < chunks.Length; i++)
            result[i] = PluralChunk(chunks[i], effectiveCase, gender, rules);

        return string.Join("-", result);
    }

    private static string PluralChunk(string chunk, Case @case, Gender gender, PluralEntry[] rules)
    {
        var lowered = chunk.ToLowerInvariant();
        for (int i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];
            if (rule.Gender == Gender.Female && gender != Gender.Female)
                continue; // мужские и андрогинные склоняются по одним правилам

            if (SuffixMatches(lowered, rule.Test))
                return Apply(chunk, rule.Mods[PluralCaseIndex(@case)]);
        }

        return chunk;
    }

    private static bool SuffixMatches(string lowered, string[] tests)
    {
        for (int i = 0; i < tests.Length; i++)
        {
            var test = tests[i];
            int start = lowered.Length - test.Length;
            if (start >= 0 && string.CompareOrdinal(lowered, start, test, 0, test.Length) == 0)
                return true;
        }

        return false;
    }

    private static int PluralCaseIndex(Case @case)
    {
        switch (@case)
        {
            case Case.Nominative: return 0;
            case Case.Genitive: return 1;
            case Case.Dative: return 2;
            case Case.Accusative: return 3;
            case Case.Instrumental: return 4;
            case Case.Prepositional: return 5;
            default:
                throw new ArgumentOutOfRangeException(nameof(@case), @case, "Неизвестный падеж.");
        }
    }

    /// <summary>Звательный падеж имени в выбранном стиле.</summary>
    private static string Vocative(string name, Gender gender, VocativeStyle style)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var chunks = name.Split('-');
        var result = new string[chunks.Length];
        for (int i = 0; i < chunks.Length; i++)
        {
            result[i] = style == VocativeStyle.Church
                ? ChurchVocativeChunk(chunks[i], gender)
                : ColloquialVocativeChunk(chunks[i]);
        }

        return string.Join("-", result);
    }

    /// <summary>
    /// Современный разговорный звательный: конечное -а усекается (Саша → Саш),
    /// конечное -я после согласной заменяется на ь (Ваня → Вань, Катя → Кать).
    /// Слова на согласный, -й, -ь и на -я после гласной (Илья, Мария, Зоя) не меняются.
    /// </summary>
    private static string ColloquialVocativeChunk(string chunk)
    {
        if (chunk.Length < 2)
            return chunk;

        var last = chunk[chunk.Length - 1];
        if (last == '\u0430') // а
            return chunk.Remove(chunk.Length - 1);
        if (last == '\u044f' && Consonants.IndexOf(chunk[chunk.Length - 2]) >= 0) // я после согласной
            return chunk.Remove(chunk.Length - 1) + '\u044c'; // я → ь

        return chunk;
    }

    /// <summary>
    /// Церковно-славянский звательный. Мужские: согласный → +е (Пётр → Пётре),
    /// -й/-ь → -е (Николае, Игоре), -а → -о (Данило), -я → -е (Илье).
    /// Женские: -а → -о (Анно), -ия/-я после гласной → -е (Марие, Зое),
    /// -я после согласной → -о (Дарьо), -ь → -и (Любови).
    /// Андрогинные склоняются по мужской модели; прочие окончания — без изменений.
    /// </summary>
    private static string ChurchVocativeChunk(string chunk, Gender gender)
    {
        if (chunk.Length < 2)
            return chunk;

        bool male = gender != Gender.Female;
        var last = chunk[chunk.Length - 1];

        if (male)
        {
            if (last == '\u0439' || last == '\u044c' || last == '\u044f') // й, ь, я
                return ReplaceLast(chunk, '\u0435'); // е
            if (last == '\u0430') // а
                return ReplaceLast(chunk, '\u043e'); // о
            if (Consonants.IndexOf(last) >= 0)
                return chunk + '\u0435'; // е
            return chunk;
        }

        if (last == '\u0430') // а
            return ReplaceLast(chunk, '\u043e'); // о
        if (last == '\u044c') // ь
            return ReplaceLast(chunk, '\u0438'); // и
        if (last == '\u044f') // я
        {
            var prev = chunk[chunk.Length - 2];
            if (prev == '\u0438') // ия → ие
                return ReplaceLast(chunk, '\u0435');
            const string Vowels = "\u0430\u0435\u0451\u0438\u043e\u0443\u044b\u044d\u044e"; // а е ё и о у ы э ю
            if (Vowels.IndexOf(prev) >= 0) // я после гласной → е (Зое)
                return ReplaceLast(chunk, '\u0435');
            return ReplaceLast(chunk, '\u043e'); // я после согласной или ь → о (Дарьо)
        }

        return chunk;
    }

    private static string ReplaceLast(string chunk, char replacement)
        => chunk.Remove(chunk.Length - 1) + replacement;
}
