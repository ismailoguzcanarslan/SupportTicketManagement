namespace SupportTicket.Base.Registration;

/// <summary>
/// Entity image aliases shared by registration and code. Use the same constant in
/// <c>[PluginStep(PreImageName = ImageNames.PreImage)]</c> and when reading the image
/// (<c>context.GetChange&lt;T&gt;()</c> reads <see cref="PreImage"/> by default), so a renamed alias
/// cannot make the image silently missing.
/// </summary>
public static class ImageNames
{
    public const string PreImage = "PreImage";

    public const string PostImage = "PostImage";
}
