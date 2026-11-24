"""Обучение char-level GRU seq2seq для нейро-fallback склонения фамилий.

Вход:  lemma (нижний регистр) + род + падеж
Выход: форма (нижний регистр)

Архитектура: Emb -> GRU энкодер (инициализация скрытого состояния эмбеддингами
рода и падежа) -> GRU декодер с жадным декодированием. Экспорт в ONNX:
encoder.onnx (один проход) и decoder.onnx (один шаг), инференс — авторегрессия
на стороне C#.

Запуск: python training/train.py
"""
import io
import os
import random
import time

import torch
import torch.nn as nn

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "data")
MODELS = os.path.join(HERE, "models")

PAD, BOS, EOS, UNK = 0, 1, 2, 3
EMB, HID = 64, 192
MAX_LEN = 32
CASES, GENDERS = 6, 3
EPOCHS = 12
BATCH = 256
SEED = 42


GENDER_IDS = {"M": 0, "F": 1, "A": 2}


def load(path):
    rows = []
    for line in io.open(path, encoding="utf-8"):
        lemma, gender, case_idx, form = line.rstrip("\n").split("\t")
        rows.append((lemma, GENDER_IDS[gender], int(case_idx), form))
    return rows


def encode(word, stoi, bos=False, eos=False):
    ids = [stoi.get(ch, UNK) for ch in word]
    if bos:
        ids = [BOS] + ids
    if eos:
        ids = ids + [EOS]
    return ids


class Model(nn.Module):
    def __init__(self, vocab_size):
        super().__init__()
        self.emb = nn.Embedding(vocab_size, EMB, padding_idx=PAD)
        self.case_emb = nn.Embedding(CASES, HID)
        self.gender_emb = nn.Embedding(GENDERS, HID)
        self.encoder = nn.GRU(EMB, HID, batch_first=False)
        self.decoder = nn.GRU(EMB, HID, batch_first=False)
        self.out = nn.Linear(HID, vocab_size)

    def encode(self, src, case_id, gender_id, lengths=None):
        # src: [L, B]. Если lengths задан — pack_padded_sequence: финальный hidden
        # берётся на истинном конце каждой последовательности, без PAD-шагов.
        x = self.emb(src)
        h0 = self.case_emb(case_id)[None] + self.gender_emb(gender_id)[None]  # [1, B, HID]
        if lengths is None:
            _, hidden = self.encoder(x, h0)
            return hidden
        packed = nn.utils.rnn.pack_padded_sequence(x, lengths, enforce_sorted=False)
        _, hidden = self.encoder(packed, h0)
        return hidden

    def decode_step(self, tok, hidden):
        # tok: [1, B], hidden: [1, B, HID]
        out, hidden = self.decoder(self.emb(tok), hidden)
        return self.out(out), hidden

    def forward(self, src, case_id, gender_id, tgt_in, lengths=None):
        # teacher forcing: tgt_in — цель без последнего токена (с BOS)
        hidden = self.encode(src, case_id, gender_id, lengths)
        out, _ = self.decoder(self.emb(tgt_in), hidden)
        return self.out(out)


def decode_batch(model, batch, stoi, itos, device):
    """Жадное декодирование батча: возвращает список предсказанных форм."""
    model.eval()
    with torch.no_grad():
        src_lens = [len(r[0]) + 1 for r in batch]
        src = torch.zeros(max(src_lens), len(batch), dtype=torch.long)
        for b, r in enumerate(batch):
            ids = encode(r[0], stoi, eos=True)
            src[:len(ids), b] = torch.tensor(ids)
        case_id = torch.tensor([r[2] for r in batch], dtype=torch.long)
        gender_id = torch.tensor([r[1] for r in batch], dtype=torch.long)
        hidden = model.encode(
            src.to(device), case_id.to(device), gender_id.to(device),
            lengths=torch.tensor(src_lens, dtype=torch.long))

    tok = torch.full((1, len(batch)), BOS, dtype=torch.long, device=device)
    done = [False] * len(batch)
    outs = [[] for _ in batch]
    for _ in range(MAX_LEN):
        logits, hidden = model.decode_step(tok, hidden)
        tok = logits.argmax(-1)
        for b in range(len(batch)):
            t = tok[0, b].item()
            if t == EOS or t == PAD:
                done[b] = True
            elif not done[b]:
                outs[b].append(itos[t])
    return ["".join(o) for o in outs]


def greedy(model, rows, stoi, itos, device):
    ok = 0
    for i in range(0, len(rows), 64):
        batch = rows[i:i + 64]
        preds = decode_batch(model, batch, stoi, itos, device)
        for r, p in zip(batch, preds):
            if p == r[3]:
                ok += 1
    return ok / len(rows)


def greedy_misses(model, rows, stoi, itos, device, limit=25):
    """Список промахов: (лемма, род, падеж, ожидалось, получено)."""
    out = []
    for i in range(0, len(rows), 64):
        batch = rows[i:i + 64]
        preds = decode_batch(model, batch, stoi, itos, device)
        for r, p in zip(batch, preds):
            if p != r[3]:
                out.append((r[0], "MFA"[r[1]], "имрддтвнтвпр"[r[2]], r[3], p))
                if len(out) >= limit:
                    return out
    return out


def main():
    random.seed(SEED)
    torch.manual_seed(SEED)
    device = "cpu"

    vocab = [c for c in io.open(os.path.join(DATA, "vocab.txt"), encoding="utf-8").read().split("\n") if c]
    V = len(vocab) + 4  # + PAD/BOS/EOS/UNK
    stoi = {c: i + 4 for i, c in enumerate(vocab)}
    itos = {i + 4: c for i, c in enumerate(vocab)}
    itos[PAD], itos[BOS], itos[EOS] = "", "", ""

    train_rows = load(os.path.join(DATA, "train.tsv"))
    holdout_rows = load(os.path.join(DATA, "holdout.tsv"))
    print(f"train={len(train_rows)} holdout={len(holdout_rows)} vocab={V}")

    model = Model(V).to(device)
    opt = torch.optim.Adam(model.parameters(), lr=1e-3)
    ce = nn.CrossEntropyLoss(ignore_index=PAD)

    def batch_tensors(batch):
        tgt_lens = [len(r[3]) + 2 for r in batch]  # BOS + form + EOS
        src_lens = [len(r[0]) + 1 for r in batch]  # lemma + EOS
        L, Lmax = max(src_lens), max(tgt_lens)
        src = torch.zeros(L, len(batch), dtype=torch.long)
        tgt = torch.full((Lmax, len(batch)), PAD, dtype=torch.long)
        for b, r in enumerate(batch):
            s = encode(r[0], stoi, eos=True)
            src[:len(s), b] = torch.tensor(s)
            t = encode(r[3], stoi, bos=True, eos=True)
            tgt[:len(t), b] = torch.tensor(t)
        case_id = torch.tensor([r[2] for r in batch], dtype=torch.long)
        gender_id = torch.tensor([r[1] for r in batch], dtype=torch.long)
        lengths = torch.tensor(src_lens, dtype=torch.long)
        return src, tgt, case_id, gender_id, lengths

    for epoch in range(1, EPOCHS + 1):
        model.train()
        order = list(range(len(train_rows)))
        random.shuffle(order)
        total, n = 0.0, 0
        t0 = time.time()
        for i in range(0, len(order), BATCH):
            batch = [train_rows[j] for j in order[i:i + BATCH]]
            src, tgt, case_id, gender_id, lengths = batch_tensors(batch)
            logits = model(src, case_id, gender_id, tgt[:-1], lengths)
            loss = ce(logits.reshape(-1, V), tgt[1:].reshape(-1))
            opt.zero_grad()
            loss.backward()
            opt.step()
            total += loss.item()
            n += 1
        acc = greedy(model, random.sample(holdout_rows, min(3000, len(holdout_rows))), stoi, itos, device)
        print(f"epoch {epoch}: loss={total / n:.4f} holdout_acc={acc:.4f} ({time.time() - t0:.0f}s)", flush=True)

    # --- финальная метрика на всём holdout + анализ промахов ---
    acc = greedy(model, holdout_rows, stoi, itos, device)
    print(f"FINAL holdout_acc={acc:.4f}")

    misses = greedy_misses(model, holdout_rows, stoi, itos, device, limit=25)
    for m in misses:
        print("MISS", m)

    # Чекпоинт: экспорт может упасть, обучение терять нельзя.
    torch.save(model.state_dict(), os.path.join(MODELS, "checkpoint.pt"))

    # --- экспорт ONNX ---
    os.makedirs(MODELS, exist_ok=True)
    model.eval()
    enc_path = os.path.join(MODELS, "encoder.onnx")
    dec_path = os.path.join(MODELS, "decoder.onnx")

    src = torch.zeros(4, 1, dtype=torch.long)
    case_id = torch.zeros(1, dtype=torch.long)
    gender_id = torch.zeros(1, dtype=torch.long)
    torch.onnx.export(
        model, (src, case_id, gender_id), enc_path,
        input_names=["src", "case_id", "gender_id"], output_names=["hidden"],
        dynamic_axes={"src": {0: "L", 1: "B"}, "case_id": {0: "B"}, "gender_id": {0: "B"}, "hidden": {1: "B"}},
        opset_version=17)
    tok = torch.zeros(1, 1, dtype=torch.long)
    hidden = torch.zeros(1, 1, HID, dtype=torch.float)
    torch.onnx.export(
        model, (tok, hidden), dec_path,
        input_names=["tok", "hidden"], output_names=["logits", "new_hidden"],
        dynamic_axes={"tok": {1: "B"}, "hidden": {1: "B"}, "logits": {1: "B"}, "new_hidden": {1: "B"}},
        opset_version=17)
    print("exported:", enc_path, dec_path)

    # --- метаданные для C# ---
    with io.open(os.path.join(MODELS, "meta.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(vocab) + "\n")
    with io.open(os.path.join(MODELS, "report.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write(f"holdout_pairs={len(holdout_rows)}\n")
        f.write(f"train_pairs={len(train_rows)}\n")
        f.write(f"vocab={V}\n")
        f.write(f"emb={EMB} hidden={HID} epochs={EPOCHS}\n")
        f.write(f"holdout_accuracy={acc:.4f}\n")


if __name__ == "__main__":
    main()
