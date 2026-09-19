// Anything the player can look at and press the interact key on while on foot
public interface IInteractable {
    string Prompt { get; }
    bool CanInteract { get; }
    void Interact();
    // The player started or stopped looking at this
    void SetFocused(bool focused);
}
