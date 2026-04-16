using Microsoft.Maui.Graphics;

namespace PowerHunter.Controls;

/// <summary>
/// Lightweight circular progress ring for the hero card.
/// </summary>
public sealed class CircularProgressView : GraphicsView, IDrawable
{
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress),
        typeof(double),
        typeof(CircularProgressView),
        0d,
        propertyChanged: OnDrawablePropertyChanged);

    public static readonly BindableProperty TrackColorProperty = BindableProperty.Create(
        nameof(TrackColor),
        typeof(Color),
        typeof(CircularProgressView),
        Color.FromArgb("#ECEEEE"),
        propertyChanged: OnDrawablePropertyChanged);

    public static readonly BindableProperty ProgressStartColorProperty = BindableProperty.Create(
        nameof(ProgressStartColor),
        typeof(Color),
        typeof(CircularProgressView),
        Color.FromArgb("#006B54"),
        propertyChanged: OnDrawablePropertyChanged);

    public static readonly BindableProperty ProgressEndColorProperty = BindableProperty.Create(
        nameof(ProgressEndColor),
        typeof(Color),
        typeof(CircularProgressView),
        Color.FromArgb("#24FFCD"),
        propertyChanged: OnDrawablePropertyChanged);

    public static readonly BindableProperty StrokeThicknessProperty = BindableProperty.Create(
        nameof(StrokeThickness),
        typeof(float),
        typeof(CircularProgressView),
        14f,
        propertyChanged: OnDrawablePropertyChanged);

    public CircularProgressView()
    {
        Drawable = this;
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public Color TrackColor
    {
        get => (Color)GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    public Color ProgressStartColor
    {
        get => (Color)GetValue(ProgressStartColorProperty);
        set => SetValue(ProgressStartColorProperty, value);
    }

    public Color ProgressEndColor
    {
        get => (Color)GetValue(ProgressEndColorProperty);
        set => SetValue(ProgressEndColorProperty, value);
    }

    public float StrokeThickness
    {
        get => (float)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var normalizedProgress = Math.Clamp(Progress, 0, 1);
        var strokeThickness = Math.Max(StrokeThickness, 1f);
        var padding = strokeThickness / 2f + 2f;
        var diameter = Math.Max(0, Math.Min(dirtyRect.Width, dirtyRect.Height) - (padding * 2f));
        var x = dirtyRect.Center.X - (diameter / 2f);
        var y = dirtyRect.Center.Y - (diameter / 2f);

        canvas.Antialias = true;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeSize = strokeThickness;

        canvas.StrokeColor = TrackColor;
        canvas.DrawCircle(dirtyRect.Center.X, dirtyRect.Center.Y, diameter / 2f);

        if (normalizedProgress <= 0)
            return;

        canvas.StrokeColor = BlendColor(ProgressStartColor, ProgressEndColor, 0.5f);
        canvas.DrawArc(
            x,
            y,
            diameter,
            diameter,
            -90,
            (float)(360d * normalizedProgress),
            false,
            false);
    }

    private static void OnDrawablePropertyChanged(BindableObject bindable, object? oldValue, object? newValue)
        => ((CircularProgressView)bindable).Invalidate();

    private static Color BlendColor(Color start, Color end, float amount)
    {
        var normalizedAmount = Math.Clamp(amount, 0f, 1f);
        var red = start.Red + ((end.Red - start.Red) * normalizedAmount);
        var green = start.Green + ((end.Green - start.Green) * normalizedAmount);
        var blue = start.Blue + ((end.Blue - start.Blue) * normalizedAmount);
        var alpha = start.Alpha + ((end.Alpha - start.Alpha) * normalizedAmount);

        return new Color(red, green, blue, alpha);
    }
}
