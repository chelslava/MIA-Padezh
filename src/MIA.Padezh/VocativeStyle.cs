namespace MIA.Padezh;

/// <summary>
/// Стиль звательного падежа.
/// <see cref="Colloquial"/> — современный разговорный (Саша → Саш, Ваня → Вань);
/// <see cref="Church"/> — церковно-славянский (Пётр → Пётре, Николай → Николае,
/// Анна → Анно, Мария → Марие).
/// </summary>
public enum VocativeStyle
{
    /// <summary>Современный разговорный (по умолчанию).</summary>
    Colloquial,
    /// <summary>Церковно-славянский стиль обращений.</summary>
    Church,
}
