
namespace blazingzephyr.itmo.tensors.impl2;

public ref struct Tensor<T> where T : unmanaged, System.Numerics.INumberBase<T>
{
    private readonly Span<T> data;
    private readonly char[] covec;
    private readonly char[] vec;
    private readonly int[] size;

    public Tensor(Span<T> data, char[] covec, char[] vec, int[] size)
    {
        this.data = data;
        this.covec = covec;
        this.vec = vec;
        this.size = size;
    }

    public ref T this[ReadOnlySpan<(char label, int index)> indices] => ref this.data[FlatIndex(indices)];
    public ref T this[params (char label, int index)[] indices] => ref this[indices];

    public int FlatIndex(ReadOnlySpan<(char label, int index)> indices)
    {
        //for (int i = 0; i < this.vec.Length; i++)
        //{
        //    char symbol = this.vec[i];
        //    if (indices.)
        //}

        return -1;
    }

    public static Tensor<T> OuterProduct(Tensor<T> a, Tensor<T> b, Span<T> dest)
    {
        Tensor<T> result = new Tensor<T>(
            dest,
            [..a.covec, ..b.covec],
            [..a.vec, ..b.vec],
            [..a.size, ..b.size]
        );

        for (int flat = 0; flat < dest.Length; flat++)
        {

        }

        return result;
    }
}