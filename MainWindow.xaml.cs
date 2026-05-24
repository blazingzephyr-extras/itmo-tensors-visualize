
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Geometry;
using HelixToolkit.Wpf;

namespace blazingzephyr.itmo.tensors;

public partial class MainWindow : Window
{
    private readonly List<TensorData> _tensors;

    // Состояние каждой вкладки хранится здесь, а не в полях окна
    private readonly List<TabState> _states = [];

    // ── Конструкторы ─────────────────────────────────────────────────────
    public MainWindow(String title, IEnumerable<TensorData> tensors)
    {
        _tensors = tensors.ToList();
        Init(title);
    }

    private void Init(string title)
    {
        InitializeComponent();
        Title = title;
        Loaded += (_, _) => BuildTabs();
    }

    // ── Построение вкладок ───────────────────────────────────────────────
    private void BuildTabs()
    {
        tabControl.Items.Clear();
        _states.Clear();

        foreach (var data in _tensors)
        {
            var state = new TabState(data);
            _states.Add(state);

            var tab = new TabItem
            {
                Header = data.Label,
                Content = BuildTabContent(state),
            };
            tabControl.Items.Add(tab);
        }

        // Рендерим первую вкладку сразу
        if (_states.Count > 0)
            RebuildMesh(_states[0]);

        // Сразу выбираем первую вкладку.
        tabControl.SelectedIndex = 0;

        tabControl.SelectionChanged += (_, _) =>
        {
            if (tabControl.SelectedIndex >= 0)
                RebuildMesh(_states[tabControl.SelectedIndex]);
        };
    }

    // ── Создание содержимого одной вкладки ───────────────────────────────
    private static Grid BuildTabContent(TabState state)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Viewport
        var viewport = new HelixViewport3D
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 46)),
            ShowCoordinateSystem = true,
        };
        viewport.Children.Add(new DefaultLights());

        var model = new ModelVisual3D();
        viewport.Children.Add(model);

        state.Viewport = viewport;
        state.TensorModel = model;

        Grid.SetRow(viewport, 0);
        grid.Children.Add(viewport);

        // Панель управления
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 4, 8, 4),
        };

        // Слайдер среза Z
        panel.Children.Add(MakeLabel("Срез Z:"));
        var sliceSlider = new Slider
        {
            Width = 180,
            Minimum = 0,
            Maximum = Math.Max(state.SizeZ - 1, 1),
            Value = 0,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
        };
        var sliceLabel = MakeLabel("0");
        sliceSlider.ValueChanged += (_, e) =>
        {
            state.SliceZ = (int)e.NewValue;
            sliceLabel.Text = state.SliceZ.ToString();
            RebuildMesh(state);
        };
        panel.Children.Add(sliceSlider);
        panel.Children.Add(sliceLabel);

        // Кнопка сброса среза
        var resetSlice = MakeButton("Полный куб");
        resetSlice.Click += (_, _) =>
        {
            sliceSlider.Value = 0;
            state.SliceZ = -1;
            sliceLabel.Text = "—";
            RebuildMesh(state);
        };
        panel.Children.Add(resetSlice);

        // Слайдер прозрачности
        panel.Children.Add(MakeLabel("  Прозрачность:"));
        var alphaSlider = new Slider
        {
            Width = 140,
            Minimum = 0.1,
            Maximum = 1.0,
            Value = 0.85,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
        };
        alphaSlider.ValueChanged += (_, e) =>
        {
            state.Alpha = e.NewValue;
            RebuildMesh(state);
        };
        panel.Children.Add(alphaSlider);

        // Кнопка сброса камеры
        var resetCamera = MakeButton("  Сбросить камеру");
        resetCamera.Click += (_, _) => viewport.ZoomExtents(500);
        panel.Children.Add(resetCamera);

        Grid.SetRow(panel, 1);
        grid.Children.Add(panel);

        return grid;
    }

    // ── Перестройка сцены для конкретной вкладки ─────────────────────────
    private static void RebuildMesh(TabState state)
    {
        var tensor = state.Tensor;
        int sliceZ = state.SliceZ;
        var group = new Model3DGroup();

        float minVal = float.MaxValue, maxVal = float.MinValue;
        foreach (var v in tensor) { if (v < minVal) minVal = v; if (v > maxVal) maxVal = v; }

        const double cellSize = 1.0;
        const double gap = 0.05;
        const double step = cellSize + gap;

        for (int i = 0; i < state.SizeX; i++)
            for (int j = 0; j < state.SizeY; j++)
                for (int k = 0; k < state.SizeZ; k++)
                {
                    if (sliceZ >= 0 && k != sliceZ) continue;

                    float val = tensor[i, j, k];
                    double t = (val - minVal) / (maxVal - minVal + 1e-9);
                    Color color = ValueToColor(t);
                    double alpha = sliceZ >= 0
                        ? state.Alpha
                        : state.Alpha * (0.4 + 0.6 * t);

                    var mat = new DiffuseMaterial(new SolidColorBrush(
                        Color.FromArgb((byte)(alpha * 255), color.R, color.G, color.B)));

                    var builder = new MeshBuilder(false, false);
                    builder.AddBox(
                        new System.Numerics.Vector3((float)(i * step), (float)(j * step), (float)(k * step)),
                        (float)cellSize, (float)cellSize, (float)cellSize);

                    group.Children.Add(new GeometryModel3D(
                        builder.ToMesh().ToWndMeshGeometry3D(), mat)
                    { BackMaterial = mat });
                }

        state.TensorModel!.Content = group;

        bool showLabels = sliceZ >= 0 || (state.SizeX * state.SizeY * state.SizeZ <= 125);
        if (showLabels) AddTensorLabels(state, sliceZ, minVal, maxVal);
        else RemoveLabels(state);
    }

    // ── Подписи значений ─────────────────────────────────────────────────
    private static void AddTensorLabels(TabState state, int sliceZ, float minVal, float maxVal)
    {
        RemoveLabels(state);

        const double step = 1.05;
        const double cellSize = 1.0;
        var tensor = state.Tensor;

        for (int i = 0; i < state.SizeX; i++)
            for (int j = 0; j < state.SizeY; j++)
                for (int k = 0; k < state.SizeZ; k++)
                {
                    if (sliceZ >= 0 && k != sliceZ) continue;

                    bool isOnSurface = sliceZ >= 0
                        || i == 0 || i == state.SizeX - 1
                        || j == 0 || j == state.SizeY - 1
                        || k == 0 || k == state.SizeZ - 1;

                    if (!isOnSurface) continue;

                    float val = tensor[i, j, k];
                    double t = (val - minVal) / (maxVal - minVal + 1e-9);
                    Color col = ValueToColor(t);
                    double lum = 0.299 * col.R + 0.587 * col.G + 0.114 * col.B;

                    double ox = 0, oy = 0, oz = 0;
                    if (sliceZ < 0)
                    {
                        if (i == 0) ox = -cellSize * 0.55;
                        if (i == state.SizeX - 1) ox = cellSize * 0.55;
                        if (j == 0) oy = -cellSize * 0.55;
                        if (j == state.SizeY - 1) oy = cellSize * 0.55;
                        if (k == 0) oz = -cellSize * 0.55;
                        if (k == state.SizeZ - 1) oz = cellSize * 0.55;
                    }

                    var label = new BillboardTextVisual3D
                    {
                        Text = val.ToString("F2"),
                        Position = new Point3D(i * step + ox, j * step + oy, k * step + oz),
                        FontSize = 11,
                        Foreground = new SolidColorBrush(lum > 140 ? Colors.Black : Colors.White),
                        Background = new SolidColorBrush(Color.FromArgb(200, col.R, col.G, col.B)),
                        Padding = new Thickness(2),
                        DepthOffset = 0.01,
                    };

                    state.Viewport!.Children.Add(label);
                }
    }

    private static void RemoveLabels(TabState state)
    {
        foreach (var v in state.Viewport!.Children.OfType<BillboardTextVisual3D>().ToList())
            state.Viewport.Children.Remove(v);
    }

    // ── Colormap ─────────────────────────────────────────────────────────
    private static Color ValueToColor(double t)
    {
        Color[] stops =
        [
            Color.FromRgb(0,   0,   200),
            Color.FromRgb(0,   200, 200),
            Color.FromRgb(0,   200, 0),
            Color.FromRgb(200, 200, 0),
            Color.FromRgb(220, 30,  30),
        ];
        double pos = t * (stops.Length - 1);
        int lo = Math.Min((int)pos, stops.Length - 2);
        double f = pos - lo;
        return Color.FromRgb(
            (byte)(stops[lo].R + f * (stops[lo + 1].R - stops[lo].R)),
            (byte)(stops[lo].G + f * (stops[lo + 1].G - stops[lo].G)),
            (byte)(stops[lo].B + f * (stops[lo + 1].B - stops[lo].B)));
    }

    // ── Вспомогательные UI-элементы ──────────────────────────────────────
    private static TextBlock MakeLabel(string text) => new()
    {
        Text = text,
        Foreground = Brushes.White,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(8, 0, 0, 0),
    };

    private static Button MakeButton(string text) => new()
    {
        Content = text,
        Padding = new Thickness(8, 2, 8, 2),
        Margin = new Thickness(8, 2, 0, 2),
        VerticalAlignment = VerticalAlignment.Center,
    };
}

// ── Состояние одной вкладки ───────────────────────────────────────────────
internal class TabState
{
    public float[,,] Tensor { get; }
    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ { get; }
    public double Alpha { get; set; } = 0.85;
    public int SliceZ { get; set; } = -1;  // -1 = полный куб
    public HelixViewport3D? Viewport { get; set; }
    public ModelVisual3D? TensorModel { get; set; }

    public TabState(TensorData data)
    {
        Tensor = data.ToFloat3D();
        SizeX = Tensor.GetLength(0);
        SizeY = Tensor.GetLength(1);
        SizeZ = Tensor.GetLength(2);
    }
}

// ── Контейнер данных для передачи между потоками ─────────────────────────
public class TensorData
{
    public double[] Data { get; }
    public int[] Shape { get; }
    public string Label { get; }

    public TensorData(double[] data, int[] shape, string label = "Tensor")
    {
        Data = data;
        Shape = shape;
        Label = label;
    }

    public static TensorData FromTensor(impl1.Tensor<double> tensor, string label = "Tensor")
        => new(tensor.Data.ToArray(), tensor.Shape, label);

    public float[,,] ToFloat3D()
    {
        int sx = Shape.Length > 0 ? Shape[0] : 1;
        int sy = Shape.Length > 1 ? Shape[1] : 1;
        int sz = Shape.Length > 2 ? Shape[2] : 1;

        var result = new float[sx, sy, sz];
        int[] idx = new int[Shape.Length];
        int[] strides = new int[Shape.Length];

        if (Shape.Length > 0)
        {
            strides[^1] = 1;
            for (int d = Shape.Length - 2; d >= 0; d--)
                strides[d] = strides[d + 1] * Shape[d + 1];
        }

        for (int i = 0; i < sx; i++)
            for (int j = 0; j < sy; j++)
                for (int k = 0; k < sz; k++)
                {
                    if (Shape.Length > 0) idx[0] = i;
                    if (Shape.Length > 1) idx[1] = j;
                    if (Shape.Length > 2) idx[2] = k;

                    int flat = 0;
                    for (int d = 0; d < Shape.Length; d++)
                        flat += idx[d] * strides[d];

                    result[i, j, k] = (float)Data[flat];
                }

        return result;
    }
}
