"""Проверка экспорта: encoder.onnx + decoder.onnx в onnxruntime должны давать
тот же результат, что обученная модель (сравнение с эталоном holdout).

Запуск: python training/verify_onnx.py
"""
import io
import os

import numpy as np
import onnxruntime as ort

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "data")
MODELS = os.path.join(HERE, "models")

PAD, BOS, EOS, UNK = 0, 1, 2, 3
MAX_LEN = 32
GENDER_IDS = {"M": 0, "F": 1, "A": 2}


def main():
    vocab = [c for c in io.open(os.path.join(DATA, "vocab.txt"), encoding="utf-8").read().split("\n") if c]
    stoi = {c: i + 4 for i, c in enumerate(vocab)}

    enc = ort.InferenceSession(os.path.join(MODELS, "encoder.onnx"))
    dec = ort.InferenceSession(os.path.join(MODELS, "decoder.onnx"))

    rows = []
    for line in io.open(os.path.join(DATA, "holdout.tsv"), encoding="utf-8"):
        lemma, gender, case_idx, form = line.rstrip("\n").split("\t")
        rows.append((lemma, GENDER_IDS[gender], int(case_idx), form))

    ok = 0
    misses_done = []
    for i, (lemma, gender, case_idx, form) in enumerate(rows):
        ids = [stoi.get(ch, UNK) for ch in lemma] + [EOS]
        src = np.zeros((len(ids), 1), dtype=np.int64)
        for j, t in enumerate(ids):
            src[j, 0] = t
        hidden = enc.run(["hidden"], {
            "src": src,
            "case_id": np.array([case_idx], dtype=np.int64),
            "gender_id": np.array([gender], dtype=np.int64),
        })[0]

        tok = np.array([[BOS]], dtype=np.int64)
        out = []
        for _ in range(MAX_LEN):
            logits, hidden = dec.run(["logits", "new_hidden"], {"tok": tok, "hidden": hidden})
            t = int(np.argmax(logits[0, 0]))
            if t in (EOS, PAD):
                break
            chars = [c for c, k in stoi.items() if k == t]
            out.append(chars[0] if chars else "?")
            tok = np.array([[t]], dtype=np.int64)

        pred = "".join(out)
        if pred == form:
            ok += 1
        elif len([r for r in misses_done]) < 8:
            misses_done.append(1)
            print("miss:", lemma, f"[{gender},{case_idx}] ->", repr(pred), "| ожидание:", repr(form))

    print(f"ONNX holdout: {ok}/{len(rows)} = {ok / len(rows):.4f}")


if __name__ == "__main__":
    main()
