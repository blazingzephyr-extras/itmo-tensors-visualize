
namespace blazingzephyr.itmo.tensors;

/// <summary>
/// Алгоритм Хипа для перестановок.
/// </summary>
internal static class HeapAlgorithm
{
    /// <summary>
    /// Перебирает все возможные перестановки Span<T>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data"></param>
    /// <param name="action"></param>
    public static void Permute<T>(Span<T> data, Action<Span<T>, int> action)
    {
        Span<int> c = stackalloc int[data.Length];
        c.Clear();
        action(data, 0);

        int i = 0;
        while (i < data.Length)
        {
            if (c[i] < i)
            {
                if (i % 2 == 0) (data[0], data[i]) = (data[i], data[0]);
                else (data[c[i]], data[i]) = (data[i], data[c[i]]);

                action(data, i);
                c[i]++;
                i = 0;
            }
            else
            {
                c[i] = 0;
                i++;
            }
        }
    }
}
