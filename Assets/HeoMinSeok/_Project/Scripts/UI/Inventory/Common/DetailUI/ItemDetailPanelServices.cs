using System;

public sealed class ItemDetailPanelServices
{
    public GlossaryDatabase glossary;
    public Func<string, string> formatText;
    public Action<string> showGlossary;
}
