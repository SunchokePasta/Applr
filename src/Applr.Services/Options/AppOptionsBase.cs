namespace Applr.Services.Options;

/// <summary>
/// Base for bindable configuration-section options classes. Currently just
/// a marker with one no-op hook -- exists so a shared behaviour (e.g.
/// validation, a common property) can be added in one place later without
/// touching every derived options class.
/// </summary>
public abstract class AppOptionsBase
{
    /// <summary>
    /// Called by whoever binds the section, immediately after binding, so a
    /// bad value fails at startup next to the section name rather than much
    /// later at the point of use. Override when there's something worth
    /// checking; the base no-op is the right answer for a section whose
    /// values are all independently valid.
    /// </summary>
    public virtual void Validate()
    {
    }
}
