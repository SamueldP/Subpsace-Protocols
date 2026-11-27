using System.Text.Json;
using System.Text.Json.Serialization;
using Raylib_cs;

namespace SubspaceProtocols.Core;

public sealed class OfficerVoiceSettings
{
    [JsonPropertyName("officerAnchorMatch")]
    public string OfficerAnchorMatch { get; set; } = "Send subspace protocols";

    [JsonPropertyName("officerConsoleCooldownMs")]
    public int OfficerConsoleCooldownMs { get; set; } = 62;

    [JsonPropertyName("officerConsolePitch")]
    public float OfficerConsolePitch { get; set; } = 1.0f;

    [JsonPropertyName("officerConsoleJitter")]
    public float OfficerConsoleJitter { get; set; } = 0.025f;

    [JsonPropertyName("officerConsoleVolume")]
    public float OfficerConsoleVolume { get; set; } = 0.75f;
}

public sealed class SpeciesVoiceProfile
{
    [JsonPropertyName("bank")]
    public string Bank { get; set; } = "Human_GU";

    [JsonPropertyName("cooldownMs")]
    public int CooldownMs { get; set; } = 68;

    [JsonPropertyName("pitchMin")]
    public float PitchMin { get; set; } = 0.94f;

    [JsonPropertyName("pitchMax")]
    public float PitchMax { get; set; } = 1.06f;

    [JsonPropertyName("jitter")]
    public float Jitter { get; set; } = 0.04f;

    [JsonPropertyName("volume")]
    public float Volume { get; set; } = 0.85f;
}

/// <summary>
/// Audio event logger, comms voice bank runtime, and Raylib audio device manager.
/// Gracefully runs in headless mode and with missing .wav files without throwing.
/// </summary>
public static class AudioHooks
{
    private const int MaxLoggedEvents = 128;
    private const int AliasesPerClip = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private sealed class VoiceProfilesDocument
    {
        [JsonPropertyName("officerSettings")]
        public OfficerVoiceSettings? OfficerSettings { get; set; }

        [JsonPropertyName("speciesProfiles")]
        public Dictionary<string, SpeciesVoiceProfile>? SpeciesProfiles { get; set; }
    }

    private sealed class LoadedClipSlot
    {
        public Sound BaseSound;
        public Sound[] Aliases = [];
        public int NextAliasIndex;
    }

    private static readonly List<string> RecentEvents = [];
    private static readonly List<string> CommsEventLog = [];
    private static readonly Dictionary<string, SpeciesVoiceProfile> SpeciesProfiles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, LoadedClipSlot> SystemClips = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<LoadedClipSlot>> VoiceBankClips = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<Sound> LoadedBaseSounds = [];
    private static readonly List<Sound> LoadedAliases = [];
    private static readonly Random Rng = new();

    private static OfficerVoiceSettings _officerSettings = new();
    private static bool _profilesLoaded;
    private static float _blipCooldownSeconds;
    private static bool _captainLineOpen;
    private static int _lastClipIndex = -1;

    // Preserved legacy public surface
    public static IReadOnlyList<string> LoggedEvents => RecentEvents;
    public static string LastPlayedSoundId { get; private set; } = "NONE";
    public static float MasterVolume { get; set; } = 0.80f;
    public static bool IsMuted { get; set; } = false;

    // Extended comms audio & voice profile surface
    public static bool IsAudioDeviceInitialized { get; private set; }
    public static int ActiveSoundCount { get; private set; }
    public static int ActiveSoundAliasCount { get; private set; }
    public static int LoadedSpeciesProfileCount => SpeciesProfiles.Count;
    public static string ActivePresentedSpecies { get; private set; } = "Human";
    public static string ActiveVoiceBank { get; private set; } = "Human_GU";
    public static float ActiveEncounterBasePitch { get; private set; } = 1.0f;
    public static bool ActiveUseRadioSquelch { get; private set; } = true;
    public static bool IsAnchorBlipSuppressed { get; private set; }
    public static float CurrentBlipCooldownSeconds => _blipCooldownSeconds;
    public static IReadOnlyList<string> CommsEvents => CommsEventLog;

    public static void Play(string soundId)
    {
        if (string.IsNullOrWhiteSpace(soundId))
            return;

        LastPlayedSoundId = IsMuted ? $"{soundId} [MUTED]" : soundId;
        AppendBounded(RecentEvents, soundId);
    }

    public static void Initialize(
        bool headlessMode = false,
        string? voiceProfilesPathOverride = null,
        string? commsAudioDirOverride = null)
    {
        // Cleanly release any prior audio resources before re-initializing.
        if (IsAudioDeviceInitialized || LoadedBaseSounds.Count > 0 || LoadedAliases.Count > 0)
        {
            Shutdown();
        }

        LoadVoiceProfiles(voiceProfilesPathOverride);

        if (headlessMode)
            return;

        try
        {
            if (!Raylib.IsAudioDeviceReady())
            {
                Raylib.InitAudioDevice();
            }

            if (!Raylib.IsAudioDeviceReady())
                return;

            IsAudioDeviceInitialized = true;
            string commsDir = string.IsNullOrWhiteSpace(commsAudioDirOverride)
                ? Path.Combine(AppContext.BaseDirectory, "assets", "audio", "comms")
                : commsAudioDirOverride;

            ScanAndLoadCommsAudio(commsDir);
        }
        catch
        {
            // Audio hardware/driver unavailable; continue silently.
            IsAudioDeviceInitialized = RaylibIsDeviceReadySafe();
        }
    }

    public static void Shutdown()
    {
        StopActiveCommsVoices();

        if (IsAudioDeviceInitialized)
        {
            for (int i = 0; i < LoadedAliases.Count; i++)
            {
                try
                {
                    Raylib.UnloadSoundAlias(LoadedAliases[i]);
                }
                catch
                {
                }
            }

            for (int i = 0; i < LoadedBaseSounds.Count; i++)
            {
                try
                {
                    Raylib.UnloadSound(LoadedBaseSounds[i]);
                }
                catch
                {
                }
            }
        }

        LoadedAliases.Clear();
        LoadedBaseSounds.Clear();
        SystemClips.Clear();
        VoiceBankClips.Clear();
        ActiveSoundCount = 0;
        ActiveSoundAliasCount = 0;

        if (IsAudioDeviceInitialized)
        {
            try
            {
                if (Raylib.IsAudioDeviceReady())
                {
                    Raylib.CloseAudioDevice();
                }
            }
            catch
            {
            }

            IsAudioDeviceInitialized = false;
        }
    }

    public static string NormalizeSpeciesKey(string? presentedSpecies)
    {
        if (string.IsNullOrWhiteSpace(presentedSpecies))
            return "Human";

        string trimmed = presentedSpecies.Trim();
        return trimmed.ToLowerInvariant() switch
        {
            "human" => "Human",
            "helani" => "Helani",
            "klaasari" => "Klaasari",
            "thane" => "Thane",
            "veldor" => "Veldor",
            "kordani" => "Kordani",
            "brengop" => "Brengop",
            "krushvak" => "Krushvak",
            "hunborian" or "hun-borian" or "hun borian" => "HunBorian",
            "oruun" or "orun" => "Oruun",
            "prunjun" => "Prunjun",
            "tolvak" => "Tolvak",
            "dopple" => "Human", // Zero-Tell Dopple Rule: never leak internal impostor species
            _ => "Human"
        };
    }

    public static SpeciesVoiceProfile GetSpeciesProfile(string? presentedSpecies)
    {
        EnsureProfilesLoaded();
        string key = NormalizeSpeciesKey(presentedSpecies);
        if (SpeciesProfiles.TryGetValue(key, out SpeciesVoiceProfile? profile))
            return profile;
        if (SpeciesProfiles.TryGetValue("Human", out SpeciesVoiceProfile? fallback))
            return fallback;
        return new SpeciesVoiceProfile();
    }

    public static OfficerVoiceSettings GetOfficerSettings()
    {
        EnsureProfilesLoaded();
        return _officerSettings;
    }

    public static void BeginEncounterVoice(string presentedSpecies, int seed, bool useRadioSquelch = true)
    {
        EnsureProfilesLoaded();

        string canonical = NormalizeSpeciesKey(presentedSpecies);
        SpeciesVoiceProfile profile = GetSpeciesProfile(canonical);

        ActivePresentedSpecies = canonical;
        ActiveVoiceBank = string.IsNullOrWhiteSpace(profile.Bank) ? "Human_GU" : profile.Bank;
        ActiveUseRadioSquelch = useRadioSquelch;

        float minPitch = Math.Min(profile.PitchMin, profile.PitchMax);
        float maxPitch = Math.Max(profile.PitchMin, profile.PitchMax);
        float t = ComputeDeterministicUnit(canonical, seed);
        ActiveEncounterBasePitch = Math.Clamp(minPitch + (maxPitch - minPitch) * t, 0.5f, 2.0f);

        _blipCooldownSeconds = 0f;
        IsAnchorBlipSuppressed = false;
        _captainLineOpen = false;
        _lastClipIndex = -1;
    }

    public static void TickCommsCooldown(float rawDeltaSeconds)
    {
        if (rawDeltaSeconds <= 0f || _blipCooldownSeconds <= 0f)
            return;

        _blipCooldownSeconds = Math.Max(0f, _blipCooldownSeconds - rawDeltaSeconds);
    }

    public static void OnCommsLineStart(bool isOfficer, bool isCaptain, string lineText)
    {
        EnsureProfilesLoaded();

        _blipCooldownSeconds = 0f;
        string text = lineText ?? string.Empty;
        string anchor = _officerSettings.OfficerAnchorMatch;

        if (isOfficer &&
            !string.IsNullOrWhiteSpace(anchor) &&
            text.Contains(anchor, StringComparison.OrdinalIgnoreCase))
        {
            PlaySystemClip("officer_protocols", 1.0f, _officerSettings.OfficerConsoleVolume);
            RecordCommsEvent("officer_protocols");
            IsAnchorBlipSuppressed = true;
            _captainLineOpen = false;
            return;
        }

        IsAnchorBlipSuppressed = false;

        if (isCaptain)
        {
            _captainLineOpen = true;
            if (ActiveUseRadioSquelch)
            {
                PlaySystemClip("squelch_open", 1.0f, GetSpeciesProfile(ActivePresentedSpecies).Volume);
                RecordCommsEvent("squelch_open");
            }
        }
        else
        {
            _captainLineOpen = false;
        }
    }

    public static bool OnCommsCharRevealed(char revealedChar, bool isOfficer, bool isCaptain)
    {
        if (!char.IsLetterOrDigit(revealedChar))
            return false;

        if (IsAnchorBlipSuppressed || _blipCooldownSeconds > 0f)
            return false;

        EnsureProfilesLoaded();

        if (isOfficer)
        {
            float jitter = Math.Max(0f, _officerSettings.OfficerConsoleJitter);
            float pitch = Math.Clamp(
                _officerSettings.OfficerConsolePitch * (1.0f + NextSignedUnit() * jitter),
                0.5f,
                2.0f);

            PlaySystemClip("officer_console_blip", pitch, _officerSettings.OfficerConsoleVolume);
            _blipCooldownSeconds = Math.Max(0f, _officerSettings.OfficerConsoleCooldownMs / 1000.0f);
            RecordCommsEvent("officer_console_blip");
            Play("comms_type_officer");
            return true;
        }

        if (isCaptain)
        {
            SpeciesVoiceProfile profile = GetSpeciesProfile(ActivePresentedSpecies);
            string bank = string.IsNullOrWhiteSpace(ActiveVoiceBank) ? profile.Bank : ActiveVoiceBank;
            float jitter = Math.Max(0f, profile.Jitter);
            float pitch = Math.Clamp(
                ActiveEncounterBasePitch * (1.0f + NextSignedUnit() * jitter),
                0.5f,
                2.0f);

            if (VoiceBankClips.TryGetValue(bank, out List<LoadedClipSlot>? clips) && clips.Count > 0)
            {
                int clipIdx = PickNonRepeatingClipIndex(clips.Count, _lastClipIndex, Rng);
                if (clipIdx >= 0 && clipIdx < clips.Count)
                {
                    _lastClipIndex = clipIdx;
                    PlayClipSlot(clips[clipIdx], pitch, profile.Volume);
                }
            }

            _blipCooldownSeconds = Math.Max(0f, profile.CooldownMs / 1000.0f);
            RecordCommsEvent($"voice_blip:{bank}");
            Play("comms_type_captain");
            return true;
        }

        return false;
    }

    public static void OnCommsLineEnd(bool isCaptain)
    {
        bool wasAnchorSuppressed = IsAnchorBlipSuppressed;
        IsAnchorBlipSuppressed = false;

        if (wasAnchorSuppressed && !isCaptain)
        {
            StopSystemClip("officer_protocols");
        }

        if (isCaptain && _captainLineOpen)
        {
            _captainLineOpen = false;
            if (ActiveUseRadioSquelch)
            {
                PlaySystemClip("squelch_close", 1.0f, GetSpeciesProfile(ActivePresentedSpecies).Volume);
                RecordCommsEvent("squelch_close");
            }
        }
    }

    public static void StopActiveCommsVoices()
    {
        if (IsAudioDeviceInitialized)
        {
            for (int i = 0; i < LoadedAliases.Count; i++)
            {
                try
                {
                    Raylib.StopSound(LoadedAliases[i]);
                }
                catch
                {
                }
            }

            for (int i = 0; i < LoadedBaseSounds.Count; i++)
            {
                try
                {
                    Raylib.StopSound(LoadedBaseSounds[i]);
                }
                catch
                {
                }
            }
        }

        _blipCooldownSeconds = 0f;
        IsAnchorBlipSuppressed = false;
        _captainLineOpen = false;
    }

    private static void StopSystemClip(string key)
    {
        if (!IsAudioDeviceInitialized || string.IsNullOrWhiteSpace(key))
            return;

        if (!SystemClips.TryGetValue(key, out LoadedClipSlot? slot))
            return;

        try
        {
            Raylib.StopSound(slot.BaseSound);
        }
        catch
        {
        }

        for (int i = 0; i < slot.Aliases.Length; i++)
        {
            try
            {
                Raylib.StopSound(slot.Aliases[i]);
            }
            catch
            {
            }
        }
    }

    public static int PickNonRepeatingClipIndex(int clipCount, int lastIndex, Random? rng = null)
    {
        if (clipCount <= 0)
            return -1;
        if (clipCount == 1)
            return 0;

        Random random = rng ?? Rng;
        if (lastIndex < 0 || lastIndex >= clipCount)
            return random.Next(clipCount);

        int pick = random.Next(clipCount - 1);
        return pick >= lastIndex ? pick + 1 : pick;
    }

    public static void ClearEventHistory()
    {
        RecentEvents.Clear();
        CommsEventLog.Clear();
        LastPlayedSoundId = "NONE";
    }

    private static void EnsureProfilesLoaded()
    {
        if (!_profilesLoaded)
        {
            LoadVoiceProfiles(null);
        }
    }

    private static void LoadVoiceProfiles(string? voiceProfilesPathOverride)
    {
        SpeciesProfiles.Clear();
        _officerSettings = new OfficerVoiceSettings();

        string path = string.IsNullOrWhiteSpace(voiceProfilesPathOverride)
            ? Path.Combine(AppContext.BaseDirectory, "data", "voice_profiles.json")
            : voiceProfilesPathOverride;

        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                VoiceProfilesDocument? doc = JsonSerializer.Deserialize<VoiceProfilesDocument>(json, JsonOptions);
                if (doc?.OfficerSettings != null)
                {
                    _officerSettings = doc.OfficerSettings;
                }
                if (doc?.SpeciesProfiles != null)
                {
                    foreach (KeyValuePair<string, SpeciesVoiceProfile> kvp in doc.SpeciesProfiles)
                    {
                        if (!string.IsNullOrWhiteSpace(kvp.Key) && kvp.Value != null)
                        {
                            SpeciesProfiles[NormalizeSpeciesKey(kvp.Key)] = kvp.Value;
                        }
                    }
                }
            }
        }
        catch
        {
            // Fall back cleanly to built-in defaults below.
        }

        SeedMissingDefaultProfiles();
        _profilesLoaded = true;
    }

    private static void SeedMissingDefaultProfiles()
    {
        void Ensure(string species, string bank, int cooldownMs, float pitchMin, float pitchMax, float jitter, float volume)
        {
            if (!SpeciesProfiles.ContainsKey(species))
            {
                SpeciesProfiles[species] = new SpeciesVoiceProfile
                {
                    Bank = bank,
                    CooldownMs = cooldownMs,
                    PitchMin = pitchMin,
                    PitchMax = pitchMax,
                    Jitter = jitter,
                    Volume = volume
                };
            }
        }

        Ensure("Human", "Human_GU", 68, 0.94f, 1.06f, 0.04f, 0.85f);
        Ensure("Helani", "Human_GU", 120, 0.96f, 1.04f, 0.015f, 0.82f);
        Ensure("Klaasari", "GU_Alien", 65, 1.12f, 1.24f, 0.045f, 0.85f);
        Ensure("Thane", "GU_Alien", 74, 0.76f, 0.88f, 0.035f, 0.88f);
        Ensure("Veldor", "GU_Alien", 70, 0.92f, 1.04f, 0.05f, 0.84f);
        Ensure("Kordani", "Kordani", 66, 0.86f, 0.98f, 0.035f, 0.88f);
        Ensure("Brengop", "Brengop", 64, 1.04f, 1.18f, 0.055f, 0.86f);
        Ensure("Krushvak", "Krushvak", 72, 0.74f, 0.86f, 0.045f, 0.92f);
        Ensure("HunBorian", "HunBorian", 75, 0.84f, 0.94f, 0.03f, 0.84f);
        Ensure("Oruun", "Oruun", 78, 0.78f, 0.88f, 0.02f, 0.86f);
        Ensure("Prunjun", "Prunjun", 58, 1.18f, 1.34f, 0.065f, 0.85f);
        Ensure("Tolvak", "Tolvak", 64, 0.96f, 1.10f, 0.05f, 0.85f);
    }

    private static void ScanAndLoadCommsAudio(string commsDir)
    {
        if (string.IsNullOrWhiteSpace(commsDir) || !Directory.Exists(commsDir))
            return;

        string systemDir = Path.Combine(commsDir, "system");
        if (Directory.Exists(systemDir))
        {
            string[] wavFiles = Directory.GetFiles(systemDir, "*.wav", SearchOption.TopDirectoryOnly);
            Array.Sort(wavFiles, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < wavFiles.Length; i++)
            {
                LoadedClipSlot? slot = TryLoadClipSlot(wavFiles[i]);
                if (slot != null)
                {
                    string key = Path.GetFileNameWithoutExtension(wavFiles[i]);
                    SystemClips[key] = slot;
                }
            }
        }

        string voicesDir = Path.Combine(commsDir, "voices");
        if (Directory.Exists(voicesDir))
        {
            string[] bankDirs = Directory.GetDirectories(voicesDir);
            Array.Sort(bankDirs, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < bankDirs.Length; i++)
            {
                string bankName = Path.GetFileName(bankDirs[i]);
                if (string.IsNullOrWhiteSpace(bankName))
                    continue;

                string[] wavFiles = Directory.GetFiles(bankDirs[i], "*.wav", SearchOption.TopDirectoryOnly);
                Array.Sort(wavFiles, StringComparer.OrdinalIgnoreCase);
                List<LoadedClipSlot> bankList = [];
                for (int j = 0; j < wavFiles.Length; j++)
                {
                    LoadedClipSlot? slot = TryLoadClipSlot(wavFiles[j]);
                    if (slot != null)
                    {
                        bankList.Add(slot);
                    }
                }

                if (bankList.Count > 0)
                {
                    VoiceBankClips[bankName] = bankList;
                }
            }
        }

        ActiveSoundCount = LoadedBaseSounds.Count;
        ActiveSoundAliasCount = LoadedAliases.Count;
    }

    private static LoadedClipSlot? TryLoadClipSlot(string wavPath)
    {
        try
        {
            Sound baseSound = Raylib.LoadSound(wavPath);
            if (!Raylib.IsSoundReady(baseSound))
                return null;

            LoadedBaseSounds.Add(baseSound);
            List<Sound> aliasList = new(AliasesPerClip);
            for (int i = 0; i < AliasesPerClip; i++)
            {
                Sound alias = Raylib.LoadSoundAlias(baseSound);
                if (Raylib.IsSoundReady(alias))
                {
                    aliasList.Add(alias);
                    LoadedAliases.Add(alias);
                }
            }

            return new LoadedClipSlot
            {
                BaseSound = baseSound,
                Aliases = aliasList.ToArray(),
                NextAliasIndex = 0
            };
        }
        catch
        {
            return null;
        }
    }

    private static void PlaySystemClip(string clipKey, float pitch, float volume)
    {
        if (!IsAudioDeviceInitialized)
            return;

        if (SystemClips.TryGetValue(clipKey, out LoadedClipSlot? slot))
        {
            PlayClipSlot(slot, pitch, volume);
        }
    }

    private static void PlayClipSlot(LoadedClipSlot slot, float pitch, float clipVolume)
    {
        if (!IsAudioDeviceInitialized || IsMuted || MasterVolume <= 0.0001f)
            return;

        try
        {
            Sound soundToPlay;
            if (slot.Aliases.Length > 0)
            {
                soundToPlay = slot.Aliases[slot.NextAliasIndex % slot.Aliases.Length];
                slot.NextAliasIndex = (slot.NextAliasIndex + 1) % slot.Aliases.Length;
            }
            else
            {
                soundToPlay = slot.BaseSound;
            }

            float clampedPitch = Math.Clamp(pitch, 0.5f, 2.0f);
            float effectiveVolume = Math.Clamp(clipVolume * MasterVolume, 0f, 1f);
            Raylib.SetSoundPitch(soundToPlay, clampedPitch);
            Raylib.SetSoundVolume(soundToPlay, effectiveVolume);
            Raylib.PlaySound(soundToPlay);
        }
        catch
        {
        }
    }

    private static void RecordCommsEvent(string eventId)
    {
        AppendBounded(CommsEventLog, eventId);
    }

    private static void AppendBounded(List<string> list, string value)
    {
        if (list.Count >= MaxLoggedEvents)
        {
            list.RemoveAt(0);
        }
        list.Add(value);
    }

    private static float NextSignedUnit() => (float)(Rng.NextDouble() * 2.0 - 1.0);

    private static float ComputeDeterministicUnit(string canonicalSpecies, int seed)
    {
        unchecked
        {
            uint hash = 2166136261u;
            for (int i = 0; i < canonicalSpecies.Length; i++)
            {
                hash ^= canonicalSpecies[i];
                hash *= 16777619u;
            }
            hash ^= (uint)seed;
            hash *= 16777619u;
            hash ^= hash >> 16;
            hash *= 0x85ebca6bu;
            hash ^= hash >> 13;
            hash *= 0xc2b2ae35u;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) / (float)0x00FFFFFFu;
        }
    }

    private static bool RaylibIsDeviceReadySafe()
    {
        try
        {
            return Raylib.IsAudioDeviceReady();
        }
        catch
        {
            return false;
        }
    }
}
