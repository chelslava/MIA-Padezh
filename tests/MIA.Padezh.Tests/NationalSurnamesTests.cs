using MIA.Padezh;

namespace MIA.Padezh.Tests;

/// <summary>Фамилии стран СНГ и национальные классы (v1.2): единственное и множественное число.</summary>
public class NationalSurnamesTests
{
    // --- Армянские: мужские склоняются, женские формы (-ян у женщин) не склоняются ---
    [Theory]
    [InlineData("Папазян", Gender.Male, Case.Genitive, "Папазяна")]
    [InlineData("Папазян", Gender.Male, Case.Instrumental, "Папазяном")]
    [InlineData("Папазян", Gender.Female, Case.Genitive, "Папазян")]
    [InlineData("Абрамян", Gender.Male, Case.Dative, "Абрамяну")]
    [InlineData("Киркоров" /* контроль */, Gender.Male, Case.Genitive, "Киркорова")]
    public void ArmenianSingular(string word, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.Decline(NamePart.LastName, word, @case, gender));

    [Theory]
    [InlineData("Папазян", Gender.Male, Case.Nominative, "Папазяны")]
    [InlineData("Папазян", Gender.Male, Case.Genitive, "Папазянов")]
    [InlineData("Папазян", Gender.Male, Case.Dative, "Папазянам")]
    [InlineData("Абрамян", Gender.Male, Case.Genitive, "Абрамянов")]
    public void ArmenianPlural(string word, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.DeclinePlural(NamePart.LastName, word, @case, gender));

    // --- Грузинские: -швили/-дзе не склоняются вовсе ---
    [Theory]
    [InlineData("Гогебашвили", Gender.Male, Case.Genitive, "Гогебашвили")]
    [InlineData("Гогебашвили", Gender.Male, Case.Instrumental, "Гогебашвили")]
    [InlineData("Шеварднадзе", Gender.Male, Case.Genitive, "Шеварднадзе")]
    public void GeorgianUnchangeable(string word, Gender gender, Case @case, string expected)
    {
        Assert.Equal(expected, Padezh.Decline(NamePart.LastName, word, @case, gender));
        Assert.Equal(word, Padezh.DeclinePlural(NamePart.LastName, word, @case, gender));
    }

    // --- Украинские: -енко мужские не склоняются (норма корпуса), женские тоже ---
    [Theory]
    [InlineData("Шевченко", Gender.Male, Case.Genitive, "Шевченко")]
    [InlineData("Шевченко", Gender.Female, Case.Dative, "Шевченко")]
    [InlineData("Мельник", Gender.Male, Case.Dative, "Мельнику")]
    public void UkrainianSingular(string word, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.Decline(NamePart.LastName, word, @case, gender));

    [Theory]
    [InlineData("Мельник", Gender.Male, Case.Nominative, "Мельники")]
    [InlineData("Мельник", Gender.Male, Case.Genitive, "Мельников")]
    [InlineData("Абарчук", Gender.Male, Case.Nominative, "Абарчуки")]
    [InlineData("Абарчук", Gender.Male, Case.Genitive, "Абарчуков")]
    [InlineData("Гончар", Gender.Male, Case.Nominative, "Гончары")]
    public void UkrainianPlural(string word, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.DeclinePlural(NamePart.LastName, word, @case, gender));

    // --- Балтийские: -ис/-ас/-ус мужские склоняются ---
    [Theory]
    [InlineData("Вайткус", Gender.Male, false, Case.Dative, "Вайткусу")]
    [InlineData("Лапинас", Gender.Male, false, Case.Genitive, "Лапинаса")]
    [InlineData("Вайткус", Gender.Male, true, Case.Nominative, "Вайткусы")]
    [InlineData("Вайткус", Gender.Male, true, Case.Genitive, "Вайткусов")]
    public void Baltic(string word, Gender gender, bool plural, Case @case, string expected)
    {
        var actual = plural
            ? Padezh.DeclinePlural(NamePart.LastName, word, @case, gender)
            : Padezh.Decline(NamePart.LastName, word, @case, gender);
        Assert.Equal(expected, actual);
    }

    // --- Татарские/тюркские: -ин продуктивный класс ---
    [Theory]
    [InlineData("Тухватулин", Gender.Male, false, Case.Genitive, "Тухватулина")]
    [InlineData("Тухватулин", Gender.Male, true, Case.Nominative, "Тухватулины")]
    public void Tatar(string word, Gender gender, bool plural, Case @case, string expected)
    {
        var actual = plural
            ? Padezh.DeclinePlural(NamePart.LastName, word, @case, gender)
            : Padezh.Decline(NamePart.LastName, word, @case, gender);
        Assert.Equal(expected, actual);
    }

    // --- Азербайджанские/казахские оглы/кызы/улы/уулу: не склоняются ---
    [Theory]
    [InlineData("Гусейноглы", Gender.Male, Case.Genitive, "Гусейноглы")]
    [InlineData("Асланкызы", Gender.Female, Case.Dative, "Асланкызы")]
    public void OglyKizi(string word, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.Decline(NamePart.LastName, word, @case, gender));

    [Fact]
    public void HyphenatedWithUulu()
    {
        // По норме двойных фамилий склоняется первая часть
        var name = new RuName(lastName: "Абдылдаев-Уулу", genderHint: Gender.Male);
        Assert.Equal("Абдылдаева-Уулу", name.DeclinePart(NamePart.LastName, Case.Genitive));
    }
}
