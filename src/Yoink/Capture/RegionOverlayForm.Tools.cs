using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Yoink.Models;

namespace Yoink.Capture;

public sealed partial class RegionOverlayForm
{
    private void CompleteFreeform()
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var p in _freeformPoints)
        { minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); }
        var bb = new Rectangle(minX, minY, maxX - minX, maxY - minY);
        if (bb.Width < 3 || bb.Height < 3) return;

        var annotated = RenderAnnotatedBitmap();
        var r = new Bitmap(bb.Width, bb.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(r))
        {
            var pts = _freeformPoints.Select(p => new Point(p.X - minX, p.Y - minY)).ToArray();
            using var cp = new GraphicsPath(); cp.AddPolygon(pts); g.SetClip(cp);
            g.DrawImage(annotated, new Rectangle(0, 0, bb.Width, bb.Height), bb, GraphicsUnit.Pixel);
        }
        annotated.Dispose();
        FreeformSelected?.Invoke(r);
    }

    /// <summary>
    /// Rectangle-mode: lock in the selected rect and enter the annotation phase.
    /// Annotation tools become available via the flyout and selection is no longer editable.
    /// </summary>
    private void CommitSelection(Rectangle rect)
    {
        if (rect.Width < 2 || rect.Height < 2)
            return;

        _selectionRect = rect;
        _hasSelection = true;
        _isSelecting = false;
        _hasDragged = false;
        _selectionCommitted = true;
        _committedIsFreeform = false;
        _autoDetectActive = false;
        _autoDetectRect = Rectangle.Empty;

        // Switch to the annotation Select tool as the default entry point and open the flyout.
        SetMode(CaptureMode.Select);
        SetFlyoutOpen(true);
        EnsureToolbarReady();
        RefreshToolbar();
        Invalidate();
    }

    /// <summary>
    /// Freeform-mode: lock in the polygon selection and enter the annotation phase.
    /// </summary>
    private void CommitFreeformSelection()
    {
        if (_freeformPoints.Count < 3)
            return;

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var p in _freeformPoints)
        { minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); }
        var bb = new Rectangle(minX, minY, maxX - minX, maxY - minY);
        if (bb.Width < 3 || bb.Height < 3)
            return;

        _selectionRect = bb;
        _hasSelection = true;
        _isSelecting = false;
        _hasDragged = false;
        _selectionCommitted = true;
        _committedIsFreeform = true;

        SetMode(CaptureMode.Select);
        SetFlyoutOpen(true);
        EnsureToolbarReady();
        RefreshToolbar();
        Invalidate();
    }

    /// <summary>
    /// Annotation phase: bake annotations onto the screenshot and fire the appropriate
    /// completion event. This is invoked by Enter key or the toolbar Done button.
    /// </summary>
    private void ConfirmCapture()
    {
        if (!_selectionCommitted || !_hasSelection)
            return;

        // Commit any in-progress text before baking annotations.
        if (_isTyping)
            CommitText();

        if (_committedIsFreeform)
        {
            CompleteFreeform();
            return;
        }

        RegionSelected?.Invoke(_selectionRect);
    }

    /// <summary>
    /// Renders the screenshot with all annotations in creation order (Excalidraw style).
    /// </summary>
    public Bitmap RenderAnnotatedBitmap()
    {
        return new Bitmap(GetCommittedAnnotationsBitmap());
    }

    /// <summary>
    /// Shared annotation rendering: iterates the typed undo stack and draws each annotation.
    /// Used by both on-screen paint and final bitmap rendering.
    /// </summary>
    private void RenderAnnotationsTo(Graphics g)
    {
        foreach (var entry in _undoStack)
        {
            switch (entry)
            {
                case EraserFill ef:
                    using (var brush = new SolidBrush(ef.Color))
                        g.FillRectangle(brush, ef.Rect);
                    break;
                case BlurRect br:
                    PaintBlurRect(g, br.Rect);
                    break;
                case DrawStroke ds:
                    SketchRenderer.DrawFreehandStroke(g, ds.Points, ds.Color, 6f, AnnotationStrokeShadow);
                    break;
                case HighlightAnnotation h:
                    SketchRenderer.DrawHighlightRect(g, h.Rect, h.Color);
                    break;
                case RectShapeAnnotation rs:
                    SketchRenderer.DrawRectShape(g, rs.Rect, rs.Color, AnnotationStrokeShadow);
                    break;
                case CircleShapeAnnotation cs:
                    SketchRenderer.DrawCircleShape(g, cs.Rect, cs.Color, AnnotationStrokeShadow);
                    break;
                case LineAnnotation ln:
                    SketchRenderer.DrawLine(g, ln.From, ln.To, ln.Color, ln.From.GetHashCode(), AnnotationStrokeShadow);
                    break;
                case RulerAnnotation ra:
                    PaintRuler(g, ra.From, ra.To);
                    break;
                case ArrowAnnotation a:
                    SketchRenderer.DrawArrow(g, a.From, a.To, a.Color, a.From.GetHashCode(), strokeShadow: AnnotationStrokeShadow);
                    break;
                case CurvedArrowAnnotation ca:
                    SketchRenderer.DrawCurvedArrow(g, ca.Points, ca.Color, ca.Points.Count * 7919, AnnotationStrokeShadow);
                    break;
                case StepNumberAnnotation sn:
                    PaintStepNumber(g, sn.Pos, sn.Number, sn.Color);
                    break;
                case TextAnnotation ta:
                    PaintExcalidrawText(g, ta.Pos, ta.Text, ta.FontSize, ta.Color, ta.Bold, ta.Italic, ta.Stroke, ta.Shadow, ta.FontFamily);
                    break;
                case MagnifierAnnotation ma:
                    PaintPlacedMagnifier(g, ma.Pos, ma.SrcRect);
                    break;
                case EmojiAnnotation ea:
                    PaintEmojiAnnotation(g, ea.Pos, ea.Emoji, ea.Size);
                    break;
            }
        }
    }
}
