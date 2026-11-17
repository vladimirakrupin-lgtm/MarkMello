using Avalonia.Media;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// A loaded (or failed) inline image, cached by the fragment until its
/// styled text, resolver or theme changes. <see cref="BackingStream"/> is the
/// decoded source and is disposed together with the image.
/// </summary>
internal sealed record MarkdownInlineImageState(IImage? Image, Stream? BackingStream, bool Failed)
{
    public static MarkdownInlineImageState FailedState { get; } = new(null, null, true);
}
