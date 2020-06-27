namespace Digi.BuildInfo.Features.TextAPIMenu
{
    public interface IItem
    {
        bool Interactable { get; set; }

        void UpdateTitle();

        void UpdateValue();
    }
}
