using System;
using System.Collections.Generic;
using MIA.Padezh.Generated;

namespace MIA.Padezh;

/// <summary>
/// Определение рода по правилам gender.json (сгенерированы в RulesTable).
/// Логика портирована с NPetrovich GenderDeterminator.
/// </summary>
internal static class GenderDetector
{
    /// <summary>Определяет род ФИО целиком. Отчество приоритетно; конфликт имени и фамилии даёт Androgynous.</summary>
    public static Gender Detect(string? firstName, string? lastName, string? middleName)
    {
        Gender firstNameGender = DetectPart(NamePart.FirstName, firstName);
        Gender lastNameGender = DetectPart(NamePart.LastName, lastName);
        Gender middleNameGender = DetectPart(NamePart.MiddleName, middleName);

        // Отчество первично.
        if (middleNameGender != Gender.Androgynous)
            return middleNameGender;

        Gender[] others = { firstNameGender, lastNameGender };
        int definiteCount = 0;
        Gender definite = Gender.Androgynous;
        for (int i = 0; i < others.Length; i++)
        {
            if (others[i] == Gender.Androgynous)
                continue;
            if (definiteCount == 0)
                definite = others[i];
            definiteCount++;
        }

        if (definiteCount > 1)
            return Gender.Androgynous; // Имя и фамилия противоречат друг другу.

        return definite;
    }

    /// <summary>Определяет род одной части ФИО. Части дефисных слов проверяются по очереди,
    /// последнее однозначное совпадение побеждает.</summary>
    public static Gender DetectPart(NamePart part, string? value)
    {
        var chunks = value?.Trim().Split('-');
        if (chunks is null || chunks.Length == 0)
            return Gender.Androgynous;

        bool hasContent = false;
        for (int i = 0; i < chunks.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(chunks[i]))
                hasContent = true;
        }

        if (!hasContent)
            return Gender.Androgynous;

        Gender? gender = MatchGender(chunks, RulesTable.GenderExceptions(part), wholeWord: true);
        if (gender.HasValue)
            return gender.Value;

        if (part == NamePart.FirstName)
        {
            gender = LookupFirstNameDictionary(chunks);
            if (gender.HasValue)
                return gender.Value;
        }

        gender = MatchGender(chunks, RulesTable.GenderSuffixes(part), wholeWord: false);
        return gender ?? Gender.Androgynous;
    }

    /// <summary>
    /// Словарь пола имён (~1,8 тыс. имён из корпуса petrovich-eval, детект которых
    /// невозможен суффиксными правилами: АГИДЕЛЬ, ЕРМЕК, ДЖУДИТ…). Точное совпадение слова.
    /// </summary>
    private static Gender? LookupFirstNameDictionary(string[] chunks)
    {
        Gender? result = null;
        for (int c = 0; c < chunks.Length; c++)
        {
            var lowered = chunks[c].ToLowerInvariant();
            Gender? match = null;
            if (Array.BinarySearch(RulesTable.GenderNameMale, lowered, StringComparer.Ordinal) >= 0)
                match = Gender.Male;
            else if (Array.BinarySearch(RulesTable.GenderNameFemale, lowered, StringComparer.Ordinal) >= 0)
                match = Gender.Female;
            else if (Array.BinarySearch(RulesTable.GenderNameAndrogynous, lowered, StringComparer.Ordinal) >= 0)
                match = Gender.Androgynous;

            if (match.HasValue)
                result = match.Value;
        }

        return result;
    }

    /// <summary>Кандидат должен совпасть ровно с одним родом, иначе совпадение считается неоднозначным.</summary>
    private static Gender? MatchGender(string[] chunks, string[][]? perGender, bool wholeWord)
    {
        if (perGender is null)
            return null;

        Gender? result = null;
        for (int c = 0; c < chunks.Length; c++)
        {
            var lowered = chunks[c].ToLowerInvariant();
            bool male = MatchesAny(lowered, perGender[(int)Gender.Male], wholeWord);
            bool female = MatchesAny(lowered, perGender[(int)Gender.Female], wholeWord);
            bool androgynous = MatchesAny(lowered, perGender[(int)Gender.Androgynous], wholeWord);

            int matches = (male ? 1 : 0) + (female ? 1 : 0) + (androgynous ? 1 : 0);
            if (matches == 1)
                result = male ? Gender.Male : female ? Gender.Female : Gender.Androgynous;
        }

        return result;
    }

    private static bool MatchesAny(string lowered, string[] patterns, bool wholeWord)
    {
        if (patterns is null || patterns.Length == 0)
            return false;

        for (int i = 0; i < patterns.Length; i++)
        {
            var pattern = patterns[i];
            if (wholeWord)
            {
                if (lowered == pattern)
                    return true;
            }
            else
            {
                int start = lowered.Length - pattern.Length;
                if (start >= 0 && string.CompareOrdinal(lowered, start, pattern, 0, pattern.Length) == 0)
                    return true;
            }
        }

        return false;
    }
}
