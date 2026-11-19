using MIA.Padezh;

namespace MIA.Padezh.Tests;

/// <summary>Церковно-славянский звательный падеж (v1.3).</summary>
public class ChurchVocativeTests
{
    [Theory]
    [InlineData("Пётр", "Пётре")]
    [InlineData("Иван", "Иване")]
    [InlineData("Александр", "Александре")]
    [InlineData("Николай", "Николае")]
    [InlineData("Андрей", "Андрее")]
    [InlineData("Игорь", "Игоре")]
    [InlineData("Дмитрий", "Дмитрие")]
    [InlineData("Данила", "Данило")]
    [InlineData("Никита", "Никито")]
    [InlineData("Илья", "Илье")]
    public void MaleNames(string name, string expected)
        => Assert.Equal(expected, Padezh.Decline(NamePart.FirstName, name, Case.Vocative, Gender.Male, VocativeStyle.Church));

    [Theory]
    [InlineData("Анна", "Анно")]
    [InlineData("Ольга", "Ольго")]
    [InlineData("Вера", "Веро")]
    [InlineData("Надежда", "Надеждо")]
    [InlineData("Мария", "Марие")]
    [InlineData("София", "Софие")]
    [InlineData("Зоя", "Зое")]
    [InlineData("Любовь", "Любови")]
    [InlineData("Дарья", "Дарьо")]
    public void FemaleNames(string name, string expected)
        => Assert.Equal(expected, Padezh.Decline(NamePart.FirstName, name, Case.Vocative, Gender.Female, VocativeStyle.Church));

    [Fact]
    public void AndrogynousFollowsMaleModel()
        => Assert.Equal("Сашо", Padezh.Decline(NamePart.FirstName, "Саша", Case.Vocative, Gender.Androgynous, VocativeStyle.Church));

    [Fact]
    public void ColloquialStyleIsDefault()
    {
        Assert.Equal("Пётр", Padezh.Decline(NamePart.FirstName, "Пётр", Case.Vocative, Gender.Male, VocativeStyle.Colloquial));
        Assert.Equal("Саш", Padezh.Decline(NamePart.FirstName, "Саша", Case.Vocative, Gender.Male, VocativeStyle.Colloquial));
    }

    [Fact]
    public void RuNameChurchVocative()
    {
        // Только имя склоняется в звательном; фамилия и отчество — без изменений
        var name = RuName.Parse("Иванов Пётр Сергеевич");
        Assert.Equal("Иванов Пётре Сергеевич", name.Decline(Case.Vocative, VocativeStyle.Church));
        Assert.Equal("Иванов Пётр Сергеевич", name.Decline(Case.Vocative));
    }

    [Fact]
    public void SurnameVocativeUnchangedInBothStyles()
    {
        Assert.Equal("Иванов", Padezh.Decline(NamePart.LastName, "Иванов", Case.Vocative, Gender.Male, VocativeStyle.Church));
        Assert.Equal("Иванович", Padezh.Decline(NamePart.MiddleName, "Иванович", Case.Vocative, Gender.Male, VocativeStyle.Church));
    }
}

/// <summary>Множественное число имён и отчеств (v1.3).</summary>
public class PluralNamesTests
{
    [Theory]
    [InlineData("Иван", Gender.Male, Case.Nominative, "Иваны")]
    [InlineData("Иван", Gender.Male, Case.Genitive, "Иванов")]
    [InlineData("Иван", Gender.Male, Case.Dative, "Иванам")]
    [InlineData("Иван", Gender.Male, Case.Instrumental, "Иванами")]
    [InlineData("Пётр", Gender.Male, Case.Nominative, "Пётры")]
    [InlineData("Виктор", Gender.Male, Case.Instrumental, "Викторами")]
    public void MaleHardConsonant(string name, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.DeclinePlural(NamePart.FirstName, name, @case, gender));

    [Theory]
    [InlineData("Николай", Gender.Male, Case.Nominative, "Николаи")]
    [InlineData("Николай", Gender.Male, Case.Genitive, "Николаев")]
    [InlineData("Николай", Gender.Male, Case.Dative, "Николаям")]
    [InlineData("Игорь", Gender.Male, Case.Nominative, "Игори")]
    [InlineData("Игорь", Gender.Male, Case.Genitive, "Игорей")]
    public void MaleSoftEnding(string name, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.DeclinePlural(NamePart.FirstName, name, @case, gender));

    [Theory]
    [InlineData("Анна", Gender.Female, Case.Nominative, "Анны")]
    [InlineData("Анна", Gender.Female, Case.Genitive, "Анн")]
    [InlineData("Анна", Gender.Female, Case.Instrumental, "Аннами")]
    [InlineData("Вера", Gender.Female, Case.Genitive, "Вер")]
    [InlineData("Ольга", Gender.Female, Case.Dative, "Ольгам")]
    public void FemaleHardA(string name, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.DeclinePlural(NamePart.FirstName, name, @case, gender));

    [Theory]
    [InlineData("Мария", Gender.Female, Case.Nominative, "Марии")]
    [InlineData("Мария", Gender.Female, Case.Genitive, "Марий")]
    [InlineData("Мария", Gender.Female, Case.Dative, "Мариям")]
    [InlineData("Мария", Gender.Female, Case.Instrumental, "Мариями")]
    [InlineData("Оля", Gender.Female, Case.Nominative, "Оли")]
    [InlineData("Оля", Gender.Female, Case.Genitive, "Оль")]
    [InlineData("Дарья", Gender.Female, Case.Genitive, "Дарь")]
    public void FemaleYa(string name, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.DeclinePlural(NamePart.FirstName, name, @case, gender));

    [Theory]
    [InlineData("Иванович", Gender.Male, Case.Nominative, "Ивановичи")]
    [InlineData("Иванович", Gender.Male, Case.Genitive, "Ивановичей")]
    [InlineData("Иванович", Gender.Male, Case.Dative, "Ивановичам")]
    [InlineData("Иванович", Gender.Male, Case.Instrumental, "Ивановичами")]
    public void MalePatronymicPlural(string name, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.DeclinePlural(NamePart.MiddleName, name, @case, gender));

    [Theory]
    [InlineData("Ивановна", Gender.Female, Case.Nominative, "Ивановны")]
    [InlineData("Ивановна", Gender.Female, Case.Genitive, "Ивановн")]
    [InlineData("Ивановна", Gender.Female, Case.Dative, "Ивановнам")]
    [InlineData("Ивановна", Gender.Female, Case.Instrumental, "Ивановнами")]
    public void FemalePatronymicPlural(string name, Gender gender, Case @case, string expected)
        => Assert.Equal(expected, Padezh.DeclinePlural(NamePart.MiddleName, name, @case, gender));

    [Fact]
    public void UnchangeableNamesPlural()
    {
        // Неизменяемые формы (конечная гласная и т.п.) — без изменений
        Assert.Equal("Левко", Padezh.DeclinePlural(NamePart.FirstName, "Левко", Case.Genitive, Gender.Male));
    }

    [Fact]
    public void RuNamePluralIsSurnameOnly()
    {
        // RuName.DeclinePlural — форма «семья» (только фамилия)
        var name = RuName.Parse("Иванов Иван Иванович");
        Assert.Equal("Ивановы", name.DeclinePlural(Case.Nominative));
    }
}
