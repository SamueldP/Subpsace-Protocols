using System.Text.Json.Serialization;

namespace SubspaceProtocols.Data;

public sealed class ForumPowerCondition
{
    [JsonPropertyName("prefix")]
    public string Prefix { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty; // "Weakened", "Dominant", "Crisis"
}

public sealed class ForumPostModel
{
    [JsonPropertyName("handle")]
    public string Handle { get; set; } = string.Empty;

    [JsonPropertyName("roleTag")]
    public string RoleTag { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}

public sealed class ForumThreadModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("boardTag")]
    public string BoardTag { get; set; } = "GENERAL";

    [JsonPropertyName("minDay")]
    public int MinDay { get; set; } = 1;

    [JsonPropertyName("maxDay")]
    public int MaxDay { get; set; } = 30;

    [JsonPropertyName("requiredFlags")]
    public List<string> RequiredFlags { get; set; } = [];

    [JsonPropertyName("powerScoreCondition")]
    public ForumPowerCondition? PowerScoreCondition { get; set; }

    [JsonPropertyName("posts")]
    public List<ForumPostModel> Posts { get; set; } = [];
}

public sealed class ForumDatabase
{
    [JsonPropertyName("boardName")]
    public string BoardName { get; set; } = "VG3-NET // STATION COMMUNAL FORUM v1.4";

    [JsonPropertyName("threads")]
    public List<ForumThreadModel> Threads { get; set; } = [];

    public static ForumDatabase Load() =>
        DataLoader.LoadJson<ForumDatabase>("forum.json") ?? new ForumDatabase();
}

public sealed class CovertMissionSpec
{
    public const string TypeDelivery = "Delivery";
    public const string TypeIntercept = "Intercept";

    [JsonPropertyName("missionId")]
    public string MissionId { get; set; } = string.Empty;

    [JsonPropertyName("missionType")]
    public string MissionType { get; set; } = TypeDelivery;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("assigningFaction")]
    public string AssigningFaction { get; set; } = string.Empty;

    [JsonPropertyName("minAvailableDay")]
    public int MinAvailableDay { get; set; } = 7;

    [JsonPropertyName("autoActivateOnHack")]
    public bool AutoActivateOnHack { get; set; } = false;

    [JsonPropertyName("triggerEmailDay")]
    public int TriggerEmailDay { get; set; } = 0;

    [JsonPropertyName("triggerEmailPhase")]
    public string TriggerEmailPhase { get; set; } = "Evening";

    [JsonPropertyName("scheduledDeliveryDay")]
    public int ScheduledDeliveryDay { get; set; } = 8;

    [JsonPropertyName("targetPrefix")]
    public string TargetPrefix { get; set; } = "FTV-";

    [JsonPropertyName("targetRegistration")]
    public string TargetRegistration { get; set; } = string.Empty;

    [JsonPropertyName("targetShipName")]
    public string TargetShipName { get; set; } = string.Empty;

    [JsonPropertyName("slatePayloadLabel")]
    public string SlatePayloadLabel { get; set; } = "COVERT SLATE";

    [JsonPropertyName("payloadDescription")]
    public string PayloadDescription { get; set; } = string.Empty;

    [JsonPropertyName("encryptedSubject")]
    public string EncryptedSubject { get; set; } = string.Empty;

    [JsonPropertyName("decryptedBody")]
    public string DecryptedBody { get; set; } = string.Empty;

    [JsonPropertyName("rewardScrip")]
    public int RewardScrip { get; set; } = 45;

    [JsonPropertyName("rewardFactionPrefix")]
    public string RewardFactionPrefix { get; set; } = string.Empty;

    [JsonPropertyName("rewardRepDelta")]
    public int RewardRepDelta { get; set; } = 0;

    [JsonPropertyName("targetPowerDelta")]
    public int TargetPowerDelta { get; set; } = -1;

    [JsonPropertyName("grantDoubleAgentTrust")]
    public bool GrantDoubleAgentTrust { get; set; } = false;

    [JsonIgnore]
    public bool IsDeliveryMission =>
        !MissionType.Equals(TypeIntercept, StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsInterceptMission =>
        MissionType.Equals(TypeIntercept, StringComparison.OrdinalIgnoreCase);

    public bool MatchesInterceptSlate(Core.ContrabandPocketItem? item)
    {
        if (item == null || item.IsBurnt || item.ItemType != Core.ContrabandPocketItem.TypeCopiedDataSlate)
            return false;

        if (!string.IsNullOrWhiteSpace(TargetRegistration))
        {
            return item.TargetShipRegistration.Equals(TargetRegistration, StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(TargetPrefix) &&
               item.TargetShipPrefix.Equals(TargetPrefix, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class CovertMissionDatabase
{
    private static CovertMissionDatabase? _cachedInstance;
    public static int DiskLoadCount { get; private set; }

    [JsonPropertyName("missions")]
    public List<CovertMissionSpec> Missions { get; set; } = [];

    public static CovertMissionDatabase Load(bool forceReload = false)
    {
        if (!forceReload && _cachedInstance != null)
            return _cachedInstance;

        DiskLoadCount++;
        _cachedInstance = DataLoader.LoadJson<CovertMissionDatabase>("covert_missions.json") ?? new CovertMissionDatabase();
        return _cachedInstance;
    }

    public static CovertMissionDatabase Reload() => Load(forceReload: true);
}
