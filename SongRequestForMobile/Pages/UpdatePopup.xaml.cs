namespace SongRequestForMobile.Pages;

public partial class UpdatePopup : ContentPage
{
    public UpdatePopup()
    {
        InitializeComponent();
    }

    public void SetReleaseNotes(string notes)
    {
        ReleaseNotesLabel.Text = notes;
    }

    public void ShowProgress()
    {
        ProgressContainer.IsVisible = true;
        DownloadProgressBar.IsVisible = true;
        ButtonsContainer.IsEnabled = false;
    }

    public void HideProgress()
    {
        ProgressContainer.IsVisible = false;
        DownloadProgressBar.IsVisible = false;
        ButtonsContainer.IsEnabled = true;
    }

    public void UpdateProgress(int percentage, string status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DownloadProgressBar.Progress = percentage / 100.0;
            ProgressStatusLabel.Text = status;
        });
    }
}
