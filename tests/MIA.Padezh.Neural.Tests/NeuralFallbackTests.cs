using System;
using System.IO;
using System.Linq;
using MIA.Padezh;
using MIA.Padezh.Neural;
using Xunit.Abstractions;

namespace MIA.Padezh.Neural.Tests;

/// <summary>
/// Тесты нейро-fallback: правила в приоритете, точность модели на holdout
/// корпуса petrovich-eval, детерминизм.
/// </summary>
public class NeuralFallbackTests
{
    private readonly ITestOutputHelper _output;

    public NeuralFallbackTests(ITestOutputHelper output) => _output = output;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
               && !File.Exists(Path.Combine(dir.FullName, "MIA.Padezh.slnx"))
               && !File.Exists(Path.Combine(dir.FullName, "MIA.Padezh.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static readonly System.Collections.Generic.Dictionary<string, int> GenderIds =
        new() { ["M"] = 0, ["F"] = 1, ["A"] = 2 };

    private static (string Lemma, int Gender, int CaseIdx, string Form)[] LoadDemo()
    {
        var path = Path.Combine(FindRepoRoot(), "tests", "MIA.Padezh.Neural.Tests", "testdata", "holdout-demo.tsv");
        Assert.True(File.Exists(path), $"нет файла {path}");
        return File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Split('\t'))
            .Select(c => (c[0], GenderIds[c[1]], int.Parse(c[2]), c[3]))
            .ToArray();
    }

    private static string CaseName(int caseIdx) => caseIdx switch
    {
        0 => Case.Nominative.ToString(),
        1 => Case.Genitive.ToString(),
        2 => Case.Dative.ToString(),
        3 => Case.Accusative.ToString(),
        4 => Case.Instrumental.ToString(),
        _ => Case.Prepositional.ToString(),
    };

    [Fact]
    public void RulesHavePriority()
    {
        using var padezh = new NeuralPadezh();
        // Покрытые правилами фамилии должны возвращать ровно результат правил.
        foreach (var @case in new[] { Case.Genitive, Case.Dative, Case.Instrumental, Case.Prepositional })
        {
            var expected = Padezh.Decline(NamePart.LastName, "Иванов", @case, Gender.Male);
            Assert.Equal(expected, padezh.DeclineLastName("Иванов", @case, Gender.Male));
        }

        Assert.Equal("Шевченко", padezh.DeclineLastName("Шевченко", Case.Genitive, Gender.Male));
    }

    [Fact]
    public void UnmatchedRatio()
    {
        // Сколько форм holdout-словаря правила не покрывают вовсе (там вызывается модель).
        var demo = LoadDemo();
        int total = 0, unmatched = 0;
        var examples = new System.Collections.Generic.List<string>();
        foreach (var (lemma, gender, caseIdx, _) in demo)
        {
            for (int cs = 1; cs <= 5; cs++)
            {
                total++;
                if (!Padezh.TryDecline(NamePart.LastName, lemma, (Case)cs, out _, (Gender)gender))
                {
                    unmatched++;
                    if (examples.Count < 30)
                        examples.Add($"{lemma} [{(Gender)gender} {CaseName(cs)}]");
                }
            }
        }

        _output.WriteLine($"правила не покрыли: {unmatched}/{total} ({(double)unmatched / total:P2})");
        foreach (var e in examples)
            _output.WriteLine("  " + e);
    }

    [Fact]
    public void HoldoutAccuracy()
    {
        // Гибрид end-to-end (DeclineLastName): правила покрывают все кириллические
        // словарные фамилии, модель не вызывается.
        using var padezh = new NeuralPadezh();
        var demo = LoadDemo();
        int total = 0, ok = 0;
        var misses = new System.Collections.Generic.List<string>();

        foreach (var (lemma, gender, caseIdx, form) in demo)
        {
            var actual = padezh.DeclineLastName(lemma, (Case)caseIdx, (Gender)gender);
            total++;
            if (string.Equals(actual, form, StringComparison.OrdinalIgnoreCase))
                ok++;
            else if (misses.Count < 20)
                misses.Add($"{lemma} [{(Gender)gender} {CaseName(caseIdx)}]: ожидалось «{form}», получено «{actual}»");
        }

        var accuracy = (double)ok / total;
        _output.WriteLine($"гибрид: {ok}/{total} = {accuracy:P2}");
        foreach (var m in misses)
            _output.WriteLine("  " + m);

        Assert.True(accuracy >= 0.95, $"Точность гибрида {accuracy:P2} ниже порога 95%");
    }

    [Fact]
    public void ModelSoloAccuracy()
    {
        // Модель соло на holdout (DeclineRaw): ~91% — остаток редкие экзотические стемы.
        using var padezh = new NeuralPadezh();
        var demo = LoadDemo();
        int total = 0, ok = 0;
        foreach (var (lemma, gender, caseIdx, form) in demo)
        {
            var actual = padezh.DeclineRaw(lemma, (Case)caseIdx, (Gender)gender);
            total++;
            if (string.Equals(actual, form, StringComparison.OrdinalIgnoreCase))
                ok++;
        }

        var accuracy = (double)ok / total;
        _output.WriteLine($"модель соло: {ok}/{total} = {accuracy:P2}");
        Assert.True(accuracy >= 0.85, $"Точность модели {accuracy:P2} ниже порога 85%");
    }

    [Fact]
    public void ForeignSurnamesGoThroughRules()
    {
        // Кириллические фамилии даже вне словаря корпуса покрыты суффиксными правилами:
        // согласная + «а» (муж.), финальная гласная — без изменений.
        using var padezh = new NeuralPadezh();
        Assert.Equal("Шварценеггера", padezh.DeclineLastName("Шварценеггер", Case.Genitive, Gender.Male));
        Assert.Equal("Мбаппе", padezh.DeclineLastName("Мбаппе", Case.Genitive, Gender.Male));
        Assert.Equal("Шварценеггер", padezh.DeclineLastName("Шварценеггер", Case.Nominative));
    }

    [Fact]
    public void DeterministicOutput()
    {
        using var padezh = new NeuralPadezh();
        var a1 = padezh.DeclineLastName("Фрейндлих", Case.Instrumental, Gender.Male);
        var a2 = padezh.DeclineLastName("Фрейндлих", Case.Instrumental, Gender.Male);
        Assert.Equal(a1, a2);
    }

    [Fact]
    public void CaseRestoration()
    {
        // Регистр восстанавливается по входу на прямом пути модели (DeclineRaw).
        using var padezh = new NeuralPadezh();
        var (lemma, gender, caseIdx, form) = LoadDemo().First(r => r.CaseIdx != 0);

        var title = padezh.DeclineRaw(char.ToUpperInvariant(lemma[0]) + lemma.Substring(1), (Case)caseIdx, (Gender)gender);
        Assert.StartsWith(char.ToUpperInvariant(form[0]).ToString(), title);

        var upper = padezh.DeclineRaw(lemma.ToUpperInvariant(), (Case)caseIdx, (Gender)gender);
        Assert.Equal(form.ToUpperInvariant(), upper);
    }
}
