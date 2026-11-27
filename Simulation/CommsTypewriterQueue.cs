using SubspaceProtocols.Core;

namespace SubspaceProtocols.Simulation;

public enum CommsSpeakerKind
{
    System = 0,
    Officer = 1,
    Captain = 2
}

public enum CommsTextTone
{
    AmberPrimary = 0,
    AmberDimmed = 1,
    ClearanceTeal = 2,
    AlertCrimson = 3
}

public sealed class CommsTranscriptLine
{
    public string Text { get; init; } = string.Empty;
    public CommsSpeakerKind Speaker { get; init; }
    public CommsTextTone Tone { get; init; }
    public bool IsCaptainStatementEvidence { get; init; }
}

public sealed class CommsQueuedLine
{
    public string Text { get; init; } = string.Empty;
    public CommsSpeakerKind Speaker { get; init; } = CommsSpeakerKind.System;
    public CommsTextTone Tone { get; init; } = CommsTextTone.AmberPrimary;
    public int InstantPrefixLength { get; init; }
    public bool IsInstant { get; init; }
    public float PreDelaySeconds { get; init; }
    public bool ShowReceivingIndicatorDuringPreDelay { get; init; }
    public bool SuppressAutoReceivingLag { get; init; }
    public bool IsTalkingHeadLine { get; init; }
    public bool IsCaptainStatementEvidence { get; init; }
    public Action? OnStart { get; init; }
    public Action? OnComplete { get; init; }
}

/// <summary>
/// Sequential non-blocking CRT Comms Dialogue typewriter queue.
/// Streams spoken Officer/Captain lines at configurable chars/sec (default 65 chars/sec scaled by AnimationSpeedMultiplier),
/// applies punctuation micro-pauses (, +0.06s, .?! +0.15s, ... +0.25s), inserts a 0.55-0.65s [RECEIVING...] subspace lag
/// beat between an Officer line finishing and a Captain reply starting, supports click-to-finish on the active line while
/// preserving subsequent queued lines, freezes cleanly when Update is not called (Pause Menu / Tutorial card), and emits
/// throttled audio stubs (comms_type_officer / comms_type_captain) every 2-3 printed characters.
/// Strictly decoupled from Raylib rendering and input.
/// </summary>
public sealed class CommsTypewriterQueue
{
    private const int MaxCommittedLines = 64;

    private readonly List<CommsTranscriptLine> _committedLines = [];
    private readonly Queue<CommsQueuedLine> _queue = new();

    private CommsQueuedLine? _activeLine;
    private bool _activeLineStarted;
    private float _preDelayRemaining;
    private bool _isShowingReceivingIndicator;
    private float _postLinePauseRemaining;
    private bool _awaitingTalkingHeadClickAdvance;
    private int _visibleChars;
    private float _charCooldown;
    private CommsSpeakerKind _lastSpokenSpeaker = CommsSpeakerKind.System;

    public IReadOnlyList<CommsTranscriptLine> CommittedLines => _committedLines;
    public int QueuedCount => _queue.Count;
    public bool HasPendingOrActive =>
        _activeLine != null ||
        _queue.Count > 0 ||
        _awaitingTalkingHeadClickAdvance ||
        _postLinePauseRemaining > 0f;
    public bool IsWaitingPreDelay => _activeLine != null && _preDelayRemaining > 0f;
    public bool IsWaitingReceiving => _activeLine != null && _preDelayRemaining > 0f && _isShowingReceivingIndicator;
    public float ReceivingSecondsRemaining => IsWaitingReceiving ? _preDelayRemaining : 0f;
    public bool IsWaitingTalkingHeadAutoPause => _activeLine == null && _postLinePauseRemaining > 0f && _queue.Count > 0;
    public float TalkingHeadAutoPauseRemaining => IsWaitingTalkingHeadAutoPause ? _postLinePauseRemaining : 0f;
    public bool IsWaitingTalkingHeadClickAdvance => _awaitingTalkingHeadClickAdvance && _queue.Count > 0;
    public bool IsTypingActiveLine => _activeLine != null && _preDelayRemaining <= 0f && !_activeLine.IsInstant;

    public string ActiveFullText => IsTypingActiveLine ? _activeLine!.Text : string.Empty;
    public int ActiveVisibleCharCount => IsTypingActiveLine ? Math.Clamp(_visibleChars, 0, _activeLine!.Text.Length) : 0;
    public string ActiveVisibleText => IsTypingActiveLine ? _activeLine!.Text[..ActiveVisibleCharCount] : string.Empty;
    public CommsTextTone ActiveTone => _activeLine?.Tone ?? CommsTextTone.AmberPrimary;
    public CommsSpeakerKind ActiveSpeaker => _activeLine?.Speaker ?? CommsSpeakerKind.System;
    public bool ActiveIsCaptainStatementEvidence => _activeLine?.IsCaptainStatementEvidence ?? false;

    public void Clear()
    {
        AudioHooks.OnCommsLineEnd(isCaptain: false);
        AudioHooks.StopActiveCommsVoices();

        _committedLines.Clear();
        _queue.Clear();
        _activeLine = null;
        _activeLineStarted = false;
        _preDelayRemaining = 0f;
        _isShowingReceivingIndicator = false;
        _postLinePauseRemaining = 0f;
        _awaitingTalkingHeadClickAdvance = false;
        _visibleChars = 0;
        _charCooldown = 0f;
        _lastSpokenSpeaker = CommsSpeakerKind.System;
    }

    /// <summary>
    /// Enqueues an instant system/hardware log line.
    /// Commits immediately if the queue is completely idle and preDelaySeconds == 0; otherwise stays strictly ordered.
    /// </summary>
    public void EnqueueInstantLine(
        string text,
        CommsTextTone tone,
        float preDelaySeconds = 0f,
        Action? onStart = null,
        Action? onComplete = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (_activeLine == null && _queue.Count == 0 && preDelaySeconds <= 0f)
        {
            onStart?.Invoke();
            AppendCommitted(text, CommsSpeakerKind.System, tone);
            onComplete?.Invoke();
            return;
        }

        Enqueue(new CommsQueuedLine
        {
            Text = text,
            Speaker = CommsSpeakerKind.System,
            Tone = tone,
            IsInstant = true,
            PreDelaySeconds = Math.Max(0f, preDelaySeconds),
            SuppressAutoReceivingLag = true,
            OnStart = onStart,
            OnComplete = onComplete
        });
    }

    /// <summary>
    /// Enqueues a spoken Officer or Captain line for character-by-character typewriter streaming.
    /// </summary>
    public void EnqueueSpokenLine(
        string text,
        CommsSpeakerKind speaker,
        CommsTextTone tone,
        int instantPrefixLength = 0,
        float preDelaySeconds = 0f,
        bool showReceivingIndicatorDuringPreDelay = false,
        bool suppressAutoReceivingLag = false,
        bool isTalkingHeadLine = false,
        bool isCaptainStatementEvidence = false,
        Action? onStart = null,
        Action? onComplete = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        Enqueue(new CommsQueuedLine
        {
            Text = text,
            Speaker = speaker,
            Tone = tone,
            InstantPrefixLength = Math.Clamp(instantPrefixLength, 0, text.Length),
            IsInstant = false,
            PreDelaySeconds = Math.Max(0f, preDelaySeconds),
            ShowReceivingIndicatorDuringPreDelay = showReceivingIndicatorDuringPreDelay,
            SuppressAutoReceivingLag = suppressAutoReceivingLag,
            IsTalkingHeadLine = isTalkingHeadLine,
            IsCaptainStatementEvidence = isCaptainStatementEvidence,
            OnStart = onStart,
            OnComplete = onComplete
        });
    }

    public void Enqueue(CommsQueuedLine line)
    {
        if (line == null || string.IsNullOrWhiteSpace(line.Text))
            return;

        if (_activeLine == null && _queue.Count == 0 &&
            _postLinePauseRemaining <= 0f && !_awaitingTalkingHeadClickAdvance)
        {
            BeginDequeuedLine(line);
        }
        else
        {
            _queue.Enqueue(line);
        }
    }

    /// <summary>
    /// Advances typewriter streaming, punctuation micro-pauses, [RECEIVING...] lag beats,
    /// and scripted talking-head inter-line pauses/click holds.
    /// Pass scaledDeltaSeconds = deltaTime * AnimationSpeedMultiplier.
    /// </summary>
    public void Update(
        float scaledDeltaSeconds,
        float charsPerSec,
        float talkingHeadCharsPerSec = 32.0f,
        float talkingHeadLinePauseSeconds = 0.95f,
        TalkingHeadAdvanceMode talkingHeadAdvanceMode = TalkingHeadAdvanceMode.AutoAdvance,
        float? rawDeltaSeconds = null)
    {
        if (scaledDeltaSeconds <= 0f)
            return;

        float routineStep = 1.0f / Math.Clamp(charsPerSec, 15.0f, 300.0f);
        float talkingHeadStep = 1.0f / Math.Clamp(talkingHeadCharsPerSec, 10.0f, 150.0f);
        float clampedPause = Math.Clamp(talkingHeadLinePauseSeconds, 0.10f, 5.0f);
        float rawRatio = (scaledDeltaSeconds > 1e-6f && rawDeltaSeconds.HasValue)
            ? Math.Max(0f, rawDeltaSeconds.Value) / scaledDeltaSeconds
            : 1.0f;
        float remainingDt = scaledDeltaSeconds;

        int guard = 0;
        while (guard++ < 1024)
        {
            if (_activeLine == null)
            {
                if (_awaitingTalkingHeadClickAdvance)
                {
                    if (_queue.Count == 0)
                    {
                        _awaitingTalkingHeadClickAdvance = false;
                        break;
                    }

                    if (talkingHeadAdvanceMode == TalkingHeadAdvanceMode.AutoAdvance)
                    {
                        _awaitingTalkingHeadClickAdvance = false;
                        _postLinePauseRemaining = 0f;
                        BeginDequeuedLine(_queue.Dequeue());
                    }
                    else
                    {
                        break;
                    }
                }
                else if (_postLinePauseRemaining > 0f)
                {
                    if (_queue.Count == 0)
                    {
                        _postLinePauseRemaining = 0f;
                        break;
                    }

                    if (talkingHeadAdvanceMode == TalkingHeadAdvanceMode.ClickToAdvance)
                    {
                        _postLinePauseRemaining = 0f;
                        _awaitingTalkingHeadClickAdvance = true;
                        break;
                    }

                    if (remainingDt < _postLinePauseRemaining)
                    {
                        AudioHooks.TickCommsCooldown(remainingDt * rawRatio);
                        _postLinePauseRemaining -= remainingDt;
                        remainingDt = 0f;
                        break;
                    }

                    float consumedPause = _postLinePauseRemaining;
                    AudioHooks.TickCommsCooldown(consumedPause * rawRatio);
                    remainingDt -= consumedPause;
                    _postLinePauseRemaining = 0f;
                    BeginDequeuedLine(_queue.Dequeue());
                }
                else
                {
                    if (_queue.Count == 0)
                        break;
                    BeginDequeuedLine(_queue.Dequeue());
                }
            }

            // 1. Pre-delay (e.g. 0.50s PROTOCOL FAULT beat or 0.55-0.65s [RECEIVING...] subspace lag)
            if (_preDelayRemaining > 0f)
            {
                if (remainingDt < _preDelayRemaining)
                {
                    AudioHooks.TickCommsCooldown(remainingDt * rawRatio);
                    _preDelayRemaining -= remainingDt;
                    remainingDt = 0f;
                    break;
                }

                float consumedPreDelay = _preDelayRemaining;
                AudioHooks.TickCommsCooldown(consumedPreDelay * rawRatio);
                remainingDt -= consumedPreDelay;
                _preDelayRemaining = 0f;
                _isShowingReceivingIndicator = false;
            }

            float activeCharStep = _activeLine!.IsTalkingHeadLine ? talkingHeadStep : routineStep;

            // 2. Initialize active line once pre-delay finishes
            if (!_activeLineStarted)
            {
                _activeLineStarted = true;
                _activeLine.OnStart?.Invoke();

                if (_activeLine.IsInstant || _activeLine.Text.Length == 0)
                {
                    CommitActiveLine();
                    CommitTrailingZeroDelayInstantLines();
                    continue;
                }

                if (_activeLine.Speaker != CommsSpeakerKind.System)
                {
                    AudioHooks.OnCommsLineStart(
                        _activeLine.Speaker == CommsSpeakerKind.Officer,
                        _activeLine.Speaker == CommsSpeakerKind.Captain,
                        _activeLine.Text);
                }

                _visibleChars = Math.Clamp(_activeLine.InstantPrefixLength, 0, _activeLine.Text.Length);
                _charCooldown = activeCharStep;

                if (_visibleChars >= _activeLine.Text.Length)
                {
                    bool wasTalkingHeadInstant = _activeLine.IsTalkingHeadLine;
                    CommitActiveLine();
                    AdvanceQueueAfterLineCommit(wasTalkingHeadInstant, talkingHeadAdvanceMode, clampedPause);
                    continue;
                }
            }

            // 3. Wait per-character / punctuation cooldown
            if (_charCooldown > 0f)
            {
                if (remainingDt < _charCooldown)
                {
                    AudioHooks.TickCommsCooldown(remainingDt * rawRatio);
                    _charCooldown -= remainingDt;
                    remainingDt = 0f;
                    break;
                }

                float consumedCooldown = _charCooldown;
                AudioHooks.TickCommsCooldown(consumedCooldown * rawRatio);
                remainingDt -= consumedCooldown;
                _charCooldown = 0f;
            }

            // 4. Reveal next single character
            int charIdx = _visibleChars;
            char printed = _activeLine.Text[charIdx];
            _visibleChars++;

            if (_activeLine.Speaker != CommsSpeakerKind.System)
            {
                AudioHooks.OnCommsCharRevealed(
                    printed,
                    _activeLine.Speaker == CommsSpeakerKind.Officer,
                    _activeLine.Speaker == CommsSpeakerKind.Captain);
            }

            if (_visibleChars >= _activeLine.Text.Length)
            {
                bool wasTalkingHead = _activeLine.IsTalkingHeadLine;
                CommitActiveLine();
                AdvanceQueueAfterLineCommit(wasTalkingHead, talkingHeadAdvanceMode, clampedPause);
                if (remainingDt <= 0f || _awaitingTalkingHeadClickAdvance)
                    break;
                continue;
            }

            _charCooldown = activeCharStep + ComputePunctuationPause(_activeLine.Text, charIdx);

            if (remainingDt <= 0f)
                break;
        }
    }

    /// <summary>
    /// Click-to-finish: snaps ONLY the currently typing line to 100% immediately while keeping
    /// all queued subsequent lines (and any [RECEIVING...] lag or talking-head pause/click hold) intact.
    /// </summary>
    public bool FinishActiveLineImmediately(
        TalkingHeadAdvanceMode talkingHeadAdvanceMode = TalkingHeadAdvanceMode.AutoAdvance,
        float talkingHeadLinePauseSeconds = 0.95f)
    {
        if (!IsTypingActiveLine || _activeLine == null)
            return false;

        if (!_activeLineStarted)
        {
            _activeLineStarted = true;
            _activeLine.OnStart?.Invoke();
        }

        bool wasTalkingHead = _activeLine.IsTalkingHeadLine;
        _visibleChars = _activeLine.Text.Length;
        CommitActiveLine();
        AdvanceQueueAfterLineCommit(
            wasTalkingHead,
            talkingHeadAdvanceMode,
            Math.Clamp(talkingHeadLinePauseSeconds, 0.10f, 5.0f));
        return true;
    }

    public bool AdvanceTalkingHeadAfterClick()
    {
        if (!_awaitingTalkingHeadClickAdvance)
            return false;

        _awaitingTalkingHeadClickAdvance = false;
        _postLinePauseRemaining = 0f;
        CommitTrailingZeroDelayInstantLines();
        if (_activeLine == null && _queue.Count > 0)
        {
            BeginDequeuedLine(_queue.Dequeue());
        }
        return true;
    }

    public bool SkipTalkingHeadPauseImmediately()
    {
        if (_postLinePauseRemaining <= 0f)
            return false;

        _postLinePauseRemaining = 0f;
        CommitTrailingZeroDelayInstantLines();
        if (_activeLine == null && _queue.Count > 0)
        {
            BeginDequeuedLine(_queue.Dequeue());
        }
        return true;
    }

    public bool SkipPreDelayImmediately()
    {
        if (_activeLine == null || _preDelayRemaining <= 0f)
            return false;

        _preDelayRemaining = 0f;
        _isShowingReceivingIndicator = false;
        return true;
    }

    /// <summary>
    /// Immediately commits the active line and all queued lines in chronological order.
    /// Used for scripted skip buttons (Pre-Shift Live Hail SKIP), pre-fault routine flush, and smoke staging.
    /// </summary>
    public void FlushAllImmediately(bool invokeCallbacks = true)
    {
        _postLinePauseRemaining = 0f;
        _awaitingTalkingHeadClickAdvance = false;
        int guard = 0;
        while ((_activeLine != null || _queue.Count > 0) && guard++ < 256)
        {
            if (_activeLine == null)
            {
                BeginDequeuedLine(_queue.Dequeue());
            }

            CommsQueuedLine current = _activeLine!;
            bool wasStarted = _activeLineStarted;
            _preDelayRemaining = 0f;
            _isShowingReceivingIndicator = false;
            if (!_activeLineStarted)
            {
                _activeLineStarted = true;
                if (invokeCallbacks)
                    current.OnStart?.Invoke();
            }

            _visibleChars = current.Text.Length;
            _activeLine = null;
            _activeLineStarted = false;
            _charCooldown = 0f;

            AppendCommitted(current.Text, current.Speaker, current.Tone, current.IsCaptainStatementEvidence);
            if (current.Speaker != CommsSpeakerKind.System)
            {
                _lastSpokenSpeaker = current.Speaker;
            }
            if (wasStarted && !current.IsInstant && current.Speaker != CommsSpeakerKind.System)
            {
                AudioHooks.OnCommsLineEnd(current.Speaker == CommsSpeakerKind.Captain);
            }
            if (invokeCallbacks)
            {
                current.OnComplete?.Invoke();
            }
        }
    }

    /// <summary>
    /// Commits only the currently typing spoken line (if any) and drops any unstarted queued items
    /// (e.g. when pulling the Detain lever mid-confrontation so stale future replies do not fire post-clamp).
    /// </summary>
    public void SnapActiveAndDropPendingQueue()
    {
        bool resolvedLineEnd = false;
        if (IsTypingActiveLine && _activeLine != null)
        {
            CommsQueuedLine finished = _activeLine;
            bool wasStarted = _activeLineStarted;
            AppendCommitted(finished.Text, finished.Speaker, finished.Tone, finished.IsCaptainStatementEvidence);
            if (finished.Speaker != CommsSpeakerKind.System)
            {
                _lastSpokenSpeaker = finished.Speaker;
            }
            if (wasStarted && !finished.IsInstant && finished.Speaker != CommsSpeakerKind.System)
            {
                AudioHooks.OnCommsLineEnd(finished.Speaker == CommsSpeakerKind.Captain);
                resolvedLineEnd = true;
            }
        }

        if (!resolvedLineEnd)
        {
            AudioHooks.OnCommsLineEnd(isCaptain: false);
        }

        _activeLine = null;
        _activeLineStarted = false;
        _preDelayRemaining = 0f;
        _isShowingReceivingIndicator = false;
        _postLinePauseRemaining = 0f;
        _awaitingTalkingHeadClickAdvance = false;
        _visibleChars = 0;
        _charCooldown = 0f;
        _queue.Clear();
    }

    /// <summary>
    /// Returns all committed plus active/queued text strings (for inspection/tests verifying no leaked text).
    /// </summary>
    public IEnumerable<string> EnumerateAllTextIncludingPending()
    {
        foreach (CommsTranscriptLine committed in _committedLines)
            yield return committed.Text;
        if (_activeLine != null)
            yield return _activeLine.Text;
        foreach (CommsQueuedLine pending in _queue)
            yield return pending.Text;
    }

    public static float ComputePunctuationPause(string text, int charIndex)
    {
        if (string.IsNullOrEmpty(text) || charIndex < 0 || charIndex >= text.Length)
            return 0f;

        char ch = text[charIndex];
        if (ch == ',')
            return ShiftController.TypewriterCommaPauseSeconds;

        if (ch == '\u2026')
            return ShiftController.TypewriterEllipsisPauseSeconds;

        if (ch == '?' || ch == '!')
            return ShiftController.TypewriterSentencePauseSeconds;

        if (ch == '.')
        {
            // Ignore decimal/cycle dots such as "2409.04" or "1.80"
            if (charIndex + 1 < text.Length && char.IsDigit(text[charIndex + 1]))
                return 0f;

            // Inside an ellipsis run ("...") -> wait until the final dot of the ellipsis
            if (charIndex + 1 < text.Length && text[charIndex + 1] == '.')
                return 0f;

            if (charIndex >= 2 && text[charIndex - 1] == '.' && text[charIndex - 2] == '.')
                return ShiftController.TypewriterEllipsisPauseSeconds;

            return ShiftController.TypewriterSentencePauseSeconds;
        }

        return 0f;
    }

    public static int DetectSpeakerPrefixLength(string line)
    {
        if (string.IsNullOrEmpty(line))
            return 0;

        if (line.StartsWith("> BOOTH 04: ", StringComparison.Ordinal))
            return "> BOOTH 04: ".Length;
        if (line.StartsWith("CAPT: ", StringComparison.Ordinal))
            return "CAPT: ".Length;
        if (line.StartsWith("CMDR: ", StringComparison.Ordinal))
            return "CMDR: ".Length;
        if (line.StartsWith("Vorst: ", StringComparison.Ordinal))
            return "Vorst: ".Length;
        if (line.StartsWith("HAIL [", StringComparison.Ordinal))
        {
            int idx = line.IndexOf("]: ", StringComparison.Ordinal);
            if (idx >= 0)
                return idx + 3;
        }
        return 0;
    }

    private void BeginDequeuedLine(CommsQueuedLine item)
    {
        _activeLine = item;
        _activeLineStarted = false;
        _visibleChars = 0;
        _charCooldown = 0f;

        bool autoReceivingLag =
            !item.IsInstant &&
            !item.SuppressAutoReceivingLag &&
            item.Speaker == CommsSpeakerKind.Captain &&
            _lastSpokenSpeaker == CommsSpeakerKind.Officer;

        if (item.PreDelaySeconds > 0f)
        {
            _preDelayRemaining = item.PreDelaySeconds;
            _isShowingReceivingIndicator = item.ShowReceivingIndicatorDuringPreDelay || autoReceivingLag;
        }
        else if (autoReceivingLag)
        {
            _preDelayRemaining = ShiftController.SubspaceReceivingLagSeconds;
            _isShowingReceivingIndicator = true;
        }
        else
        {
            _preDelayRemaining = 0f;
            _isShowingReceivingIndicator = false;
            if (!item.IsInstant)
            {
                _visibleChars = Math.Clamp(item.InstantPrefixLength, 0, item.Text.Length);
            }
        }
    }

    private void AdvanceQueueAfterLineCommit(
        bool wasTalkingHead,
        TalkingHeadAdvanceMode talkingHeadAdvanceMode,
        float clampedPause)
    {
        CommitTrailingZeroDelayInstantLines();
        if (_activeLine != null || _queue.Count == 0)
            return;

        if (wasTalkingHead)
        {
            if (talkingHeadAdvanceMode == TalkingHeadAdvanceMode.ClickToAdvance)
            {
                _awaitingTalkingHeadClickAdvance = true;
            }
            else
            {
                _postLinePauseRemaining = clampedPause;
            }
        }
        else
        {
            BeginDequeuedLine(_queue.Dequeue());
        }
    }

    private void CommitTrailingZeroDelayInstantLines()
    {
        while (_activeLine == null && _queue.Count > 0 &&
               _queue.Peek().IsInstant && _queue.Peek().PreDelaySeconds <= 0f)
        {
            CommsQueuedLine instant = _queue.Dequeue();
            instant.OnStart?.Invoke();
            AppendCommitted(instant.Text, instant.Speaker, instant.Tone, instant.IsCaptainStatementEvidence);
            instant.OnComplete?.Invoke();
        }
    }

    private void CommitActiveLine()
    {
        if (_activeLine == null)
            return;

        CommsQueuedLine finished = _activeLine;
        bool wasStarted = _activeLineStarted;
        _activeLine = null;
        _activeLineStarted = false;
        _preDelayRemaining = 0f;
        _isShowingReceivingIndicator = false;
        _visibleChars = 0;
        _charCooldown = 0f;

        AppendCommitted(finished.Text, finished.Speaker, finished.Tone, finished.IsCaptainStatementEvidence);
        if (finished.Speaker != CommsSpeakerKind.System)
        {
            _lastSpokenSpeaker = finished.Speaker;
        }

        if (wasStarted && !finished.IsInstant && finished.Speaker != CommsSpeakerKind.System)
        {
            AudioHooks.OnCommsLineEnd(finished.Speaker == CommsSpeakerKind.Captain);
        }

        finished.OnComplete?.Invoke();
    }

    private void AppendCommitted(string text, CommsSpeakerKind speaker, CommsTextTone tone, bool isStatementEvidence = false)
    {
        _committedLines.Add(new CommsTranscriptLine
        {
            Text = text,
            Speaker = speaker,
            Tone = tone,
            IsCaptainStatementEvidence = isStatementEvidence
        });

        while (_committedLines.Count > MaxCommittedLines)
        {
            _committedLines.RemoveAt(0);
        }
    }
}
