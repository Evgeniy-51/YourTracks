using System.Windows.Markup;

namespace MetadataEditor.App.Localization;

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public required string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
