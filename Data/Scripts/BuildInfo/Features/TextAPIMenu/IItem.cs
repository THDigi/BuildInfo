namespace Digi.BuildInfo.Features.TextAPIMenu
{
    public interface IItem
    {
        bool Interactable { get; set; }

        /// <summary>
        /// Updates both value and title.
        /// </summary>
        void Update();
    }
}
