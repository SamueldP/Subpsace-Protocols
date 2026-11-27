using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Data;

namespace SubspaceProtocols.Procedural;

public sealed record CorridorSpriteRenderOptions
{
    public int Seed { get; init; } = 4041;
    public string? SeedIdOverride { get; init; }
    public PortraitSpecies Species { get; init; } = PortraitSpecies.Human;
    public CorridorSpriteRole Role { get; init; } = CorridorSpriteRole.PlayerDeck4Worker;
    public CorridorFacing Facing { get; init; } = CorridorFacing.Right;
    public CorridorAnimMode AnimMode { get; init; } = CorridorAnimMode.Idle;
    public int WalkFrame { get; init; } = 0;
    public string FactionPrefix { get; init; } = string.Empty;
    public bool EnableBrownDwarfRimLight { get; init; } = true;
}

/// <summary>
/// Crisp 16x32 full-body procedural pixel sprite generator (drawn scaled 3x nearest-neighbor -> 48x96)
/// synchronized directly with PortraitRenderer's deterministic trait profile, species color ramps,
/// hair cuts, facial hair/opticals, and uniform palettes.
/// </summary>
public static class CorridorSpriteRenderer
{
    public static string ResolveFactionPrefix(CorridorSpriteRole role, PortraitSpecies species, string? explicitPrefix = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPrefix))
            return explicitPrefix;

        return role switch
        {
            CorridorSpriteRole.PlayerDeck4Worker => "FTV-",
            CorridorSpriteRole.Deck4Laborer => "MNG-",
            CorridorSpriteRole.KordaniSentry => "KDF-",
            CorridorSpriteRole.BrengopClerk => "BMC-",
            _ => species switch
            {
                PortraitSpecies.Kordani => "KDF-",
                PortraitSpecies.Brengop => "BMC-",
                PortraitSpecies.Krushvak => "KRW-",
                PortraitSpecies.Prunjun => "PRJ-",
                PortraitSpecies.Orun => "ORN-",
                PortraitSpecies.Tolvak => "TLV-",
                PortraitSpecies.HunBorian => "HBR-",
                PortraitSpecies.Human => "FTV-",
                PortraitSpecies.Helani => "GUV-",
                PortraitSpecies.Klaasari => "GUV-",
                PortraitSpecies.Thane => "FTC-",
                PortraitSpecies.Veldor => "ZNI-",
                _ => "FTV-"
            }
        };
    }

    public static string BuildSharedSeedId(int numericSeed, PortraitSpecies species, CorridorSpriteRole role) =>
        $"corridor-{role}-{species}-{numericSeed}";

    public static PortraitRenderOptions BuildMatchingPortraitOptions(CorridorSpriteRenderOptions spriteOpt)
    {
        string prefix = ResolveFactionPrefix(spriteOpt.Role, spriteOpt.Species, spriteOpt.FactionPrefix);
        string seedId = string.IsNullOrWhiteSpace(spriteOpt.SeedIdOverride)
            ? BuildSharedSeedId(spriteOpt.Seed, spriteOpt.Species, spriteOpt.Role)
            : spriteOpt.SeedIdOverride;

        return new PortraitRenderOptions
        {
            SeedId = seedId,
            Species = spriteOpt.Species,
            FactionPrefix = prefix,
            Mode = PortraitRenderMode.Live,
            IsMolting = spriteOpt.Species == PortraitSpecies.Tolvak,
            DoppleInTrueForm = true,
            IsKordaniSecurityOfficer = false
        };
    }

    public static string DescribeCandidateSummary(CorridorSpriteRenderOptions spriteOpt)
    {
        PortraitRenderOptions pOpt = BuildMatchingPortraitOptions(spriteOpt);
        DeterministicRng rng = new(pOpt.SeedId);
        PortraitTraitProfile traits = PortraitTraitProfile.Roll(rng);

        string hairName = spriteOpt.Species is PortraitSpecies.Human or PortraitSpecies.Helani
            ? (traits.HairArchetype % 10) switch
            {
                0 => "BALD",
                1 => "BUZZ",
                2 => "PART",
                3 => "POMP",
                4 => "PEAK",
                5 => "SPIKY",
                6 => "BOB",
                7 => "BUN",
                8 => "CREST",
                _ => "HALO"
            }
            : spriteOpt.Species.ToString().ToUpperInvariant();

        string beardName = spriteOpt.Species == PortraitSpecies.Human
            ? traits.FacialHairVariant switch
            {
                1 => "STUBBLE",
                2 => "STACHE",
                3 => "GOATEE",
                4 => "STRAP",
                5 => "BEARD",
                _ => "CLEAN"
            }
            : (spriteOpt.Role.ToString().ToUpperInvariant());

        return $"{hairName} | {beardName} | UNI#{traits.UniformArchetype}";
    }

    public static Texture2D RenderTexture(CorridorSpriteRenderOptions options)
    {
        PixelBuffer buf = RenderBuffer(options);
        return buf.ToTexture2D();
    }

    public static PixelBuffer RenderBuffer(CorridorSpriteRenderOptions options)
    {
        int w = Constants.CorridorSpriteWidth;  // 16
        int h = Constants.CorridorSpriteHeight; // 32
        PixelBuffer buf = new(w, h);

        PortraitRenderOptions pOpt = BuildMatchingPortraitOptions(options);
        DeterministicRng rng = new(pOpt.SeedId);
        PortraitTraitProfile traits = PortraitTraitProfile.Roll(rng);

        bool isTrueDopple = options.Species == PortraitSpecies.Dopple;
        (string[] skinRamp, string[] secondarySkinRamp) = PortraitRenderer.SelectSpeciesSkinRamps(options.Species, isTrueDopple, rng);
        string[] uniformRamp = PortraitRenderer.SelectUniformRamp(options.Species, pOpt.FactionPrefix, traits);
        string[] hairRamp = PortraitRenderer.SelectHairRamp(rng);

        ResolveCostumeRamps(
            options.Role,
            options.Species,
            uniformRamp,
            traits,
            out string[] torsoRamp,
            out string[] trimRamp,
            out string[] pantsRamp,
            out string[] bootRamp);

        int frame = options.AnimMode == CorridorAnimMode.Walk
            ? ((options.WalkFrame % 4) + 4) % 4
            : -1;
        int bobY = (frame == 1 || frame == 3) ? -1 : 0;

        bool isWideStocky = options.Species is PortraitSpecies.HunBorian or PortraitSpecies.Prunjun or PortraitSpecies.Krushvak or PortraitSpecies.Brengop;
        int torsoLeft = isWideStocky ? 4 : 5;
        int torsoW = isWideStocky ? 8 : 6;
        int torsoTop = 12 + bobY;
        int torsoH = 9;
        int hipY = torsoTop + torsoH - 1; // 20 + bobY

        // 1. Back Arm (partially occluded behind torso)
        DrawBackArm(buf, frame, bobY, torsoLeft, torsoW, torsoTop, torsoRamp, skinRamp);

        // 2. Back Leg & Front Leg (y = 20..29)
        DrawLegs(buf, frame, hipY, isWideStocky, options.Role, pantsRamp, bootRamp);

        // 3. Torso & Role Costume Attire (y = 12..21)
        DrawTorsoAndAttire(buf, options.Role, options.Species, traits, torsoLeft, torsoTop, torsoW, torsoH, torsoRamp, trimRamp, secondarySkinRamp);

        // 4. Neck & Species-Specific Head + Portrait Traits (y = 2..12)
        DrawHeadAndFeatures(buf, options.Role, options.Species, isTrueDopple, traits, bobY, skinRamp, secondarySkinRamp, hairRamp, torsoRamp, trimRamp);

        // 5. Front Arm & Role Prop (y = 12..21)
        DrawFrontArm(buf, frame, bobY, options.Role, torsoLeft, torsoTop, torsoRamp, trimRamp, skinRamp);

        // 6. Optional Horizontal Mirror for Left-Facing Walk/Idle
        if (options.Facing == CorridorFacing.Left)
        {
            buf = buf.MirrorHorizontal();
        }

        // 7. Subtle Brown Dwarf directional rim-light on lower/outer silhouette edge BEFORE outline
        if (options.EnableBrownDwarfRimLight)
        {
            ApplySubtleCorridorRimLight(buf, options.Facing);
        }

        // 8. Final crisp 1px dark silhouette outline (#080b10)
        buf.ApplyOutline(Palette.HexOutlineVoid);
        return buf;
    }

    private static void ResolveCostumeRamps(
        CorridorSpriteRole role,
        PortraitSpecies species,
        string[] portraitUniformRamp,
        PortraitTraitProfile traits,
        out string[] torsoRamp,
        out string[] trimRamp,
        out string[] pantsRamp,
        out string[] bootRamp)
    {
        switch (role)
        {
            case CorridorSpriteRole.PlayerDeck4Worker:
                torsoRamp = (traits.UniformArchetype % 2 == 0)
                    ? Palette.FreeTraderPrimarySlateRamp
                    : Palette.MiningGuildPrimaryCharcoalRamp;
                trimRamp = Palette.FreeTraderTrimRustRamp;
                pantsRamp = Palette.GuSecondaryGunmetalRamp;
                bootRamp = Palette.KrushvakSecondaryEarthBrownRamp;
                break;

            case CorridorSpriteRole.Deck4Laborer:
                torsoRamp = (traits.UniformArchetype % 3) switch
                {
                    0 => Palette.MiningGuildPrimaryCharcoalRamp,
                    1 => Palette.FreeTraderPrimarySlateRamp,
                    _ => Palette.KrushvakSecondaryEarthBrownRamp
                };
                trimRamp = Palette.MiningGuildTrimHazardYellowRamp;
                pantsRamp = Palette.KordaniSecondaryDarkSlateRamp;
                bootRamp = Palette.KrushvakSecondaryEarthBrownRamp;
                break;

            case CorridorSpriteRole.KordaniSentry:
                torsoRamp = Palette.KordaniSecondaryDarkSlateRamp;
                trimRamp = Palette.KordaniPrimaryBlueGreenRamp;
                pantsRamp = Palette.KordaniSecondaryDarkSlateRamp;
                bootRamp = Palette.ThaneBeardDeepBlackRamp;
                break;

            case CorridorSpriteRole.BrengopClerk:
                torsoRamp = Palette.BrengopPrimaryGrossYellowRamp;
                trimRamp = Palette.BrengopSecondaryGrimyBrassRamp;
                pantsRamp = Palette.BrengopSecondaryGrimyBrassRamp;
                bootRamp = Palette.ThaneBeardDeepBlackRamp;
                break;

            default: // OffDutyCaptain
                torsoRamp = portraitUniformRamp;
                trimRamp = species == PortraitSpecies.Veldor
                    ? Palette.VeldorBrassTrimRamp
                    : (species == PortraitSpecies.Kordani ? Palette.KordaniPrimaryBlueGreenRamp : Palette.FreeTraderTrimRustRamp);
                pantsRamp = Palette.GuSecondaryGunmetalRamp;
                bootRamp = Palette.ThaneBeardDeepBlackRamp;
                break;
        }
    }

    private static void DrawBackArm(
        PixelBuffer buf,
        int frame,
        int bobY,
        int torsoLeft,
        int torsoW,
        int torsoTop,
        string[] torsoRamp,
        string[] skinRamp)
    {
        int armX = frame switch
        {
            0 => torsoLeft + torsoW,     // forward swing
            2 => torsoLeft + torsoW - 2, // back swing
            _ => torsoLeft + torsoW - 1
        };
        int armTop = torsoTop + 1;
        int swingX = frame switch
        {
            0 => armX + 1,
            2 => armX - 1,
            _ => armX
        };
        buf.FillRect(armX, armTop, 2, 4, torsoRamp[0]);
        buf.FillRect(swingX, armTop + 4, 2, 2, torsoRamp[0]);
        buf.Set(swingX + 1, armTop + 6 + (bobY == -1 ? 0 : 0), skinRamp[1]);
    }

    private static void DrawFrontArm(
        PixelBuffer buf,
        int frame,
        int bobY,
        CorridorSpriteRole role,
        int torsoLeft,
        int torsoTop,
        string[] torsoRamp,
        string[] trimRamp,
        string[] skinRamp)
    {
        int baseX = torsoLeft - 1;
        int armTop = torsoTop + 1;

        int handDx = frame switch
        {
            0 => -1, // back arm goes forward, front arm goes back
            2 => 2,  // front arm swings forward
            1 or 3 => 1,
            _ => 0
        };

        // Shoulder sleeve
        buf.FillRect(baseX, armTop, 2, 3, torsoRamp[2]);
        buf.Set(baseX, armTop, trimRamp[2]); // cuff/shoulder accent

        // Forearm + hand
        int fx = Math.Clamp(baseX + handDx, 1, 13);
        buf.FillRect(fx, armTop + 3, 2, 3, torsoRamp[1]);
        buf.Set(fx, armTop + 6, skinRamp[2]);

        // Clerk carries an amber/teal cargo ledger slate in hand
        if (role == CorridorSpriteRole.BrengopClerk)
        {
            buf.FillRect(fx + 1, armTop + 4, 2, 3, Palette.HexOutlineVoid);
            buf.Set(fx + 1, armTop + 5, Palette.HexCrtAmberPrimary);
        }
        else if (role == CorridorSpriteRole.KordaniSentry)
        {
            buf.DrawLine(fx, armTop + 4, fx + 2, armTop + 6, Palette.GuSecondaryGunmetalRamp[3]);
        }
    }

    private static void DrawLegs(
        PixelBuffer buf,
        int frame,
        int hipY,
        bool isWideStocky,
        CorridorSpriteRole role,
        string[] pantsRamp,
        string[] bootRamp)
    {
        int backHipX = isWideStocky ? 8 : 8;
        int frontHipX = isWideStocky ? 5 : 6;

        // Back leg + boot
        switch (frame)
        {
            case 0: // Back leg stretched trailing left
                buf.FillRect(frontHipX, hipY, 2, 4, pantsRamp[0]);
                buf.FillRect(frontHipX - 1, hipY + 4, 2, 4, pantsRamp[0]);
                buf.FillRect(frontHipX - 2, hipY + 8, 3, 2, bootRamp[0]);
                // Front leg striding right
                buf.FillRect(backHipX, hipY, 2, 4, pantsRamp[2]);
                buf.FillRect(backHipX + 1, hipY + 4, 2, 4, pantsRamp[1]);
                buf.FillRect(backHipX + 1, hipY + 8, 3, 2, bootRamp[2]);
                break;

            case 1: // Passing frame: back leg planted, front knee bent forward
                buf.FillRect(7, hipY, 2, 8, pantsRamp[1]);
                buf.FillRect(7, hipY + 8, 3, 2, bootRamp[1]);
                buf.FillRect(frontHipX + 1, hipY + 1, 2, 3, pantsRamp[2]);
                buf.FillRect(frontHipX, hipY + 4, 2, 3, pantsRamp[2]);
                buf.FillRect(frontHipX, hipY + 7, 3, 2, bootRamp[2]);
                break;

            case 2: // Opposite stride: back leg forward, front leg back
                buf.FillRect(backHipX, hipY, 2, 4, pantsRamp[0]);
                buf.FillRect(backHipX + 1, hipY + 4, 2, 4, pantsRamp[0]);
                buf.FillRect(backHipX + 1, hipY + 8, 3, 2, bootRamp[1]);
                buf.FillRect(frontHipX, hipY, 2, 4, pantsRamp[2]);
                buf.FillRect(frontHipX - 1, hipY + 4, 2, 4, pantsRamp[1]);
                buf.FillRect(frontHipX - 1, hipY + 8, 3, 2, bootRamp[2]);
                break;

            case 3: // Passing frame opposite
                buf.FillRect(7, hipY, 2, 8, pantsRamp[2]);
                buf.FillRect(7, hipY + 8, 3, 2, bootRamp[2]);
                buf.FillRect(backHipX, hipY + 1, 2, 3, pantsRamp[0]);
                buf.FillRect(backHipX - 1, hipY + 4, 2, 3, pantsRamp[1]);
                buf.FillRect(backHipX - 1, hipY + 7, 3, 2, bootRamp[1]);
                break;

            default: // Idle: sturdy planted two-leg stance
                buf.FillRect(backHipX, hipY, 2, 7, pantsRamp[0]);
                buf.FillRect(backHipX, hipY + 7, 3, 2, bootRamp[1]);
                buf.FillRect(frontHipX, hipY, 2, 7, pantsRamp[2]);
                buf.FillRect(frontHipX, hipY + 7, 3, 2, bootRamp[2]);
                break;
        }

        // Brengop clerk wears a longer corporate coat-tail over upper thighs
        if (role == CorridorSpriteRole.BrengopClerk)
        {
            buf.FillRect(frontHipX - 1, hipY, isWideStocky ? 7 : 6, 3, pantsRamp[1]);
        }
    }

    private static void DrawTorsoAndAttire(
        PixelBuffer buf,
        CorridorSpriteRole role,
        PortraitSpecies species,
        PortraitTraitProfile traits,
        int leftX,
        int topY,
        int w,
        int h,
        string[] torsoRamp,
        string[] trimRamp,
        string[] secondarySkinRamp)
    {
        buf.FillRect(leftX, topY, w, h, (px, py) =>
        {
            float t = Math.Clamp(0.84f - (py - topY) / (float)(h + 2) - (px - leftX) / (float)(w * 2), 0.12f, 0.92f);
            return Dither.SampleRamp(px, py, torsoRamp, t);
        });

        // Waist utility belt
        buf.FillRect(leftX, topY + h - 2, w, 1, Palette.HexOutlineVoid);

        switch (role)
        {
            case CorridorSpriteRole.PlayerDeck4Worker:
                // Diagonal Deck 4 rust-orange harness strap + clearance teal ID pip + brass belt buckle
                buf.DrawLine(leftX + 1, topY + 1, leftX + w - 2, topY + h - 3, trimRamp[2]);
                buf.Set(leftX + 1, topY + 2, Palette.HexClearanceTeal);
                buf.Set(leftX + w / 2, topY + h - 2, Palette.HexOrunBrassTrim);
                break;

            case CorridorSpriteRole.Deck4Laborer:
                // High-visibility safety vest stripe across chest
                buf.FillRect(leftX, topY + 2, w, 2, trimRamp[2]);
                buf.Set(leftX + w / 2, topY + h - 2, trimRamp[3]);
                break;

            case CorridorSpriteRole.KordaniSentry:
                // Authoritarian KDF teal-blue sash, pauldrons, and red collar pips
                buf.FillRect(leftX - 1, topY, 2, 2, trimRamp[2]);
                buf.FillRect(leftX + w - 1, topY, 2, 2, trimRamp[1]);
                buf.DrawLine(leftX + 1, topY + 1, leftX + w - 2, topY + h - 3, trimRamp[3]);
                buf.Set(leftX + w / 2, topY + 1, Palette.HexAlertCrimson);
                break;

            case CorridorSpriteRole.BrengopClerk:
                // Grimy brass lapels & amber tie/collar
                buf.FillRect(leftX + 1, topY, w - 2, 2, trimRamp[2]);
                buf.Set(leftX + w / 2, topY + 2, Palette.HexCrtAmberPrimary);
                break;

            default:
                // Off-duty captain uniform detail from traits.UniformArchetype
                if (traits.UniformArchetype == 0)
                {
                    buf.FillRect(leftX + 1, topY, w - 2, 2, trimRamp[2]);
                }
                else if (traits.UniformArchetype == 1)
                {
                    buf.DrawLine(leftX + 2, topY, leftX + 2, topY + h - 2, trimRamp[3]);
                }
                else if (traits.UniformArchetype == 2)
                {
                    buf.DrawLine(leftX, topY + 2, leftX + w - 1, topY + 2, trimRamp[2]);
                }
                else
                {
                    buf.FillRect(leftX - 1, topY, 2, 2, trimRamp[2]);
                    buf.FillRect(leftX + w - 1, topY, 2, 2, trimRamp[1]);
                }

                if (species == PortraitSpecies.Veldor)
                {
                    // Yellow breather hose across chest
                    buf.DrawLine(leftX + 2, topY, leftX + w - 2, topY + 5, "#e8b93c");
                }
                else if (species == PortraitSpecies.Thane)
                {
                    // Braided deep-black Thane chest beard
                    buf.FillRect(leftX + 2, topY, 3, 4, secondarySkinRamp[1]);
                }
                break;
        }
    }

    private static void DrawHeadAndFeatures(
        PixelBuffer buf,
        CorridorSpriteRole role,
        PortraitSpecies species,
        bool isTrueDopple,
        PortraitTraitProfile traits,
        int bobY,
        string[] skinRamp,
        string[] secondarySkinRamp,
        string[] hairRamp,
        string[] torsoRamp,
        string[] trimRamp)
    {
        int neckY = 10 + bobY;
        if (species != PortraitSpecies.HunBorian)
        {
            buf.FillRect(7, neckY, 3, 2, skinRamp[1]);
        }

        int headX = 5;
        int headY = 4 + bobY;
        int headW = 6;
        int headH = 7;

        if (species is PortraitSpecies.Brengop or PortraitSpecies.Krushvak or PortraitSpecies.HunBorian or PortraitSpecies.Prunjun)
        {
            headX = 4;
            headW = 8;
        }
        else if (species == PortraitSpecies.Orun)
        {
            headY = 3 + bobY;
            headH = 8;
        }

        // Base skull
        if (isTrueDopple)
        {
            buf.FillEllipse(8, headY + 3, 3, 3, (px, py, dx, dy, dist) =>
                Dither.SampleRamp(px, py, skinRamp, 0.85f - dist * 0.45f));
            buf.Set(8, headY + 3, Palette.HexOutlineVoid);
            buf.Set(10, headY + 3, Palette.HexOutlineVoid);
            return;
        }

        buf.FillRect(headX, headY, headW, headH, (px, py) =>
        {
            float t = Math.Clamp(0.88f - (py - headY) / 10.0f - (px - headX) / 16.0f, 0.14f, 0.95f);
            return Dither.SampleRamp(px, py, skinRamp, t);
        });
        // Slightly round top-left / top-right corners for 16-bit silhouette readability
        buf.Set(headX, headY, (string?)null);
        buf.Set(headX + headW - 1, headY, (string?)null);

        // Species-specific silhouette, eyes, hair, beard & headgear
        switch (species)
        {
            case PortraitSpecies.Human:
            case PortraitSpecies.Helani:
            {
                if (species == PortraitSpecies.Helani)
                {
                    buf.FillRect(headX + 2, headY + headH, 3, 1, skinRamp[2]);
                }

                // Side-3/4 eyes
                buf.Set(headX + headW - 3, headY + 3, "#e2e8f0");
                buf.Set(headX + headW - 2, headY + 3, Palette.HexOutlineVoid);
                buf.Set(headX, headY + 3, skinRamp[1]); // ear

                // 10 matching hair styles
                int hairStyle = traits.HairArchetype % 10;
                switch (hairStyle)
                {
                    case 0: // Bald
                        if (species == PortraitSpecies.Helani)
                            buf.Set(headX + 1, headY + 2, hairRamp[1]);
                        break;
                    case 1: // Buzzcut
                        buf.FillRect(headX + 1, headY, headW - 2, 2, hairRamp[1]);
                        break;
                    case 2: // Side-part
                        buf.FillRect(headX, headY, headW - 1, 2, hairRamp[2]);
                        buf.Set(headX, headY + 2, hairRamp[1]);
                        break;
                    case 3: // Pompadour
                        buf.FillRect(headX + 1, headY - 1, headW - 1, 2, hairRamp[2]);
                        buf.Set(headX, headY + 1, hairRamp[1]);
                        break;
                    case 4: // Widow's peak
                        buf.FillRect(headX, headY, 2, 3, hairRamp[1]);
                        buf.Set(headX + 3, headY, hairRamp[2]);
                        break;
                    case 5: // Spiky tufts
                        buf.Set(headX + 1, headY - 1, hairRamp[2]);
                        buf.Set(headX + 3, headY - 1, hairRamp[3]);
                        buf.Set(headX + 5, headY - 1, hairRamp[2]);
                        buf.FillRect(headX + 1, headY, headW - 2, 1, hairRamp[1]);
                        break;
                    case 6: // Bob
                        buf.FillRect(headX, headY, headW - 1, 2, hairRamp[2]);
                        buf.FillRect(headX - 1, headY + 2, 2, 4, hairRamp[1]);
                        break;
                    case 7: // Top bun
                        buf.FillRect(headX + 1, headY, headW - 2, 2, hairRamp[1]);
                        buf.FillRect(headX + 1, headY - 2, 3, 2, hairRamp[2]);
                        break;
                    case 8: // Mohawk crest
                        buf.FillRect(headX + 2, headY - 2, 3, 3, hairRamp[3]);
                        break;
                    default: // Afro halo
                        buf.FillRect(headX - 1, headY - 1, headW + 1, 3, hairRamp[2]);
                        buf.FillRect(headX - 1, headY + 1, 2, 3, hairRamp[1]);
                        break;
                }

                // Human facial hair synced with PortraitTraitProfile.FacialHairVariant
                if (species == PortraitSpecies.Human)
                {
                    if (traits.FacialHairVariant == 2) // Mustache
                    {
                        buf.FillRect(headX + headW - 3, headY + 5, 3, 1, hairRamp[1]);
                    }
                    else if (traits.FacialHairVariant is 3 or 4 or 5) // Goatee / Chin-strap / Full beard
                    {
                        buf.FillRect(headX + 2, headY + 5, headW - 2, 2, hairRamp[1]);
                        if (traits.FacialHairVariant == 5)
                            buf.FillRect(headX + 3, headY + 7, 3, 1, hairRamp[0]);
                    }
                }
                break;
            }

            case PortraitSpecies.Klaasari:
                buf.Set(headX + 1, headY + 2, secondarySkinRamp[2]);
                buf.Set(headX + headW - 2, headY + 3, "#a7f3d0"); // bright eye
                if ((traits.HairArchetype % 5) is 1 or 2)
                {
                    buf.FillRect(headX + 1, headY - 1, 3, 1, secondarySkinRamp[3]);
                }
                else if ((traits.HairArchetype % 5) == 3)
                {
                    buf.DrawLine(headX + 1, headY + 1, headX + headW - 1, headY + 1, Palette.HexClearanceTeal);
                }
                break;

            case PortraitSpecies.Thane:
                buf.Set(headX + headW - 2, headY + 3, "#7dd3fc"); // icy glowing eye
                buf.FillRect(headX + 1, headY + 5, headW - 1, 2, secondarySkinRamp[0]);
                buf.FillRect(headX + 2, headY + 7, 3, 2, secondarySkinRamp[1]);
                break;

            case PortraitSpecies.Veldor:
                // Sealed porcelain dome + glowing sulfur visor + brass scrubber
                buf.FillRect(headX + 2, headY + 2, headW - 2, 2, Palette.VeldorSulfurVisorRamp[2]);
                buf.Set(headX + headW - 2, headY + 2, Palette.VeldorSulfurVisorRamp[3]);
                buf.FillRect(headX + 2, headY + 5, 2, 2, Palette.VeldorBrassTrimRamp[2]);
                buf.Set(headX + 2, headY, Palette.VeldorBrassTrimRamp[2]);
                break;

            case PortraitSpecies.Brengop:
                // Squat troll skull, pointed ear sticking left, small amber eye, tusk
                buf.FillRect(headX - 1, headY + 2, 2, 2, skinRamp[2]); // ear
                buf.Set(headX + headW - 3, headY + 3, "#fde047");
                buf.Set(headX + headW - 2, headY + 5, "#e2e8f0"); // tusk
                break;

            case PortraitSpecies.Kordani:
                // Extended reptilian snout & amber slit eye
                buf.FillRect(headX + headW - 1, headY + 3, 2, 3, skinRamp[2]);
                buf.Set(headX + headW - 2, headY + 2, "#f59e0b");
                if (role == CorridorSpriteRole.KordaniSentry)
                {
                    // Peaked authoritarian KDF officer cap with silver bird crest pip
                    buf.FillRect(headX - 1, headY - 1, headW + 2, 2, Palette.KordaniSecondaryDarkSlateRamp[2]);
                    buf.FillRect(headX + 2, headY + 1, headW, 1, Palette.KordaniSecondaryDarkSlateRamp[0]);
                    buf.Set(headX + 3, headY - 1, Palette.ThaneTitaniumWhiteRamp[3]);
                }
                else
                {
                    buf.FillRect(headX + 1, headY - 1, 3, 1, trimRamp[2]); // dorsal ridge crest
                }
                break;

            case PortraitSpecies.Krushvak:
                // Heavy brow ridge, red/amber eye, thick dark sideburn, tusks
                buf.FillRect(headX + 2, headY + 1, headW - 2, 2, skinRamp[0]);
                buf.Set(headX + headW - 2, headY + 3, "#f59e0b");
                buf.FillRect(headX + 1, headY + 3, 2, 3, hairRamp[0]);
                buf.Set(headX + headW - 1, headY + 5, "#e2e8f0");
                break;

            case PortraitSpecies.HunBorian:
                // Crown spikes + pale blue eyes + heavy jaw
                buf.Set(headX + 1, headY - 1, skinRamp[3]);
                buf.Set(headX + 3, headY - 2, skinRamp[3]);
                buf.Set(headX + 5, headY - 1, skinRamp[3]);
                buf.Set(headX + headW - 3, headY + 3, Palette.HexHunBorianKindEye);
                break;

            case PortraitSpecies.Prunjun:
                // Crooked salvage cap + asymmetrical cyan/amber goggles
                buf.FillRect(headX, headY - 1, headW, 2, Palette.FreeTraderPrimarySlateRamp[2]);
                buf.FillRect(headX + 2, headY + 2, 2, 2, "#38bdf8");
                buf.FillRect(headX + 5, headY + 2, 2, 2, "#f59e0b");
                break;

            case PortraitSpecies.Tolvak:
                // Terracotta head + cream molt patch + dark obsidian eye
                buf.FillRect(headX + 1, headY + 1, 2, 2, Palette.HexTolvakMoltPatch);
                buf.Set(headX + headW - 2, headY + 3, Palette.HexOutlineVoid);
                break;

            case PortraitSpecies.Orun:
                // Monolithic faceted granite + glowing amber fissure eye
                buf.Set(headX + 2, headY + 3, Palette.HexCrtAmberPrimary);
                buf.DrawLine(headX + 2, headY + 4, headX + 4, headY + 5, Palette.HexOrunBrassTrim);
                buf.Set(headX + 1, headY, skinRamp[3]);
                break;
        }

        // Non-Kordani sentry optional hard hat / headlamp for Deck4Laborer
        if (role == CorridorSpriteRole.Deck4Laborer && species is not PortraitSpecies.Veldor and not PortraitSpecies.Prunjun)
        {
            buf.FillRect(headX, headY - 1, headW, 2, Palette.MiningGuildTrimHazardYellowRamp[2]);
            buf.Set(headX + headW - 1, headY, Palette.HexCrtAmberPrimary); // headlamp
        }

        // Optional cybernetic monocle / ear-mic accent on Human/Helani if present
        if (species is PortraitSpecies.Human or PortraitSpecies.Helani && traits.AccessoryVariant == 2)
        {
            buf.Set(headX + headW - 3, headY + 3, Palette.HexCrtAmberPrimary);
        }

        _ = torsoRamp;
    }

    private static void ApplySubtleCorridorRimLight(PixelBuffer buf, CorridorFacing facing)
    {
        // Only apply a subtle 1-2px warm Brown Dwarf rim along the trailing torso/shoulder edge (never on head/face)
        int rimDirX = facing == CorridorFacing.Right ? -1 : 1;
        for (int y = 12; y <= 24; y += 2)
        {
            for (int x = 1; x < buf.Width - 1; x++)
            {
                if (!buf.HasPixel(x, y))
                    continue;
                if (!buf.HasPixel(x + rimDirX, y))
                {
                    buf.Set(x, y, Palette.HexBrownDwarfGlowRim);
                    break;
                }
            }
        }
    }
}
