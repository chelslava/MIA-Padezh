using MIA.Padezh;

namespace MIA.Padezh.Tests;

/// <summary>Определение рода по правилам (gender.json).</summary>
public class GenderTests
{
    [Theory]
    [InlineData(NamePart.MiddleName, "Иванович", Gender.Male)]
    [InlineData(NamePart.MiddleName, "Ивановна", Gender.Female)]
    [InlineData(NamePart.LastName, "Иванов", Gender.Male)]
    [InlineData(NamePart.LastName, "Смирнова", Gender.Female)]
    [InlineData(NamePart.LastName, "Шевченко", Gender.Androgynous)]
    [InlineData(NamePart.LastName, "Грин", Gender.Androgynous)]
    [InlineData(NamePart.FirstName, "Иван", Gender.Male)]
    [InlineData(NamePart.FirstName, "Анна", Gender.Female)]
    [InlineData(NamePart.FirstName, "Саша", Gender.Androgynous)]
    [InlineData(NamePart.FirstName, "Женя", Gender.Androgynous)]
    public void DetectPart(NamePart part, string value, Gender expected)
        => Assert.Equal(expected, Padezh.DetectGender(part, value));

    [Theory]
    [InlineData("Иванов", "Иван", "Иванович", Gender.Male)]
    [InlineData("Смирнова", "Анна", "Ивановна", Gender.Female)]
    [InlineData("Иванов", "Саша", null, Gender.Male)]
    [InlineData("Иванов", "Анна", null, Gender.Androgynous)]
    public void DetectCombined(string lastName, string firstName, string? middleName, Gender expected)
    {
        var name = new RuName(lastName: lastName, firstName: firstName, middleName: middleName);
        Assert.Equal(expected, name.DetectGender());
    }

    [Fact]
    public void MiddleNameTakesPriority()
    {
        // Отчество однозначно задаёт род даже при неоднозначном имени.
        var name = new RuName(lastName: "Грин", firstName: "Саша", middleName: "Иванович");
        Assert.Equal(Gender.Male, name.DetectGender());
    }
}
