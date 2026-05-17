using Plugin.Maui.Audio;
using SongRequestForMobile.Services;

namespace SongRequestForMobile.Pages;

public sealed class PlayerPage : ContentPage
{
    private readonly AppState _appState;
    private readonly Label _statusLabel;
    private readonly Label _fileLabel;
    private readonly IAudioManager _audioManager;
    private IAudioPlayer? _audioPlayer;
    private Stream? _audioStream;

    public PlayerPage(AppState appState)
    {
        _appState = appState;
        _audioManager = AudioManager.Current;

        _fileLabel = new Label
        {
            Text = "No audio selected.",
            LineBreakMode = LineBreakMode.WordWrap
        };

        _statusLabel = new Label
        {
            Text = "Nothing is playing yet.",
            LineBreakMode = LineBreakMode.WordWrap
        };

        Title = "Player";
        Content = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = "In-app player",
                    FontSize = 24,
                    FontAttributes = FontAttributes.Bold
                },
                _fileLabel,
                _statusLabel,
                new HorizontalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        new Button { Text = "Play last download", Command = new Command(PlayLastDownloaded) },
                        new Button { Text = "Pause", Command = new Command(PausePlayback) },
                        new Button { Text = "Stop", Command = new Command(StopPlayback) }
                    }
                }
            }
        };

        RefreshPlaybackInfo();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshPlaybackInfo();
    }

    private void RefreshPlaybackInfo()
    {
        _fileLabel.Text = string.IsNullOrWhiteSpace(_appState.LastDownloadedFilePath)
            ? "No audio selected."
            : Path.GetFileName(_appState.LastDownloadedFilePath);
    }

    private void PlayLastDownloaded()
    {
        var filePath = _appState.LastDownloadedFilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _statusLabel.Text = "No downloaded audio available yet.";
            return;
        }

        if (!File.Exists(filePath))
        {
            _statusLabel.Text = "The last downloaded file is no longer available.";
            return;
        }

        PlayFile(filePath);
    }

    private void PlayFile(string filePath)
    {
        try
        {
            DisposePlayer();
            _audioStream = File.OpenRead(filePath);
            _audioPlayer = _audioManager.CreatePlayer(_audioStream);
            _audioPlayer.Play();
            _appState.SetLastDownloadedFilePath(filePath);
            RefreshPlaybackInfo();
            _statusLabel.Text = $"Playing in app: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Playback failed: {ex.Message}";
        }
    }

    private void PausePlayback()
    {
        if (_audioPlayer == null)
        {
            _statusLabel.Text = "Nothing is playing yet.";
            return;
        }

        _audioPlayer.Pause();
        _statusLabel.Text = "Playback paused.";
    }

    private void StopPlayback()
    {
        if (_audioPlayer == null)
        {
            _statusLabel.Text = "Nothing is playing yet.";
            return;
        }

        _audioPlayer.Stop();
        _statusLabel.Text = "Playback stopped.";
    }

    private void DisposePlayer()
    {
        try
        {
            _audioPlayer?.Stop();
        }
        catch
        {
        }

        _audioPlayer?.Dispose();
        _audioPlayer = null;
        _audioStream?.Dispose();
        _audioStream = null;
    }
}
