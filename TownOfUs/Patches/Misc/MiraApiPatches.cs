using System.Collections;
using AmongUs.GameOptions;
using MiraAPI.Patches.Freeplay;
using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Networking;
using MiraAPI.Patches.Options;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using Reactor.Utilities;
using TownOfUs.Networking;
using TownOfUs.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfUs.Patches.Misc;

[HarmonyPatch]
public static class MiraApiPatches
{
    [HarmonyPatch(typeof(Helpers), nameof(Helpers.IsRoleBlacklisted))]
    [HarmonyPrefix]
    public static bool IsRoleBlacklisted(RoleBehaviour role, ref bool __result)
    {
        // Since TOU Engineer is just vanilla engineer with the fix mechanic, no need to have two engis around!
        if (role.Role is RoleTypes.Engineer)
        {
            __result = true;
            return false;
        }

        if (MiscUtils.CurrentGamemode() is TouGamemode.HideAndSeek && (role.Role is RoleTypes.Detective ||
                                                                       role.Role is RoleTypes.GuardianAngel ||
                                                                       role.Role is RoleTypes.Noisemaker ||
                                                                       role.Role is RoleTypes.Phantom ||
                                                                       role.Role is RoleTypes.Scientist ||
                                                                       role.Role is RoleTypes.Shapeshifter ||
                                                                       role.Role is RoleTypes.Tracker ||
                                                                       role.Role is RoleTypes.Viper))
        {
            __result = true;
            return false;
        }
        return true;
    }
    [HarmonyPatch(typeof(TeamIntroConfiguration), nameof(TeamIntroConfiguration.Neutral.IntroTeamTitle), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool NeutralTeamPrefix(ref string __result)
    {
        __result = TouLocale.Get("NeutralKeyword").ToUpperInvariant();
        return false;
    }
    [HarmonyPatch(typeof(TaskAdderPatches), nameof(TaskAdderPatches.NeutralName), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool NeutralNamePrefix(ref string __result)
    {
        __result = TouLocale.Get("NeutralKeyword");
        return false;
    }
    [HarmonyPatch(typeof(TaskAdderPatches), nameof(TaskAdderPatches.ModifiersName), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool ModifierNamePrefix(ref string __result)
    {
        __result = TouLocale.Get("Modifiers");
        return false;
    }

    [HarmonyPatch(typeof(CustomMurderRpc), nameof(CustomMurderRpc.CustomMurder))]
    [HarmonyPrefix]
    public static bool CustomMurderPatch(PlayerControl source)
    {
        if (LobbyBehaviour.Instance)
        {
            MiscUtils.RunAnticheatWarning(source);
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(CustomMurderRpc), nameof(CustomMurderRpc.CustomMurder))]
    [HarmonyFinalizer]
    public static Exception CustomMurderFinalizer(
        Exception __exception,
        PlayerControl source,
        PlayerControl target,
        MurderResultFlags resultFlags,
        bool createDeadBody)
    {
        if (__exception != null && resultFlags.HasFlag(MurderResultFlags.Succeeded))
        {
            Error($"Exception in CustomMurder, running cleanup: {__exception}");
            RunKillCoroutineCleanup(source, target, createDeadBody);
        }

        return null!;
    }

    [HarmonyPatch(typeof(CustomMurderRpc), nameof(CustomMurderRpc.RpcCustomMurder), typeof(PlayerControl), typeof(PlayerControl), typeof(MeetingCheck), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool))]
    [HarmonyPrefix]
    public static bool RpcAltCustomMurderPatch(
        this PlayerControl source,
        PlayerControl target,
        MeetingCheck inMeeting,
        bool didSucceed = true,
        bool resetKillTimer = true,
        bool createDeadBody = true,
        bool teleportMurderer = true,
        bool showKillAnim = true,
        bool playKillSound = true)
    {
        if (LobbyBehaviour.Instance)
        {
            MiscUtils.RunAnticheatWarning(source);
            return false;
        }
        var murderResultFlags = didSucceed ? MurderResultFlags.Succeeded : MurderResultFlags.FailedError;

        var beforeMurderEvent = new BeforeMurderEvent(source, target, inMeeting);
        MiraEventManager.InvokeEvent(beforeMurderEvent);
        var isMeetingActive = MeetingHud.Instance != null || ExileController.Instance != null;
        if ((inMeeting is MeetingCheck.ForMeeting && !isMeetingActive) || (inMeeting is MeetingCheck.OutsideMeeting && isMeetingActive))
        {
            beforeMurderEvent.Cancel();
        }

        if (beforeMurderEvent.IsCancelled)
        {
            murderResultFlags = MurderResultFlags.FailedError;
        }

        // Track kill cooldown before CustomMurder for Time Lord rewind
        CustomTouMurderRpcs.RecordedKillCooldown = -1f;
        if (resetKillTimer && source.AmOwner && source.Data?.Role?.CanUseKillButton == true)
        {
            CustomTouMurderRpcs.RecordedKillCooldown = source.killTimer;
        }

        if (!PlayerControl.LocalPlayer.IsHost())
        {
            return false;
        }

        CustomMurderRpc.RpcConfirmCustomMurder(
            PlayerControl.LocalPlayer,
            source,
            target,
            murderResultFlags,
            resetKillTimer,
            createDeadBody,
            teleportMurderer,
            showKillAnim,
            playKillSound);
        return false;
    }

    [HarmonyPatch(typeof(CustomMurderRpc), nameof(CustomMurderRpc.RpcConfirmCustomMurder), typeof(PlayerControl), typeof(PlayerControl), typeof(PlayerControl), typeof(MurderResultFlags), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool))]
    [HarmonyPrefix]
    public static bool RpcConfirmCustomMurderPatch(
        this PlayerControl host,
        PlayerControl source,
        PlayerControl target,
        MurderResultFlags murderResultFlags,
        bool resetKillTimer = true,
        bool createDeadBody = true,
        bool teleportMurderer = true,
        bool showKillAnim = true,
        bool playKillSound = true)
    {
        if (LobbyBehaviour.Instance)
        {
            MiscUtils.RunAnticheatWarning(source);
            return false;
        }
        if (!host.IsHost() || target.HasDied())
        {
            return false;
        }

        var murderResultFlags2 = MurderResultFlags.DecisionByHost | murderResultFlags;

        source.CustomMurder(
            target,
            murderResultFlags2,
            resetKillTimer,
            createDeadBody,
            teleportMurderer,
            showKillAnim,
            playKillSound);

        // Record kill cooldown change after CustomMurder if it was reset
        if (CustomTouMurderRpcs.RecordedKillCooldown > -1f && resetKillTimer && source.AmOwner && source.Data?.Role?.CanUseKillButton == true)
        {
            Coroutines.Start(CustomTouMurderRpcs.CoRecordKillCooldownAfterCustomMurder(source, CustomTouMurderRpcs.RecordedKillCooldown));
        }
        return false;
    }

    [HarmonyPatch(typeof(RoleSettingMenuPatches), nameof(RoleSettingMenuPatches.ClosePatch))]
    [HarmonyPrefix]
#pragma warning disable S3400
    public static bool MiraClosePatch()
#pragma warning restore S3400
    {
        // Patching this for now
        return false;
    }

    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Start))]
    [HarmonyPostfix]
    public static void OpenPatch()
    {
        HudManager.Instance.PlayerCam.OverrideScreenShakeEnabled = false;
    }

    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Close))]
    [HarmonyPostfix]
    public static void ClosePatch()
    {
        HudManager.Instance.PlayerCam.OverrideScreenShakeEnabled = true;
    }

    /// <summary>
    /// Wraps MiraAPI's CoPerformCustomKill coroutine to guarantee that AfterMurderEvent fires
    /// and cleanup occurs even if the coroutine throws an exception mid-execution.
    /// This fixes Mystic death notifications, dead body visibility, and all other
    /// AfterMurderEvent-dependent handlers when the kill animation coroutine fails.
    /// </summary>
    private static bool _bypassCoPerformPatch;

    [HarmonyPatch(typeof(CustomMurderRpc), nameof(CustomMurderRpc.CoPerformCustomKill))]
    [HarmonyPrefix]
    public static bool CoPerformCustomKillSafetyPatch(
        KillAnimation anim,
        PlayerControl source,
        PlayerControl target,
        bool createDeadBody,
        bool teleportMurderer,
        ref IEnumerator __result)
    {
        if (_bypassCoPerformPatch)
        {
            return true;
        }

        _bypassCoPerformPatch = true;
        var original = CustomMurderRpc.CoPerformCustomKill(anim, source, target, createDeadBody, teleportMurderer);
        _bypassCoPerformPatch = false;

        __result = CoSafePerformCustomKill(original, source, target, createDeadBody);
        return false;
    }

    private static IEnumerator CoSafePerformCustomKill(
        IEnumerator original,
        PlayerControl source,
        PlayerControl target,
        bool createDeadBody)
    {
        while (true)
        {
            object current;
            try
            {
                if (!original.MoveNext())
                {
                    yield break;
                }

                current = original.Current;
            }
            catch (Exception ex)
            {
                Error($"Error in CoPerformCustomKill, running cleanup: {ex}");
                RunKillCoroutineCleanup(source, target, createDeadBody);
                yield break;
            }

            yield return current;
        }
    }

    private static void RunKillCoroutineCleanup(
        PlayerControl source,
        PlayerControl target,
        bool createDeadBody)
    {
        try
        {
            // Enable the dead body if it was created but not yet enabled
            DeadBody? deadBody = null;
            if (createDeadBody)
            {
                foreach (var body in Object.FindObjectsOfType<DeadBody>())
                {
                    if (body.ParentId == target.PlayerId)
                    {
                        deadBody = body;
                        if (!body.enabled)
                        {
                            body.enabled = true;
                        }

                        break;
                    }
                }
            }

            // Fire AfterMurderEvent that the failed coroutine never reached
            var afterMurderEvent = new AfterMurderEvent(source, target, deadBody);
            MiraEventManager.InvokeEvent(afterMurderEvent);

            // Restore movement
            KillAnimation.SetMovement(source, true);
            KillAnimation.SetMovement(target, true);

            // Cleanup participant state
            if (source.AmOwner || target.AmOwner)
            {
                var cam = Camera.main?.GetComponent<FollowerCamera>();
                if (cam != null)
                {
                    cam.Locked = false;
                }

                PlayerControl.LocalPlayer.isKilling = false;
                source.isKilling = false;
            }
        }
        catch (Exception cleanupEx)
        {
            Error($"Error in CoPerformCustomKill cleanup: {cleanupEx}");
        }
    }
}
