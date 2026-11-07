using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace NameTagGenerator.Views;

/// <summary>The rotatable 3D view. Left-drag rotates, the wheel zooms, right-drag pans.</summary>
public partial class PreviewView : UserControl
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(Model3D), typeof(PreviewView), new PropertyMetadata(null, (d, _) => ((PreviewView)d).OnModelChanged()));

    const double DefaultYaw = 0, DefaultPitch = 55;

    double _yaw = DefaultYaw, _pitch = DefaultPitch, _zoom = 1;
    Vector3D _pan;
    Point3D _center;
    // Half the model's size as seen from the default view, which is what "fit to view" means here.
    double _halfWide = 50, _halfTall = 50;
    Point _lastMouse;
    double _reservedRight;

    public PreviewView()
    {
        InitializeComponent();
        UpdateCamera();
    }

    public Model3D? Model
    {
        get => (Model3D?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public void ResetView()
    {
        (_yaw, _pitch, _zoom, _pan) = (DefaultYaw, DefaultPitch, 1, default);
        UpdateCamera();
    }

    /// <summary>Draws the model only in the part left of this width, so something floating on the right does not cover it.</summary>
    public void ReserveRight(double width)
    {
        _reservedRight = width;
        View.Margin = new Thickness(0, 0, width, 0);
        UpdateCamera();
    }

    void OnModelChanged()
    {
        ModelHolder.Content = Model;
        if (Model is not null)
        {
            var b = Model.Bounds;
            _center = new Point3D(b.X + b.SizeX / 2, b.Y + b.SizeY / 2, b.Z + b.SizeZ / 2);
            var pitch = DefaultPitch * Math.PI / 180;
            _halfWide = Math.Max(5, b.SizeX / 2);
            _halfTall = Math.Max(5, (b.SizeY * Math.Sin(pitch) + b.SizeZ * Math.Cos(pitch)) / 2);
        }
        UpdateCamera();
    }

    void UpdateCamera()
    {
        var width = ActualWidth - _reservedRight;
        var aspect = width > 0 && ActualHeight > 0 ? width / ActualHeight : 1.5;
        var halfFov = Camera.FieldOfView / 2 * Math.PI / 180;
        // FieldOfView is horizontal, so the vertical view is narrower by the aspect ratio.
        // Near the camera the model looks larger, hence the margin.
        var fit = Math.Max(_halfWide, _halfTall * aspect) / Math.Tan(halfFov) * 1.3;
        var distance = fit * _zoom;

        double yaw = _yaw * Math.PI / 180, pitch = _pitch * Math.PI / 180;
        var direction = new Vector3D(Math.Sin(yaw) * Math.Cos(pitch), -Math.Cos(yaw) * Math.Cos(pitch), Math.Sin(pitch));
        var target = _center + _pan;
        Camera.Position = target + direction * distance;
        Camera.LookDirection = -direction * distance;
        Camera.NearPlaneDistance = Math.Max(0.5, distance / 200);
        Camera.FarPlaneDistance = distance * 20;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateCamera();
    }

    void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _lastMouse = e.GetPosition(Surface);
        Surface.CaptureMouse();
    }

    void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Released && e.RightButton == MouseButtonState.Released)
            Surface.ReleaseMouseCapture();
    }

    void OnMouseMove(object sender, MouseEventArgs e)
    {
        var now = e.GetPosition(Surface);
        var delta = now - _lastMouse;
        _lastMouse = now;

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _yaw -= delta.X * 0.4;
            _pitch = Math.Clamp(_pitch + delta.Y * 0.4, -85, 89);
            UpdateCamera();
        }
        else if (e.RightButton == MouseButtonState.Pressed)
        {
            var look = Camera.LookDirection;
            var right = Vector3D.CrossProduct(look, Camera.UpDirection);
            var up = Vector3D.CrossProduct(right, look);
            right.Normalize();
            up.Normalize();
            var perPixel = look.Length * 2 * Math.Tan(Camera.FieldOfView / 2 * Math.PI / 180) / Math.Max(1, ActualWidth);
            _pan += (-right * delta.X + up * delta.Y) * perPixel;
            UpdateCamera();
        }
    }

    void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _zoom = Math.Clamp(_zoom * Math.Pow(0.999, e.Delta), 0.1, 5);
        UpdateCamera();
    }
}
