using System.Windows.Input;
using SongRequestForMobile.Services;

namespace SongRequestForMobile.Pages;

public class UpdatePopupViewModel
{
    private readonly UpdatePopup _popup;
    private readonly IUpdateDownloadService _downloadService;
    private readonly UpdateInfo _updateInfo;

    public string CurrentVersion { get; }
    public string LatestVersion { get; }

    public ICommand UpdateCommand { get; }
    public ICommand IgnoreCommand { get; }

    public UpdatePopupViewModel(UpdatePopup popup, IUpdateDownloadService downloadService, UpdateInfo updateInfo)
    {
        _popup = popup;
        _downloadService = downloadService;
        _updateInfo = updateInfo;

        CurrentVersion = updateInfo.CurrentVersion;
        LatestVersion = updateInfo.LatestVersion;

        UpdateCommand = new Command(async () => await OnUpdateAsync());
        IgnoreCommand = new Command(async () => await OnIgnoreAsync());

        _popup.SetReleaseNotes(updateInfo.ReleaseNotes);
    }

    private async Task OnUpdateAsync()
    {
        _popup.ShowProgress();

        var progress = new Progress<UpdateProgress>(report =>
        {
            _popup.UpdateProgress(report.Percentage, report.Status);
        });

        var result = await _downloadService.DownloadAndInstallAsync(_updateInfo.DownloadUrl, progress);

        if (result)
        {
            // APK install will close the app
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await _popup.DisplayAlert("Update", "Update started. The app will restart.", "OK");
            });
        }
        else
        {
            _popup.HideProgress();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await _popup.DisplayAlert("Error", "Failed to download or install update.", "OK");
            });
        }
    }

    private async Task OnIgnoreAsync()
    {
        await _popup.Navigation.PopModalAsync();
    }
}
