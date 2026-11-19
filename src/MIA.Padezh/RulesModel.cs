namespace MIA.Padezh;

/// <summary>
/// Правило склонения: по каким суффиксам (или целым формам) срабатывает
/// и какие модификаторы применяются для каждого падежа.
/// </summary>
internal readonly struct RuleEntry
{
    public RuleEntry(string[] test, string[] mods, string[]? tags, Gender gender)
    {
        Test = test;
        Mods = mods;
        Tags = tags;
        Gender = gender;
    }

    /// <summary>Суффиксы для сопоставления (или целые слова — для исключений). В нижнем регистре.</summary>
    public string[] Test { get; }

    /// <summary>Модификаторы по падежам: [Род., Дат., Вин., Твор., Предл.].
    /// Символ '-' — отрезать одну букву с конца; '.' — не менять; остальные символы дописываются.</summary>
    public string[] Mods { get; }

    /// <summary>Фичи правила (поддерживается "first_word" для первой части дефисного слова); null — фич нет.</summary>
    public string[]? Tags { get; }

    /// <summary>Род правила.</summary>
    public Gender Gender { get; }
}

/// <summary>
/// Правило образования множественного числа фамилии. Отсчитывается от формы
/// единственного числа (мужской или женской); 6 модификаторов по падежам.
/// </summary>
internal readonly struct PluralEntry
{
    public PluralEntry(string[] test, string[] mods, Gender gender)
    {
        Test = test;
        Mods = mods;
        Gender = gender;
    }

    /// <summary>Суффиксы для сопоставления (в нижнем регистре).</summary>
    public string[] Test { get; }

    /// <summary>Модификаторы по падежам: [Им., Род., Дат., Вин., Твор., Предл.].</summary>
    public string[] Mods { get; }

    /// <summary>Род правила (мужские и андрогинные фамилии склоняются одинаково).</summary>
    public Gender Gender { get; }
}
