using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Numerics;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using Come.Models;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Assimp;
using HelixToolkit.SharpDX.Model.Scene;
using HelixToolkit.Wpf.SharpDX;
using MediaPoint3D = System.Windows.Media.Media3D.Point3D;
using MediaVector3D = System.Windows.Media.Media3D.Vector3D;

namespace Come.Views;

[SupportedOSPlatform("windows7.0")]
public partial class PcPreview3D : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(PcPreview3D), new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty HighDetailProperty = DependencyProperty.Register(
        nameof(HighDetail), typeof(bool), typeof(PcPreview3D), new PropertyMetadata(false, OnHighDetailChanged));

    private INotifyCollectionChanged? _observableSource;
    private bool _exploded;
    private int _loadVersion;

    public PcPreview3D()
    {
        EffectsManager = new DefaultEffectsManager();
        Camera3D = CreateCamera();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public IEffectsManager EffectsManager { get; }
    public PerspectiveCamera Camera3D { get; }
    public SceneNodeGroupModel3D GroupModel { get; } = new();

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public bool HighDetail
    {
        get => (bool)GetValue(HighDetailProperty);
        set => SetValue(HighDetailProperty, value);
    }

    private static PerspectiveCamera CreateCamera() => new()
    {
        Position = new MediaPoint3D(0, 0, 13.8),
        LookDirection = new MediaVector3D(0, 0, -13.8),
        UpDirection = new MediaVector3D(0, 1, 0),
        NearPlaneDistance = 0.05,
        FarPlaneDistance = 5000,
        FieldOfView = 40
    };

    private void OnLoaded(object sender, RoutedEventArgs e) => _ = RebuildSceneAsync();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _loadVersion++;
        ClearScene();
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PcPreview3D)d;
        if (control._observableSource is not null) control._observableSource.CollectionChanged -= control.OnCollectionChanged;
        control._observableSource = e.NewValue as INotifyCollectionChanged;
        if (control._observableSource is not null) control._observableSource.CollectionChanged += control.OnCollectionChanged;
        if (control.IsLoaded) _ = control.RebuildSceneAsync();
    }

    private static void OnHighDetailChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PcPreview3D)d;
        if (control.IsLoaded) _ = control.RebuildSceneAsync();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsLoaded) _ = RebuildSceneAsync();
    }

    private async Task RebuildSceneAsync()
    {
        var version = ++_loadVersion;
        LoadingOverlay.Visibility = Visibility.Visible;
        var selected = (ItemsSource?.Cast<object>().OfType<SelectedPartLine>() ?? []).ToArray();
        var requests = CreateRequests(selected);

        try
        {
            var loaded = await Task.Run(() => requests.Select(LoadModel).Where(node => node is not null).Cast<SceneNode>().ToArray());
            if (version != _loadVersion)
            {
                foreach (var node in loaded) node.Dispose();
                return;
            }

            ClearScene();
            foreach (var node in loaded) GroupModel.AddNode(node);
            RenderStatusText.Text = $"실물 GLB {loaded.Length}개 · PBR 재질 · DirectX 11";
            ResetCamera();
        }
        catch
        {
            ClearScene();
            RenderStatusText.Text = "3D 렌더러를 초기화할 수 없습니다";
        }
        finally
        {
            if (version == _loadVersion) LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private IReadOnlyList<ModelRequest> CreateRequests(IReadOnlyList<SelectedPartLine> selected)
    {
        var modelsDirectory = Path.Combine(AppContext.BaseDirectory, "Resources", "Models3D");
        if (!_exploded)
        {
            return [new(Path.Combine(modelsDirectory, "system_unit_update.glb"), Vector3.Zero, HighDetail ? 7.2f : 6.6f)];
        }

        var parts = selected.Count > 0
            ? selected.Where(line => line.Category != PartCategory.Case).ToArray()
            : Enum.GetValues<PartCategory>().Where(category => category != PartCategory.Case)
                .Select(category => new SelectedPartLine(category, string.Empty, string.Empty, Placeholder(category))).ToArray();

        var columns = parts.Length <= 4 ? 2 : 3;
        var spacingX = HighDetail ? 4.6f : 3.8f;
        var spacingY = HighDetail ? 3.7f : 3.1f;
        var list = new List<ModelRequest>();
        for (var index = 0; index < parts.Length; index++)
        {
            var row = index / columns;
            var column = index % columns;
            var x = (column - (columns - 1) / 2f) * spacingX;
            var y = (1.15f - row) * spacingY;
            var targetSize = parts[index].Category is PartCategory.Mainboard or PartCategory.Graphics ? 2.8f : 2.15f;
            list.Add(new(Path.Combine(modelsDirectory, parts[index].Part.Model3DFile), new Vector3(x, y, 0), targetSize));
        }
        return list;
    }

    private SceneNode? LoadModel(ModelRequest request)
    {
        if (!File.Exists(request.Path)) return null;
        var scene = new Importer().Load(request.Path);
        if (scene?.Root is null) return null;

        scene.Root.Attach(EffectsManager);
        scene.Root.UpdateAllTransformMatrix();
        if (scene.Root.TryGetBound(out var bound))
        {
            var maxDimension = Math.Max(bound.Width, Math.Max(bound.Height, bound.Depth));
            var scale = maxDimension > 0.0001f ? request.TargetSize / maxDimension : 1f;
            scene.Root.ModelMatrix =
                Matrix4x4.CreateTranslation(-bound.Center) *
                Matrix4x4.CreateRotationX(-MathF.PI / 2f) *
                Matrix4x4.CreateScale(scale) *
                Matrix4x4.CreateTranslation(request.Position);
            scene.Root.UpdateAllTransformMatrix();
        }
        return scene.Root;
    }

    private static PartItem Placeholder(PartCategory category) => new()
    {
        Id = $"preview-{category}", Category = category, Name = category.ToString(), Manufacturer = "COME",
        Price = 0, Stock = 0, SpecSummary = string.Empty, DetailSummary = string.Empty,
        Accent = "#48E0C2", Glyph = category.ToString(), IsCatalogOnly = true
    };

    private void ClearScene()
    {
        var oldNodes = GroupModel.SceneNode.Items.ToArray();
        GroupModel.Clear(false);
        foreach (var node in oldNodes) node.Dispose();
    }

    private void ResetCamera()
    {
        var distance = _exploded ? (HighDetail ? 22d : 18d) : (HighDetail ? 13.8d : 12.8d);
        Camera3D.Position = new MediaPoint3D(0, 0, distance);
        Camera3D.LookDirection = new MediaVector3D(-Camera3D.Position.X, -Camera3D.Position.Y, -Camera3D.Position.Z);
        Camera3D.UpDirection = new MediaVector3D(0, 1, 0);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _exploded = false;
        ExplodeButton.Content = "분해 보기";
        _ = RebuildSceneAsync();
    }

    private void Explode_Click(object sender, RoutedEventArgs e)
    {
        _exploded = !_exploded;
        ExplodeButton.Content = _exploded ? "조립 보기" : "분해 보기";
        _ = RebuildSceneAsync();
    }

    private sealed record ModelRequest(string Path, Vector3 Position, float TargetSize);
}
