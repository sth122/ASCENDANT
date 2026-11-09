public class LoadingScreenModel : IUIModel
{
    public float Progress { get; private set; } = 0f;
    public string DescriptionText { get; private set; } = "Loading...";

    public void SetProgress(float progress, string description = null)
    {
        Progress = UnityEngine.Mathf.Clamp01(progress);
        if (!string.IsNullOrEmpty(description))
        {
            DescriptionText = description;
        }
    }

    public void Reset()
    {
        Progress = 0f;
        DescriptionText = "Loading...";
    }
}