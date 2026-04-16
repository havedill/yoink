using System.Drawing;
using System.Windows.Forms;
using Yoink.Models;

namespace Yoink.Capture;

public sealed partial class RegionOverlayForm
{
    /// <summary>
    /// Invoked from the Print Screen low-level hook when the overlay may not have foreground focus.
    /// Marshals to the UI thread and runs the same Escape path as <see cref="ProcessCmdKey"/>.
    /// </summary>
    internal void ProcessEscapeFromGlobalHotkey()
    {
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(ProcessEscapeFromGlobalHotkey);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
            return;
        }

        if (IsDisposed || Disposing || !Visible)
            return;

        var msg = new Message();
        ProcessCmdKey(ref msg, Keys.Escape);
    }

    // ProcessCmdKey always receives ESC (OnKeyDown sometimes doesn't)
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Enter key: in the annotation phase, confirms the capture and bakes annotations.
        if (keyData == Keys.Enter || keyData == Keys.Return)
        {
            if (_selectionCommitted && !_isTyping && !_emojiPickerOpen && !_fontPickerOpen)
            {
                ConfirmCapture();
                return true;
            }
        }

        if (keyData == Keys.Escape)
        {
            // Annotation phase: Escape wipes annotations but keeps the selection locked
            // so the user can start over without restarting the capture.
            if (_selectionCommitted)
            {
                // Let popups/transient state close first via the generic handler below.
                // If none are active, clear the annotations instead.
                if (!_emojiPickerOpen && !_fontPickerOpen && !_colorPickerOpen
                    && !_isPlacingEmoji && !_isRulerDragging && !_isArrowDragging
                    && !_isLineDragging && !_isCurvedArrowDragging && !_isBlurring
                    && !_isEraserDragging && !_isHighlighting && !_isRectShapeDragging
                    && !_isCircleShapeDragging && !_isSelectDragging && !_isSelectResizing
                    && _selectedAnnotationIndex < 0 && !_isTyping)
                {
                    if (_undoStack.Count > 0)
                    {
                        _undoStack.Clear();
                        _nextStepNumber = 1;
                        MarkCommittedAnnotationsDirty();
                        Invalidate();
                    }
                    return true;
                }
            }

            // Pre-commit selection phase: Escape exits print screen entirely.
            if (!_selectionCommitted && (_mode == CaptureMode.Rectangle || _mode == CaptureMode.Freeform))
            {
                Cancel();
                return true;
            }

            // Close all popups and transient state in one pass
            bool anyClosed = false;
            if (_mode == CaptureMode.ColorPicker) { CloseMagWindow(); anyClosed = true; }
            if (_emojiPickerOpen) { _emojiPickerOpen = false; HideEmojiSearchBox(); anyClosed = true; }
            if (_fontPickerOpen) { _fontPickerOpen = false; _fontSearch = ""; _filteredFonts = null; HideFontSearchBox(); anyClosed = true; }
            if (_colorPickerOpen) { _colorPickerOpen = false; anyClosed = true; }
            if (_isPlacingEmoji) { _isPlacingEmoji = false; _selectedEmoji = null; anyClosed = true; }
            if (_isRulerDragging) { _isRulerDragging = false; anyClosed = true; }
            if (_isSelecting) { _isSelecting = false; _hasSelection = false; anyClosed = true; }
            if (_isArrowDragging) { _isArrowDragging = false; anyClosed = true; }
            if (_isLineDragging) { _isLineDragging = false; anyClosed = true; }
            if (_isCurvedArrowDragging) { _isCurvedArrowDragging = false; anyClosed = true; }
            if (_isBlurring) { _isBlurring = false; anyClosed = true; }
            if (_isEraserDragging) { _isEraserDragging = false; anyClosed = true; }
            if (_isHighlighting) { _isHighlighting = false; anyClosed = true; }
            if (_isRectShapeDragging) { _isRectShapeDragging = false; anyClosed = true; }
            if (_isCircleShapeDragging) { _isCircleShapeDragging = false; anyClosed = true; }
            if (_isSelectDragging) { _isSelectDragging = false; anyClosed = true; }
            if (_isSelectResizing) { _isSelectResizing = false; _selectResizeHandle = -1; anyClosed = true; }
            if (_selectedAnnotationIndex >= 0) { _selectedAnnotationIndex = -1; anyClosed = true; }
            if (_isTyping)
            {
                _isTyping = false;
                _textBuffer = "";
                HideTextBox();
                anyClosed = true;
            }
            if (anyClosed)
            {
                Invalidate();
                RefreshToolbar();
                return true;
            }

            // In the annotation phase, Escape never cancels or switches modes once all
            // popups/drags have been closed above. The user stays locked into their
            // committed selection (the earlier annotation-wipe branch handled empty state).
            if (_selectionCommitted)
                return true;

            // If in an annotation tool, return to the last capture mode instead of closing
            if (ToolDef.IsAnnotationTool(_mode))
            {
                SetMode(_lastCaptureMode);
                return true;
            }

            Cancel();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Undo must work in all states (emoji placing, typing, etc.)
        if (e.KeyCode == Keys.Z && e.Control && _undoStack.Count > 0)
        {
            var last = RemoveLastAnnotation();
            // Update step counter when undoing a step number
            if (last is StepNumberAnnotation)
            {
                var remaining = _undoStack.OfType<StepNumberAnnotation>().LastOrDefault();
                _nextStepNumber = remaining != null ? remaining.Number + 1 : 1;
            }
            Invalidate(InflateForRepaint(GetAnnotationBounds(last)));
            return;
        }

        // All search/text input is handled by off-screen TextBoxes
        if (_emojiPickerOpen) return;

        // Emoji placing: Tab re-opens picker
        if (_mode == CaptureMode.Emoji && _isPlacingEmoji)
        {
            if (e.KeyCode == Keys.Tab) { _emojiPickerOpen = true; _isPlacingEmoji = false; ShowEmojiSearchBox(); RefreshToolbar(); }
            return;
        }

        if (_fontPickerOpen) return;
        if (_isTyping) return;
        if (TryHandleAnnotationToolHotkey(e.KeyCode))
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
            RefreshToolbar();
            Invalidate();
            return;
        }

        // Delete selected annotation
        if (e.KeyCode == Keys.Delete && _mode == CaptureMode.Select && _selectedAnnotationIndex >= 0 && _selectedAnnotationIndex < _undoStack.Count)
        {
            var bounds = InflateForRepaint(GetAnnotationBounds(_undoStack[_selectedAnnotationIndex]));
            _undoStack.RemoveAt(_selectedAnnotationIndex);
            MarkCommittedAnnotationsDirty();
            _selectedAnnotationIndex = -1;
            Invalidate(bounds);
            return;
        }
    }

    private bool TryHandleAnnotationToolHotkey(Keys keyCode)
    {
        var settings = Services.SettingsService.LoadStatic();
        if (settings is null)
            return false;

        uint mod = 0;
        if ((ModifierKeys & Keys.Control) != 0) mod |= Native.User32.MOD_CONTROL;
        if ((ModifierKeys & Keys.Alt) != 0) mod |= Native.User32.MOD_ALT;
        if ((ModifierKeys & Keys.Shift) != 0) mod |= Native.User32.MOD_SHIFT;
        uint vk = unchecked((uint)(keyCode & Keys.KeyCode));

        var toolId = settings.FindAnnotationToolId(mod, vk, _visibleTools.Where(t => t.Group == 1).Select(t => t.Id));
        if (toolId is null)
            return false;

        var tool = _visibleTools.FirstOrDefault(t => string.Equals(t.Id, toolId, StringComparison.OrdinalIgnoreCase));
        if (tool?.Mode is not { } mode)
            return false;

        SetMode(mode);
        return true;
    }
}
