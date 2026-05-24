
using System.Numerics;

namespace blazingzephyr.itmo.tensors.impl1;

public readonly ref struct Tensor<T> where T : unmanaged, ISignedNumber<T>
{
    public int[] Shape { get; init; }
    public int[] Strides { get; init; }
    public Span<T> Data { get; init; }

    public Tensor(Span<T> data, params int[] shape)
    {
        Data = data;
        Shape = shape;
        Strides = CalculateStrides(shape);
    }

    public ref T this[ReadOnlySpan<int> indices] => ref Data[FlatIndex(indices)];
    public ref T this[params int[] indices] => ref Data[FlatIndex(indices)];

    private Tensor(T[] data, int[] shape, int[] strides)
    {
        Data = data;
        Shape = shape;
        Strides = strides;
    }

    private static int[] CalculateStrides(int[] shape)
    {
        int[] strides = new int[shape.Length];
        strides[^1] = 1;
        for (int i = shape.Length - 2; i >= 0; i--)
            strides[i] = strides[i + 1] * shape[i + 1];

        return strides;
    }

    public readonly int FlatIndex(ReadOnlySpan<int> indices)
    {
        int result = 0;
        for (int i = 0; i < indices.Length; i++)
        {
            result += indices[i] * Strides[i];
        }

        return result;
    }

    public static Tensor<T> Add(Tensor<T> a, T ac, Tensor<T> b, T bc, Span<T> dest)
    {
        if (!a.Shape.SequenceEqual(b.Shape))
        {
            throw new ArgumentException(
                "Сложить можно только два тензора с одинаковой формой/размерностью");
        }

        Tensor<T> result = new Tensor<T>(dest, a.Shape);
        for (int i = 0; i < dest.Length; i++)
        {
            dest[i] = a.Data[i] * ac + b.Data[i] * bc;
        }

        return result;
    }

    /// <summary>
    /// Тензорное произведение тензоров.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="dest"></param>
    public static Tensor<T> OuterProduct(Tensor<T> a, Tensor<T> b, Span<T> dest)
    {
        int[] shape = [.. a.Shape, .. b.Shape];
        Tensor<T> result = new Tensor<T>(dest, shape);
        Span<int> ai = stackalloc int[a.Shape.Length];
        Span<int> bi = stackalloc int[b.Shape.Length];
        for (int flat = 0; flat < dest.Length; flat++)
        {
            int i = flat;
            for (int j = 0; j < a.Shape.Length; j++)
            {
                ai[j] = i / result.Strides[j];
                i %= result.Strides[j];
            }
            for (int j = 0; j < b.Shape.Length; j++)
            {
                bi[j] = i / result.Strides[j + a.Shape.Length];
                i %= result.Strides[j + a.Shape.Length];
            }
            dest[flat] = a.Data[a.FlatIndex(ai)] * b.Data[b.FlatIndex(bi)];
        }
        return result;
    }

    /// <summary>
    /// Произведение Кронекера - операция между двумя матрицами,
    /// подвид тензорного произведения,
    /// результатом которого является блочная матрица.
    /// </summary>
    /// <param name="a">Левый элемент произведения.</param>
    /// <param name="b">Правый элемент произведения.</param>
    /// <param name="dest">Представление данных,
    /// куда требуется записать новые компоненты</param>
    /// <returns>Тензор, представляющий блочную матрицу.</returns>
    public static Tensor<T> KroneckerProduct(Tensor<T> a, Tensor<T> b, Span<T> dest)
    {
        if (a.Shape.Length != b.Shape.Length)
        {
            throw new InvalidOperationException(
                "Произведение Кронекера можно применить только к матрицам." +
                "Для обобщения в этой программе также можно применить произведение Кронекера" +
                "к тензорам одинаковой размерности");
        }

        int rank = a.Shape.Length;
        int[] shape = new int[rank];
        for (int i = 0; i < shape.Length; i++)
        {
            shape[i] = a.Shape[i] * b.Shape[i];
        }

        Tensor<T> result = new Tensor<T>(dest, shape);
        Span<int> ai = stackalloc int[rank];
        Span<int> bi = stackalloc int[rank];

        for (int flat = 0; flat < dest.Length; flat++)
        {
            int rem = flat;
            for (int d = rank - 1; d >= 0; d--)
            {
                int idx = rem % shape[d];
                ai[d] = idx / b.Shape[d];  // номер блока A
                bi[d] = idx % b.Shape[d];  // позиция внутри блока B
                rem /= shape[d];
            }

            dest[flat] = a.Data[a.FlatIndex(ai)] * b.Data[b.FlatIndex(bi)];
        }

        return result;
    }

    /// <summary>
    /// Операция симметрирования тензора.
    /// </summary>
    /// <param name="tensor"></param>
    /// <param name="dest"></param>
    /// <returns></returns>
    public static Tensor<T> Symmetrize(Tensor<T> tensor, Span<T> dest)
    {
        return Symmetrize(tensor, dest, _ => T.One);
    }

    /// <summary>
    /// Операция альтернирования тензора.
    /// </summary>
    /// <param name="tensor"></param>
    /// <param name="dest"></param>
    /// <returns></returns>
    public static Tensor<T> Antisymmetrize(Tensor<T> tensor, Span<T> dest)
    {
        return Symmetrize(tensor, dest, i => i % 2 == 0 ? T.One : T.NegativeOne);
    }

    private static Tensor<T> Symmetrize(Tensor<T> tensor, Span<T> dest, Func<int, T> sign)
    {
        int rank = tensor.Shape.Length;
        T coeff = T.One / Enumerable
            .Range(1, rank)
            .Aggregate(T.One, (f, i) => f * T.CreateChecked(i));

        // Индексы перестановки — [0, 1, 2, ..., rank-1]
        Span<int> perm = stackalloc int[rank];
        for (int i = 0; i < rank; i++) perm[i] = i;

        // Поля, необходимые из-за ограничения компилятора.
        int[] shape = tensor.Shape;
        int[] strides = tensor.Strides;
        int destLen = dest.Length;

        // Скорее всего без развёртки алгоритма здесь и
        // дополнительного кода невозможно обойтись без аллокаций.
        T[] data = tensor.Data.ToArray();
        T[] destData = dest.ToArray();

        // Для каждой перестановки осей — добавляем транспонированный тензор
        HeapAlgorithm.Permute(perm, (cp, i) =>
        {
            // Переставляем shape и strides согласно перестановке
            Span<int> newShape = stackalloc int[rank];
            Span<int> newStrides = stackalloc int[rank];
            for (int d = 0; d < rank; d++)
            {
                newShape[d] = shape[cp[d]];
                newStrides[d] = strides[cp[d]];
            }

            // Поэлементно прибавляем к dest
            for (int flat = 0; flat < destLen; flat++)
            {
                int rem = flat, srcFlat = 0;
                for (int d = rank - 1; d >= 0; d--)
                {
                    srcFlat += rem % newShape[d] * newStrides[d];
                    rem /= newShape[d];
                }
                destData[flat] += sign(i) * data[srcFlat];
            }
        });

        // Прибавляем компоненты с коэффициентом.
        for (int i = 0; i < dest.Length; i++)
        {
            dest[i] = coeff * destData[i];
        }

        Tensor<T> result = new Tensor<T>(dest, tensor.Shape);
        return result;
    }

    /// <summary>
    /// Выводит в консоль все возможные транспонирования этого тензора.
    /// </summary>
    /// <param name="tensor"></param>
    public static IList<T[]> Transpositions(Tensor<T> tensor)
    {
        int rank = tensor.Shape.Length;

        // Индексы перестановки — [0, 1, 2, ..., rank-1]
        Span<int> perm = stackalloc int[rank];
        for (int i = 0; i < rank; i++) perm[i] = i;

        // Поля, необходимые из-за ограничения компилятора.
        int[] shape = tensor.Shape;
        int[] strides = tensor.Strides;

        // Скорее всего без развёртки алгоритма здесь и
        // дополнительного кода невозможно обойтись без аллокаций.
        T[] data = tensor.Data.ToArray();

        // Из-за ограничений компилятора вынуждены будем вернуть так.
        List<T[]> result = new List<T[]>();

        // Для каждой перестановки осей — добавляем транспонированный тензор
        HeapAlgorithm.Permute(perm, (cp, i) =>
        {
            // Переставляем shape и strides согласно перестановке
            Span<int> newShape = stackalloc int[rank];
            Span<int> newStrides = stackalloc int[rank];
            for (int d = 0; d < rank; d++)
            {
                newShape[d] = shape[cp[d]];
                newStrides[d] = strides[cp[d]];
            }

            // Поэлементно прибавляем к dest
            Span<T> dest = stackalloc T[data.Length];
            for (int flat = 0; flat < dest.Length; flat++)
            {
                int rem = flat, srcFlat = 0;
                for (int d = rank - 1; d >= 0; d--)
                {
                    srcFlat += rem % newShape[d] * newStrides[d];
                    rem /= newShape[d];
                }

                dest[flat] = data[srcFlat];
            }

            result.Add(dest.ToArray());
        });

        // Выводим тензор в консоль.
        // Использовать итераторы для Tensor<T> или же Span<T> невозможно
        // из-за ограничений компилятора.
        //
        // Todo: сделать печать красивее.
        return result;
    }

    /// <summary>
    /// Сворачивает тензор по двум осям.
    /// </summary>
    /// <param name="a">Исходный тензор</param>
    /// <param name="axis1">Индекс первой оси сворачивания</param>
    /// <param name="axis2">Индекс второй оси сворачивания</param>
    /// <param name="dest"></param>
    /// <returns></returns>
    public static Tensor<T> Contract(Tensor<T> a, int axis1, int axis2, Span<T> dest)
    {
        if (a.Shape[axis1] != a.Shape[axis2])
        {
            throw new InvalidOperationException(
                "Размерности осей должны совпадать: " + 
                $"shape[{axis1}]={a.Shape[axis1]} != shape[{axis2}]={a.Shape[axis2]}");
        }

        int rank = a.Shape.Length;
        int sumSize = a.Shape[axis1];
        int[] shape = [.. a.Shape
                            .Where((_, d) => d != axis1 && d != axis2)
                            .DefaultIfEmpty(1)];

        // Индекс A фиксирован везде кроме axis1/axis2 — берём из result
        Tensor<T> result = new Tensor<T>(dest, shape);
        int[] aIdx = new int[rank];

        for (int flat = 0; flat < dest.Length; flat++)
        {
            // Восстанавливаем индекс.
            int rem = flat;
            int[] rIdx = new int[shape.Length];

            for (int d = shape.Length - 1; d >= 0; d--)
            {
                rIdx[d] = rem % shape[d];
                rem /= shape[d];
            }

            // Заполняем индекс A - пропускаем оси axis1 и axis2.
            int ri = 0;
            for (int d = 0; d < rank; d++)
            {
                if (d == axis1 || d == axis2) continue;
                aIdx[d] = rIdx[ri++];
            }

            // Суммируем по общему индексу k (диагонали).
            T sum = T.Zero;
            for (int k = 0; k < sumSize; k++)
            {
                aIdx[axis1] = k;
                aIdx[axis2] = k;
                sum += a.Data[a.FlatIndex(aIdx)];
            }

            dest[flat] = sum;
        }

        return result;
    }

    /// <summary>
    /// Контракция двух тензоров по указанным осям.
    /// result[i..., k...] = Σⱼ A[i..., j] * B[j, k...]
    /// </summary>
    public static Tensor<T> Contract(Tensor<T> a, Tensor<T> b,
        int axisA, int axisB, Span<T> dest)
    {
        if (a.Shape[axisA] != b.Shape[axisB])
        {
            throw new InvalidOperationException(
                $"Размерности осей должны совпадать: " +
                $"a.shape[{axisA}]={a.Shape[axisA]} != b.shape[{axisB}]={b.Shape[axisB]}.");
        }

        int sumSize = a.Shape[axisA];

        // Результирующий shape — оси A без axisA, оси B без axisB
        int[] shape =
        [
            ..a.Shape.Where((_, d) => d != axisA),
            ..b.Shape.Where((_, d) => d != axisB)
        ];

        Tensor<T> result = new Tensor<T>(dest, shape);
        int[] aIdx = new int[a.Shape.Length];
        int[] bIdx = new int[b.Shape.Length];

        for (int flat = 0; flat < dest.Length; flat++)
        {
            // Восстанавливаем индексы A и B из flat
            int rem = flat;
            for (int d = shape.Length - 1; d >= 0; d--)
            {
                int idx = rem % shape[d];
                rem /= shape[d];

                // Первые (a.Rank-1) осей — индексы A, остальные — B
                if (d < a.Shape.Length - 1)
                {
                    int ad = d < axisA ? d : d + 1;
                    aIdx[ad] = idx;
                }
                else
                {
                    int bd = d - (a.Shape.Length - 1);
                    bd = bd < axisB ? bd : bd + 1;
                    bIdx[bd] = idx;
                }
            }

            // Суммируем по общей оси
            T sum = T.Zero;
            for (int k = 0; k < sumSize; k++)
            {
                aIdx[axisA] = k;
                bIdx[axisB] = k;
                sum += a.Data[a.FlatIndex(aIdx)] * b.Data[b.FlatIndex(bIdx)];
            }

            dest[flat] = sum;
        }

        return result;
    }
}
