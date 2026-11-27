SUBSPACE PROTOCOLS - COMMS AUDIO ASSET DROP-IN GUIDE
===================================================

Drop uncompressed .wav files into the folders below. Missing files fail
silently at runtime so the game remains fully playable before audio assets
are recorded.

REQUIRED AUDIO FORMAT
---------------------
- Container:   WAV (.wav)
- Encoding:    16-bit PCM (uncompressed)
- Channels:    Mono (1 channel)
- Sample Rate: 44100 Hz

SYSTEM AUDIO FILES (assets/audio/comms/system/)
-----------------------------------------------
Place these exact filenames in `assets/audio/comms/system/`:
- `squelch_open.wav`         Played once when a Captain radio line begins (if radio squelch enabled)
- `squelch_close.wav`        Played once when a Captain radio line ends (if radio squelch enabled)
- `officer_protocols.wav`    Played once on the Officer anchor line ("Send subspace protocols")
- `officer_console_blip.wav` Cooldown-gated typewriter blip for Officer console lines

SPECIES VOICE BANKS (assets/audio/comms/voices/<BankName>/*.wav)
----------------------------------------------------------------
Drop any number of short voice blip `.wav` clips (e.g., `blip_01.wav`,
`blip_02.wav`, ...) into each bank folder. Clips are scanned once at startup
and selected non-repeatingly with per-encounter base pitch and per-blip jitter
configured in `data/voice_profiles.json`:

- `assets/audio/comms/voices/Human_GU/`   -> Human, Helani (and fallback/Dopple presentation)
- `assets/audio/comms/voices/GU_Alien/`   -> Klaasari, Thane, Veldor
- `assets/audio/comms/voices/Kordani/`    -> Kordani
- `assets/audio/comms/voices/Brengop/`    -> Brengop
- `assets/audio/comms/voices/Krushvak/`   -> Krushvak
- `assets/audio/comms/voices/HunBorian/`  -> HunBorian
- `assets/audio/comms/voices/Oruun/`      -> Oruun
- `assets/audio/comms/voices/Prunjun/`    -> Prunjun
- `assets/audio/comms/voices/Tolvak/`     -> Tolvak
