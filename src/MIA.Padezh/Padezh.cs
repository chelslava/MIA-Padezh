namespace MIA.Padezh;

/// <summary>
/// Основная точка входа: склонение одной части ФИО в падеж.
/// Для склонения ФИО целиком см. <see cref="RuName"/>.
/// </summary>
public static class Padezh
{
    /// <summary>Склоняет часть ФИО в падеж. Если род не задан — определяется автоматически.</summary>
    /// <param name="part">Часть ФИО (фамилия, имя или отчество).</param>
    /// <param name="value">Слово в именительном падеже, например «Иванов».</param>
    /// <param name="case">Целевой падеж.</param>
    /// <param name="gender">Подсказка рода; полезна для неоднозначных слов (Саша, Женя).</param>
    /// <param name="vocativeStyle">Стиль звательного падежа (для <see cref="Case.Vocative"/>).</param>
    /// <returns>Слово в целевом падеже; для им. падежа возвращается как есть.</returns>
    public static string Decline(NamePart part, string value, Case @case, Gender? gender = null, VocativeStyle vocativeStyle = VocativeStyle.Colloquial)
    {
        var resolvedGender = gender ?? GenderDetector.DetectPart(part, value);
        return DeclensionEngine.Inflect(value, @case, part, resolvedGender, vocativeStyle);
    }

    /// <summary>Определяет род части ФИО по правилам.</summary>
    public static Gender DetectGender(NamePart part, string value)
        => GenderDetector.DetectPart(part, value);

    /// <summary>
    /// Множественное число части ФИО: «Иванов» → им. «Ивановы», род. «Ивановых»;
    /// «Иван» → «Ивановы», «Иванович» → «Ивановичей», «Ивановна» → «Ивановн».
    /// Неизменяемые формы (Шевченко, Черных, Бильжо) остаются без изменений.
    /// </summary>
    /// <param name="value">Слово в единственном числе, именительный падеж.</param>
    /// <param name="case">Целевой падеж множественного числа.</param>
    /// <param name="gender">Подсказка рода; если не задан — определяется автоматически
    /// (женские формы -ова/-ина распознаются сами).</param>
    public static string DeclinePlural(NamePart part, string value, Case @case, Gender? gender = null)
    {
        var resolvedGender = gender ?? GenderDetector.DetectPart(part, value);
        return DeclensionEngine.InflectPlural(value, @case, part, resolvedGender);
    }
}
