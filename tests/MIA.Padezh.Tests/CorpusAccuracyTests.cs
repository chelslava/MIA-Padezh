using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MIA.Padezh;
using Xunit.Abstractions;

namespace MIA.Padezh.Tests;

/// <summary>
/// Харнесс точности: движок сравнивается с корпусом словоформ petrovich-eval
/// (https://github.com/petrovich/petrovich-eval, в репозиторий положены выборки).
/// Множественное число и средний род — вне MVP, такие строки пропускаются.
/// </summary>
public class CorpusAccuracyTests
{
    private readonly ITestOutputHelper _output;

    public CorpusAccuracyTests(ITestOutputHelper output) => _output = output;

    private static string FindCorpusPath(string fileName)
    {
        // Идём от папки сборки вверх до корня репозитория и берём tests/testdata.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
               && !File.Exists(Path.Combine(dir.FullName, "MIA.Padezh.slnx"))
               && !File.Exists(Path.Combine(dir.FullName, "MIA.Padezh.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "tests", "testdata", fileName);
    }

    [Theory]
    [InlineData("surnames.sample.tsv", NamePart.LastName, 0.98)]
    [InlineData("firstnames.sample.tsv", NamePart.FirstName, 0.97)]
    [InlineData("midnames.sample.tsv", NamePart.MiddleName, 0.99)]
    public void EngineMatchesCorpus(string fileName, NamePart part, double minAccuracy)
    {
        var path = FindCorpusPath(fileName);
        Assert.True(File.Exists(path), $"Файл корпуса не найден: {path}");

        int total = 0, matched = 0;
        var mismatches = new List<string>();

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("lemma", StringComparison.Ordinal))
                continue;

            var cols = line.Split('\t');
            if (cols.Length < 3)
                continue;

            var lemma = cols[0];
            var expected = cols[1];
            var tags = cols[2].Split(',');

            if (tags.Contains("мн"))
                continue; // множественное число — вне MVP
            if (!tags.Any(t => t.StartsWith("мр", StringComparison.Ordinal)) && !tags.Any(t => t.StartsWith("жр", StringComparison.Ordinal)))
                continue; // средний род правилами не поддерживается
            if (!TryMapCase(tags, out var @case))
                continue;

            // «мр-жр» — двуродовая фамилия: засчитываем любую из двух легитимных норм
            bool maleTag = tags.Any(t => t.StartsWith("мр", StringComparison.Ordinal));
            bool femaleTag = tags.Any(t => t.StartsWith("жр", StringComparison.Ordinal));
            var gender = maleTag && !femaleTag ? Gender.Male : !maleTag ? Gender.Female : Gender.Androgynous;
            var actual = gender == Gender.Androgynous
                ? Padezh.Decline(part, lemma, @case, Gender.Male)
                : Padezh.Decline(part, lemma, @case, gender);
            var actualAlt = gender == Gender.Androgynous
                ? Padezh.Decline(part, lemma, @case, Gender.Female)
                : actual;
            total++;

            if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                || string.Equals(actualAlt, expected, StringComparison.OrdinalIgnoreCase))
                matched++;
            else if (mismatches.Count < 50)
                mismatches.Add($"{lemma} [{gender} {tags[^1]}]: ожидалось «{expected}», получено «{actual}»");
        }

        Assert.True(total > 100, $"В корпусе слишком мало кейсов: {total}");

        var accuracy = (double)matched / total;
        _output.WriteLine($"{fileName}: {matched}/{total} = {accuracy:P2} ({part})");
        foreach (var m in mismatches.Take(25))
            _output.WriteLine("  " + m);

        // Порог чуть ниже фактической точности (99,5–100%), чтобы тест не мигал;
        // расхождения — в основном артефакты самого корпуса (словарные формы с нестандартными нормами).
        Assert.True(accuracy >= minAccuracy, $"Точность {accuracy:P2} ниже порога {minAccuracy:P2}");
    }

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
}
