using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Yoink.Capture;
using Yoink.Models;
using Yoink.Services;
using Yoink.UI;

namespace Yoink;

public partial class App
{
    private void HandleCaptureResult(Bitmap result)
    {
        SoundService.PlayCaptureSound();

        var settings = _settingsService!.Settings;
        var ext = CaptureOutputService.GetExtension(settings.CaptureImageFormat);
        string? requestedPath = null;
        if (settings.SaveToFile)
        {
            var defaultPath = Path.Combine(settings.SaveDirectory, $"{Helpers.FileNameTemplate.Format(settings.FileNameTemplate)}.{ext}");
            if (settings.AskForFileNameOnSave)
            {
                // SaveFileDialog must run on the WPF dispatcher thread
                string? resolved = null;
                Dispatcher.Invoke(() => resolved = ResolveSavePath(defaultPath, settings.CaptureImageFormat));
                requestedPath = resolved;
            }
            else
            {
                requestedPath = defaultPath;
            }
            if (requestedPath is null)
            {
                result.Dispose();
                _isCapturing = false;
                return;
            }
        }

        _ = PersistCaptureAsync(result, requestedPath, saveHistory: settings.SaveHistory, isSticker: false, providerName: null)
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        _isCapturing = false;
                        ToastWindow.ShowError("Capture error", task.Exception?.GetBaseException().Message ?? "Capture failed");
                        ScheduleIdleMemoryTrim();
                    });
                    return;
                }

                var persisted = task.Result;
                Dispatcher.BeginInvoke(async () =>
                {
                    var action = NormalizeAfterCaptureAction(settings.AfterCapture);
                    bool willPreview = ShouldPreviewAfterCapture(action);
                    bool willUpload = persisted.FilePath != null
                        && settings.AutoUploadScreenshots
                        && settings.ImageUploadDestination != UploadDestination.None;

                    _isCapturing = false; // unlock the capture pipeline immediately

                    // Snapshot the bitmap into the preview window (synchronous on the
                    // dispatcher) BEFORE starting the background PNG encode. This keeps
                    // reads of the GDI+ bitmap serialized — the preview's ToBitmapSource
                    // call finishes before the clipboard task's Bitmap.Save begins.
                    if (willPreview && !willUpload)
                        ToastWindow.ShowImagePreview(persisted.Output, persisted.FilePath, settings.AutoPinPreviews);

                    Task copyTask = Task.CompletedTask;
                    if (ShouldCopyAfterCapture(action))
                        copyTask = ClipboardService.CopyToClipboardAsync(persisted.Output);

                    if (willUpload)
                    {
                        // Wait for the clipboard encode to finish before disposing the bitmap.
                        try { await copyTask; } catch { }
                        persisted.Output.Dispose();
                        _ = UploadFileAsync(persisted.FilePath!, "Screenshot", persisted.HistoryEntry);
                    }
                    else if (!willPreview)
                    {
                        try { await copyTask; } catch { }
                        persisted.Output.Dispose();
                        ToastWindow.Show("Screenshot ready", "", persisted.FilePath);
                    }
                    // willPreview && !willUpload: the preview window retains the bitmap
                    // via ToastWindow._previewBitmap, so the clipboard task can finish in
                    // the background without an explicit await.

                    ScheduleIdleMemoryTrim();
                });
            }, TaskScheduler.Default);
    }

    private void HandleStickerResult(Bitmap result, string providerName)
    {
        SoundService.PlayCaptureSound();

        var settings = _settingsService!.Settings;
        string? requestedPath = null;
        if (settings.SaveToFile)
        {
            var defaultStickerPath = Path.Combine(settings.SaveDirectory, $"{Helpers.FileNameTemplate.Format(settings.FileNameTemplate)}_sticker.png");
            requestedPath = settings.AskForFileNameOnSave
                ? ResolveSavePath(defaultStickerPath, CaptureImageFormat.Png)
                : defaultStickerPath;
            if (requestedPath is null)
            {
                result.Dispose();
                _isCapturing = false;
                return;
            }
        }

        _ = PersistCaptureAsync(result, requestedPath, saveHistory: settings.SaveHistory, isSticker: true, providerName: providerName)
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        _isCapturing = false;
                        ToastWindow.ShowError("Sticker error", task.Exception?.GetBaseException().Message ?? "Sticker processing failed");
                        ScheduleIdleMemoryTrim();
                    });
                    return;
                }

                var persisted = task.Result;
                Dispatcher.BeginInvoke(async () =>
                {
                    var action = NormalizeAfterCaptureAction(settings.AfterCapture);
                    bool willPreview = ShouldPreviewAfterCapture(action);
                    bool willCopy = ShouldCopyAfterCapture(action);

                    _isCapturing = false;

                    // Show the preview synchronously first (it snapshots the bitmap into
                    // a BitmapSource on the dispatcher), then kick off the PNG encode on
                    // a background thread so the two reads don't interleave.
                    if (willPreview)
                        ToastWindow.ShowImagePreview(persisted.Output, persisted.FilePath, settings.AutoPinPreviews);

                    Task copyTask = Task.CompletedTask;
                    if (willCopy)
                        copyTask = ClipboardService.CopyToClipboardAsync(persisted.Output);

                    if (!willPreview)
                    {
                        try { await copyTask; } catch { }
                        persisted.Output.Dispose();
                        ToastWindow.Show(willCopy ? "Sticker copied" : "Sticker ready");
                    }
                    // willPreview: the preview window retains the bitmap for the lifetime
                    // of the clipboard encode task.

                    if (persisted.FilePath != null && settings.AutoUploadScreenshots
                        && settings.ImageUploadDestination != UploadDestination.None)
                    {
                        _ = UploadFileAsync(persisted.FilePath, "Sticker", persisted.HistoryEntry);
                    }

                    ScheduleIdleMemoryTrim();
                });
            }, TaskScheduler.Default);
    }

    private Task<PersistedCaptureResult> PersistCaptureAsync(
        Bitmap source,
        string? requestedPath,
        bool saveHistory,
        bool isSticker,
        string? providerName)
    {
        var settings = _settingsService!.Settings;
        int maxLongEdge = settings.CaptureMaxLongEdge;
        var captureFormat = settings.CaptureImageFormat;
        int jpegQuality = settings.JpegQuality;

        return Task.Run(() =>
        {
            using (source)
            {
                var prepared = CaptureOutputService.PrepareBitmap(source, maxLongEdge);
                var output = prepared;
                string? filePath = requestedPath;
                Services.HistoryEntry? historyEntry = null;
                var historyService = saveHistory ? EnsureHistoryService() : null;

                if (requestedPath != null)
                {
                    var directory = Path.GetDirectoryName(requestedPath);
                    if (string.IsNullOrWhiteSpace(directory))
                        throw new InvalidOperationException("Save path must include a directory.");

                    Directory.CreateDirectory(directory);
                    if (isSticker)
                        output.Save(requestedPath, ImageFormat.Png);
                    else
                        CaptureOutputService.SaveBitmap(output, requestedPath, captureFormat, jpegQuality);

                    filePath = requestedPath;
                }

                if (historyService != null)
                {
                    if (filePath != null && !isSticker)
                    {
                        historyEntry = historyService.TrackExistingCapture(
                            filePath,
                            output.Width,
                            output.Height,
                            isSticker ? HistoryKind.Sticker : HistoryKind.Image,
                            providerName);
                    }
                    else
                    {
                        historyEntry = isSticker
                            ? historyService.SaveStickerEntry(output, providerName)
                            : historyService.SaveCapture(output);
                        filePath = historyEntry.FilePath;
                    }
                }

                if (historyEntry is not null)
                    SettingsWindow.WarmRecentHistoryThumbs(new[] { historyEntry }, maxCount: 1);

                return new PersistedCaptureResult
                {
                    Output = output,
                    FilePath = filePath,
                    HistoryEntry = historyEntry
                };
            }
        });
    }

    private static AfterCaptureAction NormalizeAfterCaptureAction(AfterCaptureAction action) =>
        Enum.IsDefined(typeof(AfterCaptureAction), action)
            ? action
            : AfterCaptureAction.PreviewAndCopy;

    private static bool ShouldCopyAfterCapture(AfterCaptureAction action) =>
        action is AfterCaptureAction.CopyToClipboard or AfterCaptureAction.PreviewAndCopy;

    private static bool ShouldPreviewAfterCapture(AfterCaptureAction action) =>
        action is AfterCaptureAction.PreviewAndCopy or AfterCaptureAction.PreviewOnly;

    private void HandleOcrResult(Bitmap result)
    {
        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                var langTag = _settingsService?.Settings.OcrLanguageTag;
                string text = await OcrService.RecognizeAsync(result, langTag);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    SoundService.PlayTextSound();

                    if (_settingsService!.Settings.SaveHistory)
                        EnsureHistoryService().SaveOcrEntry(text);

                    // Open OCR result window instead of copying directly
                    var window = new OcrResultWindow(text, _settingsService);
                    window.Show();
                }
                else
                {
                    ToastWindow.Show("OCR", "No text found");
                }
            }
            catch (Exception ex)
            {
                ToastWindow.ShowError("OCR error", ex.Message);
            }
            finally { result.Dispose(); }
            ScheduleIdleMemoryTrim();
        });
    }
}
