using System.Numerics;
using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Data;
using SubspaceProtocols.Procedural;

namespace SubspaceProtocols.UI;

public sealed partial class ArtTestScreen
{
    private const int CorridorNpcLineupCount = 6;
    private const int CorridorSpriteFramesPerCharacter = 5; // Frame 0 = Idle, Frames 1..4 = Walk 0..3

    private sealed class CorridorNpcSlot
    {
        public int Seed { get; set; }
        public PortraitSpecies Species { get; set; }
        public CorridorSpriteRole Role { get; set; }
        public CorridorFacing Facing { get; set; }
        public Texture2D PortraitTex;
        public readonly Texture2D[] SpriteFrames = new Texture2D[CorridorSpriteFramesPerCharacter];
    }

    private static readonly CorridorSpriteRole[] CorridorNpcRoles =
    [
        CorridorSpriteRole.Deck4Laborer,
        CorridorSpriteRole.KordaniSentry,
        CorridorSpriteRole.BrengopClerk,
        CorridorSpriteRole.OffDutyCaptain
    ];

    private static readonly PortraitSpecies[] CorridorLineupSpeciesPool =
    [
        PortraitSpecies.Human,
        PortraitSpecies.Helani,
        PortraitSpecies.Klaasari,
        PortraitSpecies.Thane,
        PortraitSpecies.Veldor,
        PortraitSpecies.Brengop,
        PortraitSpecies.Kordani,
        PortraitSpecies.Krushvak,
        PortraitSpecies.HunBorian,
        PortraitSpecies.Prunjun,
        PortraitSpecies.Tolvak,
        PortraitSpecies.Orun
    ];

    private CorridorSpritesConfig _corridorConfig = new();
    private int _playerCandidateSeed = 4041;
    private CorridorAnimMode _corridorAnimMode = CorridorAnimMode.Walk;
    private CorridorFacing _playerFacing = CorridorFacing.Right;
    private int _lockedPlayerSeedBadge = 4041;
    private float _corridorSavedBannerTimer = 0f;
    private string _corridorSaveStatusText = string.Empty;

    private Texture2D _playerCandidatePortraitTex;
    private readonly Texture2D[] _playerCandidateFrames = new Texture2D[CorridorSpriteFramesPerCharacter];
    private int _builtPlayerSeed = int.MinValue;
    private CorridorFacing _builtPlayerFacing;

    // NPC Lineup Preview state
    private int _npcRollBatch = 1;
    private int _npcRoleFilterIndex = 0;    // 0 = ALL, 1..4 = specific role
    private int _npcSpeciesFilterIndex = 0; // 0 = ALL, 1..12 = specific species
    private bool _npcLineupDirty = true;
    private readonly CorridorNpcSlot[] _npcSlots = new CorridorNpcSlot[CorridorNpcLineupCount];

    public ArtTestTab ActiveTab => _activeTab;
    internal int LockedPlayerSeed => _lockedPlayerSeedBadge;
    internal int CurrentPlayerCandidateSeed => _playerCandidateSeed;

    private void InitializeCorridorTabState()
    {
        _corridorConfig = CorridorSpritesConfig.Load();
        _corridorConfig.NormalizeDefaults();

        _playerCandidateSeed = _corridorConfig.LockedPlayerCharacter.Seed;
        _lockedPlayerSeedBadge = _corridorConfig.LockedPlayerCharacter.Seed;
        _playerFacing = Enum.TryParse<CorridorFacing>(
            _corridorConfig.LockedPlayerCharacter.FacingDefault,
            ignoreCase: true,
            out CorridorFacing parsedFacing)
            ? parsedFacing
            : CorridorFacing.Right;

        for (int i = 0; i < _npcSlots.Length; i++)
        {
            _npcSlots[i] ??= new CorridorNpcSlot();
        }
        _npcLineupDirty = true;
    }

    private void HandleCorridorTabKeyboard()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.A))
        {
            StepPlayerCandidateSeed(-1);
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.D))
        {
            StepPlayerCandidateSeed(1);
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            RandomizePlayerCandidateSeed();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.W))
        {
            ToggleCorridorWalkIdle();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.F))
        {
            TogglePlayerFacing();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.N))
        {
            RollRandomNpcLineup();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            LockAndSavePlayerCharacter();
        }
    }

    private void HandleCorridorTabMouse(VirtualMouseState mouse)
    {
        if (!mouse.LeftPressed)
            return;

        // Top Header Tab Switcher & Main Menu
        if (mouse.IsHit(12, 6, 196, 28))
        {
            SwitchTab(ArtTestTab.PortraitsAndShips);
            return;
        }
        if (mouse.IsHit(214, 6, 148, 28))
        {
            SwitchTab(ArtTestTab.Corridor);
            return;
        }
        if (mouse.IsHit(1100, 6, 164, 28))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.SwitchTo(ScreenId.MainMenu);
            return;
        }

        // Player Selector Panel Controls (y = 56..92)
        if (mouse.IsHit(24, 92, 132, 34))
        {
            StepPlayerCandidateSeed(-1);
        }
        else if (mouse.IsHit(164, 92, 132, 34))
        {
            StepPlayerCandidateSeed(1);
        }
        else if (mouse.IsHit(304, 92, 138, 34))
        {
            RandomizePlayerCandidateSeed();
        }
        else if (mouse.IsHit(450, 92, 168, 34))
        {
            ToggleCorridorWalkIdle();
        }
        else if (mouse.IsHit(626, 92, 138, 34))
        {
            TogglePlayerFacing();
        }
        else if (mouse.IsHit(958, 92, 298, 34))
        {
            LockAndSavePlayerCharacter();
        }
        // NPC Lineup Controls (y = 352)
        else if (mouse.IsHit(24, 352, 186, 34))
        {
            RollRandomNpcLineup();
        }
        else if (mouse.IsHit(218, 352, 240, 34))
        {
            CycleNpcRoleFilter();
        }
        else if (mouse.IsHit(466, 352, 248, 34))
        {
            CycleNpcSpeciesFilter();
        }
    }

    private void StepPlayerCandidateSeed(int delta)
    {
        _playerCandidateSeed = Math.Max(1, _playerCandidateSeed + delta);
        AudioHooks.Play("terminal_key_tab");
    }

    private void RandomizePlayerCandidateSeed()
    {
        _randomCounter++;
        uint hash = DeterministicRng.HashSeedFnv1a($"player_cand_{_randomCounter}_{_animTick}_{_playerCandidateSeed}");
        _playerCandidateSeed = (int)((hash % 89999u) + 1000u);
        AudioHooks.Play("terminal_key_confirm");
    }

    private void ToggleCorridorWalkIdle()
    {
        _corridorAnimMode = _corridorAnimMode == CorridorAnimMode.Walk
            ? CorridorAnimMode.Idle
            : CorridorAnimMode.Walk;
        AudioHooks.Play("terminal_key_tab");
    }

    private void TogglePlayerFacing()
    {
        _playerFacing = _playerFacing == CorridorFacing.Right
            ? CorridorFacing.Left
            : CorridorFacing.Right;
        AudioHooks.Play("terminal_key_tab");
    }

    private void RollRandomNpcLineup()
    {
        _npcRollBatch++;
        _npcLineupDirty = true;
        AudioHooks.Play("terminal_key_confirm");
    }

    private void CycleNpcRoleFilter()
    {
        _npcRoleFilterIndex = (_npcRoleFilterIndex + 1) % (CorridorNpcRoles.Length + 1);
        _npcLineupDirty = true;
        AudioHooks.Play("terminal_key_tab");
    }

    private void CycleNpcSpeciesFilter()
    {
        _npcSpeciesFilterIndex = (_npcSpeciesFilterIndex + 1) % (CorridorLineupSpeciesPool.Length + 1);
        _npcLineupDirty = true;
        AudioHooks.Play("terminal_key_tab");
    }

    private bool LockAndSavePlayerCharacter()
    {
        _corridorConfig.LockedPlayerCharacter.Seed = _playerCandidateSeed;
        _corridorConfig.LockedPlayerCharacter.Species = "Human";
        _corridorConfig.LockedPlayerCharacter.Role = CorridorSpriteRole.PlayerDeck4Worker.ToString();
        _corridorConfig.LockedPlayerCharacter.FacingDefault = _playerFacing.ToString();

        bool ok = _corridorConfig.SaveAtomic();
        _lockedPlayerSeedBadge = _playerCandidateSeed;
        _corridorSavedBannerTimer = 4.5f;
        _corridorSaveStatusText = ok
            ? $"SAVED TO data/corridor_sprites.json (SEED #{_playerCandidateSeed})"
            : "SAVE FAILED -- CHECK PERMISSIONS ON data/corridor_sprites.json";
        AudioHooks.Play(ok ? "terminal_key_confirm" : "citation_buzz");
        return ok;
    }

    private void EnsureCorridorTabTexturesUpToDate(bool forceRebuild)
    {
        bool playerDirty = forceRebuild ||
            _playerCandidatePortraitTex.Id == 0 ||
            _builtPlayerSeed != _playerCandidateSeed ||
            _builtPlayerFacing != _playerFacing;

        if (playerDirty)
        {
            UnloadPlayerCorridorTextures();

            CorridorSpriteRenderOptions baseOpt = new()
            {
                Seed = _playerCandidateSeed,
                Species = PortraitSpecies.Human,
                Role = CorridorSpriteRole.PlayerDeck4Worker,
                Facing = _playerFacing,
                AnimMode = CorridorAnimMode.Idle,
                WalkFrame = 0
            };

            PortraitRenderOptions portOpt = CorridorSpriteRenderer.BuildMatchingPortraitOptions(baseOpt);
            _playerCandidatePortraitTex = PortraitRenderer.RenderBuffer(portOpt).ToTexture2D();
            _playerCandidateFrames[0] = CorridorSpriteRenderer.RenderTexture(baseOpt);
            for (int f = 0; f < 4; f++)
            {
                _playerCandidateFrames[f + 1] = CorridorSpriteRenderer.RenderTexture(baseOpt with
                {
                    AnimMode = CorridorAnimMode.Walk,
                    WalkFrame = f
                });
            }

            _builtPlayerSeed = _playerCandidateSeed;
            _builtPlayerFacing = _playerFacing;
        }

        if (forceRebuild || _npcLineupDirty || _npcSlots[0].SpriteFrames[0].Id == 0)
        {
            UnloadNpcCorridorTextures();

            DeterministicRng rng = new($"corridor-npc-lineup-batch-{_npcRollBatch}-r{_npcRoleFilterIndex}-s{_npcSpeciesFilterIndex}");
            for (int i = 0; i < CorridorNpcLineupCount; i++)
            {
                CorridorSpriteRole role = _npcRoleFilterIndex == 0
                    ? PickWeightedNpcRole(rng.Fork($"role_{i}"))
                    : CorridorNpcRoles[_npcRoleFilterIndex - 1];

                PortraitSpecies species = ResolveNpcSpeciesForRole(role, rng.Fork($"sp_{i}"));
                int seed = rng.Fork($"seed_{i}").NextInt(1000, 99999);
                CorridorFacing facing = (i % 2 == 0) ? CorridorFacing.Right : CorridorFacing.Left;

                CorridorNpcSlot slot = _npcSlots[i] ?? new CorridorNpcSlot();
                slot.Seed = seed;
                slot.Species = species;
                slot.Role = role;
                slot.Facing = facing;

                CorridorSpriteRenderOptions npcOpt = new()
                {
                    Seed = seed,
                    Species = species,
                    Role = role,
                    Facing = facing,
                    AnimMode = CorridorAnimMode.Idle,
                    WalkFrame = 0
                };

                slot.PortraitTex = PortraitRenderer.RenderBuffer(CorridorSpriteRenderer.BuildMatchingPortraitOptions(npcOpt)).ToTexture2D();
                slot.SpriteFrames[0] = CorridorSpriteRenderer.RenderTexture(npcOpt);
                for (int f = 0; f < 4; f++)
                {
                    slot.SpriteFrames[f + 1] = CorridorSpriteRenderer.RenderTexture(npcOpt with
                    {
                        AnimMode = CorridorAnimMode.Walk,
                        WalkFrame = f
                    });
                }

                _npcSlots[i] = slot;
            }

            _npcLineupDirty = false;
        }
    }

    private CorridorSpriteRole PickWeightedNpcRole(DeterministicRng rng)
    {
        Dictionary<string, int> weights = _corridorConfig.NpcProceduralConfig.RoleWeights;
        int totalWeight = 0;
        foreach (CorridorSpriteRole role in CorridorNpcRoles)
        {
            int w = weights.TryGetValue(role.ToString(), out int raw) ? Math.Max(1, raw) : 20;
            totalWeight += w;
        }

        int roll = rng.NextInt(0, Math.Max(1, totalWeight));
        int accum = 0;
        foreach (CorridorSpriteRole role in CorridorNpcRoles)
        {
            int w = weights.TryGetValue(role.ToString(), out int raw) ? Math.Max(1, raw) : 20;
            accum += w;
            if (roll < accum)
                return role;
        }
        return CorridorSpriteRole.Deck4Laborer;
    }

    private PortraitSpecies ResolveNpcSpeciesForRole(CorridorSpriteRole role, DeterministicRng rng)
    {
        if (_npcSpeciesFilterIndex > 0)
        {
            return CorridorLineupSpeciesPool[_npcSpeciesFilterIndex - 1];
        }

        if (_npcRoleFilterIndex == 0)
        {
            if (role == CorridorSpriteRole.KordaniSentry)
                return PortraitSpecies.Kordani;
            if (role == CorridorSpriteRole.BrengopClerk)
                return PortraitSpecies.Brengop;
        }

        List<string> allowed = _corridorConfig.NpcProceduralConfig.AllowedSpecies;
        if (allowed.Count > 0)
        {
            string picked = allowed[rng.NextInt(0, allowed.Count)];
            return PortraitRenderer.ParseSpecies(picked);
        }

        return CorridorLineupSpeciesPool[rng.NextInt(0, CorridorLineupSpeciesPool.Length)];
    }

    private void DrawCorridorTabVirtual(VirtualMouseState mouse)
    {
        // =====================================================================
        // PANEL A: PLAYER CHARACTER SELECTOR (16x32 @ 3x + 64x64 Portrait Ref)
        // =====================================================================
        Raylib.DrawRectangle(12, 48, 1256, 288, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(12, 48, 1256, 288, Palette.WeatheredGunmetalEdge);

        UiText.Draw("DECK 4 PLAYER SELECTOR (PLAYER_DECK4_WORKER // HUMAN // 16x32 @ 3X NEAREST-NEIGHBOR)",
            24, 58, 10, Palette.CrtAmberPrimary);

        string lockedBadge = $"LOCKED JSON SEED: #{_lockedPlayerSeedBadge}";
        int lbw = UiText.MeasureWidth(lockedBadge, 10);
        Raylib.DrawRectangle(1256 - lbw - 18, 54, lbw + 14, 20, Palette.OxidizedBronzeShadow);
        Raylib.DrawRectangleLines(1256 - lbw - 18, 54, lbw + 14, 20, Palette.ClearanceTeal);
        UiText.Draw(lockedBadge, 1256 - lbw - 11, 59, 10, Palette.ClearanceTeal);

        if (_corridorSavedBannerTimer > 0f && !string.IsNullOrEmpty(_corridorSaveStatusText))
        {
            UiText.Draw(_corridorSaveStatusText, 640, 59, 10, Palette.ClearanceTeal);
        }

        // Control Row (y = 92)
        DrawButton(mouse, 24, 92, 132, 34, "< PREV SEED [A]");
        DrawButton(mouse, 164, 92, 132, 34, "NEXT SEED > [D]");
        DrawButton(mouse, 304, 92, 138, 34, "RANDOMIZE [R]");
        DrawToggleButton(mouse, 450, 92, 168, 34,
            _corridorAnimMode == CorridorAnimMode.Walk ? "ANIM: WALK [W]" : "ANIM: IDLE [W]",
            _corridorAnimMode == CorridorAnimMode.Walk, Palette.ClearanceTeal);
        DrawToggleButton(mouse, 626, 92, 138, 34,
            $"FACE: {(_playerFacing == CorridorFacing.Right ? "RIGHT" : "LEFT")} [F]",
            true, Palette.CrtAmberPrimary);
        UiText.Draw($"CANDIDATE SEED: #{_playerCandidateSeed}", 780, 104, 10, Palette.CrtAmberPrimary);
        DrawToggleButton(mouse, 958, 92, 298, 34,
            "LOCK & SAVE PLAYER -> JSON [ENTER]",
            _playerCandidateSeed == _lockedPlayerSeedBadge, Palette.ClearanceTeal);

        // Portrait Reference Box (x = 24, y = 138, 136x136)
        Raylib.DrawRectangle(24, 138, 136, 136, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(24, 138, 136, 136, Palette.CrtAmberDimmed);
        if (_playerCandidatePortraitTex.Id != 0)
        {
            Rectangle pSrc = new(0, 0, Constants.PortraitWidth, Constants.PortraitHeight);
            Rectangle pDst = new(28, 142, 128, 128);
            Raylib.DrawTexturePro(_playerCandidatePortraitTex, pSrc, pDst, Vector2.Zero, 0f, Color.White);
        }
        UiText.Draw("64x64 PORTRAIT @ 2X", 28, 282, 10, Palette.CrtAmberDimmed);

        CorridorSpriteRenderOptions traitPreviewOpt = new()
        {
            Seed = _playerCandidateSeed,
            Species = PortraitSpecies.Human,
            Role = CorridorSpriteRole.PlayerDeck4Worker
        };
        UiText.Draw(CorridorSpriteRenderer.DescribeCandidateSummary(traitPreviewOpt), 24, 302, 10, Palette.ClearanceTeal);

        // 21:9 Corridor Preview Strip for Player Candidate (x = 180..1256, y = 138..318 -> W=1076, H=180)
        DrawCorridorPreviewBackdropStrip(180, 138, 1076, 180, scrollOffsetPx: (_corridorAnimMode == CorridorAnimMode.Walk ? (_animTick / 2) : 0));

        int frameSlot = _corridorAnimMode == CorridorAnimMode.Walk ? (1 + ((_animTick / 8) % 4)) : 0;
        Texture2D activePlayerTex = _playerCandidateFrames[frameSlot];
        Rectangle sprSrc = new(0, 0, Constants.CorridorSpriteWidth, Constants.CorridorSpriteHeight);

        // Center 3x animated sprite on floor (48x96)
        int pDrawW = Constants.CorridorSpriteWidth * Constants.CorridorSpritePreviewScale;
        int pDrawH = Constants.CorridorSpriteHeight * Constants.CorridorSpritePreviewScale;
        int floorFootY = 138 + 180 - 24;
        if (activePlayerTex.Id != 0)
        {
            Raylib.DrawTexturePro(activePlayerTex, sprSrc,
                new Rectangle(460, floorFootY - pDrawH, pDrawW, pDrawH), Vector2.Zero, 0f, Color.White);
            UiText.Draw("3X (48x96)", 452, floorFootY + 6, 10, Palette.ClearanceTeal);

            // Also show 1x native 16x32 & all 5 frames side-by-side cleanly for visual inspection
            Raylib.DrawTexture(_playerCandidateFrames[0], 568, floorFootY - Constants.CorridorSpriteHeight, Color.White);
            UiText.Draw("1X", 570, floorFootY + 6, 10, Palette.CrtAmberDimmed);

            for (int f = 0; f < 4; f++)
            {
                Texture2D fTex = _playerCandidateFrames[f + 1];
                if (fTex.Id != 0)
                {
                    int fx = 656 + f * 96;
                    Raylib.DrawTexturePro(fTex, sprSrc,
                        new Rectangle(fx, floorFootY - pDrawH, pDrawW, pDrawH), Vector2.Zero, 0f, Color.White);
                    UiText.Draw($"WALK {f + 1}", fx + 4, floorFootY + 6, 10,
                        frameSlot == f + 1 ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
                }
            }
        }

        // =====================================================================
        // PANEL B: RANDOM PROCEDURAL NPC LINEUP PREVIEW (6 NPCs on 21:9 Floor)
        // =====================================================================
        Raylib.DrawRectangle(12, 344, 1256, 326, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(12, 344, 1256, 326, Palette.WeatheredGunmetalEdge);

        DrawButton(mouse, 24, 352, 186, 34, "ROLL RANDOM NPCs [N]");
        string roleFilterLabel = _npcRoleFilterIndex == 0
            ? "ROLE: ALL"
            : $"ROLE: {CorridorNpcRoles[_npcRoleFilterIndex - 1]}";
        DrawToggleButton(mouse, 218, 352, 240, 34, roleFilterLabel, _npcRoleFilterIndex > 0, Palette.CrtAmberPrimary);

        string speciesFilterLabel = _npcSpeciesFilterIndex == 0
            ? "SPECIES: ALL (12)"
            : $"SPECIES: {PortraitRenderer.FormatSpeciesLabel(CorridorLineupSpeciesPool[_npcSpeciesFilterIndex - 1]).ToUpperInvariant()}";
        DrawToggleButton(mouse, 466, 352, 248, 34, speciesFilterLabel, _npcSpeciesFilterIndex > 0, Palette.ClearanceTeal);

        UiText.Draw("PROCEDURAL NPC LINEUP (16x32 @ 3X ON 21:9 CORRIDOR FLOOR + MATCHING PORTRAIT TRAIT REF)",
            730, 364, 10, Palette.CrtAmberDimmed);

        // Wide 21:9 Corridor Backdrop Strip (x = 24..1256, y = 396..576)
        DrawCorridorPreviewBackdropStrip(24, 396, 1232, 180, scrollOffsetPx: 0);

        int npcFootY = 396 + 180 - 20;
        for (int i = 0; i < CorridorNpcLineupCount; i++)
        {
            CorridorNpcSlot slot = _npcSlots[i];
            int cellX = 36 + i * 202;

            int animIdx = _corridorAnimMode == CorridorAnimMode.Walk
                ? 1 + (((_animTick / 8) + i) % 4)
                : 0;
            Texture2D npcTex = slot.SpriteFrames[animIdx];
            if (npcTex.Id != 0)
            {
                Raylib.DrawTexturePro(npcTex, sprSrc,
                    new Rectangle(cellX + 26, npcFootY - pDrawH, pDrawW, pDrawH), Vector2.Zero, 0f, Color.White);
            }

            if (slot.PortraitTex.Id != 0)
            {
                Rectangle pSrc = new(0, 0, Constants.PortraitWidth, Constants.PortraitHeight);
                Raylib.DrawTexturePro(slot.PortraitTex, pSrc,
                    new Rectangle(cellX + 94, npcFootY - 48, 48, 48), Vector2.Zero, 0f, Color.White);
                Raylib.DrawRectangleLines(cellX + 93, npcFootY - 49, 50, 50, Palette.CrtAmberDimmed);
            }

            string roleShort = slot.Role switch
            {
                CorridorSpriteRole.Deck4Laborer => "D4 LABORER",
                CorridorSpriteRole.KordaniSentry => "KDF SENTRY",
                CorridorSpriteRole.BrengopClerk => "BMC CLERK",
                _ => "OFF-CAPTAIN"
            };
            UiText.Draw(roleShort, cellX + 8, 584, 10, Palette.CrtAmberPrimary);
            UiText.Draw($"{PortraitRenderer.FormatSpeciesLabel(slot.Species).ToUpperInvariant()} #{slot.Seed}", cellX + 8, 600, 10, Palette.ClearanceTeal);
            UiText.Draw($"FACE: {slot.Facing.ToString().ToUpperInvariant()}", cellX + 8, 616, 10, Palette.WeatheredGunmetalEdge);
        }

        // Footer bar
        Raylib.DrawRectangle(0, 680, Constants.VirtualWidth, 40, Palette.OutlineVoid);
        Raylib.DrawLine(0, 680, Constants.VirtualWidth, 680, Palette.WeatheredGunmetalEdge);
        UiText.Draw(
            "CORRIDOR KEYS: [TAB] SWITCH TAB | [A/D] PREV/NEXT SEED | [R] RANDOM | [W] WALK/IDLE | [F] FACING | [N] ROLL NPCs | [ENTER] SAVE JSON",
            16, 695, 10, Palette.CrtAmberDimmed);
    }

    private static void DrawCorridorPreviewBackdropStrip(int x, int y, int w, int h, int scrollOffsetPx)
    {
        Raylib.BeginScissorMode(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, new Color(24, 29, 36, 255));

        int ceilH = 26;
        int floorY = y + h - 42;

        // Upper ceiling conduit truss
        Raylib.DrawRectangle(x, y, w, ceilH, Palette.OutlineVoid);
        Raylib.DrawLine(x, y + ceilH, x + w, y + ceilH, Palette.WeatheredGunmetalEdge);

        // Vertical corridor bulkhead ribs & Brown Dwarf viewport glow window strips
        int ribStart = x - ((scrollOffsetPx * 2) % 140);
        for (int rx = ribStart; rx < x + w; rx += 140)
        {
            Raylib.DrawRectangle(rx, y + ceilH, 10, floorY - (y + ceilH), Palette.WeatheredGunmetalMid);
            Raylib.DrawRectangle(rx + 28, y + ceilH + 14, 72, 46, Palette.BrownDwarfGlowDeep);
            Raylib.DrawRectangleLines(rx + 28, y + ceilH + 14, 72, 46, Palette.BrownDwarfGlowRim);
        }

        // Deck 4 floor grating + safety yellow/amber edge line
        Raylib.DrawRectangle(x, floorY, w, h - (floorY - y), new Color(18, 22, 28, 255));
        Raylib.DrawLine(x, floorY, x + w, floorY, Palette.OxidizedBronzeWorn);
        for (int gx = x - ((scrollOffsetPx * 3) % 32); gx < x + w; gx += 32)
        {
            Raylib.DrawLine(gx, floorY + 4, gx - 10, y + h, new Color(34, 40, 50, 255));
        }

        Raylib.EndScissorMode();
        Raylib.DrawRectangleLines(x, y, w, h, Palette.WeatheredGunmetalEdge);
    }

    private void UnloadPlayerCorridorTextures()
    {
        PixelBuffer.UnloadTexture(ref _playerCandidatePortraitTex);
        for (int i = 0; i < _playerCandidateFrames.Length; i++)
        {
            PixelBuffer.UnloadTexture(ref _playerCandidateFrames[i]);
        }
        _builtPlayerSeed = int.MinValue;
    }

    private void UnloadNpcCorridorTextures()
    {
        for (int s = 0; s < _npcSlots.Length; s++)
        {
            CorridorNpcSlot? slot = _npcSlots[s];
            if (slot == null)
                continue;

            PixelBuffer.UnloadTexture(ref slot.PortraitTex);
            for (int f = 0; f < slot.SpriteFrames.Length; f++)
            {
                PixelBuffer.UnloadTexture(ref slot.SpriteFrames[f]);
            }
        }
    }

    private void UnloadCorridorTabTextures()
    {
        UnloadPlayerCorridorTextures();
        UnloadNpcCorridorTextures();
    }

    // Smoke-test helpers (non-invasive, explicit cleanup via Unload / OnExit)
    internal void SmokeSwitchToCorridorTab() => SwitchTab(ArtTestTab.Corridor);
    internal void SmokeStepPlayerCandidate(int delta)
    {
        StepPlayerCandidateSeed(delta);
        EnsureCorridorTabTexturesUpToDate(forceRebuild: false);
    }
    internal bool SmokeSaveLockedPlayerToPath(string explicitPath)
    {
        _corridorConfig.LockedPlayerCharacter.Seed = _playerCandidateSeed;
        _corridorConfig.LockedPlayerCharacter.Species = "Human";
        _corridorConfig.LockedPlayerCharacter.Role = CorridorSpriteRole.PlayerDeck4Worker.ToString();
        _corridorConfig.LockedPlayerCharacter.FacingDefault = _playerFacing.ToString();
        return _corridorConfig.SaveAtomic(explicitPath);
    }
    internal (bool HasPlayerTex, int NpcCount, int LoadedNpcSprites) SmokeGetCorridorTabMetrics()
    {
        int loadedNpcs = 0;
        for (int i = 0; i < _npcSlots.Length; i++)
        {
            if (_npcSlots[i]?.SpriteFrames[0].Id != 0 && _npcSlots[i]?.SpriteFrames[4].Id != 0)
                loadedNpcs++;
        }
        bool hasPlayer = _playerCandidatePortraitTex.Id != 0 &&
                         _playerCandidateFrames[0].Id != 0 &&
                         _playerCandidateFrames[4].Id != 0;
        return (hasPlayer, _npcSlots.Length, loadedNpcs);
    }
}
