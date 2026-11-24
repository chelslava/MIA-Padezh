"""Подготовка обучающих данных нейро-fallback из корпуса petrovich-eval.

Вход: surnames.tsv (lemma, word, grammemes).
Выход: train.tsv / holdout.tsv (lemma_lower, gender, case_idx, form_lower),
       vocab.txt, holdout-demo.tsv (выборка для C#-тестов).

Разбиение по лемме (детерминированное), чтобы формы одной фамилии не попали
одновременно в train и holdout.
"""
import io
import os

SRC = r"C:\Users\ChelSlava\.zcode\workspace\1795111575763-013bb1f2-fd8d-4a7d-be73-2298ec0c492a\research\upstream-petrovich-net\petrovich-eval\surnames.tsv"
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "data")

MR, ZR = "\u043c\u0440", "\u0436\u0440"
CASES = ["\u0438\u043c", "\u0440\u0434", "\u0434\u0442", "\u0432\u043d", "\u0442\u0432", "\u043f\u0440"]  # им рд дт вн тв пр


def lemma_bucket(lemma: str) -> int:
    h = 2166136261
    for ch in lemma:
        h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
    return h % 100


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    train, holdout = [], []
    lemmas = set()
    chars = set()

    seen = set()
    for line in io.open(SRC, encoding="utf-8"):
        c = line.rstrip("\n").split("\t")
        if len(c) < 3 or c[2] == "grammemes":
            continue
        if "\u043c\u043d" in c[2]:  # мн — вне скоупа fallback
            continue
        tags = c[2].split(",")
        male = any(t.startswith(MR) for t in tags)
        female = any(t.startswith(ZR) for t in tags)
        if not male and not female:
            continue
        if not any(t in tags for t in CASES):
            continue
        case_idx = CASES.index(next(t for t in CASES if t in tags))
        gender = "A" if (male and female) else ("M" if male else "F")
        lemma = c[0].lower()
        form = c[1].lower()
        key = (lemma, gender, case_idx)
        if key in seen:
            continue
        seen.add(key)
        lemmas.add(lemma)
        chars.update(lemma)
        chars.update(form)
        row = (lemma, gender, case_idx, form)
        (holdout if lemma_bucket(lemma) < 3 else train).append(row)

    # vocab: отсортированные символы
    vocab = sorted(chars)
    with io.open(os.path.join(OUT_DIR, "vocab.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(vocab) + "\n")

    for name, rows in (("train.tsv", train), ("holdout.tsv", holdout)):
        with io.open(os.path.join(OUT_DIR, name), "w", encoding="utf-8", newline="\n") as f:
            for r in rows:
                f.write("\t".join(str(x) for x in r) + "\n")

    # демо-выборка для C#-тестов: полные парадигмы первых 50 holdout-лемм
    from collections import defaultdict
    by_lemma = defaultdict(list)
    for r in holdout:
        by_lemma[r[0]].append(r)
    demo = []
    for lemma, rows in sorted(by_lemma.items()):
        demo.extend(rows)
        if len(demo) >= 300:
            break
    with io.open(os.path.join(OUT_DIR, "holdout-demo.tsv"), "w", encoding="utf-8", newline="\n") as f:
        for r in demo:
            f.write("\t".join(str(x) for x in r) + "\n")

    print("train:", len(train), "| holdout:", len(holdout), "| lemmas:", len(lemmas), "| vocab:", len(vocab))


if __name__ == "__main__":
    main()
