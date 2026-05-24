using System;
using System.Threading;
using System.Windows;
using blazingzephyr.itmo.tensors.impl1;

namespace blazingzephyr.itmo.tensors;

internal class Program
{
    private static Application _app = null!;
    private static Thread _uiThread = null!;

    [STAThread]
    static void Main()
    {
        InitWpf();

        Action[] actions =
        [
            Example1, Example2, OuterProduct1, OuterProduct2,
            KroneckerProduct, Add, Symmetrize, Transpositions,
            Contract, Contract2, Contract3
        ];

        string[] lines =
        [
            "Примитивные тензорные / SIMD операции из .NET8",
            "Примитивные тензорные / SIMD операции из .NET8",
            "Внешнее (тензорное) произведение",
            "Внешнее (тензорное) произведение",
            "Произведение Кронекера",
            "Сложение (с коэффициентом, A + 1.5 * B)",
            "Симметрирование / Альтернирование",
            "Транспонирование",
            "Свёртка тензора (след матрицы)",
            "Свёртка тензора",
            "Свёртка тензоров (умножение матриц)"
        ];

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) Console.WriteLine("----------");
            Console.WriteLine($"{lines[i]}:");
            actions[i]();
        }

        // Завершаем WPF-поток вместе с приложением
        _app.Dispatcher.Invoke(() => _app.Shutdown());
    }

    // ── WPF живёт в отдельном постоянном STA-потоке ──────────────────────
    static void InitWpf()
    {
        var ready = new ManualResetEventSlim(false);
        _uiThread = new Thread(() =>
        {
            _app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            _app.Startup += (_, _) => ready.Set();
            _app.Run();
        });
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.IsBackground = true;
        _uiThread.Start();
        ready.Wait();
    }

    // ── Открыть окно с несколькими тензорами на вкладках ─────────────────
    static void Visualize(string title, params TensorData[] tensors)
    {
        var closed = new ManualResetEventSlim(false);
        _app.Dispatcher.Invoke(() =>
        {
            var window = new MainWindow(title, tensors);
            window.Closed += (_, _) => closed.Set();

            window.WindowState = WindowState.Maximized;
            window.Show();
        });
        closed.Wait();
    }

    // ── Примеры ──────────────────────────────────────────────────────────

    static void Example1()
    {
        ReadOnlySpan<double> x = [10.0, 5.0, 6.0, 8.0, 9.0];
        double y = 8.0;
        Span<double> result = stackalloc double[5];
        System.Numerics.Tensors.TensorPrimitives.Add(x, y, result);
        Console.WriteLine(String.Join(", ", result.ToArray()));

        Visualize("Пример System.Numerics.Tensors", new TensorData(result.ToArray(), [1, 5, 1], "x + 8"));
    }

    static void Example2()
    {
        ReadOnlySpan<System.Numerics.Complex> x =
        [
            new(11.0, 4.0), new(2.0, 4.0), new(5.0, 0.0)
        ];
        Span<bool> result = stackalloc bool[3];
        System.Numerics.Tensors.TensorPrimitives.IsComplexNumber(x, result);
        Console.WriteLine(String.Join(", ", result.ToArray()));

        double[] copy = result.ToArray().Select(b => b ? 1.0 : 0.0).ToArray();
        Visualize("Пример System.Numerics.Tensors", new TensorData(copy, [1, 3, 1], "IsComplex"));
    }

    static void OuterProduct1()
    {
        double[] a = [1, 0, 0, 1];
        Tensor<double> A = new(a, 2, 2);

        double[] b = [1, 1, 1, -1];
        Tensor<double> B = new(b, 2, 2);

        double[] c = new double[2 * 2 * 2 * 2];
        Tensor<double> C = Tensor<double>.OuterProduct(A, B, c);

        PrintTensors(A, B, C);
        Console.WriteLine($"C[0,1,1,1] = {C[0, 1, 1, 1]}");

        // Показываем A, B и результат C на трёх вкладках
        Visualize(
            "Внешнее (тензорное) произведение",
            new TensorData(a, [2, 2, 1], "A [2×2]"),
            new TensorData(b, [2, 2, 1], "B [2×2]"),
            new TensorData(c[0..8], [2, 2, 2], "C = A⊗B [2×2×2×2] [1]"),
            new TensorData(c[8..], [2, 2, 2], "C = A⊗B [2×2×2×2] [2]")
        );
    }

    static void OuterProduct2()
    {
        double[] a = [1, 2, 3, 4];
        Tensor<double> A = new(a, 4, 1);

        double[] b = [5, 6, 7, 8];
        Tensor<double> B = new(b, 1, 4);

        double[] c = new double[4 * 1 * 1 * 4];
        Tensor<double> C = Tensor<double>.OuterProduct(A, B, c);

        PrintTensors(A, B, C);

        Visualize(
            "Внешнее (тензорное) произведение",
            new TensorData(a, [4, 1, 1], "A [4×1]"),
            new TensorData(b, [1, 4, 1], "B [1×4]"),
            new TensorData(c, [4, 4], "C = A⊗B [4×1×1×4]")
        );
    }

    static void KroneckerProduct()
    {
        double[] a = [1, -4, 7, -2, 3, 3];
        Tensor<double> A = new(a, 2, 3);

        double[] b = [8, -9, -6, 5, 1, -3, -4, 7, 2, 8, -8, -3, 1, 2, -5, -1];
        Tensor<double> B = new(b, 4, 4);

        double[] c = new double[2 * 3 * 4 * 4];
        Tensor<double> C = Tensor<double>.KroneckerProduct(A, B, c);

        PrintTensors(A, B, C);

        Visualize(
            "Произведение Кронекера (свёртка тензорного)",
            new TensorData(a, [2, 3, 1], "A [2×3]"),
            new TensorData(b, [4, 4, 1], "B [4×4]"),
            new TensorData(C.Data.ToArray(), [8, 12, 1], "C = A⊗ₖB [8×12]")
        );
    }

    static void Add()
    {
        double[] a = [1, 2, 3, 4];
        Tensor<double> A = new(a, 1, 4);

        double[] b = [5, 6, 7, 8];
        Tensor<double> B = new(b, 1, 4);

        double[] dest = new double[4];
        Tensor<double> C = Tensor<double>.Add(A, 1.0, B, 1.5, dest);

        PrintTensors(A, B, C);

        Visualize(
            "Сложение тензоров",
            new TensorData(a, [1, 4, 1], "A"),
            new TensorData(b, [1, 4, 1], "B"),
            new TensorData(dest, [1, 4, 1], "C = A + 1.5·B")
        );
    }

    static void Symmetrize()
    {
        double[] a = [7, -1, 3, 2, 4, 1, 6, 5];
        Tensor<double> A = new(a, 2, 2, 2);

        double[] sym = new double[8];
        Tensor<double> B = Tensor<double>.Symmetrize(A, sym);

        double[] antisym = new double[8];
        Tensor<double> C = Tensor<double>.Antisymmetrize(A, antisym);

        PrintTensors(A, B, C);

        // Все три на вкладках — удобно сравнивать
        Visualize(
            "Симметрирование и альтернирование",
            TensorData.FromTensor(A, "A [2×2×2]"),
            TensorData.FromTensor(B, "Sym(A)"),
            TensorData.FromTensor(C, "Antisym(A)")
        );
    }

    static void Transpositions()
    {
        double[] a = [7, -1, 3, 2, 4, 1, 6, 5];
        Tensor<double> A = new(a, 2, 2, 2);

        var list = Tensor<double>.Transpositions(A);
        TensorData[] transpositions = new TensorData[list.Count()];

        for (int i = 0; i < list.Count; i++)
        {
            Tensor<double> t = new(list[i], 2, 2, 2);
            transpositions[i] = TensorData.FromTensor(t, $"A [2×2×2] {i + 1}");
        }

        Visualize(
            "Транспонирование тензора", 
            [TensorData.FromTensor(A, "A [2×2×2]"),
            ..transpositions]
        );
    }

    static void Contract()
    {
        double[] a = [1, 2, 3, 4, 5, 6, 7, 8, 9];
        Tensor<double> A = new(a, 3, 3);

        double[] dest = new double[1];
        Tensor<double>.Contract(A, 0, 1, dest);

        PrintTensor(A, 'A');
        Console.WriteLine($"Tr(A) = {dest[0]}");

        Visualize("Свёртка тензора (след)", new TensorData(a, [3, 3, 1], $"A [3×3], Tr = {dest[0]}"));
    }

    static void Contract2()
    {
        double[] a = [7, -1, 3, 2, 4, 1, 6, 5];
        Tensor<double> A = new(a, 2, 2, 2);

        double[] dest = new double[2];
        Tensor<double> B = Tensor<double>.Contract(A, 1, 2, dest);

        PrintTensor(A, 'A');
        PrintTensor(B, 'B');

        Visualize(
            "Свёртка тензоров (матричное произведение)",
            TensorData.FromTensor(A, "A [2×2×2]"),
            new TensorData(dest, [1, 2, 1], "B = Contract(A,1,2)")
        );
    }

    static void Contract3()
    {
        double[] a = [1, 2, 3, 4, 5, 6];
        Tensor<double> A = new(a, 2, 3);

        double[] b = [7, 8, 9, 10, 11, 12];
        Tensor<double> B = new(b, 3, 2);

        double[] dest = new double[4];
        Tensor<double>.Contract(A, B, 1, 0, dest);

        PrintTensor(A, 'A');
        PrintTensor(B, 'B');

        Visualize(
            "Свёртка тензоров (матричное произведение)",
            new TensorData(a, [2, 3, 1], "A [2×3]"),
            new TensorData(b, [3, 2, 1], "B [3×2]"),
            new TensorData(dest, [2, 2, 1], "C = A·B [2×2]")
        );
    }

    // ── Печать ───────────────────────────────────────────────────────────
    static void PrintTensors<T>(Tensor<T> A, Tensor<T> B, Tensor<T> C)
        where T : unmanaged, System.Numerics.ISignedNumber<T>
    {
        PrintTensor(A, 'A'); PrintTensor(B, 'B');
        Console.WriteLine(); PrintTensor(C, 'C');
    }

    static void PrintTensor<T>(Tensor<T> t, char s)
        where T : unmanaged, System.Numerics.ISignedNumber<T>
    {
        Console.WriteLine($"{s} [{String.Join(",", t.Shape)}; " +
            $"{String.Join(",", t.Strides)}]: ({String.Join(", ", t.Data.ToArray())})");
    }
}
