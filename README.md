# MIA-Padezh

[![CI](https://github.com/chelslava/MIA-Padezh/actions/workflows/ci.yml/badge.svg)](https://github.com/chelslava/MIA-Padezh/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/MIA.Padezh.svg)](https://www.nuget.org/packages/MIA.Padezh/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Склонение русских ФИО по падежам для .NET. Детерминированное, без внешних зависимостей, правила компилируются в код на этапе сборки.

> **English summary below.**

## Возможности

- 6 падежей для фамилий, имён и отчеств — по отдельности или ФИО целиком
- **Множественное число фамилий**: `DeclinePlural` — «Иванов» → «Ивановых», «Толстой» → «Толстыми», «семья Шевченко»
- **Звательный падеж**: `Case.Vocative` — современный разговорный (Саша → Саш, Ваня → Вань)
  и церковный стиль `VocativeStyle.Church` (Пётр → Пётре, Анна → Анно)
- **Словарь пола имён** (1 765 записей из корпуса): уверенный детект для имён типа Агидель, Ермек, Никита
- Автоопределение рода (отчество → фамилия/имя, конфликты дают `Androgynous`)
- Подсказка рода для неоднозначных имён (Саша, Женя)
- Дефисные фамилии и имена (Римская-Корсакова), неизменяемые фамилии (Шевченко, Бильжо)
- **Zero dependencies**, AOT- и trimming-friendly: правила Petrovich компилируются в C#-код инструментом [RulesGen](tools/MIA.Padezh.RulesGen), в рантайме нет парсинга JSON
- Мультитаргет: `netstandard2.0` + `net8.0` + `net10.0`

## Быстрый старт

```csharp
using MIA.Padezh;

// ФИО целиком
var name = RuName.Parse("Иванов Пётр Сергеевич");
name.Decline(Case.Dative);      // "Иванову Петру Сергеевичу"
name.Decline(Case.Genitive);    // "Иванова Петра Сергеевича"

// Одна часть
Padezh.Decline(NamePart.LastName, "Шевченко", Case.Genitive);      // "Шевченко" (неизменяемая)
Padezh.Decline(NamePart.FirstName, "Анна", Case.Dative);           // "Анне"
Padezh.Decline(NamePart.FirstName, "Саша", Case.Dative, Gender.Male); // "Саше" (подсказка рода)

// Множественное число: фамилии, имена и отчества
Padezh.DeclinePlural(NamePart.LastName, "Иванов", Case.Genitive);     // "Ивановых"
Padezh.DeclinePlural(NamePart.LastName, "Толстой", Case.Instrumental); // "Толстыми"
Padezh.DeclinePlural(NamePart.LastName, "Шевченко", Case.Genitive);   // "Шевченко" (неизменяемая)
Padezh.DeclinePlural(NamePart.FirstName, "Иван", Case.Instrumental);  // "Иванами"
Padezh.DeclinePlural(NamePart.MiddleName, "Иванович", Case.Genitive); // "Ивановичей"
RuName.Parse("Иванов Пётр Сергеевич").DeclinePlural(Case.Genitive);   // "Ивановых" (форма «семья»)

// Звательный падеж: разговорный и церковный стили
Padezh.Decline(NamePart.FirstName, "Саша", Case.Vocative); // "Саш"
Padezh.Decline(NamePart.FirstName, "Ваня", Case.Vocative); // "Вань"
Padezh.Decline(NamePart.FirstName, "Пётр", Case.Vocative); // "Пётр" (на согласный — без изменений)
Padezh.Decline(NamePart.FirstName, "Пётр", Case.Vocative, Gender.Male, VocativeStyle.Church); // "Пётре"
RuName.Parse("Иванов Пётр Сергеевич").Decline(Case.Vocative, VocativeStyle.Church); // "Иванов Пётре Сергеевич"

// Определение пола
Padezh.DetectGender(NamePart.MiddleName, "Ивановна"); // Gender.Female
Padezh.DetectGender(NamePart.FirstName, "Агидель");   // Gender.Female (словарь)
new RuName(lastName: "Иванов", firstName: "Анна").DetectGender(); // Gender.Androgynous (конфликт)
```

## Точность

Движок сверяется с полным корпусом словоформ [petrovich-eval](https://github.com/petrovich/petrovich-eval)
(~248 000 форм единственного числа; инструмент `tools/MIA.Padezh.Eval`, выборки — в `tests/testdata`):

| Часть ФИО | С родом из корпуса | Род автодетектом |
|---|---|---|
| Фамилии (100 980) | **99,88 %** | **99,89 %**¹ |
| Имена (76 422) | **99,70 %** | 99,63 % |
| Отчества (97 626) | **100,00 %** | 100,00 % |

¹ Двуродовые фамилии (тег «мр-жр», 4 950 строк) засчитываются при попадании
в любую из двух легитимных норм. Точность по национальным классам
(армянские, грузинские, украинские, балтийские, татарские, греческие) —
в [data/CHANGES-v1.2.md](data/CHANGES-v1.2.md).

Правила v2 (5 исправлений относительно upstream) — см. [data/CHANGES-v2.md](data/CHANGES-v2.md);
v1.1 (множественное число, звательный, словарь пола) — [data/CHANGES-v1.1.md](data/CHANGES-v1.1.md);
v1.2 (национальные фамилии СНГ, велярное мн.ч., двуродовые в метрике) — [data/CHANGES-v1.2.md](data/CHANGES-v1.2.md);
v1.3 (церковный звательный, мн.ч. имён и отчеств) — [data/CHANGES-v1.3.md](data/CHANGES-v1.3.md).
Плюс золотые кейсы из тестов NPetrovich и юнит-тесты (193 теста).

## Почему не NPetrovich / другие библиотеки

| | MIA-Padezh | NPetrovich | NPetrovichLite | NameCaseLib |
|---|---|---|---|---|
| Зависимости | **0** | System.Text.Json | 0 | 0 |
| Правила в рантайме | **компилируются в код** | JSON-парсинг | JSON-парсинг | — |
| AOT/trimming | **да** | нет | нет | нет |
| Таргеты | ns2.0 + net8 + net10 | ns2.0 | ns2.0 | ns2.0 |
| Тесты (корпус) | **275К форм upstream + golden** | частичные | частичные | частичные |
| Состояние | активный | спит с 2024 | спит | спит |

## Структура репозитория

```
data/                    правила (формат petrovich-rules, MIT) + plural.json + gender-names.json + CHANGES-*.md
src/MIA.Padezh/          библиотека (ядро + сгенерированные таблицы)
tools/MIA.Padezh.RulesGen/  генератор: data/*.json → Generated/RulesTable.g.cs
tools/MIA.Padezh.Eval/   прогон корпуса petrovich-eval: общая точность, per-class отчёт по национальным классам, пробелы правил
tests/                   золотые кейсы + харнесс точности + корпус
```

## Перегенерация правил

```bash
dotnet run --project tools/MIA.Padezh.RulesGen
```

Генератор читает `data/rules.json` и `data/gender.json` и обновляет `src/MIA.Padezh/Generated/RulesTable.g.cs`. Валидация встроена: 5 модификаторов на правило, известные значения рода.

## Дорожная карта

- [x] Правила v2: исправления, подтверждённые корпусом ([data/CHANGES-v2.md](data/CHANGES-v2.md))
- [x] v1.1: множественное число фамилий, звательный падеж, словарь пола имён ([data/CHANGES-v1.1.md](data/CHANGES-v1.1.md))
- [x] v1.2: национальные фамилии СНГ (армянские, грузинские, украинские, балтийские, татарские, греческие), велярное мн.ч. ([data/CHANGES-v1.2.md](data/CHANGES-v1.2.md))
- [x] v1.3: церковный звательный (`VocativeStyle.Church`), мн.ч. имён и отчеств ([data/CHANGES-v1.3.md](data/CHANGES-v1.3.md))
- [ ] v2.0: опциональный нейро-fallback (ONNX) для непокрытых правилами фамилий

## Лицензия

MIT. Правила — [petrovich-rules](https://github.com/petrovich/petrovich-rules) (MIT), логика портирована с [petrovich-net](https://github.com/petrovich/petrovich-net) (MIT).

---

# MIA-Padezh (EN)

Russian full-name (FIO) declension for .NET: 6 cases for surnames, given names and patronymics, with automatic gender detection. Deterministic, zero dependencies, AOT-friendly — Petrovich rules are compiled into C# source at build time via the RulesGen tool, so there is no JSON parsing at runtime.

```csharp
RuName.Parse("Иванов Пётр Сергеевич").Decline(Case.Dative); // "Иванову Петру Сергеевичу"
Padezh.Decline(NamePart.LastName, "Шевченко", Case.Genitive); // "Шевченко"
```

Targets `netstandard2.0`, `net8.0`, `net10.0`. Validated against the 275K-form [petrovich-eval](https://github.com/petrovich/petrovich-eval) corpus: 99.9% surnames, 99.5% given names, 100% patronymics. MIT licensed; rules data derived from [petrovich-rules](https://github.com/petrovich/petrovich-rules).
