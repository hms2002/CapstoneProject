public interface IItemDetailView
{
    bool CanShow(object def);
    void Show(object def, ItemDetailContext ctx, ItemDetailPanelServices services);
    void Hide();
}
