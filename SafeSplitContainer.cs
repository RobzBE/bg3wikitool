using System.Runtime.InteropServices;

namespace BG3ItemExplorer;

/// <summary>
/// WinForms can throw a transient GDI+ ExternalException while repainting a
/// splitter during the first high-DPI layout pass. The panels themselves have
/// already been laid out at that point, so skipping only that splitter repaint
/// is safe; the next normal paint draws it again.
/// </summary>
internal sealed class SafeSplitContainer : SplitContainer
{
    protected override void OnLayout(LayoutEventArgs e)
    {
        try
        {
            base.OnLayout(e);
        }
        catch (ExternalException exception) when (exception.ErrorCode == unchecked((int)0x80004005))
        {
            Invalidate();
        }
    }
}
