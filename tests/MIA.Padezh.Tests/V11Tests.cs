using MIA.Padezh;

namespace MIA.Padezh.Tests;

/// <summary>Множественное число фамилий (v1.1).</summary>
public class PluralTests
{
    [Theory]
    [InlineData("Иванов", Gender.Male, Case.Nominative, "Ивановы")]
    [InlineData("Иванов", Gender.Male, Case.Genitive, "Ивановых")]
    [InlineData("Иванов", Gender.Male, Case.Dative, "Ивановым")]
    [InlineData("Иванов", Gender.Male, Case.Accusative, "Ивановых")]
    [InlineData("Иванов", Gender.Male, Case.Instrumental, "Ивановыми")]
    [InlineData("Иванов", Gender.Male, Case.Prepositional, "Ивановых")]
    [InlineData("Иванова", Gender.Female, Case.Nominative, "Ивановы")]
    [InlineData("Иванова", Gender.Female, Case.Genitive, "Ивановых")]
    [InlineData("Иванова", Gender.Female, Case.Instrumental, "Ивановыми")]
    [InlineData("Кузьмина", Gender.Female, Case.Genitive, "Кузьминых")]
    public void ProductiveOvIn(string word, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.DeclinePlural(NamePart.LastName, word, @case, gender));

    [Theory]
    [InlineData("Толстой", Gender.Male, Case.Nominative, "Толстые")]
    [InlineData("Толстой", Gender.Male, Case.Genitive, "Толстых")]
    [InlineData("Толстой", Gender.Male, Case.Instrumental, "Толстыми")]
    [InlineData("Толстая", Gender.Female, Case.Nominative, "Толстые")]
    [InlineData("Толстая", Gender.Female, Case.Genitive, "Толстых")]
    [InlineData("Скворцовский", Gender.Male, Case.Nominative, "Скворцовские")]
    [InlineData("Скворцовский", Gender.Male, Case.Genitive, "Скворцовских")]
    [InlineData("Скворцовский", Gender.Male, Case.Dative, "Скворцовским")]
    [InlineData("Ильинская", Gender.Female, Case.Nominative, "Ильинские")]
    public void AdjectiveType(string word, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.DeclinePlural(NamePart.LastName, word, @case, gender));

    [Theory]
    [InlineData("Шевченко", null, Case.Genitive, "Шевченко")]
    [InlineData("Черных", null, Case.Genitive, "Черных")]
    [InlineData("Бильжо", null, Case.Instrumental, "Бильжо")]
    [InlineData("Данелия", Gender.Female, Case.Genitive, "Данелия")]
    [InlineData("Кучма", Gender.Female, Case.Genitive, "Кучма")]
    public void Unchangeable(string word, Gender? gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.DeclinePlural(NamePart.LastName, word, @case, gender));

    [Theory]
    [InlineData("Шмидт", Gender.Male, Case.Nominative, "Шмидты")]
    [InlineData("Шмидт", Gender.Male, Case.Genitive, "Шмидтов")]
    [InlineData("Шмидт", Gender.Male, Case.Dative, "Шмидтам")]
    [InlineData("Шмидт", Gender.Male, Case.Instrumental, "Шмидтами")]
    [InlineData("Кузнец", Gender.Male, Case.Nominative, "Кузнецы")]
    [InlineData("Кузнец", Gender.Male, Case.Genitive, "Кузнецов")]
    [InlineData("Кузнец", Gender.Male, Case.Instrumental, "Кузнецами")]
    [InlineData("Кашенок", Gender.Male, Case.Nominative, "Кашенки")]
    [InlineData("Кашенок", Gender.Male, Case.Genitive, "Кашенков")]
    [InlineData("Бабаец", Gender.Male, Case.Nominative, "Бабайцы")]
    [InlineData("Бабаец", Gender.Male, Case.Genitive, "Бабайцов")]
    public void ConsonantType(string word, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.DeclinePlural(NamePart.LastName, word, @case, gender));

    [Fact]
    public void RuNamePlural()
    {
        var name = RuName.Parse("Иванов Пётр Сергеевич");
        Assert.Equal("Ивановы", name.DeclinePlural(Case.Nominative));
        Assert.Equal("Ивановых", name.DeclinePlural(Case.Genitive));
    }

    [Fact]
    public void FemaleFormAutoDetect()
    {
        // Женская форма без явного рода: детект по суффиксу -ова даёт Female
        Assert.Equal("Ивановых", Padezh.DeclinePlural(NamePart.LastName, "Иванова", Case.Genitive));
    }
}

/// <summary>Современный разговорный звательный падеж (v1.1).</summary>
public class VocativeTests
{
    [Theory]
    [InlineData("Ваня", "Вань")]
    [InlineData("Миша", "Миш")]
    [InlineData("Саша", "Саш")]
    [InlineData("Оля", "Оль")]
    [InlineData("Катя", "Кать")]
    [InlineData("Наташа", "Наташ")]
    [InlineData("Вера", "Вер")]
    [InlineData("Женя", "Жень")]
    public void TruncatesAVowelEndings(string word, string expected)
        => Assert.Equal(expected, Padezh.Decline(NamePart.FirstName, word, Case.Vocative));

    [Theory]
    [InlineData("Пётр")]
    [InlineData("Игорь")]
    [InlineData("Мария")]
    [InlineData("Зоя")]
    [InlineData("Илья")]
    [InlineData("Иван")]
    public void KeepsConsonantAndVowelStems(string word)
        => Assert.Equal(word, Padezh.Decline(NamePart.FirstName, word, Case.Vocative));

    [Fact]
    public void SurnameAndMiddleNameUnchanged()
    {
        var name = RuName.Parse("Иванов Пётр Сергеевич");
        Assert.Equal("Иванов Пётр Сергеевич", name.Decline(Case.Vocative));
    }

    [Fact]
    public void HyphenatedName()
    {
        Assert.Equal("Анн-Мария", Padezh.Decline(NamePart.FirstName, "Анна-Мария", Case.Vocative));
    }
}

/// <summary>Словарь пола имён (v1.1): детект для имён, непокрываемых суффиксными правилами.</summary>
public class GenderDictionaryTests
{
    [Theory]
    [InlineData("Агидель", Gender.Female)]
    [InlineData("Ермек", Gender.Male)]
    [InlineData("Джудит", Gender.Female)]
    [InlineData("Аннет", Gender.Female)]
    [InlineData("Арлен", Gender.Female)]
    [InlineData("Никита", Gender.Male)]
    public void DictionaryEntries(string name, Gender expected)
        => Assert.Equal(expected, Padezh.DetectGender(NamePart.FirstName, name));

    [Fact]
    public void DictionaryPriorityOverSuffixes()
    {
        // Суффиксные правила не покрывают -ль/-т женских имён; словарь даёт уверенный ответ
        Assert.NotEqual(Gender.Androgynous, Padezh.DetectGender(NamePart.FirstName, "Агидель"));
    }
}
