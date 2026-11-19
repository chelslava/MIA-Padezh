namespace MIA.Padezh;

/// <summary>Падежи русского языка.</summary>
public enum Case
{
    /// <summary>Именительный (кто? что?).</summary>
    Nominative,
    /// <summary>Родительный (кого? чего?).</summary>
    Genitive,
    /// <summary>Дательный (кому? чему?).</summary>
    Dative,
    /// <summary>Винительный (кого? что?).</summary>
    Accusative,
    /// <summary>Творительный (кем? чем?).</summary>
    Instrumental,
    /// <summary>Предложный (о ком? о чём?).</summary>
    Prepositional,
    /// <summary>Звательный — современный разговорный (Ваня → Вань, Саша → Саш);
    /// применяется к именам, остальные части без изменений.</summary>
    Vocative,
}
