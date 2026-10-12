namespace CMAScenes
{
    internal static class UiInput
    {
        internal static bool IsTextControl(string name) => name == "search" || name == "hex" || name == "profile" || name == "outfit";
    }
}
