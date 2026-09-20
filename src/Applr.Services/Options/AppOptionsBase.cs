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
    /// Not currently called by anything. Reserved for a future shared
    /// validation/registration path; override when there's something
    /// worth checking.
    /// </summary>
    public virtual void Validate()
    {
    }
}
