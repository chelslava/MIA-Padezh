using MIA.Padezh;

namespace MIA.Padezh.Tests;

/// <summary>Золотые кейсы, перенесённые из тестовых данных NPetrovich (Data/*.csv).</summary>
public class GoldenTests
{
    [Theory]
    [InlineData("Иванов", Gender.Male, Case.Genitive, "Иванова")]
    [InlineData("Иванов", Gender.Male, Case.Dative, "Иванову")]
    [InlineData("Иванов", Gender.Male, Case.Accusative, "Иванова")]
    [InlineData("Иванов", Gender.Male, Case.Instrumental, "Ивановым")]
    [InlineData("Иванов", Gender.Male, Case.Prepositional, "Иванове")]
    [InlineData("Бильжо", Gender.Male, Case.Dative, "Бильжо")]
    [InlineData("Ничипорук", Gender.Male, Case.Dative, "Ничипоруку")]
    [InlineData("Щусь", Gender.Male, Case.Dative, "Щусю")]
    [InlineData("Фидря", Gender.Male, Case.Dative, "Фидре")]
    [InlineData("Белоконь", Gender.Male, Case.Dative, "Белоконю")]
    [InlineData("Добробаба", Gender.Male, Case.Dative, "Добробабе")]
    public void LastNameGolden(string word, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.Decline(NamePart.LastName, word, @case, gender));

    [Theory]
    [InlineData("Иван", Gender.Male, Case.Genitive, "Ивана")]
    [InlineData("Иван", Gender.Male, Case.Dative, "Ивану")]
    [InlineData("Иван", Gender.Male, Case.Accusative, "Ивана")]
    [InlineData("Иван", Gender.Male, Case.Instrumental, "Иваном")]
    [InlineData("Иван", Gender.Male, Case.Prepositional, "Иване")]
    public void FirstNameGolden(string word, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.Decline(NamePart.FirstName, word, @case, gender));

    [Theory]
    [InlineData("Иванович", Gender.Male, Case.Genitive, "Ивановича")]
    [InlineData("Иванович", Gender.Male, Case.Dative, "Ивановичу")]
    [InlineData("Иванович", Gender.Male, Case.Accusative, "Ивановича")]
    [InlineData("Иванович", Gender.Male, Case.Instrumental, "Ивановичем")]
    [InlineData("Иванович", Gender.Male, Case.Prepositional, "Ивановиче")]
    [InlineData("Ивановна", Gender.Female, Case.Dative, "Ивановне")]
    public void MiddleNameGolden(string word, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.Decline(NamePart.MiddleName, word, @case, gender));
}

/// <summary>Поведение RuName: разбор, склонение ФИО целиком, подсказка рода.</summary>
public class RuNameTests
{
    [Fact]
    public void FullFioDative()
    {
        var name = RuName.Parse("Иванов Пётр Сергеевич");
        Assert.Equal("Иванову Петру Сергеевичу", name.Decline(Case.Dative));
    }

    [Fact]
    public void TwoPartsGenitive()
    {
        var name = RuName.Parse("Иванов Иван");
        Assert.Equal("Иванова Ивана", name.Decline(Case.Genitive));
    }

    [Fact]
    public void SingleWord()
    {
        var name = RuName.Parse("Иванов");
        Assert.Equal("Иванову", name.Decline(Case.Dative));
    }

    [Fact]
    public void NominativeIsIdentity()
    {
        var name = RuName.Parse("Иванов Пётр Сергеевич");
        Assert.Equal("Иванов Пётр Сергеевич", name.Decline(Case.Nominative));
    }

    [Fact]
    public void TooManyWordsThrows()
        => Assert.Throws<FormatException>(() => RuName.Parse("Иванов Иван Иванович иван"));

    [Fact]
    public void GenderHintOverridesDetection()
    {
        var name = new RuName(lastName: "Иванов", firstName: "Саша", genderHint: Gender.Male);
        Assert.Equal(Gender.Male, name.DetectGender());
    }

    [Fact]
    public void DeclinePartWorks()
    {
        var name = RuName.Parse("Иванов Пётр Сергеевич");
        Assert.Equal("Иванову", name.DeclinePart(NamePart.LastName, Case.Dative));
        Assert.Equal("Петру", name.DeclinePart(NamePart.FirstName, Case.Dative));
        Assert.Equal("Сергеевичу", name.DeclinePart(NamePart.MiddleName, Case.Dative));
    }

    [Fact]
    public void MissingPartGivesEmpty()
    {
        var name = new RuName(lastName: "Иванов");
        Assert.Equal(string.Empty, name.DeclinePart(NamePart.FirstName, Case.Dative));
    }
}
