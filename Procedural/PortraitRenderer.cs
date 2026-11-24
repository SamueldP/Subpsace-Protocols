using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.Procedural;

/// <summary>
/// Canonical active species roster from /design/08_Physical_Look/Procedural_Art_Bible_and_Asset_Spec.md.
/// Note: Removed species (Zulak, Vesh, Prenn) are intentionally excluded per Roster Update.
/// </summary>
public enum PortraitSpecies
{
    Human = 0,
    Helani = 1,
    Klaasari = 2,
    Thane = 3,
    Veldor = 4,
    Brengop = 5,
    Kordani = 6,
    Krushvak = 7,
    Dopple = 8,
    HunBorian = 9,
    Prunjun = 10,
    Tolvak = 11,
    Orun = 12
}

public enum PortraitRenderMode
{
    Live = 0,
    Static = 1,
    Holo = 2,
    CrudeCrayon = 3
}

public sealed record PortraitRenderOptions
{
    public string SeedId { get; init; } = "capt-vek-409";
    public PortraitSpecies Species { get; init; } = PortraitSpecies.Human;
    public PortraitRenderMode Mode { get; init; } = PortraitRenderMode.Live;
    public string FactionPrefix { get; init; } = "GUV-";
    public bool IsMolting { get; init; } = false;
    public bool DoppleInTrueForm { get; init; } = true;
    public bool CrudeCrayon { get; init; } = false;
    public bool HoloAmber { get; init; } = false;
    public int AnimTick { get; init; } = 0;
    public bool IsScriptedOverride { get; init; } = false;
    public string? ScriptedAssetName { get; init; } = null;
    public bool IsKordaniSecurityOfficer { get; init; } = false;

    /// <summary>
    /// Quantizes animation tick so static snow updates at discrete intervals (~10 Hz)
    /// and non-animated modes never re-rasterize when AnimTick advances.
    /// </summary>
    public int EffectiveAnimBucket =>
        Mode == PortraitRenderMode.Static ? (AnimTick / 6) : 0;
}

/// <summary>
/// Deterministic seed-derived structural trait profile ensuring two captains of the same species
/// have distinct macro head silhouettes, facial features, hair/beard/horn cuts, uniform collars,
/// and accessories—even in monochrome Data Slate HOLO mode.
/// </summary>
internal readonly struct PortraitTraitProfile
{
    public int HeadRxDelta { get; init; }       // -2..+2
    public int HeadRyDelta { get; init; }       // -2..+2
    public int JawWidthDelta { get; init; }     // -2..+3
    public int ChinLengthDelta { get; init; }   // -1..+2
    public int CheekArchetype { get; init; }    // 0: Ovoid, 1: High Angular Cheekbones, 2: Wide Square Jaw, 3: Narrow Gaunt
    public int EarStyle { get; init; }          // 0..3
    public int NeckWidthDelta { get; init; }    // -1..+2
    public int ShoulderDelta { get; init; }     // -3..+3
    public int UniformArchetype { get; init; }  // 0: High Tunic, 1: Open Flight Jacket, 2: Cargo Vest/Harness, 3: Heavy Pauldrons

    public int EyeVariant { get; init; }        // 0..5 (Wide, Narrow/Squint, Deep-Set, Hooded, Tired Bags, Round Piercing)
    public int EyeSpacingDelta { get; init; }   // -1..+1
    public int EyeYDelta { get; init; }         // -1..+1
    public int BrowVariant { get; init; }       // 0..5 (Flat, Stern V, Arched, Heavy Bushy, Upturned Furrow, Scarred/Notched)
    public int NoseVariant { get; init; }       // 0..5 (Narrow, Broad Flat, Hooked, Blunt Snub, Crooked/Broken, Ridged)
    public int MouthVariant { get; init; }      // 0..5 (Neutral, Stern Frown, Smirk, Wide Stoic, Parted Full, Clenched Grimace)

    public int HairArchetype { get; init; }     // 0..9 (10 distinct outer hair silhouettes)
    public int FacialHairVariant { get; init; } // 0..5
    public int SpeciesSubVariantA { get; init; }// 0..4 (Thane beard cut, Krushvak ridge, Veldor visor, Orun polygon, etc.)
    public int SpeciesSubVariantB { get; init; }// 0..4 (Krushvak sideburns, Veldor cowl/stripes, Klaasari spot pattern, etc.)
    public int SpeciesSubVariantC { get; init; }// 0..3 (Veldor tubes, Prunjun goggle swap, Hun-Borian spike count, etc.)
    public int AccessoryVariant { get; init; }  // 0: None, 1: Comms Ear-Mic, 2: Cyber Monocle, 3: Cheek Scar, 4: Nose Bandage, 5: Ear Studs

    public static PortraitTraitProfile Roll(DeterministicRng rng)
    {
        DeterministicRng tRng = rng.Fork("portrait_traits_v2");
        return new PortraitTraitProfile
        {
            HeadRxDelta = tRng.NextInt(-2, 3),
            HeadRyDelta = tRng.NextInt(-2, 3),
            JawWidthDelta = tRng.NextInt(-2, 4),
            ChinLengthDelta = tRng.NextInt(-1, 3),
            CheekArchetype = tRng.NextInt(0, 4),
            EarStyle = tRng.NextInt(0, 4),
            NeckWidthDelta = tRng.NextInt(-1, 3),
            ShoulderDelta = tRng.NextInt(-3, 4),
            UniformArchetype = tRng.NextInt(0, 4),
            EyeVariant = tRng.NextInt(0, 6),
            EyeSpacingDelta = tRng.NextInt(-1, 2),
            EyeYDelta = tRng.NextInt(-1, 2),
            BrowVariant = tRng.NextInt(0, 6),
            NoseVariant = tRng.NextInt(0, 6),
            MouthVariant = tRng.NextInt(0, 6),
            HairArchetype = tRng.NextInt(0, 10),
            FacialHairVariant = tRng.NextInt(0, 6),
            SpeciesSubVariantA = tRng.NextInt(0, 5),
            SpeciesSubVariantB = tRng.NextInt(0, 5),
            SpeciesSubVariantC = tRng.NextInt(0, 4),
            AccessoryVariant = tRng.NextInt(0, 8) // 0..5 active, 6..7 none
        };
    }
}

/// <summary>
/// 64x64 procedural portrait engine implementing the 7-layer bottom-to-top pipeline
/// across all 13 active species and 4 render modes (live, static, holo, crudeCrayon)
/// with deep deterministic within-species structural variation.
/// </summary>
public static class PortraitRenderer
{
    private static readonly string[][] HairRamps =
    [
        ["#090a0f", "#141824", "#222938", "#333d52"], // Jet Black
        ["#1c110a", "#331f14", "#4d3020", "#69432e"], // Dark Espresso Brown
        ["#2e180d", "#522d1a", "#784429", "#9e5c3b"], // Warm Chestnut
        ["#421c14", "#6e2e20", "#9c4330", "#c45d47"], // Auburn Red
        ["#5e471e", "#8f6f32", "#bf984c", "#e3be74"], // Golden Blonde
        ["#475569", "#64748b", "#94a3b8", "#cbd5e1"]  // Silver Grey
    ];

    private static readonly string[] HoloCyan5Step =
    [
        "#06131c",
        "#0c3547",
        "#156d7a",
        "#2dd4bf",
        "#a7f3d0"
    ];

    private static readonly string[] HoloAmber5Step =
    [
        "#140c04",
        "#3d2006",
        "#7c430a",
        "#c97416",
        "#ffb84d"
    ];

    private static readonly string[] StaticSnowRamp =
    [
        "#080b10",
        "#1e293b",
        "#475569",
        "#94a3b8",
        "#e2e8f0"
    ];

    public static readonly IReadOnlyList<PortraitSpecies> AllActiveSpecies =
    [
        PortraitSpecies.Human,
        PortraitSpecies.Helani,
        PortraitSpecies.Klaasari,
        PortraitSpecies.Thane,
        PortraitSpecies.Veldor,
        PortraitSpecies.Brengop,
        PortraitSpecies.Kordani,
        PortraitSpecies.Krushvak,
        PortraitSpecies.Dopple,
        PortraitSpecies.HunBorian,
        PortraitSpecies.Prunjun,
        PortraitSpecies.Tolvak,
        PortraitSpecies.Orun
    ];

    /// <summary>
    /// Checks assets/images/scripted/ ONLY when options.IsScriptedOverride == true.
    /// Returns true if a valid static scripted texture was found and loaded.
    /// </summary>
    public static bool TryGetScriptedOverrideTexture(PortraitRenderOptions options, out Texture2D texture)
    {
        texture = default;
        if (!options.IsScriptedOverride || string.IsNullOrWhiteSpace(options.ScriptedAssetName))
            return false;

        string relPath = Path.Combine("scripted", options.ScriptedAssetName);
        return AssetManager.TryGetTexture(relPath, out texture);
    }

    public static PixelBuffer RenderPlaceholderBuffer(string seedId) =>
        RenderBuffer(new PortraitRenderOptions { SeedId = seedId });

    /// <summary>
    /// Renders a 64x64 PixelBuffer for the given PortraitRenderOptions.
    /// </summary>
    public static PixelBuffer RenderBuffer(PortraitRenderOptions options)
    {
        if (options.IsKordaniSecurityOfficer)
        {
            return RenderKordaniSecurityOfficerBuffer();
        }

        if (options.Mode == PortraitRenderMode.Static)
        {
            return RenderStaticMode(options);
        }

        bool isCrayon = options.CrudeCrayon || options.Mode == PortraitRenderMode.CrudeCrayon;
        DeterministicRng rng = new(options.SeedId);
        PortraitTraitProfile traits = PortraitTraitProfile.Roll(rng);

        // Resolve effective species (if Dopple is disguised, pick a deterministic target species)
        PortraitSpecies effectiveSpecies = options.Species;
        bool isTrueDopple = false;
        if (options.Species == PortraitSpecies.Dopple)
        {
            if (options.DoppleInTrueForm)
            {
                isTrueDopple = true;
            }
            else
            {
                PortraitSpecies[] disguisePool =
                [
                    PortraitSpecies.Human,
                    PortraitSpecies.Helani,
                    PortraitSpecies.Klaasari,
                    PortraitSpecies.Thane,
                    PortraitSpecies.Kordani,
                    PortraitSpecies.Krushvak,
                    PortraitSpecies.Tolvak
                ];
                effectiveSpecies = rng.Fork("dopple_disguise").Pick(disguisePool);
            }
        }

        // Character foreground buffer (transparent background so ApplyOutline outlines the character)
        PixelBuffer charBuf = new(Constants.PortraitWidth, Constants.PortraitHeight);

        (string[] skinRamp, string[] secondarySkinRamp) = SelectSpeciesSkinRamps(effectiveSpecies, isTrueDopple, rng);
        string[] uniformRamp = SelectUniformRamp(effectiveSpecies, options.FactionPrefix, traits);
        string[] hairRamp = rng.Fork("hair_col").Pick(HairRamps);

        DeterministicRng jitterRng = rng.Fork("crayon_jitter");
        int J(int v, int mag = 2) => isCrayon ? v + jitterRng.NextInt(-mag, mag + 1) : v;

        string SampleMaterial(int x, int y, string[] ramp, float t) =>
            isCrayon ? Dither.SampleFlatRamp(ramp, t) : Dither.SampleRamp(x, y, ramp, t);

        // =====================================================================
        // LAYER 1: Neck & Uniform/Collar (y = 44..63)
        // =====================================================================
        RenderLayer1CollarAndNeck(charBuf, effectiveSpecies, isTrueDopple, traits, skinRamp, uniformRamp, J, SampleMaterial);

        // =====================================================================
        // LAYER 2: Head Base / Skull (cx = 32, cy = 28)
        // =====================================================================
        PixelBuffer headOnlyBuf = new(Constants.PortraitWidth, Constants.PortraitHeight);
        RenderLayer2Skull(headOnlyBuf, effectiveSpecies, isTrueDopple, traits, skinRamp, rng, J, SampleMaterial);
        bool[] headMask = headOnlyBuf.CaptureNonNullMask(excludeOutline: true);
        charBuf.BlitFrom(headOnlyBuf);

        // =====================================================================
        // LAYER 3: Clipped Skin Markings (Klaasari spots, Tolvak molt patches, Orun facets/cracks, etc.)
        // =====================================================================
        RenderLayer3ClippedMarkings(
            charBuf,
            effectiveSpecies,
            isTrueDopple,
            options.IsMolting,
            traits,
            skinRamp,
            secondarySkinRamp,
            headMask,
            rng,
            J,
            SampleMaterial);

        // =====================================================================
        // LAYER 4: Facial Features (y = 24..42)
        // =====================================================================
        RenderLayer4FacialFeatures(
            charBuf,
            effectiveSpecies,
            isTrueDopple,
            isCrayon,
            traits,
            skinRamp,
            hairRamp,
            rng,
            J,
            SampleMaterial);

        // =====================================================================
        // LAYER 5: Hair / Facial Hair / Spikes
        // =====================================================================
        RenderLayer5HairBeardSpikes(
            charBuf,
            effectiveSpecies,
            isTrueDopple,
            traits,
            skinRamp,
            hairRamp,
            rng,
            J,
            SampleMaterial);

        // =====================================================================
        // LAYER 6: Full Headgear / Optics / Accessories
        // =====================================================================
        RenderLayer6HeadgearAndOptics(
            charBuf,
            effectiveSpecies,
            isTrueDopple,
            traits,
            skinRamp,
            rng,
            J,
            SampleMaterial);

        // =====================================================================
        // LAYER 7: Final Pass — 4-Neighbor 1px Dark Outline (#080b10)
        // =====================================================================
        charBuf.ApplyOutline(Palette.HexOutlineVoid);

        // =====================================================================
        // Compose Final Buffer by Render Mode (Live, Holo, CrudeCrayon)
        // =====================================================================
        if (options.Mode == PortraitRenderMode.Holo)
        {
            PixelBuffer holo = ComposeHoloMode(charBuf, options.HoloAmber);
            if (isCrayon)
            {
                ApplyCrudeCrayonOverlay(holo, jitterRng);
            }
            return holo;
        }

        PixelBuffer finalBuf = new(Constants.PortraitWidth, Constants.PortraitHeight);
        RenderBridgeBackdrop(finalBuf, effectiveSpecies, options.FactionPrefix, isCrayon, rng);
        finalBuf.BlitFrom(charBuf);

        if (isCrayon)
        {
            ApplyCrudeCrayonOverlay(finalBuf, jitterRng);
        }
        else
        {
            ApplyLiveCrtScanlines(finalBuf);
        }

        return finalBuf;
    }

    private static (string[] Primary, string[] Secondary) SelectSpeciesSkinRamps(
        PortraitSpecies species,
        bool isTrueDopple,
        DeterministicRng rng)
    {
        if (isTrueDopple)
        {
            return (Palette.DoppleTrueFormRamp, Palette.DoppleTrueFormRamp);
        }

        return species switch
        {
            PortraitSpecies.Human => (Palette.HumanSkinRamps[rng.NextInt(0, Palette.HumanSkinRamps.Length)], Palette.HumanSkinRamps[0]),
            PortraitSpecies.Helani => (Palette.HumanSkinRamps[rng.NextInt(0, Palette.HumanSkinRamps.Length)], Palette.HumanSkinRamps[0]),
            PortraitSpecies.Klaasari => rng.NextBool()
                ? (Palette.KlaasariOceanBlueRamp, Palette.KlaasariEmeraldGreenRamp)
                : (Palette.KlaasariEmeraldGreenRamp, Palette.KlaasariOceanBlueRamp),
            PortraitSpecies.Thane => (Palette.ThaneTitaniumWhiteRamp, Palette.ThaneBeardDeepBlackRamp),
            PortraitSpecies.Veldor => (Palette.VeldorHelmetPorcelainRamp, Palette.VeldorBrassTrimRamp),
            PortraitSpecies.Brengop => (Palette.BrengopSkinRamps[rng.NextInt(0, Palette.BrengopSkinRamps.Length)], Palette.BrengopSkinRamps[0]),
            PortraitSpecies.Kordani => SelectKordaniSkinRamp(rng),
            PortraitSpecies.Krushvak => (Palette.KrushvakSkinRamps[rng.NextInt(0, Palette.KrushvakSkinRamps.Length)], Palette.ThaneBeardDeepBlackRamp),
            PortraitSpecies.HunBorian => (Palette.HunBorianSkinRamps[rng.NextInt(0, Palette.HunBorianSkinRamps.Length)], Palette.HunBorianSkinRamps[0]),
            PortraitSpecies.Prunjun => (Palette.PrunjunSkinRamps[rng.NextInt(0, Palette.PrunjunSkinRamps.Length)], Palette.BrengopSecondaryGrimyBrassRamp),
            PortraitSpecies.Tolvak => (Palette.TolvakSkinRamp, Palette.TolvakSkinRamp),
            PortraitSpecies.Orun => (Palette.OrunSkinRamp, Palette.OrunSkinRamp),
            _ => (Palette.HumanSkinRamps[2], Palette.HumanSkinRamps[0])
        };
    }

    private static (string[] Primary, string[] Secondary) SelectKordaniSkinRamp(DeterministicRng rng)
    {
        float roll = rng.NextFloat();
        int idx = roll < 0.45f ? 0 : (roll < 0.80f ? 1 : 2);
        return (Palette.KordaniSkinRamps[idx], Palette.KordaniSecondaryDarkSlateRamp);
    }

    private static string[] SelectUniformRamp(PortraitSpecies species, string factionPrefix, PortraitTraitProfile traits)
    {
        string[][] guAlternates =
        [
            Palette.GuSecondaryGunmetalRamp,
            Palette.FarLineAccentNavyRamp,
            Palette.FreeTraderPrimarySlateRamp,
            Palette.GuPrimaryWhiteRamp
        ];

        return species switch
        {
            PortraitSpecies.Kordani => traits.UniformArchetype % 2 == 0
                ? Palette.KordaniPrimaryBlueGreenRamp
                : Palette.KordaniSecondaryDarkSlateRamp,
            PortraitSpecies.Brengop => traits.UniformArchetype % 2 == 0
                ? Palette.BrengopSecondaryGrimyBrassRamp
                : Palette.BrengopPrimaryGrossYellowRamp,
            PortraitSpecies.Krushvak => traits.UniformArchetype % 2 == 0
                ? Palette.KrushvakSecondaryEarthBrownRamp
                : Palette.KrushvakPrimaryOliveGreenRamp,
            PortraitSpecies.Orun => Palette.OrunPrimaryGraniteRamp,
            PortraitSpecies.Veldor => Palette.VeldorHelmetPorcelainRamp,
            PortraitSpecies.Prunjun => traits.UniformArchetype % 2 == 0
                ? Palette.FreeTraderTrimRustRamp
                : Palette.MiningGuildPrimaryCharcoalRamp,
            PortraitSpecies.HunBorian => traits.UniformArchetype % 2 == 0
                ? Palette.FreeTraderPrimarySlateRamp
                : Palette.FreeTraderTrimRustRamp,
            PortraitSpecies.Tolvak => Palette.TolvakPrimaryTerracottaRamp,
            _ => factionPrefix.ToUpperInvariant() switch
            {
                "KDF-" => Palette.KordaniPrimaryBlueGreenRamp,
                "BMC-" => Palette.BrengopSecondaryGrimyBrassRamp,
                "KRW-" => Palette.KrushvakPrimaryOliveGreenRamp,
                "FTV-" or "HBR-" => Palette.FreeTraderPrimarySlateRamp,
                "FTC-" => Palette.FarLineAccentNavyRamp,
                "MNG-" => Palette.MiningGuildPrimaryCharcoalRamp,
                "ZNI-" => Palette.ZenithPrimaryBurgundyRamp,
                _ => guAlternates[traits.UniformArchetype % guAlternates.Length]
            }
        };
    }

    // =========================================================================
    // LAYER 1: NECK & UNIFORM/COLLAR VARIETY (4 Distinct Clothing Silhouettes)
    // =========================================================================
    private static void RenderLayer1CollarAndNeck(
        PixelBuffer buf,
        PortraitSpecies species,
        bool isTrueDopple,
        PortraitTraitProfile traits,
        string[] skinRamp,
        string[] uniformRamp,
        Func<int, int, int> J,
        Func<int, int, string[], float, string> sampleFn)
    {
        bool isWideStocky = !isTrueDopple &&
            (species is PortraitSpecies.HunBorian or PortraitSpecies.Prunjun or PortraitSpecies.Krushvak or PortraitSpecies.Brengop);
        int baseLeftX = (isWideStocky ? 5 : 10) + Math.Clamp(traits.ShoulderDelta, -3, 2);
        int baseRightX = (isWideStocky ? 59 : 54) - Math.Clamp(traits.ShoulderDelta, -3, 2);
        int collarTopY = (species is PortraitSpecies.Kordani or PortraitSpecies.Orun ? 42 : 45) +
                         (traits.UniformArchetype == 0 ? -1 : 1);

        ReadOnlySpan<(int X, int Y)> shoulders =
        [
            (J(baseLeftX, 2), 63),
            (J(baseLeftX + 4, 2), J(collarTopY + 3, 1)),
            (J(21, 1), J(collarTopY, 1)),
            (J(43, 1), J(collarTopY, 1)),
            (J(baseRightX - 4, 2), J(collarTopY + 3, 1)),
            (J(baseRightX, 2), 63)
        ];

        buf.FillPoly(shoulders, (px, py) =>
        {
            float t = Math.Clamp(0.9f - (py - collarTopY) / 22.0f - MathF.Abs(px - 32) / 36.0f, 0.1f, 0.95f);
            return sampleFn(px, py, uniformRamp, t);
        });

        // 4 Distinct Seeded Clothing / Collar Archetypes
        if (species == PortraitSpecies.Orun && !isTrueDopple)
        {
            // Orun geometric robes with #d4af37 brass trim
            buf.DrawLine(21, collarTopY, 27, 62, Palette.HexOrunBrassTrim);
            buf.DrawLine(43, collarTopY, 37, 62, Palette.HexOrunBrassTrim);
            buf.DrawLine(22, collarTopY + 1, 42, collarTopY + 1, Palette.HexOrunBrassTrim);
            if (traits.UniformArchetype % 2 == 1)
            {
                buf.FillRect(29, 51, 6, 8, Palette.HexOrunBrassTrim);
                buf.FillRect(30, 52, 4, 6, uniformRamp[0]);
            }
        }
        else if (species == PortraitSpecies.Veldor && !isTrueDopple)
        {
            // Brass environmental neck ring seal
            buf.FillRect(19, 43, 26, 4, (px, py) =>
                sampleFn(px, py, Palette.VeldorBrassTrimRamp, 1.0f - MathF.Abs(px - 32) / 14.0f));
            buf.FillRect(27, 49, 10, 8, Palette.VeldorBrassTrimRamp[1]);
        }
        else
        {
            switch (traits.UniformArchetype)
            {
                case 0:
                    // Archetype 0: High-Collared Military Tunic with diagonal breast flap & rank pips
                    buf.FillRect(20, collarTopY - 2, 24, 4, uniformRamp[2]);
                    buf.DrawLine(22, collarTopY, 35, 62, uniformRamp[3]);
                    buf.Set(23, collarTopY + 3, Palette.HexCrtAmberPrimary);
                    buf.Set(25, collarTopY + 3, Palette.HexCrtAmberPrimary);
                    break;

                case 1:
                    // Archetype 1: Open V-Neck Flight Jacket over dark undershirt
                    ReadOnlySpan<(int X, int Y)> undershirt = [(24, collarTopY), (40, collarTopY), (34, 60), (30, 60)];
                    buf.FillPoly(undershirt, (_, _) => Palette.HexOutlineVoid);
                    buf.DrawLine(23, collarTopY, 29, 61, uniformRamp[3]);
                    buf.DrawLine(41, collarTopY, 35, 61, uniformRamp[3]);
                    break;

                case 2:
                    // Archetype 2: Layered Cargo Vest & Utility Harness Straps
                    buf.FillRect(17, collarTopY + 2, 5, 18, uniformRamp[0]);
                    buf.FillRect(42, collarTopY + 2, 5, 18, uniformRamp[0]);
                    buf.FillRect(18, 52, 3, 3, Palette.HexCrtAmberDimmed);
                    buf.FillRect(43, 52, 3, 3, Palette.HexCrtAmberDimmed);
                    break;

                default:
                    // Archetype 3: Heavy Armored Pauldrons & Gorget
                    buf.FillEllipse(baseLeftX + 5, collarTopY + 7, 6, 5, (px, py, _, _, _) => sampleFn(px, py, uniformRamp, 0.88f));
                    buf.FillEllipse(baseRightX - 5, collarTopY + 7, 6, 5, (px, py, _, _, _) => sampleFn(px, py, uniformRamp, 0.72f));
                    buf.DrawLine(20, collarTopY + 2, 44, collarTopY + 2, uniformRamp[3]);
                    break;
            }
        }

        buf.ApplyOutline(Palette.HexOutlineVoid);

        if (species == PortraitSpecies.Veldor && !isTrueDopple)
            return;

        int baseNeckW = species == PortraitSpecies.HunBorian ? 10 : (species == PortraitSpecies.Prunjun ? 8 : 5);
        int neckHalfW = Math.Clamp(baseNeckW + traits.NeckWidthDelta, 4, 12);
        ReadOnlySpan<(int X, int Y)> neckPts =
        [
            (32 - neckHalfW, 35),
            (32 + neckHalfW, 35),
            (32 + neckHalfW + 1, 47),
            (32 - neckHalfW - 1, 47)
        ];
        buf.FillPoly(neckPts, (px, py) =>
        {
            float t = Math.Clamp(0.65f - MathF.Abs(px - 32) / (neckHalfW + 4.0f), 0.12f, 0.78f);
            return sampleFn(px, py, skinRamp, t);
        });
    }

    // =========================================================================
    // LAYER 2: SKULL & MACRO HEAD PROPORTIONS (Varying Width, Height, Jaw, Ears)
    // =========================================================================
    private static void RenderLayer2Skull(
        PixelBuffer headBuf,
        PortraitSpecies species,
        bool isTrueDopple,
        PortraitTraitProfile traits,
        string[] skinRamp,
        DeterministicRng rng,
        Func<int, int, int> J,
        Func<int, int, string[], float, string> sampleFn)
    {
        float RadialLight(float dx, float dy, float dist) =>
            Math.Clamp(0.92f - dist * 0.52f - dx * 0.16f - dy * 0.20f, 0.08f, 0.98f);

        if (isTrueDopple)
        {
            int rx = 12 + traits.HeadRxDelta;
            int ry = 15 + traits.HeadRyDelta;
            headBuf.FillEllipse(J(32, 1), J(28, 1), rx, ry, (px, py, dx, dy, dist) =>
                sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist)));
            headBuf.FillEllipse(J(24 - traits.JawWidthDelta, 1), J(37, 1), 5, 6, (px, py, dx, dy, dist) =>
                sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist) * 0.85f));
            headBuf.FillEllipse(J(39 + traits.JawWidthDelta, 1), J(38, 1), 6, 6, (px, py, dx, dy, dist) =>
                sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist) * 0.82f));
            return;
        }

        switch (species)
        {
            case PortraitSpecies.Human:
            case PortraitSpecies.Klaasari:
            case PortraitSpecies.Thane:
            {
                int rx = Math.Clamp(12 + traits.HeadRxDelta, 10, 14);
                int ry = Math.Clamp(15 + traits.HeadRyDelta, 13, 17);
                int earRx = traits.EarStyle == 2 ? 3 : 2;
                int earRy = traits.EarStyle == 0 ? 2 : (traits.EarStyle == 3 ? 4 : 3);
                int earY = 28 + (traits.EarStyle == 3 ? 2 : 0);

                // Side ears
                headBuf.FillEllipse(32 - rx - 1, earY, earRx, earRy, (px, py, _, _, _) => sampleFn(px, py, skinRamp, 0.42f));
                headBuf.FillEllipse(32 + rx + 1, earY, earRx, earRy, (px, py, _, _, _) => sampleFn(px, py, skinRamp, 0.35f));

                // Upper cranium ellipse
                headBuf.FillEllipse(J(32, 1), J(27, 1), rx, ry, (px, py, dx, dy, dist) =>
                    sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist)));

                // Varied jaw/cheekbone contour polygon so different seeds have distinct face shapes
                int jawHalfW = Math.Clamp(rx - 2 + traits.JawWidthDelta, 6, 14);
                int chinY = Math.Clamp(42 + traits.ChinLengthDelta, 40, 45);
                int cheekHalfW = traits.CheekArchetype == 1 ? rx + 1 : rx;

                ReadOnlySpan<(int X, int Y)> jawPoly =
                [
                    (32 - cheekHalfW, 28),
                    (32 + cheekHalfW, 28),
                    (32 + jawHalfW, 37),
                    (35, chinY),
                    (29, chinY),
                    (32 - jawHalfW, 37)
                ];
                headBuf.FillPoly(jawPoly, (px, py) =>
                {
                    float dx = (px - 32) / (float)(rx + 2);
                    float dy = (py - 28) / (float)(ry + 2);
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    return sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist));
                });
                break;
            }

            case PortraitSpecies.Helani:
            {
                // Narrower upper head + mandatory elongated protruding chin polygon (y = 40..47)
                int rx = Math.Clamp(11 + traits.HeadRxDelta / 2, 10, 12);
                int ry = Math.Clamp(13 + traits.HeadRyDelta / 2, 12, 14);
                int earRx = traits.EarStyle == 2 ? 3 : 2;

                headBuf.FillEllipse(32 - rx, 28, earRx, 3, (px, py, _, _, _) => sampleFn(px, py, skinRamp, 0.42f));
                headBuf.FillEllipse(32 + rx, 28, earRx, 3, (px, py, _, _, _) => sampleFn(px, py, skinRamp, 0.35f));
                headBuf.FillEllipse(J(32, 1), J(26, 1), rx, ry, (px, py, dx, dy, dist) =>
                    sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist)));

                int chinTipHalfW = Math.Clamp(3 + traits.JawWidthDelta / 2, 2, 5);
                int chinBottomY = Math.Clamp(46 + (traits.ChinLengthDelta > 0 ? 1 : 0), 45, 47);
                ReadOnlySpan<(int X, int Y)> protrudingChin =
                [
                    (J(32 - rx + 1, 1), 32),
                    (J(32 + rx - 1, 1), 32),
                    (J(32 + chinTipHalfW + 2, 1), 40),
                    (J(32 + chinTipHalfW, 1), chinBottomY),
                    (J(32 - chinTipHalfW, 1), chinBottomY),
                    (J(32 - chinTipHalfW - 2, 1), 40)
                ];
                headBuf.FillPoly(protrudingChin, (px, py) =>
                {
                    float t = Math.Clamp(0.86f - MathF.Abs(px - 32) / 14.0f - (py - 32) * 0.022f, 0.20f, 0.95f);
                    return sampleFn(px, py, skinRamp, t);
                });
                break;
            }

            case PortraitSpecies.Veldor:
            {
                // 4 Distinct Sealed Cowl Silhouettes (Classic Dome, Tall Ribbed Bell, Wide Heavy-Pressure, Angular Hood)
                int cowlStyle = traits.SpeciesSubVariantB % 4;
                if (cowlStyle == 0)
                {
                    headBuf.FillEllipse(32, 28, 15, 17, (px, py, dx, dy, dist) =>
                        sampleFn(px, py, Palette.VeldorHelmetPorcelainRamp, RadialLight(dx, dy, dist)));
                }
                else if (cowlStyle == 1)
                {
                    // Tall Ribbed Atmospheric Bell with top brass crest
                    headBuf.FillEllipse(32, 27, 13, 18, (px, py, dx, dy, dist) =>
                        sampleFn(px, py, Palette.VeldorHelmetPorcelainRamp, RadialLight(dx, dy, dist)));
                    headBuf.FillRect(30, 8, 4, 5, Palette.VeldorBrassTrimRamp[2]);
                }
                else if (cowlStyle == 2)
                {
                    // Wide Heavy-Pressure Helmet with protruding side acoustic pods
                    headBuf.FillEllipse(32, 28, 16, 15, (px, py, dx, dy, dist) =>
                        sampleFn(px, py, Palette.VeldorHelmetPorcelainRamp, RadialLight(dx, dy, dist)));
                    headBuf.FillRect(13, 22, 4, 8, Palette.VeldorBrassTrimRamp[1]);
                    headBuf.FillRect(47, 22, 4, 8, Palette.VeldorBrassTrimRamp[1]);
                }
                else
                {
                    // Tapered Angular Enviro-Hood
                    ReadOnlySpan<(int X, int Y)> angularCowl =
                    [
                        (25, 11), (39, 11), (47, 20), (46, 39), (38, 44), (26, 44), (18, 39), (17, 20)
                    ];
                    headBuf.FillPoly(angularCowl, (px, py) =>
                    {
                        float dx = (px - 32) / 16.0f;
                        float dy = (py - 28) / 17.0f;
                        return sampleFn(px, py, Palette.VeldorHelmetPorcelainRamp, RadialLight(dx, dy, MathF.Sqrt(dx * dx + dy * dy)));
                    });
                }
                break;
            }

            case PortraitSpecies.Brengop:
            {
                // 4 Distinct Drooping Pointed Troll Ear Styles
                int earVar = traits.EarStyle;
                int earTipL = earVar == 0 ? 8 : (earVar == 1 ? 10 : 12);
                int earTipR = 64 - earTipL;
                int earDropY = earVar == 0 ? 38 : (earVar == 2 ? 31 : 35);

                ReadOnlySpan<(int X, int Y)> leftTrollEar = [(19, 25), (earTipL, earDropY - 5), (earTipL + 2, earDropY), (20, 33)];
                ReadOnlySpan<(int X, int Y)> rightTrollEar = [(45, 25), (earTipR, earDropY - 5), (earTipR - 2, earDropY), (44, 33)];
                headBuf.FillPoly(leftTrollEar, (px, py) => sampleFn(px, py, skinRamp, 0.45f));
                headBuf.FillPoly(rightTrollEar, (px, py) => sampleFn(px, py, skinRamp, 0.35f));

                int rx = Math.Clamp(14 + traits.HeadRxDelta, 13, 16);
                int ry = Math.Clamp(13 + traits.HeadRyDelta, 11, 15);
                int jowlR = Math.Clamp(6 + traits.JawWidthDelta / 2, 5, 8);

                headBuf.FillEllipse(32, 28, rx, ry, (px, py, dx, dy, dist) =>
                    sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist)));
                headBuf.FillEllipse(23 - traits.JawWidthDelta / 2, 35, jowlR, 6, (px, py, dx, dy, dist) =>
                    sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist) * 0.85f));
                headBuf.FillEllipse(41 + traits.JawWidthDelta / 2, 35, jowlR, 6, (px, py, dx, dy, dist) =>
                    sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist) * 0.80f));
                break;
            }

            case PortraitSpecies.Kordani:
            {
                // Vary angular cheekbones, jaw taper, and 4 lizard crown/frill silhouettes
                int cheekW = Math.Clamp(13 + traits.HeadRxDelta, 11, 15);
                int jawW = Math.Clamp(5 + traits.JawWidthDelta, 3, 8);
                int crownY = Math.Clamp(12 - traits.HeadRyDelta, 10, 14);

                ReadOnlySpan<(int X, int Y)> kordaniSkull =
                [
                    (32, crownY),
                    (32 + cheekW - 3, crownY + 4),
                    (32 + cheekW, 26),
                    (32 + jawW + 4, 35),
                    (32 + jawW, 42),
                    (32, 44),
                    (32 - jawW, 42),
                    (32 - jawW - 4, 35),
                    (32 - cheekW, 26),
                    (32 - cheekW + 3, crownY + 4)
                ];
                headBuf.FillPoly(kordaniSkull, (px, py) =>
                {
                    float dx = (px - 32) / (float)cheekW;
                    float dy = (py - 27) / 16.0f;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    return sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist));
                });

                // Optional Kordani temple horn stubs or lateral cheek frill fins
                int crestStyle = traits.SpeciesSubVariantA % 4;
                if (crestStyle == 1)
                {
                    // Swept temple horn nubs
                    ReadOnlySpan<(int X, int Y)> lHorn = [(22, 16), (17, 10), (26, 14)];
                    ReadOnlySpan<(int X, int Y)> rHorn = [(42, 16), (47, 10), (38, 14)];
                    headBuf.FillPoly(lHorn, (px, py) => sampleFn(px, py, skinRamp, 0.88f));
                    headBuf.FillPoly(rHorn, (px, py) => sampleFn(px, py, skinRamp, 0.72f));
                }
                else if (crestStyle == 2)
                {
                    // Flared lizard cheek frill stubs
                    ReadOnlySpan<(int X, int Y)> lFrill = [(32 - cheekW, 25), (13, 28), (15, 34), (32 - jawW - 3, 34)];
                    ReadOnlySpan<(int X, int Y)> rFrill = [(32 + cheekW, 25), (51, 28), (49, 34), (32 + jawW + 3, 34)];
                    headBuf.FillPoly(lFrill, (px, py) => sampleFn(px, py, skinRamp, 0.55f));
                    headBuf.FillPoly(rFrill, (px, py) => sampleFn(px, py, skinRamp, 0.45f));
                }
                else if (crestStyle == 3)
                {
                    // Central sagittal scale fin
                    ReadOnlySpan<(int X, int Y)> fin = [(30, 14), (32, 8), (34, 14)];
                    headBuf.FillPoly(fin, (px, py) => sampleFn(px, py, skinRamp, 0.92f));
                }
                break;
            }

            case PortraitSpecies.Krushvak:
            {
                int rx = Math.Clamp(13 + traits.HeadRxDelta, 12, 15);
                int ry = Math.Clamp(15 + traits.HeadRyDelta, 13, 16);
                headBuf.FillEllipse(32 - rx, 28, 2, 3, (px, py, _, _, _) => sampleFn(px, py, skinRamp, 0.35f));
                headBuf.FillEllipse(32 + rx, 28, 2, 3, (px, py, _, _, _) => sampleFn(px, py, skinRamp, 0.30f));
                headBuf.FillEllipse(32, 28, rx, ry, (px, py, dx, dy, dist) =>
                    sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist)));
                break;
            }

            case PortraitSpecies.HunBorian:
            {
                int templeHalfW = Math.Clamp(10 + traits.HeadRxDelta, 8, 12);
                int jawHalfW = Math.Clamp(14 + traits.JawWidthDelta, 13, 17);
                int chinY = Math.Clamp(42 + traits.ChinLengthDelta, 41, 44);

                ReadOnlySpan<(int X, int Y)> hunSkull =
                [
                    (32 - templeHalfW + 2, 14),
                    (32 + templeHalfW - 2, 14),
                    (32 + templeHalfW, 24),
                    (32 + jawHalfW, 33),
                    (32 + jawHalfW - 3, chinY),
                    (32 - jawHalfW + 3, chinY),
                    (32 - jawHalfW, 33),
                    (32 - templeHalfW, 24)
                ];
                headBuf.FillPoly(hunSkull, (px, py) =>
                {
                    float dx = (px - 32) / 15.0f;
                    float dy = (py - 28) / 15.0f;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    return sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist));
                });
                break;
            }

            case PortraitSpecies.Prunjun:
            {
                // Bottom-heavy pear-shaped head with seed-varied jowl asymmetry
                int upperRx = Math.Clamp(10 + traits.HeadRxDelta / 2, 9, 11);
                int lowerRx = Math.Clamp(15 + traits.JawWidthDelta, 13, 17);
                int lopsided = traits.CheekArchetype % 2 == 0 ? 1 : -1;

                headBuf.FillEllipse(32, 23, upperRx, 10, (px, py, dx, dy, dist) =>
                    sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist)));
                headBuf.FillEllipse(32 + lopsided, 34, lowerRx, 10, (px, py, dx, dy, dist) =>
                    sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist) * 0.92f));
                break;
            }

            case PortraitSpecies.Tolvak:
            {
                int templeW = Math.Clamp(9 + traits.HeadRxDelta, 8, 11);
                int cheekW = Math.Clamp(10 + traits.JawWidthDelta / 2, 9, 13);
                int chinY = Math.Clamp(43 + traits.ChinLengthDelta, 41, 45);

                ReadOnlySpan<(int X, int Y)> tolvakSkull =
                [
                    (32, 12),
                    (32 + templeW, 17),
                    (32 + cheekW, 27),
                    (32 + templeW - 2, 37),
                    (32, chinY),
                    (32 - templeW + 2, 37),
                    (32 - cheekW, 27),
                    (32 - templeW, 17)
                ];
                headBuf.FillPoly(tolvakSkull, (px, py) =>
                {
                    float dx = (px - 32) / (float)cheekW;
                    float dy = (py - 28) / 15.0f;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    return sampleFn(px, py, skinRamp, RadialLight(dx, dy, dist));
                });
                break;
            }

            case PortraitSpecies.Orun:
            {
                // Strictly angular faceted polygonal granite skull (ZERO soft ellipses!)
                // 4 Distinct Polygonal Lithoid Silhouettes:
                // 0: Tall Rectangular Monolith, 1: Broad Hexagonal Bastion, 2: Chiseled Trapezoidal Obelisk, 3: Octagonal Stepped Geode
                int lithoidType = traits.SpeciesSubVariantA % 4;
                ReadOnlySpan<(int X, int Y)> outerGraniteSkull = lithoidType switch
                {
                    0 => // Tall Rectangular Monolith
                    [
                        (22, 10), (42, 10), (43, 28), (41, 43), (23, 43), (21, 28)
                    ],
                    1 => // Broad Hexagonal Bastion
                    [
                        (24, 13), (40, 13), (48, 25), (44, 39), (37, 43), (27, 43), (20, 39), (16, 25)
                    ],
                    2 => // Chiseled Trapezoidal Obelisk (narrow top, wide heavy jaw)
                    [
                        (26, 11), (38, 11), (42, 22), (47, 36), (41, 44), (23, 44), (17, 36), (22, 22)
                    ],
                    _ => // Octagonal Stepped Geode
                    [
                        (28, 10), (36, 10), (43, 15), (45, 28), (42, 38), (35, 44), (29, 44), (22, 38), (19, 28), (21, 15)
                    ]
                };

                headBuf.FillPoly(outerGraniteSkull, (px, py) =>
                {
                    float planeT = px < 26 ? 0.85f : (px > 38 ? 0.25f : (py < 21 ? 0.92f : (py > 36 ? 0.35f : 0.62f)));
                    return sampleFn(px, py, skinRamp, planeT);
                });

                ReadOnlySpan<(int X, int Y)> crownFacet = [(26, 11), (38, 11), (36, 20), (28, 20)];
                headBuf.FillPoly(crownFacet, (px, py) => sampleFn(px, py, skinRamp, 0.92f));

                ReadOnlySpan<(int X, int Y)> leftCheekFacet = [(19, 28), (26, 26), (27, 37), (21, 38)];
                headBuf.FillPoly(leftCheekFacet, (px, py) => sampleFn(px, py, skinRamp, 0.78f));

                ReadOnlySpan<(int X, int Y)> rightCheekFacet = [(38, 26), (45, 28), (43, 38), (37, 37)];
                headBuf.FillPoly(rightCheekFacet, (px, py) => sampleFn(px, py, skinRamp, 0.22f));
                break;
            }
        }
    }

    // =========================================================================
    // LAYER 3: CLIPPED SKIN MARKINGS (Klaasari Spot Archetypes, Tolvak Molt, Orun Inlays)
    // =========================================================================
    private static void RenderLayer3ClippedMarkings(
        PixelBuffer buf,
        PortraitSpecies species,
        bool isTrueDopple,
        bool isMolting,
        PortraitTraitProfile traits,
        string[] skinRamp,
        string[] secondaryRamp,
        bool[] headMask,
        DeterministicRng rng,
        Func<int, int, int> J,
        Func<int, int, string[], float, string> sampleFn)
    {
        if (isTrueDopple)
            return;

        bool MaskCheck(int px, int py) => buf.IsMasked(headMask, px, py);

        switch (species)
        {
            case PortraitSpecies.Klaasari:
            {
                // 4 Distinct Symmetrical Ornamental Spot-Cluster Archetypes
                int pattern = traits.SpeciesSubVariantB % 4;
                (int Dx, int Y, int Rx, int Ry)[] spotClusters = pattern switch
                {
                    0 => // Archetype 0: Royal Forehead Crown Rosette & Cheekbone Arcs
                    [
                        (0, 16, 2, 2), (4, 17, 2, 1), (8, 20, 2, 2),
                        (9, 31, 2, 2), (6, 34, 2, 1), (9, 37, 1, 2)
                    ],
                    1 => // Archetype 1: Diagonal Temple-to-Jaw Spot Bands
                    [
                        (9, 17, 2, 2), (7, 21, 2, 1), (10, 26, 2, 2),
                        (8, 31, 2, 2), (5, 36, 2, 2), (3, 40, 1, 1)
                    ],
                    2 => // Archetype 2: Brow-Bridge Diamond & Lower Jaw Clusters
                    [
                        (0, 19, 2, 2), (3, 16, 1, 2), (3, 22, 1, 2),
                        (8, 34, 3, 2), (6, 38, 2, 2)
                    ],
                    _ => // Archetype 3: Dense Full-Face Constellation Scatter
                    [
                        (0, 15, 1, 2), (5, 16, 2, 2), (9, 19, 2, 1),
                        (10, 28, 1, 2), (7, 31, 2, 2), (4, 34, 1, 1),
                        (8, 37, 2, 2), (3, 39, 1, 2)
                    ]
                };

                foreach (var s in spotClusters)
                {
                    buf.FillClippedEllipse(32 - s.Dx, s.Y, s.Rx, s.Ry, MaskCheck,
                        (px, py, _, _, dist) => sampleFn(px, py, secondaryRamp, 0.88f - dist * 0.35f));

                    if (s.Dx > 0)
                    {
                        buf.FillClippedEllipse(32 + s.Dx, s.Y, s.Rx, s.Ry, MaskCheck,
                            (px, py, _, _, dist) => sampleFn(px, py, secondaryRamp, 0.80f - dist * 0.35f));
                    }
                }
                break;
            }

            case PortraitSpecies.Kordani:
            {
                // Varied scale-ridge chevron patterns along cheeks, brow, and neck
                int scalePat = traits.SpeciesSubVariantB % 3;
                for (int i = 0; i <= scalePat + 1; i++)
                {
                    int sy = 28 + i * 3;
                    buf.DrawLine(21, sy, 25, sy + 2, skinRamp[0]);
                    buf.DrawLine(21, sy + 1, 25, sy + 3, skinRamp[3]);
                    buf.DrawLine(43, sy, 39, sy + 2, skinRamp[0]);
                    buf.DrawLine(43, sy + 1, 39, sy + 3, skinRamp[3]);
                }
                break;
            }

            case PortraitSpecies.Tolvak:
            {
                buf.DrawLine(24, 30, 27, 32, skinRamp[0]);
                buf.DrawLine(40, 30, 37, 32, skinRamp[0]);

                if (isMolting)
                {
                    DeterministicRng moltRng = rng.Fork("tolvak_molt_patches");
                    int patchCount = moltRng.NextInt(4, 7);
                    for (int i = 0; i < patchCount; i++)
                    {
                        int cx = moltRng.NextInt(23, 42);
                        int cy = moltRng.NextInt(17, 41);
                        int rx = moltRng.NextInt(2, 5);
                        int ry = moltRng.NextInt(1, 3);
                        buf.FillClippedEllipse(cx, cy, rx, ry, MaskCheck,
                            (px, py, _, _, dist) => dist > 0.72f ? skinRamp[3] : Palette.HexTolvakMoltPatch);
                    }
                }
                break;
            }

            case PortraitSpecies.Brengop:
            {
                // Seeded subterranean warts / skin bumps on forehead and jowls
                DeterministicRng wartRng = rng.Fork("brengop_warts");
                int wartCount = 2 + (traits.SpeciesSubVariantB % 3);
                for (int i = 0; i < wartCount; i++)
                {
                    int wx = wartRng.NextInt(21, 43);
                    int wy = wartRng.NextInt(18, 39);
                    if (MaskCheck(wx, wy))
                    {
                        buf.Set(wx, wy, skinRamp[3]);
                        buf.Set(wx, wy + 1, skinRamp[0]);
                    }
                }
                break;
            }

            case PortraitSpecies.Prunjun:
            {
                // Varied grease smudge placements on cheeks/chin
                int smudgeMode = traits.SpeciesSubVariantB % 3;
                if (smudgeMode is 0 or 2)
                {
                    buf.FillClippedEllipse(22, 34, 4, 2, MaskCheck,
                        (px, py, _, _, _) => Dither.BayerDither(px, py, "#181a1c", skinRamp[0], 0.5f));
                }
                if (smudgeMode is 1 or 2)
                {
                    buf.FillClippedEllipse(41, 32, 4, 3, MaskCheck,
                        (px, py, _, _, _) => Dither.BayerDither(px, py, "#2e2414", skinRamp[0], 0.5f));
                }
                break;
            }

            case PortraitSpecies.Orun:
            {
                // Crisp chiseled stone facet seams + seeded fracture lines & brass/mineral inlay seams
                buf.DrawLine(26, 12, 28, 20, skinRamp[3]);
                buf.DrawLine(38, 12, 36, 20, skinRamp[0]);
                buf.DrawLine(20, 28, 26, 27, skinRamp[3]);
                buf.DrawLine(38, 27, 44, 28, skinRamp[0]);

                int inlayPattern = traits.SpeciesSubVariantB % 4;
                string inlayCol = (inlayPattern % 2 == 0) ? Palette.HexOrunBrassTrim : "#38bdf8";
                if (inlayPattern == 0)
                {
                    // Vertical forehead monolith inlay + left cheek fracture
                    buf.DrawLine(32, 11, 32, 22, inlayCol);
                    buf.DrawLine(23, 30, 27, 36, Palette.HexOutlineVoid);
                }
                else if (inlayPattern == 1)
                {
                    // Symmetrical angled temple mineral seams
                    buf.DrawLine(22, 16, 28, 22, inlayCol);
                    buf.DrawLine(42, 16, 36, 22, inlayCol);
                }
                else if (inlayPattern == 2)
                {
                    // Deep jagged granite battle-fracture crack across right brow/cheek
                    buf.DrawLine(36, 14, 39, 21, Palette.HexOutlineVoid);
                    buf.DrawLine(39, 21, 37, 32, Palette.HexOutlineVoid);
                    buf.DrawLine(25, 34, 25, 42, inlayCol);
                }
                else
                {
                    // Horizontal geometric cheekbone circuitry inlay
                    buf.DrawLine(20, 31, 27, 31, inlayCol);
                    buf.DrawLine(37, 31, 44, 31, inlayCol);
                }
                break;
            }
        }
    }

    // =========================================================================
    // LAYER 4: FACIAL FEATURES (6 Eye, 6 Brow, 6 Nose, 6 Mouth Variants)
    // =========================================================================
    private static void RenderLayer4FacialFeatures(
        PixelBuffer buf,
        PortraitSpecies species,
        bool isTrueDopple,
        bool isCrayon,
        PortraitTraitProfile traits,
        string[] skinRamp,
        string[] hairRamp,
        DeterministicRng rng,
        Func<int, int, int> J,
        Func<int, int, string[], float, string> sampleFn)
    {
        if (species == PortraitSpecies.Veldor && !isTrueDopple)
            return;

        if (isTrueDopple)
        {
            buf.FillEllipse(27, 26, 3, 2, (px, py, _, _, dist) =>
                Dither.BayerDither(px, py, skinRamp[0], skinRamp[1], dist));
            buf.FillEllipse(37, 26, 3, 2, (px, py, _, _, dist) =>
                Dither.BayerDither(px, py, skinRamp[0], skinRamp[1], dist));
            buf.FillEllipse(32, 38, 4, 2, (px, py, _, _, dist) =>
                Dither.BayerDither(px, py, skinRamp[0], skinRamp[1], dist));
            return;
        }

        int eyeSpace = traits.EyeSpacingDelta;
        int leftEyeX = 25 - eyeSpace;
        int rightEyeX = 35 + eyeSpace;
        int baseEyeY = J(26 + traits.EyeYDelta, 1);
        int rightEyeY = isCrayon ? baseEyeY + (rng.NextBool() ? 2 : -2) : baseEyeY;

        // ---------------------------------------------------------------------
        // 1. Forehead Ridge / Eyebrow Pool (6 Variants)
        // ---------------------------------------------------------------------
        if (species == PortraitSpecies.Krushvak)
        {
            // Mandatory Krushvak forehead ridge (4 distinct vertical/V/triple-crest geometries — never horizontal waves)
            int ridgeType = traits.SpeciesSubVariantA % 4;
            switch (ridgeType)
            {
                case 0:
                    // Twin vertical armored pillars
                    buf.FillRect(29, 15, 2, 9, skinRamp[3]);
                    buf.DrawLine(31, 15, 31, 24, Palette.HexOutlineVoid);
                    buf.DrawLine(32, 15, 32, 24, skinRamp[0]);
                    buf.FillRect(33, 15, 2, 9, skinRamp[3]);
                    break;
                case 1:
                    // Wide flaring V-chevron brow crest
                    buf.DrawLine(25, 15, 32, 24, skinRamp[3]);
                    buf.DrawLine(26, 15, 32, 23, Palette.HexOutlineVoid);
                    buf.DrawLine(39, 15, 32, 24, skinRamp[3]);
                    buf.DrawLine(38, 15, 32, 23, Palette.HexOutlineVoid);
                    break;
                case 2:
                    // Triple-notch vertical warrior crown ridge
                    for (int rx = 28; rx <= 36; rx += 4)
                    {
                        buf.DrawLine(rx, 15, rx, 23, skinRamp[3]);
                        buf.DrawLine(rx + 1, 15, rx + 1, 23, Palette.HexOutlineVoid);
                    }
                    break;
                default:
                    // Faceted diamond shield crest on center forehead
                    ReadOnlySpan<(int X, int Y)> diamond = [(32, 14), (36, 19), (32, 24), (28, 19)];
                    buf.FillPoly(diamond, (px, py) => px <= 32 ? skinRamp[3] : skinRamp[0]);
                    buf.DrawLine(32, 15, 32, 23, Palette.HexOutlineVoid);
                    break;
            }
        }
        else if (species == PortraitSpecies.Brengop)
        {
            // Heavy low brow ridge overhanging sunken beady eyes (varied depth/slope)
            int browDrop = traits.BrowVariant % 2;
            ReadOnlySpan<(int X, int Y)> heavyBrow =
            [
                (20, 22), (44, 22), (45, 25 + browDrop), (19, 25 + browDrop)
            ];
            buf.FillPoly(heavyBrow, (px, py) => sampleFn(px, py, skinRamp, 0.78f));
            buf.DrawLine(20, 25 + browDrop, 44, 25 + browDrop, Palette.HexOutlineVoid);
        }
        else if (species == PortraitSpecies.Helani)
        {
            // Mandatory sharp, composed angled eyebrows (varied arch height and thickness)
            int arch = (traits.BrowVariant % 3) - 1;
            buf.DrawLine(leftEyeX - 1, baseEyeY - 2 + arch, leftEyeX + 4, baseEyeY - 3, hairRamp[0]);
            buf.DrawLine(rightEyeX, baseEyeY - 3, rightEyeX + 5, baseEyeY - 2 + arch, hairRamp[0]);
        }
        else
        {
            string browCol = species is PortraitSpecies.Thane or PortraitSpecies.Orun or PortraitSpecies.Kordani or PortraitSpecies.Tolvak
                ? skinRamp[0]
                : hairRamp[0];
            int by = baseEyeY - 3;

            switch (traits.BrowVariant)
            {
                case 0: // Flat composed
                    buf.DrawLine(leftEyeX, by, leftEyeX + 4, by, browCol);
                    buf.DrawLine(rightEyeX, by, rightEyeX + 4, by, browCol);
                    break;
                case 1: // Stern V-angled downward
                    buf.DrawLine(leftEyeX - 1, by - 1, leftEyeX + 4, by + 1, browCol);
                    buf.DrawLine(rightEyeX, by + 1, rightEyeX + 5, by - 1, browCol);
                    break;
                case 2: // High arched
                    buf.DrawLine(leftEyeX - 1, by, leftEyeX + 2, by - 1, browCol);
                    buf.DrawLine(leftEyeX + 2, by - 1, leftEyeX + 4, by, browCol);
                    buf.DrawLine(rightEyeX, by, rightEyeX + 2, by - 1, browCol);
                    buf.DrawLine(rightEyeX + 2, by - 1, rightEyeX + 5, by, browCol);
                    break;
                case 3: // Heavy thick 2px brows
                    buf.FillRect(leftEyeX - 1, by - 1, 5, 2, browCol);
                    buf.FillRect(rightEyeX, by - 1, 5, 2, browCol);
                    break;
                case 4: // Upturned worried with central forehead furrow
                    buf.DrawLine(leftEyeX - 1, by + 1, leftEyeX + 4, by - 1, browCol);
                    buf.DrawLine(rightEyeX, by - 1, rightEyeX + 5, by + 1, browCol);
                    buf.Set(31, by - 1, skinRamp[0]);
                    buf.Set(33, by - 1, skinRamp[0]);
                    break;
                default: // Scarred / notched eyebrow
                    buf.DrawLine(leftEyeX - 1, by, leftEyeX + 4, by, browCol);
                    buf.DrawLine(rightEyeX, by, rightEyeX + 5, by, browCol);
                    buf.Set(rightEyeX + 2, by, skinRamp[3]); // Vertical scar notch through right brow
                    buf.Set(rightEyeX + 2, by - 1, skinRamp[0]);
                    break;
            }
        }

        // ---------------------------------------------------------------------
        // 2. Eye Pool (6 Distinct Variants)
        // ---------------------------------------------------------------------
        if (species == PortraitSpecies.HunBorian)
        {
            // Mandatory gentle wide-set pale blue eyes (#93c5fd) with #eff6ff specular dot
            int eyeH = (traits.EyeVariant % 2 == 0) ? 3 : 2;
            buf.FillRect(leftEyeX - 1, baseEyeY, 4, eyeH, Palette.HexOutlineVoid);
            buf.FillRect(leftEyeX, baseEyeY, 3, 2, Palette.HexHunBorianKindEye);
            buf.Set(leftEyeX, baseEyeY, Palette.HexHunBorianEyeSpecular);

            buf.FillRect(rightEyeX + 1, rightEyeY, 4, eyeH, Palette.HexOutlineVoid);
            buf.FillRect(rightEyeX + 1, rightEyeY, 3, 2, Palette.HexHunBorianKindEye);
            buf.Set(rightEyeX + 1, rightEyeY, Palette.HexHunBorianEyeSpecular);
        }
        else if (species is PortraitSpecies.Kordani or PortraitSpecies.Tolvak)
        {
            string irisCol = species == PortraitSpecies.Kordani
                ? (traits.EyeVariant % 2 == 0 ? "#f59e0b" : "#38bdf8")
                : "#fde047";
            int eyeH = traits.EyeVariant == 1 ? 1 : 2;
            buf.FillRect(leftEyeX, baseEyeY, 4, eyeH, irisCol);
            buf.Set(leftEyeX + 2, baseEyeY, Palette.HexOutlineVoid);
            if (eyeH > 1) buf.Set(leftEyeX + 2, baseEyeY + 1, Palette.HexOutlineVoid);

            buf.FillRect(rightEyeX, rightEyeY, 4, eyeH, irisCol);
            buf.Set(rightEyeX + 2, rightEyeY, Palette.HexOutlineVoid);
            if (eyeH > 1) buf.Set(rightEyeX + 2, rightEyeY + 1, Palette.HexOutlineVoid);
        }
        else if (species == PortraitSpecies.Brengop)
        {
            buf.FillRect(leftEyeX, 26, 3, 2, Palette.HexOutlineVoid);
            buf.Set(leftEyeX + 1, 26, "#dcd7c9");
            buf.FillRect(rightEyeX + 1, 26, 3, 2, Palette.HexOutlineVoid);
            buf.Set(rightEyeX + 2, 26, "#dcd7c9");
        }
        else if (species == PortraitSpecies.Orun)
        {
            string glintCol = (traits.EyeVariant % 2 == 0) ? "#cbd5e1" : "#38bdf8";
            buf.FillRect(leftEyeX - 1, 25, 5, 3, Palette.OrunSkinRamp[0]);
            buf.FillRect(leftEyeX, 26, 3, 1, glintCol);
            buf.FillRect(rightEyeX, 25, 5, 3, Palette.OrunSkinRamp[0]);
            buf.FillRect(rightEyeX + 1, 26, 3, 1, glintCol);
        }
        else
        {
            switch (traits.EyeVariant)
            {
                case 0: // Wide Alert (4x3)
                    buf.FillRect(leftEyeX, baseEyeY, 4, 3, "#e2e8f0");
                    buf.FillRect(leftEyeX + 1, baseEyeY, 2, 3, Palette.HexOutlineVoid);
                    buf.Set(leftEyeX + 1, baseEyeY, "#f8fafc");

                    buf.FillRect(rightEyeX, rightEyeY, 4, 3, "#e2e8f0");
                    buf.FillRect(rightEyeX + 1, rightEyeY, 2, 3, Palette.HexOutlineVoid);
                    buf.Set(rightEyeX + 1, rightEyeY, "#f8fafc");
                    break;

                case 1: // Narrow / Squinting (4x1 slit)
                    buf.DrawLine(leftEyeX, baseEyeY, leftEyeX + 3, baseEyeY, Palette.HexOutlineVoid);
                    buf.FillRect(leftEyeX, baseEyeY + 1, 4, 1, "#e2e8f0");
                    buf.Set(leftEyeX + 2, baseEyeY + 1, Palette.HexOutlineVoid);

                    buf.DrawLine(rightEyeX, rightEyeY, rightEyeX + 3, rightEyeY, Palette.HexOutlineVoid);
                    buf.FillRect(rightEyeX, rightEyeY + 1, 4, 1, "#e2e8f0");
                    buf.Set(rightEyeX + 2, rightEyeY + 1, Palette.HexOutlineVoid);
                    break;

                case 2: // Deep-Set Shadowed Sockets
                    buf.FillRect(leftEyeX - 1, baseEyeY - 1, 5, 4, skinRamp[0]);
                    buf.FillRect(leftEyeX, baseEyeY, 3, 2, "#e2e8f0");
                    buf.Set(leftEyeX + 1, baseEyeY, Palette.HexOutlineVoid);

                    buf.FillRect(rightEyeX, rightEyeY - 1, 5, 4, skinRamp[0]);
                    buf.FillRect(rightEyeX + 1, rightEyeY, 3, 2, "#e2e8f0");
                    buf.Set(rightEyeX + 2, rightEyeY, Palette.HexOutlineVoid);
                    break;

                case 3: // Hooded / Heavy-Lidded
                    buf.DrawLine(leftEyeX, baseEyeY, leftEyeX + 3, baseEyeY, skinRamp[0]);
                    buf.FillRect(leftEyeX, baseEyeY + 1, 4, 2, "#e2e8f0");
                    buf.FillRect(leftEyeX + 1, baseEyeY + 1, 2, 2, Palette.HexOutlineVoid);

                    buf.DrawLine(rightEyeX, rightEyeY, rightEyeX + 3, rightEyeY, skinRamp[0]);
                    buf.FillRect(rightEyeX, rightEyeY + 1, 4, 2, "#e2e8f0");
                    buf.FillRect(rightEyeX + 1, rightEyeY + 1, 2, 2, Palette.HexOutlineVoid);
                    break;

                case 4: // Tired with dark under-eye bags
                    buf.FillRect(leftEyeX, baseEyeY, 4, 2, "#e2e8f0");
                    buf.FillRect(leftEyeX + 1, baseEyeY, 2, 2, Palette.HexOutlineVoid);
                    buf.DrawLine(leftEyeX, baseEyeY + 3, leftEyeX + 3, baseEyeY + 3, skinRamp[0]);

                    buf.FillRect(rightEyeX, rightEyeY, 4, 2, "#e2e8f0");
                    buf.FillRect(rightEyeX + 1, rightEyeY, 2, 2, Palette.HexOutlineVoid);
                    buf.DrawLine(rightEyeX, rightEyeY + 3, rightEyeX + 3, rightEyeY + 3, skinRamp[0]);
                    break;

                default: // Round Piercing (3x3)
                    buf.FillRect(leftEyeX, baseEyeY, 3, 3, "#e2e8f0");
                    buf.Set(leftEyeX + 1, baseEyeY + 1, Palette.HexOutlineVoid);
                    buf.Set(leftEyeX + 1, baseEyeY, "#f8fafc");

                    buf.FillRect(rightEyeX + 1, rightEyeY, 3, 3, "#e2e8f0");
                    buf.Set(rightEyeX + 2, rightEyeY + 1, Palette.HexOutlineVoid);
                    buf.Set(rightEyeX + 2, rightEyeY, "#f8fafc");
                    break;
            }
        }

        // ---------------------------------------------------------------------
        // 3. Nose Pool (6 Distinct Variants at y = 28..34)
        // ---------------------------------------------------------------------
        if (species == PortraitSpecies.Brengop)
        {
            int nRx = 3 + (traits.NoseVariant % 3);
            int nRy = 2 + (traits.NoseVariant % 2);
            buf.FillEllipse(32, 31, nRx, nRy, (px, py, _, _, dist) =>
                sampleFn(px, py, skinRamp, 0.85f - dist * 0.45f));
            buf.Set(30, 32, Palette.HexOutlineVoid);
            buf.Set(34, 32, Palette.HexOutlineVoid);
        }
        else if (species == PortraitSpecies.Orun)
        {
            int nW = 2 + (traits.NoseVariant % 2);
            ReadOnlySpan<(int X, int Y)> graniteNose = [(32 - nW, 25), (32 + nW, 25), (32 + nW + 1, 33), (32 - nW - 1, 33)];
            buf.FillPoly(graniteNose, (px, py) => sampleFn(px, py, skinRamp, 0.78f));
            buf.DrawLine(32 - nW - 1, 33, 32 + nW + 1, 33, skinRamp[0]);
        }
        else
        {
            switch (traits.NoseVariant)
            {
                case 0: // Narrow Long Straight
                    buf.DrawLine(32, 27, 32, 33, skinRamp[3]);
                    buf.DrawLine(31, 28, 31, 33, skinRamp[0]);
                    buf.Set(31, 33, Palette.HexOutlineVoid);
                    buf.Set(33, 33, Palette.HexOutlineVoid);
                    break;
                case 1: // Broad Flat with wide nostrils
                    buf.FillRect(30, 30, 5, 3, skinRamp[2]);
                    buf.Set(29, 33, Palette.HexOutlineVoid);
                    buf.DrawLine(30, 33, 34, 33, skinRamp[0]);
                    buf.Set(35, 33, Palette.HexOutlineVoid);
                    break;
                case 2: // Hooked / Aquiline
                    buf.DrawLine(32, 27, 30, 32, skinRamp[0]);
                    buf.DrawLine(33, 27, 31, 32, skinRamp[3]);
                    buf.DrawLine(30, 33, 32, 34, Palette.HexOutlineVoid);
                    break;
                case 3: // Short Blunt Snub
                    buf.DrawLine(32, 28, 32, 31, skinRamp[3]);
                    buf.Set(31, 31, Palette.HexOutlineVoid);
                    buf.Set(33, 31, Palette.HexOutlineVoid);
                    break;
                case 4: // Crooked / Broken Bridge
                    buf.DrawLine(32, 27, 33, 30, skinRamp[3]);
                    buf.DrawLine(33, 30, 31, 33, skinRamp[3]);
                    buf.Set(31, 33, Palette.HexOutlineVoid);
                    buf.Set(33, 33, skinRamp[0]);
                    break;
                default: // Sculpted Ridged Trapezoid
                    buf.DrawLine(31, 27, 30, 33, skinRamp[0]);
                    buf.DrawLine(32, 27, 32, 33, skinRamp[3]);
                    buf.DrawLine(33, 27, 34, 33, skinRamp[0]);
                    buf.DrawLine(30, 33, 34, 33, skinRamp[0]);
                    break;
            }
        }

        // ---------------------------------------------------------------------
        // 4. Mouth & Jaw Accent Pool (6 Distinct Variants at y = 36..45)
        // ---------------------------------------------------------------------
        if (species == PortraitSpecies.Brengop)
        {
            buf.DrawLine(25, 39, 32, 37, Palette.HexOutlineVoid);
            buf.DrawLine(32, 37, 39, 39, Palette.HexOutlineVoid);
            int tuskMode = traits.MouthVariant % 3;
            if (tuskMode is 0 or 2) buf.Set(27, 37, "#dcd7c9");
            if (tuskMode is 1 or 2) buf.Set(37, 37, "#dcd7c9");
        }
        else if (species == PortraitSpecies.Helani)
        {
            // Distinct smaller composed Helani mouth (4px wide) + sculpted protruding chin facet highlights (y = 40..46)
            int mY = 36 + (traits.MouthVariant % 2);
            buf.DrawLine(30, mY, 34, mY, Palette.HexOutlineVoid);
            buf.Set(31, mY + 1, skinRamp[0]);

            // Protruding chin ridge/cleft variants
            buf.DrawLine(29, 41, 35, 41, skinRamp[3]);
            buf.FillRect(30, 42, 4, 3, skinRamp[3]);
            if (traits.SpeciesSubVariantA % 2 == 0)
            {
                buf.DrawLine(32, 42, 32, 45, skinRamp[0]); // Sculpted chin cleft
            }
            buf.DrawLine(28, 45, 36, 45, skinRamp[0]);
        }
        else
        {
            int mouthY = J(37, 1);
            switch (traits.MouthVariant)
            {
                case 0: // Neutral Composed
                    buf.DrawLine(29, mouthY, 35, mouthY, Palette.HexOutlineVoid);
                    buf.DrawLine(30, mouthY + 1, 34, mouthY + 1, skinRamp[0]);
                    break;
                case 1: // Stern Frown
                    buf.DrawLine(29, mouthY, 35, mouthY, Palette.HexOutlineVoid);
                    buf.Set(28, mouthY + 1, Palette.HexOutlineVoid);
                    buf.Set(36, mouthY + 1, Palette.HexOutlineVoid);
                    break;
                case 2: // Asymmetrical Smirk
                    buf.DrawLine(28, mouthY + 1, 35, mouthY, Palette.HexOutlineVoid);
                    buf.Set(36, mouthY - 1, Palette.HexOutlineVoid);
                    break;
                case 3: // Wide Stoic Compressed
                    buf.DrawLine(27, mouthY, 37, mouthY, Palette.HexOutlineVoid);
                    break;
                case 4: // Full Parted Lips
                    buf.DrawLine(29, mouthY - 1, 35, mouthY - 1, skinRamp[0]);
                    buf.DrawLine(29, mouthY, 35, mouthY, Palette.HexOutlineVoid);
                    buf.DrawLine(29, mouthY + 1, 35, mouthY + 1, skinRamp[1]);
                    break;
                default: // Clenched Grimace with teeth glint
                    buf.FillRect(29, mouthY, 7, 2, Palette.HexOutlineVoid);
                    buf.DrawLine(30, mouthY, 34, mouthY, "#e2e8f0");
                    break;
            }
        }
    }

    // =========================================================================
    // LAYER 5: HAIR / FACIAL HAIR / SPIKES (10 Hairstyles, 5 Thane Beards, 4 Krushvak Sideburns, etc.)
    // =========================================================================
    private static void RenderLayer5HairBeardSpikes(
        PixelBuffer buf,
        PortraitSpecies species,
        bool isTrueDopple,
        PortraitTraitProfile traits,
        string[] skinRamp,
        string[] hairRamp,
        DeterministicRng rng,
        Func<int, int, int> J,
        Func<int, int, string[], float, string> sampleFn)
    {
        if (isTrueDopple)
            return;

        switch (species)
        {
            case PortraitSpecies.Thane:
            {
                // Strictly bald titanium-white skull (hairLayer = null).
                // Mandatory short deep-black beard with 5 distinct silhouette cuts:
                int beardCut = traits.SpeciesSubVariantA % 5;
                switch (beardCut)
                {
                    case 0: // Squared Boxed Beard + connected mustache + crisp white lip cutout
                    {
                        ReadOnlySpan<(int X, int Y)> boxedBeard =
                        [
                            (20, 31), (22, 31), (24, 36), (40, 36), (42, 31), (44, 31),
                            (43, 41), (39, 44), (25, 44), (21, 41)
                        ];
                        buf.FillPoly(boxedBeard, (px, py) =>
                            sampleFn(px, py, Palette.ThaneBeardDeepBlackRamp, 0.72f - (py - 31) / 16.0f));
                        buf.FillRect(27, 35, 10, 2, Palette.ThaneBeardDeepBlackRamp[1]);
                        buf.FillRect(29, 37, 6, 2, Palette.ThaneTitaniumWhiteRamp[2]);
                        buf.DrawLine(29, 37, 35, 37, Palette.HexOutlineVoid);
                        break;
                    }
                    case 1: // Tapered Pointed V-Beard reaching y = 46
                    {
                        ReadOnlySpan<(int X, int Y)> pointedBeard =
                        [
                            (21, 32), (23, 32), (25, 37), (39, 37), (41, 32), (43, 32),
                            (41, 39), (32, 46), (23, 39)
                        ];
                        buf.FillPoly(pointedBeard, (px, py) =>
                            sampleFn(px, py, Palette.ThaneBeardDeepBlackRamp, 0.75f - (py - 32) / 16.0f));
                        buf.DrawLine(28, 35, 36, 35, Palette.ThaneBeardDeepBlackRamp[1]);
                        break;
                    }
                    case 2: // Razor-Carved Chin-Strap (ear-to-ear jaw strap, bare upper lip without mustache)
                    {
                        ReadOnlySpan<(int X, int Y)> chinStrap =
                        [
                            (20, 30), (22, 30), (24, 38), (40, 38), (42, 30), (44, 30),
                            (42, 40), (37, 44), (27, 44), (22, 40)
                        ];
                        buf.FillPoly(chinStrap, (px, py) =>
                            sampleFn(px, py, Palette.ThaneBeardDeepBlackRamp, 0.70f));
                        break;
                    }
                    case 3: // Heavy High-Cheek Full Jawline Block
                    {
                        ReadOnlySpan<(int X, int Y)> heavyBeard =
                        [
                            (19, 29), (24, 29), (26, 34), (38, 34), (40, 29), (45, 29),
                            (44, 41), (38, 45), (26, 45), (20, 41)
                        ];
                        buf.FillPoly(heavyBeard, (px, py) =>
                            sampleFn(px, py, Palette.ThaneBeardDeepBlackRamp, 0.78f - (py - 29) / 18.0f));
                        buf.FillRect(29, 36, 6, 2, Palette.ThaneTitaniumWhiteRamp[2]);
                        buf.DrawLine(29, 37, 35, 37, Palette.HexOutlineVoid);
                        break;
                    }
                    default: // Forked / Twin-Notched Beard + Detached Handlebar Mustache
                    {
                        ReadOnlySpan<(int X, int Y)> forkedBeard =
                        [
                            (21, 33), (23, 33), (25, 38), (39, 38), (41, 33), (43, 33),
                            (42, 40), (37, 45), (32, 42), (27, 45), (22, 40)
                        ];
                        buf.FillPoly(forkedBeard, (px, py) =>
                            sampleFn(px, py, Palette.ThaneBeardDeepBlackRamp, 0.72f));
                        buf.DrawLine(26, 35, 38, 35, Palette.ThaneBeardDeepBlackRamp[1]);
                        buf.Set(26, 36, Palette.ThaneBeardDeepBlackRamp[1]);
                        buf.Set(38, 36, Palette.ThaneBeardDeepBlackRamp[1]);
                        break;
                    }
                }
                break;
            }

            case PortraitSpecies.Krushvak:
            {
                // Mandatory large mutton-chop sideburns (4 shapes) leaving center chin clear + 4 scalp styles
                string[] sideburnRamp = Palette.ThaneBeardDeepBlackRamp;
                int chopStyle = traits.SpeciesSubVariantB % 4;
                int innerL = chopStyle == 0 ? 27 : (chopStyle == 1 ? 28 : 26);
                int innerR = 64 - innerL;
                int outerFlare = chopStyle == 3 ? 2 : 0;

                ReadOnlySpan<(int X, int Y)> leftSideburn =
                [
                    (19, 20), (24, 21), (25, 29), (innerL, 35), (innerL - 1, 42), (19 - outerFlare, 39), (18 - outerFlare, 30)
                ];
                ReadOnlySpan<(int X, int Y)> rightSideburn =
                [
                    (45, 20), (40, 21), (39, 29), (innerR, 35), (innerR + 1, 42), (45 + outerFlare, 39), (46 + outerFlare, 30)
                ];

                buf.FillPoly(leftSideburn, (px, py) => sampleFn(px, py, sideburnRamp, 0.65f));
                buf.FillPoly(rightSideburn, (px, py) => sampleFn(px, py, sideburnRamp, 0.50f));

                if (chopStyle == 2)
                {
                    // Braided chop clasps
                    buf.FillRect(21, 38, 4, 2, Palette.HexOrunBrassTrim);
                    buf.FillRect(39, 38, 4, 2, Palette.HexOrunBrassTrim);
                }

                // 4 Krushvak scalp hair styles (all keep the forehead ridge y = 15..24 completely clear)
                int scalpStyle = traits.HairArchetype % 4;
                if (scalpStyle == 1)
                {
                    // High Warrior Top-Knot at crown
                    buf.FillEllipse(32, 9, 4, 4, (px, py, _, _, _) => sampleFn(px, py, sideburnRamp, 0.70f));
                    buf.DrawLine(29, 12, 35, 12, Palette.HexAlertCrimson);
                }
                else if (scalpStyle == 2)
                {
                    // Swept-back dark mane visible at upper temples
                    buf.FillRect(18, 14, 5, 7, sideburnRamp[1]);
                    buf.FillRect(41, 14, 5, 7, sideburnRamp[0]);
                }
                else if (scalpStyle == 3)
                {
                    // Twin rear braided locks dropping behind ears
                    buf.FillRect(16, 30, 3, 15, sideburnRamp[1]);
                    buf.FillRect(45, 30, 3, 15, sideburnRamp[0]);
                }
                break;
            }

            case PortraitSpecies.HunBorian:
            {
                // 5-7 triangular bony crown spikes with varied count, height, and fan angle
                string[] spikeRamp = ["#785c38", "#a88658", "#d4b483", "#f0dec0"];
                int spikeStyle = traits.SpeciesSubVariantC % 4;
                int[] spikeCenters = spikeStyle switch
                {
                    0 => [23, 27, 32, 37, 41],             // 5 thick wide-spaced horns
                    1 => [22, 26, 30, 34, 38, 42],         // 6 tall sharp spikes
                    2 => [20, 24, 28, 32, 36, 40, 44],     // 7 dense crown spikes
                    _ => [22, 27, 32, 37, 42]              // 5 outward-fanned spikes
                };

                int heightBoost = spikeStyle == 1 ? -2 : (spikeStyle == 0 ? 1 : 0);
                for (int i = 0; i < spikeCenters.Length; i++)
                {
                    int sx = spikeCenters[i];
                    int fanOffset = spikeStyle == 3 ? (sx - 32) / 3 : 0;
                    int tipY = Math.Clamp((Math.Abs(sx - 32) <= 3 ? 8 : 10) + heightBoost, 6, 13);
                    int baseY = Math.Abs(sx - 32) <= 4 ? 15 : 17;

                    ReadOnlySpan<(int X, int Y)> spike =
                    [
                        (sx - 2, baseY),
                        (sx + fanOffset, tipY),
                        (sx + 2, baseY)
                    ];
                    buf.FillPoly(spike, (px, py) =>
                        sampleFn(px, py, spikeRamp, 1.0f - (py - tipY) / 10.0f));
                }
                break;
            }

            case PortraitSpecies.Klaasari:
            {
                // 5 Klaasari crown/headwear variants (keeping symmetrical cheek/forehead spots visible)
                int crownVar = traits.HairArchetype % 5;
                if (crownVar == 1)
                {
                    // Swept dorsal head-fin crest
                    ReadOnlySpan<(int X, int Y)> crest = [(29, 13), (32, 7), (35, 13)];
                    buf.FillPoly(crest, (px, py) => sampleFn(px, py, skinRamp, 0.90f));
                }
                else if (crownVar == 2)
                {
                    // Twin temple swept fins
                    ReadOnlySpan<(int X, int Y)> lFin = [(20, 18), (16, 11), (24, 14)];
                    ReadOnlySpan<(int X, int Y)> rFin = [(44, 18), (48, 11), (40, 14)];
                    buf.FillPoly(lFin, (px, py) => sampleFn(px, py, skinRamp, 0.85f));
                    buf.FillPoly(rFin, (px, py) => sampleFn(px, py, skinRamp, 0.75f));
                }
                else if (crownVar == 3)
                {
                    // Metallic GU circlet tiara across upper brow
                    buf.DrawLine(21, 15, 43, 15, Palette.GuPrimaryWhiteRamp[3]);
                    buf.FillRect(31, 14, 3, 3, Palette.HexClearanceTeal);
                }
                else if (crownVar == 4)
                {
                    // Low braided dark hair framing neck behind ears
                    buf.FillRect(18, 30, 3, 13, hairRamp[1]);
                    buf.FillRect(43, 30, 3, 13, hairRamp[0]);
                }
                break;
            }

            case PortraitSpecies.Human:
            case PortraitSpecies.Helani:
            {
                RenderHumanOrHelaniHair(buf, species, traits, hairRamp, sampleFn);
                if (species == PortraitSpecies.Human)
                {
                    RenderHumanFacialHair(buf, traits, hairRamp, sampleFn);
                }
                break;
            }
        }
    }

    private static void RenderHumanOrHelaniHair(
        PixelBuffer buf,
        PortraitSpecies species,
        PortraitTraitProfile traits,
        string[] hairRamp,
        Func<int, int, string[], float, string> sampleFn)
    {
        // 10 Distinct Outer Hair Silhouettes
        int style = traits.HairArchetype % 10;
        switch (style)
        {
            case 0:
                // Style 0: Bald / Shaved Dome (Helani gets neat temple side-tabs)
                if (species == PortraitSpecies.Helani)
                {
                    buf.FillRect(20, 18, 2, 6, hairRamp[1]);
                    buf.FillRect(42, 18, 2, 6, hairRamp[1]);
                }
                break;

            case 1:
                // Style 1: Close Military Buzzcut / Dithered Cap
                ReadOnlySpan<(int X, int Y)> buzz =
                [
                    (20, 21), (22, 13), (28, 11), (36, 11), (42, 13), (44, 21), (41, 17), (23, 17)
                ];
                buf.FillPoly(buzz, (px, py) => Dither.BayerDither(px, py, hairRamp[0], hairRamp[2], 0.5f));
                break;

            case 2:
                // Style 2: Classic Side-Part Officer Cut
                ReadOnlySpan<(int X, int Y)> sidePart =
                [
                    (19, 23), (20, 12), (32, 10), (43, 12), (45, 23), (41, 19), (27, 15), (22, 20)
                ];
                buf.FillPoly(sidePart, (px, py) => sampleFn(px, py, hairRamp, 0.88f - (px - 20) / 30.0f));
                break;

            case 3:
                // Style 3: Tall Swept-Back Pompadour / High Volume Crown (y = 7..19)
                ReadOnlySpan<(int X, int Y)> pomp =
                [
                    (20, 21), (19, 11), (26, 7), (38, 7), (45, 11), (44, 21), (41, 16), (23, 16)
                ];
                buf.FillPoly(pomp, (px, py) => sampleFn(px, py, hairRamp, 0.92f - (py - 7) / 15.0f));
                break;

            case 4:
                // Style 4: Receding Widow's Peak / Temple Wings
                buf.FillRect(19, 15, 4, 9, hairRamp[1]);
                buf.FillRect(41, 15, 4, 9, hairRamp[0]);
                ReadOnlySpan<(int X, int Y)> peak = [(28, 11), (36, 11), (32, 17)];
                buf.FillPoly(peak, (px, py) => sampleFn(px, py, hairRamp, 0.80f));
                break;

            case 5:
                // Style 5: Messy / Spiky Frontier Tufts
                int[] tuftX = [22, 27, 32, 37, 42];
                foreach (int tx in tuftX)
                {
                    ReadOnlySpan<(int X, int Y)> tuft = [(tx - 3, 17), (tx, 7 + (tx % 3)), (tx + 3, 17)];
                    buf.FillPoly(tuft, (px, py) => sampleFn(px, py, hairRamp, 0.90f - (py - 7) / 11.0f));
                }
                break;

            case 6:
                // Style 6: Chin-Length Framed Bob
                ReadOnlySpan<(int X, int Y)> bob =
                [
                    (17, 36), (18, 13), (26, 10), (38, 10), (46, 13), (47, 36),
                    (42, 36), (41, 17), (23, 17), (22, 36)
                ];
                buf.FillPoly(bob, (px, py) => sampleFn(px, py, hairRamp, 0.85f - (py - 10) / 28.0f));
                break;

            case 7:
                // Style 7: High Top-Bun / Tied Officer Knot
                ReadOnlySpan<(int X, int Y)> pulledCap =
                [
                    (20, 21), (22, 13), (32, 11), (42, 13), (44, 21), (41, 17), (23, 17)
                ];
                buf.FillPoly(pulledCap, (px, py) => sampleFn(px, py, hairRamp, 0.78f));
                buf.FillEllipse(32, 8, 5, 4, (px, py, _, _, dist) => sampleFn(px, py, hairRamp, 0.92f - dist * 0.4f));
                buf.DrawLine(28, 11, 36, 11, Palette.HexCrtAmberDimmed);
                break;

            case 8:
                // Style 8: Shaved-Side Undercut / High Central Mohawk Crest
                ReadOnlySpan<(int X, int Y)> crest =
                [
                    (27, 17), (26, 7), (38, 7), (37, 17)
                ];
                buf.FillPoly(crest, (px, py) => sampleFn(px, py, hairRamp, 0.92f - (py - 7) / 12.0f));
                break;

            default:
                // Style 9: Wide Textured Afro / Curly Halo
                buf.FillEllipse(32, 15, 16, 8, (px, py, _, _, dist) =>
                    sampleFn(px, py, hairRamp, 0.88f - dist * 0.45f));
                buf.FillRect(17, 16, 4, 10, hairRamp[1]);
                buf.FillRect(43, 16, 4, 10, hairRamp[0]);
                break;
        }
    }

    private static void RenderHumanFacialHair(
        PixelBuffer buf,
        PortraitTraitProfile traits,
        string[] hairRamp,
        Func<int, int, string[], float, string> sampleFn)
    {
        switch (traits.FacialHairVariant)
        {
            case 0: // Clean-shaven
                break;
            case 1: // Dithered 5-o'clock shadow stubble
                for (int y = 35; y <= 41; y++)
                {
                    for (int x = 25; x <= 39; x++)
                    {
                        if (((x + y) % 3 == 0) && buf.HasPixel(x, y))
                        {
                            buf.Set(x, y, hairRamp[1]);
                        }
                    }
                }
                break;
            case 2: // Chevron / Handlebar Mustache
                buf.FillRect(27, 35, 10, 2, hairRamp[1]);
                buf.Set(26, 36, hairRamp[0]);
                buf.Set(37, 36, hairRamp[0]);
                break;
            case 3: // Pointed Goatee
                buf.FillRect(28, 35, 8, 2, hairRamp[1]);
                ReadOnlySpan<(int X, int Y)> goatee = [(28, 39), (36, 39), (34, 44), (30, 44)];
                buf.FillPoly(goatee, (px, py) => sampleFn(px, py, hairRamp, 0.65f));
                break;
            case 4: // Short Chin-Strap Beard
                buf.FillRect(22, 33, 2, 8, hairRamp[1]);
                buf.FillRect(40, 33, 2, 8, hairRamp[0]);
                buf.FillRect(24, 40, 16, 3, hairRamp[1]);
                break;
            default: // Full Captain's Beard
                ReadOnlySpan<(int X, int Y)> fullBeard =
                [
                    (21, 32), (24, 32), (25, 36), (39, 36), (40, 32), (43, 32),
                    (42, 41), (37, 45), (27, 45), (22, 41)
                ];
                buf.FillPoly(fullBeard, (px, py) => sampleFn(px, py, hairRamp, 0.72f - (py - 32) / 15.0f));
                buf.FillRect(27, 35, 10, 2, hairRamp[1]);
                break;
        }
    }

    // =========================================================================
    // LAYER 6: FULL HEADGEAR / OPTICS / ACCESSORIES (Veldor Visors, Prunjun Goggles, Accessories)
    // =========================================================================
    private static void RenderLayer6HeadgearAndOptics(
        PixelBuffer buf,
        PortraitSpecies species,
        bool isTrueDopple,
        PortraitTraitProfile traits,
        string[] skinRamp,
        DeterministicRng rng,
        Func<int, int, int> J,
        Func<int, int, string[], float, string> sampleFn)
    {
        if (isTrueDopple)
            return;

        if (species == PortraitSpecies.Veldor)
        {
            RenderVeldorHelmetHardware(buf, traits, sampleFn);
            return;
        }

        if (species == PortraitSpecies.Prunjun)
        {
            RenderPrunjunCapAndGoggles(buf, traits, sampleFn);
            return;
        }

        // Optional seed-driven accessories for organic species
        switch (traits.AccessoryVariant)
        {
            case 1:
                // Comms Ear-Mic Boom crossing left cheek
                buf.FillRect(18, 28, 3, 4, Palette.GuSecondaryGunmetalRamp[1]);
                buf.DrawLine(20, 31, 28, 37, Palette.GuSecondaryGunmetalRamp[3]);
                buf.Set(28, 37, Palette.HexClearanceTeal);
                break;

            case 2:
                // Cybernetic Ocular Monocle over right eye
                buf.FillRect(34, 24, 7, 6, Palette.VeldorBrassTrimRamp[1]);
                buf.FillRect(35, 25, 5, 4, "#0f172a");
                buf.FillRect(36, 26, 3, 2, Palette.HexCrtAmberPrimary);
                buf.DrawLine(41, 26, 45, 26, Palette.VeldorBrassTrimRamp[2]);
                break;

            case 3:
                // Diagonal Weather/Combat Cheek Scar
                buf.DrawLine(23, 29, 27, 35, skinRamp[3]);
                buf.DrawLine(24, 29, 28, 35, skinRamp[0]);
                break;

            case 4:
                // Nose-Bridge Bandage Plaster
                buf.FillRect(29, 29, 7, 2, "#e2e8f0");
                buf.Set(32, 29, "#cbd5e1");
                break;

            case 5:
                // Metallic Ear Studs
                buf.Set(20, 31, Palette.HexOrunBrassTrim);
                buf.Set(44, 31, Palette.HexOrunBrassTrim);
                break;
        }
    }

    private static void RenderVeldorHelmetHardware(
        PixelBuffer buf,
        PortraitTraitProfile traits,
        Func<int, int, string[], float, string> sampleFn)
    {
        // 1. Painted Cowl Markings (4 Variants)
        int paintStyle = traits.SpeciesSubVariantB % 4;
        if (paintStyle == 0)
        {
            // Vertical center brass stripe above visor
            buf.FillRect(31, 11, 3, 9, Palette.VeldorBrassTrimRamp[2]);
        }
        else if (paintStyle == 1)
        {
            // Twin temple chevrons
            buf.DrawLine(22, 16, 27, 20, Palette.VeldorBrassTrimRamp[2]);
            buf.DrawLine(42, 16, 37, 20, Palette.VeldorBrassTrimRamp[2]);
        }
        else if (paintStyle == 2)
        {
            // Horizontal forehead rank band
            buf.FillRect(23, 16, 18, 2, Palette.GuSecondaryGunmetalRamp[1]);
        }
        else
        {
            // Diagonal crown hazard bars
            buf.DrawLine(25, 13, 28, 19, Palette.VeldorBrassTrimRamp[2]);
            buf.DrawLine(39, 13, 36, 19, Palette.VeldorBrassTrimRamp[2]);
        }

        // 2. Sulfur Visor Shape (4 Variants: Wide Oval, Split Dual-Optic Goggles, Narrow Armored Slit, Tall T-Dome)
        int visorStyle = traits.SpeciesSubVariantA % 4;
        switch (visorStyle)
        {
            case 0: // Wide Oval Panorama Visor
                buf.FillEllipse(32, 27, 11, 8, (px, py, dx, dy, _) =>
                    sampleFn(px, py, Palette.VeldorBrassTrimRamp, 0.75f - dx * 0.25f - dy * 0.25f));
                buf.FillEllipse(32, 27, 10, 7, (px, py, dx, dy, dist) =>
                    sampleFn(px, py, Palette.VeldorSulfurVisorRamp, 0.88f - dist * 0.55f - dx * 0.2f));
                buf.DrawLine(25, 24, 30, 21, "#f8fafc");
                break;

            case 1: // Split Dual-Optic Circular Goggles
                buf.FillEllipse(26, 27, 5, 5, Palette.VeldorBrassTrimRamp[1]);
                buf.FillEllipse(26, 27, 4, 4, (px, py, _, _, dist) =>
                    sampleFn(px, py, Palette.VeldorSulfurVisorRamp, 0.90f - dist * 0.5f));
                buf.Set(24, 25, "#f8fafc");

                buf.FillEllipse(38, 27, 5, 5, Palette.VeldorBrassTrimRamp[1]);
                buf.FillEllipse(38, 27, 4, 4, (px, py, _, _, dist) =>
                    sampleFn(px, py, Palette.VeldorSulfurVisorRamp, 0.85f - dist * 0.5f));
                buf.Set(36, 25, "#f8fafc");
                buf.FillRect(30, 26, 4, 2, Palette.VeldorBrassTrimRamp[2]);
                break;

            case 2: // Narrow Horizontal Armored Blast Slit with vertical bars
                buf.FillRect(20, 23, 24, 7, Palette.VeldorBrassTrimRamp[1]);
                buf.FillRect(21, 24, 22, 5, (px, py) =>
                    sampleFn(px, py, Palette.VeldorSulfurVisorRamp, 0.90f - MathF.Abs(px - 32) / 14.0f));
                buf.DrawLine(28, 24, 28, 28, Palette.VeldorBrassTrimRamp[0]);
                buf.DrawLine(36, 24, 36, 28, Palette.VeldorBrassTrimRamp[0]);
                buf.DrawLine(23, 25, 26, 25, "#f8fafc");
                break;

            default: // Tall Vertical T-Dome Visor
                buf.FillEllipse(32, 28, 8, 10, Palette.VeldorBrassTrimRamp[1]);
                buf.FillEllipse(32, 28, 7, 9, (px, py, _, _, dist) =>
                    sampleFn(px, py, Palette.VeldorSulfurVisorRamp, 0.92f - dist * 0.55f));
                buf.FillRect(25, 28, 14, 2, Palette.VeldorBrassTrimRamp[2]);
                buf.DrawLine(28, 23, 31, 21, "#f8fafc");
                break;
        }

        // 3. Cheek Scrubber Canisters & Breather Tube Routing (4 Variants)
        int tubeStyle = traits.SpeciesSubVariantC % 4;
        int canH = tubeStyle == 1 ? 9 : 7;
        int canY = tubeStyle == 2 ? 29 : 31;

        buf.FillRect(14, canY, 5, canH, (px, py) =>
            sampleFn(px, py, Palette.VeldorBrassTrimRamp, 0.85f - (py - canY) / (float)(canH + 1)));
        buf.DrawLine(15, canY + 2, 17, canY + 2, Palette.HexOutlineVoid);
        buf.DrawLine(15, canY + 4, 17, canY + 4, Palette.HexOutlineVoid);

        buf.FillRect(45, canY, 5, canH, (px, py) =>
            sampleFn(px, py, Palette.VeldorBrassTrimRamp, 0.70f - (py - canY) / (float)(canH + 1)));
        buf.DrawLine(46, canY + 2, 48, canY + 2, Palette.HexOutlineVoid);
        buf.DrawLine(46, canY + 4, 48, canY + 4, Palette.HexOutlineVoid);

        if (tubeStyle == 0)
        {
            // Dual symmetrical breather tubes
            buf.DrawLine(16, canY + canH, 23, 46, "#e8b93c");
            buf.DrawLine(47, canY + canH, 41, 46, "#e8b93c");
        }
        else if (tubeStyle == 1)
        {
            // Crossed front chest tubes
            buf.DrawLine(16, canY + canH, 34, 47, "#e8b93c");
            buf.DrawLine(47, canY + canH, 30, 47, "#b5841d");
        }
        else if (tubeStyle == 2)
        {
            // Heavy left braided hose + right pressure gauge
            buf.DrawLine(16, canY + canH, 25, 47, "#e8b93c");
            buf.DrawLine(17, canY + canH, 26, 47, "#b5841d");
            buf.FillRect(46, canY + canH, 3, 3, Palette.HexClearanceTeal);
        }
        else
        {
            // Central chin respirator valve + twin hoses
            buf.FillRect(29, 37, 6, 5, Palette.VeldorBrassTrimRamp[2]);
            buf.DrawLine(29, 40, 22, 46, "#e8b93c");
            buf.DrawLine(35, 40, 42, 46, "#e8b93c");
        }
    }

    private static void RenderPrunjunCapAndGoggles(
        PixelBuffer buf,
        PortraitTraitProfile traits,
        Func<int, int, string[], float, string> sampleFn)
    {
        // 4 Crooked Salvaged Flight Cap Tilt Variants
        int capVariant = traits.SpeciesSubVariantA % 4;
        ReadOnlySpan<(int X, int Y)> crookedCap = capVariant switch
        {
            0 => [(18, 20), (21, 11), (35, 8), (44, 14), (43, 21)], // Tilted left-high
            1 => [(19, 21), (20, 14), (29, 8), (43, 11), (45, 20)], // Tilted right-high
            2 => [(18, 19), (20, 11), (44, 11), (46, 19)],          // Flat battered mechanic cap
            _ => [(17, 23), (21, 10), (32, 9), (43, 10), (47, 23), (43, 19), (21, 19)] // Flapped salvage helmet
        };

        string[] capRamp = (capVariant % 2 == 0)
            ? Palette.FreeTraderPrimarySlateRamp
            : Palette.KrushvakSecondaryEarthBrownRamp;
        buf.FillPoly(crookedCap, (px, py) =>
            sampleFn(px, py, capRamp, 0.85f - (py - 8) / 15.0f));

        // Goggle strap across forehead
        buf.DrawLine(19, 19, 45, 18, "#2e2414");
        buf.DrawLine(19, 20, 45, 19, "#181a1c");

        // Asymmetrical welding goggles: vary which side is oversized and swap lens tints
        bool bigOnLeft = (traits.SpeciesSubVariantC % 2) == 0;
        int leftR = bigOnLeft ? 4 : 3;
        int rightR = bigOnLeft ? 3 : 4;
        int leftY = bigOnLeft ? 19 : 18;
        int rightY = bigOnLeft ? 18 : 19;

        buf.FillEllipse(27, leftY, leftR, leftR, Palette.VeldorBrassTrimRamp[1]);
        buf.FillEllipse(27, leftY, leftR - 1, leftR - 1, (px, py, _, _, dist) =>
            bigOnLeft
                ? sampleFn(px, py, Palette.PrunjunCockpitGlassRamp, 0.90f - dist * 0.5f)
                : "#f59e0b");
        buf.Set(26, leftY - 1, "#e0f2fe");

        buf.FillEllipse(36, rightY, rightR, rightR, Palette.VeldorBrassTrimRamp[1]);
        buf.FillEllipse(36, rightY, rightR - 1, rightR - 1, (px, py, _, _, dist) =>
            !bigOnLeft
                ? sampleFn(px, py, Palette.PrunjunCockpitGlassRamp, 0.90f - dist * 0.5f)
                : "#f59e0b");
        buf.Set(35, rightY - 1, "#fde047");
    }

    private static void RenderBridgeBackdrop(
        PixelBuffer buf,
        PortraitSpecies species,
        string factionPrefix,
        bool isCrayon,
        DeterministicRng rng)
    {
        string bgDark = Palette.HexDeepSpaceVoid;
        string bgLight = "#18202c";
        string accentCol = Palette.HexWeatheredGunmetalEdge;

        string upperPrefix = factionPrefix.ToUpperInvariant();
        if (upperPrefix == "KDF-" || species == PortraitSpecies.Kordani)
        {
            bgDark = "#071820";
            bgLight = "#0c2b36";
            accentCol = "#1b6b73";
        }
        else if (upperPrefix == "BMC-" || species == PortraitSpecies.Brengop)
        {
            bgDark = "#1c180b";
            bgLight = "#2e2712";
            accentCol = "#695f18";
        }
        else if (upperPrefix == "KRW-" || species == PortraitSpecies.Krushvak)
        {
            bgDark = "#141d12";
            bgLight = "#22301e";
            accentCol = "#4a693b";
        }
        else if (upperPrefix == "ZNI-")
        {
            bgDark = "#1f0c16";
            bgLight = "#361424";
            accentCol = "#b38424";
        }

        for (int y = 0; y < Constants.PortraitHeight; y++)
        {
            float ratio = 1.0f - (y / (float)Constants.PortraitHeight);
            for (int x = 0; x < Constants.PortraitWidth; x++)
            {
                string col = isCrayon ? bgDark : Dither.BayerDither(x, y, bgDark, bgLight, ratio);
                buf.Set(x, y, col);
            }
        }

        buf.DrawLine(10, 0, 10, 63, Palette.HexOutlineVoid);
        buf.DrawLine(11, 0, 11, 63, accentCol);
        buf.DrawLine(53, 0, 53, 63, Palette.HexOutlineVoid);
        buf.DrawLine(52, 0, 52, 63, accentCol);
        buf.DrawLine(0, 32, 10, 32, accentCol);
        buf.DrawLine(53, 32, 63, 32, accentCol);

        buf.Set(5, 12, Palette.HexCrtAmberPrimary);
        buf.Set(5, 15, Palette.HexClearanceTeal);
        buf.Set(58, 12, Palette.HexCrtAmberDimmed);
    }

    private static void ApplyLiveCrtScanlines(PixelBuffer buf)
    {
        for (int y = 1; y < buf.Height; y += 2)
        {
            buf.Set(0, y, Palette.HexOutlineVoid);
            buf.Set(buf.Width - 1, y, Palette.HexOutlineVoid);
        }
    }

    private static void ApplyCrudeCrayonOverlay(PixelBuffer buf, DeterministicRng rng)
    {
        buf.DrawLine(1, 2, 61, 1, Palette.HexCrtAmberPrimary);
        buf.DrawLine(62, 1, 60, 62, Palette.HexCrtAmberPrimary);
        buf.DrawLine(60, 62, 2, 61, Palette.HexCrtAmberPrimary);
        buf.DrawLine(2, 61, 1, 2, Palette.HexCrtAmberPrimary);

        for (int i = 0; i < 4; i++)
        {
            int sx = rng.NextInt(3, 14);
            int sy = rng.NextInt(6, 40);
            buf.DrawLine(sx, sy, sx + rng.NextInt(2, 6), sy + rng.NextInt(-3, 4), Palette.HexAlertCrimson);
        }
    }

    private static PixelBuffer ComposeHoloMode(PixelBuffer charBuf, bool useAmber)
    {
        PixelBuffer holo = new(Constants.PortraitWidth, Constants.PortraitHeight);
        string[] ramp = useAmber ? HoloAmber5Step : HoloCyan5Step;

        for (int y = 0; y < Constants.PortraitHeight; y++)
        {
            bool oddScanline = (y % 2) == 1;
            for (int x = 0; x < Constants.PortraitWidth; x++)
            {
                Color? srcCol = charBuf.GetRgba(x, y);
                int stepIdx;
                if (!srcCol.HasValue)
                {
                    stepIdx = oddScanline ? 0 : (((x + y) % 4 == 0) ? 1 : 0);
                }
                else
                {
                    float lum = Palette.GetLuminance(srcCol.Value);
                    stepIdx = Math.Clamp((int)MathF.Round(lum * 4.0f), 0, 4);
                    if (oddScanline && stepIdx > 0)
                    {
                        stepIdx--;
                    }
                }
                holo.Set(x, y, ramp[stepIdx]);
            }
        }

        string bracketCol = ramp[4];
        holo.DrawLine(2, 2, 8, 2, bracketCol);
        holo.DrawLine(2, 2, 2, 8, bracketCol);
        holo.DrawLine(55, 2, 61, 2, bracketCol);
        holo.DrawLine(61, 2, 61, 8, bracketCol);
        holo.DrawLine(2, 55, 2, 61, bracketCol);
        holo.DrawLine(2, 61, 8, 61, bracketCol);
        holo.DrawLine(55, 61, 61, 61, bracketCol);
        holo.DrawLine(61, 55, 61, 61, bracketCol);

        return holo;
    }

    private static PixelBuffer RenderStaticMode(PortraitRenderOptions options)
    {
        PixelBuffer buf = new(Constants.PortraitWidth, Constants.PortraitHeight);
        DeterministicRng snowRng = new($"{options.SeedId}:static:{options.EffectiveAnimBucket}");

        int tearBarY = snowRng.NextInt(6, 56);
        for (int y = 0; y < Constants.PortraitHeight; y++)
        {
            bool isScanlineDim = (y % 2) == 1;
            bool isTearBar = Math.Abs(y - tearBarY) <= 2;
            for (int x = 0; x < Constants.PortraitWidth; x++)
            {
                float noise = snowRng.NextFloat();
                if (isTearBar)
                    noise = Math.Min(0.99f, noise + 0.35f);
                if (isScanlineDim)
                    noise *= 0.65f;

                string col = Dither.SampleRamp(x, y, StaticSnowRamp, noise);
                buf.Set(x, y, col);
            }
        }

        buf.FillRect(2, 21, 60, 21, Palette.HexOutlineVoid);
        buf.DrawLine(2, 21, 61, 21, Palette.HexCrtAmberPrimary);
        buf.DrawLine(2, 41, 61, 41, Palette.HexCrtAmberPrimary);
        buf.DrawLine(2, 23, 61, 23, Palette.HexCrtAmberDimmed);
        buf.DrawLine(2, 39, 61, 39, Palette.HexCrtAmberDimmed);

        buf.DrawBitmapText(15, 25, "NO VISUAL", Palette.HexCrtAmberPrimary);
        buf.DrawBitmapText(21, 32, "SIGNAL", Palette.HexCrtAmberPrimary);

        return buf;
    }

    /// <summary>
    /// Dedicated hardcoded (zero RNG variation) 64x64 Kordani Security Officer portrait
    /// wearing an authoritarian peaked cap topped with a golden winged bird insignia.
    /// Used for the Day 3 Pre-Shift Kordani Live Viewscreen Hail.
    /// </summary>
    public static PixelBuffer RenderKordaniSecurityOfficerBuffer()
    {
        PixelBuffer charBuf = new(Constants.PortraitWidth, Constants.PortraitHeight);

        string[] skinRamp = Palette.KordaniSkinRamps[0];          // Cold steel-blue Kordani scales
        string[] coatRamp = Palette.KordaniSecondaryDarkSlateRamp; // Authoritarian dark slate tunic/cap
        string[] trimRamp = Palette.KordaniPrimaryBlueGreenRamp;   // KDF blue-green piping
        string[] goldRamp = Palette.ZenithTrimPolishedGoldRamp;    // Gold cap band, epaulettes & bird insignia

        static string Sample(int x, int y, string[] ramp, float t) => Dither.SampleRamp(x, y, ramp, t);

        // 1. High-Collared KDF Military Tunic & Armored Shoulders (y = 43..63)
        ReadOnlySpan<(int X, int Y)> shoulders =
        [
            (7, 63), (11, 47), (21, 43), (43, 43), (53, 47), (57, 63)
        ];
        charBuf.FillPoly(shoulders, (px, py) =>
        {
            float t = Math.Clamp(0.82f - (py - 43) / 24.0f - MathF.Abs(px - 32) / 36.0f, 0.08f, 0.92f);
            return Sample(px, py, coatRamp, t);
        });

        // High stiff authoritarian collar + diagonal KDF breast sash
        charBuf.FillRect(20, 41, 24, 6, (px, py) => Sample(px, py, trimRamp, 0.85f - MathF.Abs(px - 32) / 18.0f));
        charBuf.DrawLine(20, 41, 44, 41, goldRamp[2]);
        charBuf.DrawLine(22, 47, 36, 63, trimRamp[2]);
        charBuf.DrawLine(23, 47, 37, 63, goldRamp[1]);

        // Gold officer shoulder epaulettes
        charBuf.FillRect(11, 46, 8, 3, goldRamp[2]);
        charBuf.DrawLine(11, 46, 18, 46, goldRamp[3]);
        charBuf.FillRect(45, 46, 8, 3, goldRamp[1]);
        charBuf.DrawLine(45, 46, 52, 46, goldRamp[2]);

        // Crimson & gold security commander collar rank pips
        charBuf.Set(22, 44, Palette.HexAlertCrimson);
        charBuf.Set(24, 44, goldRamp[3]);
        charBuf.Set(26, 44, goldRamp[3]);
        charBuf.Set(38, 44, goldRamp[3]);
        charBuf.Set(40, 44, goldRamp[3]);
        charBuf.Set(42, 44, Palette.HexAlertCrimson);

        // Scaled Kordani neck
        ReadOnlySpan<(int X, int Y)> neck = [(26, 37), (38, 37), (39, 44), (25, 44)];
        charBuf.FillPoly(neck, (px, py) => Sample(px, py, skinRamp, 0.58f - MathF.Abs(px - 32) / 12.0f));

        // 2. Angular Reptilian Kordani Skull (x = 19..45, y = 18..44)
        ReadOnlySpan<(int X, int Y)> skull =
        [
            (20, 19), (44, 19), (45, 27), (41, 36), (37, 42), (32, 44), (27, 42), (23, 36), (19, 27)
        ];
        charBuf.FillPoly(skull, (px, py) =>
        {
            float dx = (px - 32) / 14.0f;
            float dy = (py - 29) / 15.0f;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            float t = Math.Clamp(0.90f - dist * 0.52f - dx * 0.16f - dy * 0.18f, 0.08f, 0.96f);
            return Sample(px, py, skinRamp, t);
        });

        // Cheek scale-ridge chevrons
        for (int i = 0; i < 3; i++)
        {
            int sy = 29 + i * 3;
            charBuf.DrawLine(21, sy, 25, sy + 2, skinRamp[0]);
            charBuf.DrawLine(21, sy + 1, 25, sy + 3, skinRamp[3]);
            charBuf.DrawLine(43, sy, 39, sy + 2, skinRamp[0]);
            charBuf.DrawLine(43, sy + 1, 39, sy + 3, skinRamp[3]);
        }

        // Sharp authoritarian furrowed V-brow + amber slit reptilian eyes
        charBuf.DrawLine(23, 24, 29, 26, skinRamp[0]);
        charBuf.DrawLine(41, 24, 35, 26, skinRamp[0]);

        charBuf.FillRect(24, 26, 5, 2, "#f59e0b");
        charBuf.Set(26, 26, Palette.HexOutlineVoid);
        charBuf.Set(26, 27, Palette.HexOutlineVoid);
        charBuf.Set(24, 26, "#fde047");

        charBuf.FillRect(35, 26, 5, 2, "#f59e0b");
        charBuf.Set(37, 26, Palette.HexOutlineVoid);
        charBuf.Set(37, 27, Palette.HexOutlineVoid);
        charBuf.Set(35, 26, "#fde047");

        // Sculpted nose bridge & compressed stern frown
        charBuf.DrawLine(31, 27, 31, 33, skinRamp[0]);
        charBuf.DrawLine(32, 27, 32, 33, skinRamp[3]);
        charBuf.Set(30, 33, Palette.HexOutlineVoid);
        charBuf.Set(34, 33, Palette.HexOutlineVoid);

        charBuf.DrawLine(27, 37, 37, 37, Palette.HexOutlineVoid);
        charBuf.Set(26, 38, Palette.HexOutlineVoid);
        charBuf.Set(38, 38, Palette.HexOutlineVoid);
        charBuf.DrawLine(29, 39, 35, 39, skinRamp[0]);

        // 3. Authoritarian Peaked Officer's Cap (y = 3..21)
        // High flared dark-slate crown
        ReadOnlySpan<(int X, int Y)> capCrown =
        [
            (13, 14), (17, 6), (25, 4), (32, 3), (39, 4), (47, 6), (51, 14), (46, 17), (18, 17)
        ];
        charBuf.FillPoly(capCrown, (px, py) =>
        {
            float t = Math.Clamp(0.84f - (py - 3) / 16.0f - MathF.Abs(px - 32) / 28.0f, 0.10f, 0.92f);
            return Sample(px, py, coatRamp, t);
        });

        // Crisp top crown piping in KDF teal-green
        charBuf.DrawLine(17, 6, 25, 4, trimRamp[3]);
        charBuf.DrawLine(25, 4, 32, 3, trimRamp[3]);
        charBuf.DrawLine(32, 3, 39, 4, trimRamp[2]);
        charBuf.DrawLine(39, 4, 47, 6, trimRamp[2]);

        // Oxidized gold/amber officer cap band (y = 15..16)
        charBuf.FillRect(18, 15, 28, 2, (px, py) =>
            py == 15 ? goldRamp[2] : goldRamp[0]);
        charBuf.DrawLine(18, 14, 46, 14, Palette.HexOutlineVoid);

        // Glossy dark peaked visor brim (y = 17..21) casting shadow over brow
        ReadOnlySpan<(int X, int Y)> visorBrim =
        [
            (16, 17), (48, 17), (45, 21), (32, 22), (19, 21)
        ];
        charBuf.FillPoly(visorBrim, (px, py) =>
            py == 18 && px >= 22 && px <= 40 ? coatRamp[2] : (py <= 19 ? coatRamp[0] : Palette.HexOutlineVoid));
        charBuf.DrawLine(21, 19, 36, 19, coatRamp[3]); // Crisp glossy visor catch-light

        // 4. Crisp Silver Winged Bird Insignia centered on top front of Peaked Cap (x = 23..41, y = 5..14)
        string[] silverRamp = Palette.ThaneTitaniumWhiteRamp;
        // Dark backing crest for contrast
        charBuf.FillRect(29, 7, 7, 7, Palette.HexOutlineVoid);
        // Bird head & beak facing left
        charBuf.FillRect(31, 6, 3, 2, silverRamp[3]);
        charBuf.Set(30, 6, silverRamp[2]); // Beak
        // Central body & breast shield
        charBuf.FillRect(31, 8, 3, 4, silverRamp[2]);
        charBuf.Set(32, 9, silverRamp[3]);
        // Tail chevron
        charBuf.DrawLine(30, 13, 34, 13, silverRamp[1]);
        charBuf.DrawLine(31, 12, 33, 12, silverRamp[2]);
        // Symmetrical swept authoritarian wings (left: x = 23..30, right: x = 34..41)
        // Top wing bar (bright silver highlight)
        charBuf.DrawLine(23, 7, 30, 8, silverRamp[3]);
        charBuf.DrawLine(34, 8, 41, 7, silverRamp[3]);
        // Mid wing feathers (silver base)
        charBuf.DrawLine(24, 8, 30, 9, silverRamp[2]);
        charBuf.DrawLine(34, 9, 40, 8, silverRamp[2]);
        charBuf.DrawLine(25, 9, 30, 10, silverRamp[2]);
        charBuf.DrawLine(34, 10, 39, 9, silverRamp[2]);
        // Lower notched wing tips (silver shadow)
        charBuf.DrawLine(26, 10, 29, 11, silverRamp[1]);
        charBuf.DrawLine(35, 11, 38, 10, silverRamp[1]);
        charBuf.Set(27, 11, silverRamp[0]);
        charBuf.Set(37, 11, silverRamp[0]);

        // 5. Final 1px dark silhouette outline
        charBuf.ApplyOutline(Palette.HexOutlineVoid);

        // 6. Compose over Kordani Security Command Bridge backdrop with live CRT scanlines
        PixelBuffer finalBuf = new(Constants.PortraitWidth, Constants.PortraitHeight);
        RenderBridgeBackdrop(finalBuf, PortraitSpecies.Kordani, "KDF-", isCrayon: false, new DeterministicRng("kdf-sec-bridge-fixed"));
        // Red security status pips on command bulkhead
        finalBuf.Set(5, 12, Palette.HexAlertCrimson);
        finalBuf.Set(58, 12, Palette.HexAlertCrimson);
        finalBuf.BlitFrom(charBuf);
        ApplyLiveCrtScanlines(finalBuf);

        return finalBuf;
    }
}
