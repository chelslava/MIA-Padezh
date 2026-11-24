using System;
using System.Collections.Generic;

namespace MIA.Padezh;

/// <summary>
/// Русские ФИО с определением рода и склонением всех частей.
/// </summary>
/// <example>
/// <code>
/// var name = RuName.Parse("Иванов Пётр Сергеевич");
/// name.Decline(Case.Dative); // "Иванову Петру Сергеевичу"
/// </code>
/// </example>
public sealed class RuName
{
    /// <summary>Создаёт ФИО из частей. Все части необязательны.</summary>
    public RuName(string? lastName = null, string? firstName = null, string? middleName = null, Gender? genderHint = null)
    {
        LastName = lastName?.Trim();
        FirstName = firstName?.Trim();
        MiddleName = middleName?.Trim();
        GenderHint = genderHint;
    }

    /// <summary>Фамилия в именительном падеже.</summary>
    public string? LastName { get; }

    /// <summary>Имя в именительном падеже.</summary>
    public string? FirstName { get; }

    /// <summary>Отчество в именительном падеже.</summary>
    public string? MiddleName { get; }

    /// <summary>Подсказка рода. Если задана — используется вместо автодетекта.</summary>
    public Gender? GenderHint { get; }

    /// <summary>Разбирает строку формата «Фамилия Имя Отчество» (отчество необязательно).</summary>
    /// <exception cref="FormatException">Больше трёх слов или пустая строка.</exception>
    public static RuName Parse(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Строка ФИО пуста.", nameof(fullName));

        var tokens = fullName.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        switch (tokens.Length)
        {
            case 1: return new RuName(lastName: tokens[0]);
            case 2: return new RuName(lastName: tokens[0], firstName: tokens[1]);
            case 3: return new RuName(lastName: tokens[0], firstName: tokens[1], middleName: tokens[2]);
            default:
                throw new FormatException("Ожидается от 1 до 3 слов в формате «Фамилия Имя Отчество».");
        }
    }

    /// <summary>Определяет род ФИО (подсказка приоритетна, затем автодетект по правилам).</summary>
    public Gender DetectGender()
        => GenderHint ?? GenderDetector.Detect(FirstName, LastName, MiddleName);

    /// <summary>Склоняет все заданные части, возвращает строку «Фамилия Имя Отчество» в целевом падеже.</summary>
    /// <param name="case">Целевой падеж.</param>
    /// <param name="vocativeStyle">Стиль звательного падежа (учитывается только для имени).</param>
    public string Decline(Case @case, VocativeStyle vocativeStyle = VocativeStyle.Colloquial)
    {
        var gender = DetectGender();
        var parts = new string[3];
        int count = 0;

        if (!string.IsNullOrEmpty(LastName))
            parts[count++] = DeclensionEngine.Inflect(LastName!, @case, NamePart.LastName, gender, vocativeStyle);
        if (!string.IsNullOrEmpty(FirstName))
            parts[count++] = DeclensionEngine.Inflect(FirstName!, @case, NamePart.FirstName, gender, vocativeStyle);
        if (!string.IsNullOrEmpty(MiddleName))
            parts[count++] = DeclensionEngine.Inflect(MiddleName!, @case, NamePart.MiddleName, gender, vocativeStyle);

        return string.Join(" ", parts, 0, count);
    }

    /// <summary>Склоняет одну часть ФИО. Пустая часть даёт пустую строку.</summary>
    public string DeclinePart(NamePart part, Case @case, VocativeStyle vocativeStyle = VocativeStyle.Colloquial)
    {
        var value = part switch
        {
            NamePart.FirstName => FirstName,
            NamePart.LastName => LastName,
            NamePart.MiddleName => MiddleName,
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, null),
        };

        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return DeclensionEngine.Inflect(value!, @case, part, DetectGender(), vocativeStyle);
    }

    /// <summary>
    /// Фамилия во множественном числе («семья Ивановых»): им. «Ивановы», род. «Ивановых».
    /// Возвращает пустую строку, если фамилия не задана.
    /// </summary>
    public string DeclinePlural(Case @case)
    {
        if (string.IsNullOrEmpty(LastName))
            return string.Empty;

        return DeclensionEngine.InflectPlural(LastName!, @case, NamePart.LastName, DetectGender());
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var parts = new string[3];
        int count = 0;
        if (!string.IsNullOrEmpty(LastName)) parts[count++] = LastName!;
        if (!string.IsNullOrEmpty(FirstName)) parts[count++] = FirstName!;
        if (!string.IsNullOrEmpty(MiddleName)) parts[count++] = MiddleName!;
        return string.Join(" ", parts, 0, count);
    }
}
