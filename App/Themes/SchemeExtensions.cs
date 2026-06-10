namespace Opcilloscope.App.Themes;

/// <summary>
/// Helpers for applying a <see cref="Scheme"/> to a view.
/// Terminal.Gui 2.4 replaced the settable <c>View.ColorScheme</c> property with the
/// <c>SetScheme(Scheme)</c> method, which cannot be used inside an object initializer.
/// <see cref="WithScheme{T}"/> restores a fluent form so a scheme can still be applied
/// in a single expression: <c>new Label { Text = "x" }.WithScheme(scheme)</c>.
/// </summary>
public static class SchemeExtensions
{
    /// <summary>
    /// Applies <paramref name="scheme"/> to <paramref name="view"/> and returns the view
    /// so the call can be chained onto a constructor expression.
    /// </summary>
    public static T WithScheme<T>(this T view, Scheme scheme) where T : View
    {
        view.SetScheme(scheme);
        return view;
    }
}
