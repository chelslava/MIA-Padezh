using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.ML.OnnxRuntime;
using MIA.Padezh;
using OrtTensor = Microsoft.ML.OnnxRuntime.Tensors;

namespace MIA.Padezh.Neural;

/// <summary>
/// Гибридное склонение: детерминированные правила в приоритете, нейро-fallback
/// для фамилий, которые правилами не покрыты. Модель — char-level GRU seq2seq,
/// обученная на корпусе petrovich-eval; экспортирована в ONNX.
/// </summary>
/// <example>
/// <code>
/// using var padezh = new NeuralPadezh();
/// padezh.DeclineLastName("Иванов", Case.Genitive);      // правила: "Иванова"
/// padezh.DeclineLastName("Шварценеггер", Case.Genitive); // правило не нашлось → модель
/// </code>
/// </example>
public sealed class NeuralPadezh : IDisposable
{
    private const int PadId = 0, BosId = 1, EosId = 2, UnkId = 3;
    private const int MaxLen = 32;

    private readonly InferenceSession _encoder;
    private readonly InferenceSession _decoder;
    private readonly Dictionary<char, int> _vocab;
    private readonly string? _tempDir;

    /// <summary>Загружает ONNX-модели из встроенных ресурсов сборки.</summary>
    public NeuralPadezh()
    {
        // InferenceSession грузит модель из файла — материализуем ресурсы во временную папку.
        _tempDir = Path.Combine(Path.GetTempPath(), "MIA.Padezh.Neural." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var encoderPath = Materialize("encoder.onnx", _tempDir);
        var decoderPath = Materialize("decoder.onnx", _tempDir);
        _encoder = new InferenceSession(encoderPath);
        _decoder = new InferenceSession(decoderPath);
        _vocab = LoadVocab(System.Text.Encoding.UTF8.GetString(ReadResource("vocab.txt")));
    }

    /// <summary>Загружает ONNX-модели из указанных файлов (для экспериментов с новыми моделями).</summary>
    public NeuralPadezh(string encoderPath, string decoderPath, string vocabPath)
    {
        _encoder = new InferenceSession(encoderPath);
        _decoder = new InferenceSession(decoderPath);
        _vocab = LoadVocab(File.ReadAllText(vocabPath));
    }

    /// <summary>
    /// Склоняет фамилию: если правило нашлось — детерминированный результат;
    /// иначе — предсказание модели. Регистр входа сохраняется.
    /// </summary>
    public string DeclineLastName(string surname, Case @case, Gender? genderHint = null)
    {
        if (@case == Case.Vocative || string.IsNullOrWhiteSpace(surname))
            return Padezh.Decline(NamePart.LastName, surname, @case, genderHint);

        var gender = genderHint ?? Padezh.DetectGender(NamePart.LastName, surname);
        if (Padezh.TryDecline(NamePart.LastName, surname, @case, out var ruled, gender))
            return ruled;

        return DeclineRaw(surname, @case, gender);
    }

    /// <summary>
    /// Прямое обращение к модели, минуя правила: для экспериментов и оценки
    /// модели. Вход — фамилия в именительном падеже; регистр входа сохраняется.
    /// </summary>
    public string DeclineRaw(string surname, Case @case, Gender gender)
    {
        if (@case == Case.Vocative || string.IsNullOrWhiteSpace(surname))
            return surname;

        var lower = Predict(surname.ToLowerInvariant(), (int)@case, (int)gender);
        return RestoreCase(lower, surname);
    }

    private string Predict(string lemmaLower, int caseId, int genderId)
    {
        var src = Encode(lemmaLower);
        const int b = 1;

        var srcTensor = new OrtTensor.DenseTensor<long>(new[] { src.Length, b });
        for (int i = 0; i < src.Length; i++)
            srcTensor[i, 0] = src[i];

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("src", srcTensor),
            NamedOnnxValue.CreateFromTensor("case_id", ScalarTensor(caseId)),
            NamedOnnxValue.CreateFromTensor("gender_id", ScalarTensor(genderId)),
        };

        OrtTensor.Tensor<float> hidden;
        using (var results = _encoder.Run(inputs))
        {
            hidden = Take(results, "hidden");
        }

        var tok = new OrtTensor.DenseTensor<long>(new[] { 1, b });
        tok[0, 0] = BosId;

        var chars = new List<char>(MaxLen);
        for (int step = 0; step < MaxLen; step++)
        {
            var decoderInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("tok", tok),
                NamedOnnxValue.CreateFromTensor("hidden", hidden),
            };

            OrtTensor.Tensor<float> logits;
            OrtTensor.Tensor<float> newHidden;
            using (var results = _decoder.Run(decoderInputs))
            {
                logits = Take(results, "logits");
                newHidden = Take(results, "new_hidden");
            }

            int best = ArgMax(logits);
            if (best == EosId || best == PadId)
                break;

            chars.Add(IdToChar(best));
            tok[0, 0] = best;
            hidden = newHidden;
        }

        return new string(chars.ToArray());
    }

    private static OrtTensor.Tensor<float> Take(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, string name)
        => results.FirstOrDefault(v => v.Name == name)?.AsTensor<float>()
           ?? throw new InvalidOperationException($"Модель не вернула выход «{name}».");

    private int[] Encode(string text)
    {
        var ids = new int[text.Length + 1];
        for (int i = 0; i < text.Length; i++)
            ids[i] = _vocab.TryGetValue(text[i], out var id) ? id : UnkId;
        ids[text.Length] = EosId;
        return ids;
    }

    private static OrtTensor.DenseTensor<long> ScalarTensor(long value)
    {
        var t = new OrtTensor.DenseTensor<long>(new[] { 1 });
        t[0] = value;
        return t;
    }

    private static int ArgMax(OrtTensor.Tensor<float> logits)
    {
        int vocab = (int)logits.Dimensions[2];
        int best = 0;
        float bestScore = float.NegativeInfinity;
        for (int v = 0; v < vocab; v++)
        {
            float score = logits[0, 0, v];
            if (score > bestScore)
            {
                bestScore = score;
                best = v;
            }
        }

        return best;
    }

    private char IdToChar(int id)
    {
        foreach (var kv in _vocab)
        {
            if (kv.Value == id)
                return kv.Key;
        }

        return '?';
    }

    /// <summary>Модель предсказывает нижний регистр; восстанавливаем регистр входа.</summary>
    private static string RestoreCase(string lower, string original)
    {
        if (string.IsNullOrEmpty(original))
            return lower;

        bool allUpper = true, anyLetter = false;
        foreach (var ch in original)
        {
            if (!char.IsLetter(ch))
                continue;
            anyLetter = true;
            if (char.ToLowerInvariant(ch) == ch)
                allUpper = false; // есть буква в нижнем регистре — вход не ALL CAPS
        }

        if (anyLetter && allUpper)
            return upper(lower);

        if (char.IsUpper(original[0]))
            return char.ToUpperInvariant(lower[0]) + lower.Substring(1);

        return lower;

        static string upper(string s)
        {
            var arr = s.ToCharArray();
            for (int i = 0; i < arr.Length; i++)
                arr[i] = char.ToUpperInvariant(arr[i]);
            return new string(arr);
        }
    }

    private static Dictionary<char, int> LoadVocab(string text)
    {
        var map = new Dictionary<char, int>();
        int id = 4; // 0..3 зарезервированы за PAD/BOS/EOS/UNK
        foreach (var line in text.Split('\n'))
        {
            var ch = line.TrimEnd('\r');
            if (ch.Length == 1 && !map.ContainsKey(ch[0]))
                map[ch[0]] = id++;
        }

        return map;
    }

    private string Materialize(string name, string dir)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, ReadResource(name));
        return path;
    }

    private static byte[] ReadResource(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream($"MIA.Padezh.Neural.Models.{name}");
        if (stream is null)
            throw new InvalidOperationException($"Встроенный ресурс не найден: {name}");

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _encoder.Dispose();
        _decoder.Dispose();
        try
        {
            if (_tempDir is not null && Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // лучшее усилие: временные файлы не критичны
        }
    }
}
