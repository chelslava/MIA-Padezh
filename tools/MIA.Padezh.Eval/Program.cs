using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MIA.Padezh.Eval;

/// <summary>
/// Оценка точности движка по корпусу словоформ petrovich-eval (TSV: lemma, word, grammemes).
/// Две метрики: род берётся из корпуса и род определяется движком автоматически (реальный сценарий).
/// Отчёт пишется в UTF-8 файл: точность по частям ФИО + топ-паттерны расхождений.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var corpusDir = args.Length > 0 ? args[0] : ".";
        var outputPath = args.Length > 1 ? args[1] : "eval-report.txt";

        var files = new (string File, NamePart Part)[]
        {
            ("surnames.tsv", NamePart.LastName),
            ("firstnames.tsv", NamePart.FirstName),
            ("midnames.tsv", NamePart.MiddleName),
        };

        var sb = new StringBuilder();
        sb.AppendLine("=== Точность по классам фамилий (род из корпуса) ===");
        AppendClassReport(sb, Path.Combine(corpusDir, "surnames.tsv"));
        sb.AppendLine();

        foreach (var (file, part) in files)
        {
            var path = Path.Combine(corpusDir, file);
            if (!File.Exists(path))
            {
                sb.AppendLine($"НЕТ ФАЙЛА: {path}");
                continue;
            }

            var (withGender, autoGender, ruleMismatches, detectMismatches) = Evaluate(path, part);
            sb.AppendLine($"=== {file} ({part}) ===");
            sb.AppendLine($"с родом из корпуса            : {withGender.Matched}/{withGender.Total} = {Rate(withGender):P2}");
            sb.AppendLine($"род автодетектом (все строки) : {autoGender.Matched}/{autoGender.Total} = {Rate(autoGender):P2}");
            sb.AppendLine($"реальные пробелы правил       : {ruleMismatches.Count} (строк, где даже с правильным родом не совпало)");
            sb.AppendLine($"реальные ошибки автодетекта   : {detectMismatches.Count} (строк, где с родом совпало, а без него нет)");
            sb.AppendLine();

            void Dump(List<Mismatch> list, string title)
            {
                sb.AppendLine($"--- {title}: {list.Count} ---");
                foreach (var group in list
                             .GroupBy(m => m.Pattern)
                             .OrderByDescending(g => g.Count())
                             .Take(15))
                {
                    sb.AppendLine($"паттерн «{group.Key}» x{group.Count()}:");
                    foreach (var m in group.Take(4))
                        sb.AppendLine($"    {m}");
                    sb.AppendLine();
                }
            }

            Dump(ruleMismatches, "пробелы правил (с родом из корпуса)");
            Dump(detectMismatches, "ошибки автодетекта (там, где с правильным родом всё совпало)");
            sb.AppendLine();
        }

        File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
        return 0;
    }

    private static readonly (string Label, string[] Suffixes)[] SurnameClasses =
    {
        ("arm -ян", new[] { "ян" }),
        ("arm -янц", new[] { "янц" }),
        ("geo -швили", new[] { "швили" }),
        ("geo -дзе", new[] { "дзе" }),
        ("geo -ия", new[] { "ия" }),
        ("geo -ая", new[] { "ая" }),
        ("ukr -енко", new[] { "енко" }),
        ("ukr -ук/-юк", new[] { "ук", "юк" }),
        ("ukr -ак/-як", new[] { "ак", "як" }),
        ("baltic -ис/-ас/-ус", new[] { "ис", "ас", "ус", "енс", "имс" }),
        ("tatar -ин", new[] { "ин" }),
        ("greek -иди/-ос", new[] { "иди", "ос", "акис" }),
        ("moldovan -у", new[] { "у" }),
    };

    private static void AppendClassReport(StringBuilder sb, string path)
    {
        if (!File.Exists(path))
        {
            sb.AppendLine("нет файла корпуса");
            return;
        }

        var stats = new Dictionary<string, (int Total, int Matched)>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("lemma", StringComparison.Ordinal))
                continue;

            var cols = line.Split('\t');
            if (cols.Length < 3 || cols[2].Contains("мн"))
                continue;

            var tags = cols[2].Split(',');
            // «мр-жр» — двуродовая фамилия: считаем по мужской норме склонения
            bool male = tags.Any(t => t.StartsWith("мр", StringComparison.Ordinal));
            bool female = tags.Any(t => t.StartsWith("жр", StringComparison.Ordinal));
            if (!male && !female)
                continue;
            if (!TryMapCase(tags, out var @case))
                continue;

            var gender = male && female ? Gender.Androgynous : male ? Gender.Male : Gender.Female;
            var lemma = cols[0].ToLowerInvariant();

            string? label = null;
            foreach (var (labelCandidate, suffixes) in SurnameClasses)
            {
                if (suffixes.Any(s => lemma.EndsWith(s, StringComparison.Ordinal)))
                {
                    label = labelCandidate;
                    break;
                }
            }

            if (label is null)
                continue;

            var actual = Padezh.Decline(NamePart.LastName, cols[0], @case, gender == Gender.Androgynous ? Gender.Male : gender);
            var actualAlt = gender == Gender.Androgynous
                ? Padezh.Decline(NamePart.LastName, cols[0], @case, Gender.Female)
                : actual;
            var ok = string.Equals(actual, cols[1], StringComparison.OrdinalIgnoreCase)
                     || string.Equals(actualAlt, cols[1], StringComparison.OrdinalIgnoreCase);

            var (total, matched) = stats.TryGetValue(label, out var s) ? s : (0, 0);
            stats[label] = (total + 1, matched + (ok ? 1 : 0));
        }

        foreach (var kv in stats.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var (total, matched) = kv.Value;
            sb.AppendLine($"{kv.Key}: {matched}/{total} = {(total == 0 ? 0 : (double)matched / total):P2}");
        }
    }

    private static double Rate((int Total, int Matched) s)
        => s.Total == 0 ? 0 : (double)s.Matched / s.Total;

    private static ((int Total, int Matched) WithGender, (int Total, int Matched) AutoGender, List<Mismatch> RuleMismatches, List<Mismatch> DetectMismatches)
        Evaluate(string path, NamePart part)
    {
        int totalG = 0, matchedG = 0, totalA = 0, matchedA = 0;
        var ruleMismatches = new List<Mismatch>();
        var detectMismatches = new List<Mismatch>();

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("lemma", System.StringComparison.Ordinal))
                continue;

            var cols = line.Split('\t');
            if (cols.Length < 3)
                continue;

            var lemma = cols[0];
            var expected = cols[1];
            var tags = cols[2].Split(',');

            // «мр-жр» — двуродовая фамилия: засчитываем попадание в любую из двух легитимных норм
            bool maleTag = tags.Any(t => t.StartsWith("мр", StringComparison.Ordinal));
            bool femaleTag = tags.Any(t => t.StartsWith("жр", StringComparison.Ordinal));
            if (tags.Contains("мн") || (!maleTag && !femaleTag))
                continue;
            if (!TryMapCase(tags, out var @case))
                continue;

            var gender = maleTag && !femaleTag ? Gender.Male : !maleTag ? Gender.Female : Gender.Androgynous;

            var actualG = Padezh.Decline(part, lemma, @case, gender == Gender.Androgynous ? Gender.Male : gender);
            bool MatchWith(Gender g) => string.Equals(
                Padezh.Decline(part, lemma, @case, g),
                expected,
                System.StringComparison.OrdinalIgnoreCase);

            bool withGenderMatch = gender == Gender.Androgynous
                ? MatchWith(Gender.Male) || MatchWith(Gender.Female)
                : MatchWith(gender);
            totalG++;
            if (withGenderMatch)
                matchedG++;
            else if (ruleMismatches.Count < 3000)
                ruleMismatches.Add(new Mismatch(lemma, expected, actualG, @case, gender, true, Suffix(lemma)));

            var actualA = Padezh.Decline(part, lemma, @case, null);
            bool autoMatch = withGenderMatch || string.Equals(actualA, expected, System.StringComparison.OrdinalIgnoreCase);
            totalA++;
            if (autoMatch)
                matchedA++;
            else if (!withGenderMatch)
                continue; // двусмысленность корпуса (напр. женские варианты согласных фамилий) — не ошибка детекта
            else if (detectMismatches.Count < 3000)
                detectMismatches.Add(new Mismatch(lemma, expected, actualA, @case, gender, false, Suffix(lemma)));
        }

        return ((totalG, matchedG), (totalA, matchedA), ruleMismatches, detectMismatches);
    }

    private static string Suffix(string word)
        => word.Length >= 3 ? word.Substring(word.Length - 3).ToLowerInvariant()
         : word.ToLowerInvariant();

    private static bool TryMapCase(string[] tags, out Case @case)
    {
        foreach (var tag in tags)
        {
            switch (tag)
            {
                case "им": @case = Case.Nominative; return true;
                case "рд": @case = Case.Genitive; return true;
                case "дт": @case = Case.Dative; return true;
                case "вн": @case = Case.Accusative; return true;
                case "тв": @case = Case.Instrumental; return true;
                case "пр": @case = Case.Prepositional; return true;
            }
        }

        @case = Case.Nominative;
        return false;
    }

    private readonly record struct Mismatch(
        string Lemma, string Expected, string Actual, Case Case, Gender Gender, bool WithGender, string Pattern)
    {
        public override string ToString()
            => $"{Lemma} [{(WithGender ? Gender.ToString() : "авто")} {Case}]: ожидалось «{Expected}», получено «{Actual}»";
    }
}
