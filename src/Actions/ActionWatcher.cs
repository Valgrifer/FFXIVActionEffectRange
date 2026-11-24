using ActionEffectRange.Actions.Data;
using ActionEffectRange.Actions.EffectRange;
using ActionEffectRange.Drawing;
using ActionEffectRange.Helpers;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Collections.Generic;

namespace ActionEffectRange.Actions
{
    internal static unsafe class ActionWatcher
    {
        private const float SeqExpiry = 2.5f; // this is arbitrary...
        private const int MaxSkippedSeqs = 256;

        private static ushort lastRecordedSeq = 0;
        private static ushort lastReceivedMainSeq = 0;

        private static readonly ActionSeqRecord playerActionSeqs = new(5);
        private static readonly HashSet<ushort> skippedSeqs = new();

        private static readonly Hook<ActionManager.Delegates.UseAction>? UseActionHook;
        private static readonly Hook<ActionManager.Delegates.UseActionLocation>? UseActionLocationHook;
        private static readonly Hook<ActionEffectHandler.Delegates.Receive>? ReceiveActionEffectHook;


        // UseAction is called when the player uses an action (hotbar, macro, other plugins...).
        // If the action is queued, it returns true without executing it, and is called again
        //  later (mode = Queue) when the queued action is actually executed.
        // UseAction internally calls UseActionLocation, and the latter is also called directly
        //  for ground targeted actions.
        // An action is actually executed (and sent to the server) when ActionManager's
        //  LastUsedActionSequence changes during the call; this is where we snapshot
        //  positions (replaces the old SendAction hook).
        private static bool UseActionDetour(ActionManager* actionManager,
            ActionType actionType, uint actionId, ulong targetId, uint extraParam,
            ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
        {
            var seqBefore = ActionManagerHelper.CurrentSeq;
            var ret = UseActionHook!.Original(actionManager, actionType, actionId,
                targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);

            try
            {
                LogUserDebug($"UseAction => actionType={actionType}, " +
                    $"actionId={actionId}, targetId={targetId:X}, mode={mode}, ret={ret}, " +
                    $"seq {seqBefore}->{ActionManagerHelper.CurrentSeq}");

                OnPossiblyExecuted(seqBefore, actionType, actionId, targetId, null);

                if (ret) CheckDrawWhenCasting(actionType);
            }
            catch (Exception e)
            {
                PluginLog.Error($"{e}");
            }

            return ret;
        }

        private static bool UseActionLocationDetour(ActionManager* actionManager,
            ActionType actionType, uint actionId, ulong targetId, Vector3* location,
            uint extraParam, byte a7)
        {
            var seqBefore = ActionManagerHelper.CurrentSeq;
            var ret = UseActionLocationHook!.Original(actionManager, actionType, actionId,
                targetId, location, extraParam, a7);

            try
            {
                LogUserDebug($"UseActionLocation => actionType={actionType}, " +
                    $"actionId={actionId}, targetId={targetId:X}, " +
                    $"loc={(location != null ? *location : Vector3.Zero)}, ret={ret}, " +
                    $"seq {seqBefore}->{ActionManagerHelper.CurrentSeq}");

                OnPossiblyExecuted(seqBefore, actionType, actionId, targetId,
                    location != null ? *location : null);
            }
            catch (Exception e)
            {
                PluginLog.Error($"{e}");
            }

            return ret;
        }

        private static void OnPossiblyExecuted(ushort seqBefore,
            ActionType actionType, uint actionId, ulong targetId, Vector3? location)
        {
            var seq = ActionManagerHelper.CurrentSeq;
            // Not executed (queued, failed...), or already handled in a nested call
            if (seq == seqBefore || seq == lastRecordedSeq) return;
            lastRecordedSeq = seq;

            if (actionType == ActionType.Action)
                actionId = ActionManager.Instance()->GetAdjustedActionId(actionId);

            LogUserDebug($"Action executed => action={actionId}, type={actionType}, " +
                $"target={targetId:X}, seq={seq}");

            if (!ShouldProcessAction((byte)actionType, actionId))
            {
                AddSkippedSeq(seq);
                return;
            }

            var actionCategory = ActionData.GetActionCategory(actionId);
            if (!ShouldDrawForActionCategory(actionCategory))
            {
                LogUserDebug($"---Skip action#{actionId}: " +
                    $"Not drawing for actions of category {actionCategory}");
                AddSkippedSeq(seq);
                return;
            }

            var isGT = ActionData.GetActionExcelRow(actionId)?.TargetArea ?? false;
            if (isGT && !Config.DrawGT)
            {
                LogUserDebug($"---Skip: Config: disabled for GT actions");
                AddSkippedSeq(seq);
                return;
            }

            SeqSnapshot snapshot;
            if (isGT && location.HasValue)
                snapshot = new(seq, LocalPlayer!.GameObjectId, location.Value);
            else if (!IsValidGameObjectId(targetId) || targetId == LocalPlayer!.GameObjectId)
                snapshot = new(seq);
            else
            {
                var target = ObjectTable.SearchById(targetId);
                if (target == null)
                {
                    LogUserDebug($"---Cannot find target #{targetId:X} for action#{actionId}; " +
                        "will use positions at effect time");
                    return;
                }
                snapshot = new(seq, target.GameObjectId, target.Position);
            }

            LogUserDebug($"---Recorded {snapshot}");
            playerActionSeqs.Add(new(actionId, snapshot, false));
        }

        // Processing draw-when-casting; when applicable, drawing is triggered immediately
        private static void CheckDrawWhenCasting(ActionType actionType)
        {
            if (!DrawWhenCasting) return;

            if (!ActionManagerHelper.IsCasting)
            {
                LogUserDebug($"---Skip: not casting");
                return;
            }

            var castActionId = ActionManagerHelper.CastingActionId;

            if (!ShouldProcessAction((byte)actionType, castActionId))
                return;

            var actionIdsToDraw = new List<uint> { castActionId };
            if (ActionData.TryGetActionWithAdditionalEffects(
                castActionId, out var additionals))
                actionIdsToDraw.AddRange(additionals);

            foreach (var a in actionIdsToDraw)
            {
                var erdata = EffectRangeDataManager.NewData(a);

                if (erdata == null)
                {
                    PluginLog.Error($"Cannot get data for action#{a}");
                    continue;
                }

                if (!ShouldDrawForActionCategory(erdata.Category, true))
                {
                    LogUserDebug($"---Skip action#{erdata.ActionId}: " +
                        $"Not drawing for actions of category {erdata.Category}");
                    continue;
                }

                erdata = EffectRangeDataManager.CustomiseEffectRangeData(erdata);
                if (!CheckShouldDrawPostCustomisation(erdata)) continue;

                var seq = ActionManagerHelper.CurrentSeq;
                float rotation = ActionManagerHelper.CastRotation;

                if (erdata.IsGTAction)
                {
                    var targetPos = ActionManagerHelper.CastTargetPosition;
                    LogUserDebug($"UseAction => Triggering draw-when-casting, " +
                        $"CastingActionId={castActionId}, GT action, " +
                        $"CastPosition={targetPos}, CastRotation={rotation}");
                    EffectRangeDrawing.AddEffectRangeToDraw(seq,
                        DrawTrigger.Casting, erdata, LocalPlayer!.Position,
                        targetPos, rotation);
                }
                else
                {
                    var castTargetId = ActionManagerHelper.CastTargetObjectId;
                    LogUserDebug($"UseAction => Triggering draw-when-casting, " +
                        $"CastingActionId={castActionId}, " +
                        $"CastTargetObjectId={castTargetId:X}, CastRotation={rotation}");

                    IGameObject? target = null;
                    if (castTargetId == LocalPlayer!.GameObjectId)
                        target = LocalPlayer;
                    else if (IsValidGameObjectId(castTargetId))
                        target = ObjectTable.SearchById(castTargetId);

                    if (target != null)
                    {
                        EffectRangeDrawing.AddEffectRangeToDraw(seq,
                            DrawTrigger.Casting, erdata, LocalPlayer!.Position,
                            target.Position, rotation);
                    }
                    else LogUserDebug($"---Failed: Target #{castTargetId:X} not found");
                }
            }
        }


        private static void ReceiveActionEffectDetour(uint casterEntityId,
            Character* casterPtr, Vector3* targetPos, ActionEffectHandler.Header* header,
            ActionEffectHandler.TargetEffects* effects, GameObjectId* targetEntityIds)
        {
            ReceiveActionEffectHook!.Original(casterEntityId, casterPtr,
                targetPos, header, effects, targetEntityIds);

            try
            {
                if (header == null)
                {
                    PluginLog.Error("ReceiveActionEffect: effectHeader ptr is null");
                    return;
                }

                var headerTargetId = (ulong)header->AnimationTargetId;
                var actionId = header->ActionId;
                var sourceSeq = header->SourceSequence;
                var position = targetPos != null ? *targetPos : Vector3.Zero;

                LogUserDebug($"ReceiveActionEffect => " +
                    $"source={casterEntityId:X}, target={headerTargetId:X}, " +
                    $"action={actionId}, seq={sourceSeq}, pos={position}");

                if (sourceSeq > 0)
                {
                    lastReceivedMainSeq = sourceSeq;
                    if (skippedSeqs.Contains(sourceSeq))
                    {
                        LogUserDebug($"---Skip: not processing Seq#{sourceSeq}");
                        return;
                    }
                }

                if (!IsPlayerLoaded)
                {
                    LogUserDebug($"---Skip: PC not loaded");
                    return;
                }

                var localPlayer = LocalPlayer!;
                if (casterEntityId != localPlayer.EntityId
                    && (!PetWatcher.HasPetPresent
                        || PetWatcher.GetPetObjectId() != casterEntityId))
                {
                    LogUserDebug($"---Skip: Effect triggered by others");
                    return;
                }

                var erdata = EffectRangeDataManager.NewData(actionId);
                if (erdata == null)
                {
                    PluginLog.Error($"Cannot get data for action#{actionId}");
                    return;
                }

                // Some additional effects (e.g. #29706 additional effect for Pneuma pvp)
                // have ActionCategory=0
                if (!ShouldDrawForActionCategory(erdata.Category, true))
                {
                    LogUserDebug($"---Skip action#{erdata.ActionId}: " +
                        $"Not drawing for actions of category {erdata.Category}");
                    return;
                }

                erdata = EffectRangeDataManager.CustomiseEffectRangeData(erdata);

                if (!CheckShouldDrawPostCustomisation(erdata)) return;

                var mainSeq = sourceSeq > 0 ? sourceSeq : lastReceivedMainSeq;

                if (casterEntityId == localPlayer.EntityId)
                {
                    // Source is pc

                    // TODO: config on/off: auto triggered effects
                    //  (such as effects on time elapsed, receiving damage, ...)
                    bool drawForAuto = true; // placeholder

                    ActionSeqInfo? seqInfo = null;

                    // For additional effects, received data always has seq=0
                    // Match seq using predefined mapping and some heuristics
                    if (!ActionData.ShouldNotUseCachedSeq(actionId))
                        seqInfo = FindRecordedSeqInfo(sourceSeq, actionId);

                    if (seqInfo != null)
                    {
                        // Additional effect may have different target (e.g. self vs targeted enemy)
                        Vector3 drawTargetPos = erdata.IsGTAction
                            ? position
                            : (headerTargetId == localPlayer.GameObjectId
                                ? seqInfo.SeqSnapshot.PlayerPosition
                                : seqInfo.SeqSnapshot.TargetPosition);

                        LogUserDebug($"---Adding DrawData for action #{actionId} " +
                            $"from player, using SeqSnapshot#{seqInfo.Seq}");
                        EffectRangeDrawing.AddEffectRangeToDraw(seqInfo.Seq,
                            DrawTrigger.Used, erdata, seqInfo.SeqSnapshot.PlayerPosition,
                            drawTargetPos, seqInfo.SeqSnapshot.PlayerRotation);
                    }
                    else if (drawForAuto)
                    {
                        LogUserDebug($"---Adding DrawData for action #{actionId} " +
                            $"from player, using current position info");

                        if (erdata.IsGTAction)
                            EffectRangeDrawing.AddEffectRangeToDraw(mainSeq,
                                DrawTrigger.Used, erdata, localPlayer.Position,
                                position, localPlayer.Rotation);
                        else
                        {
                            IGameObject? target = null;
                            if (headerTargetId == localPlayer.GameObjectId) // Self-targeting
                                target = localPlayer;
                            else if (IsValidGameObjectId(headerTargetId))
                                target = ObjectTable.SearchById(headerTargetId);

                            if (target != null)
                            {
                                EffectRangeDrawing.AddEffectRangeToDraw(mainSeq,
                                    DrawTrigger.Used, erdata, localPlayer.Position,
                                    target.Position, localPlayer.Rotation);
                            }
                            else LogUserDebug($"---Failed: Target #{headerTargetId:X} not found");
                        }
                    }
                    else LogUserDebug($"---Skip: Not drawing for auto-triggered action #{actionId}");
                }
                else
                {
                    // Source may be player's pet/pet-like object

                    // NOTE: Always use current position infos here.
                    // Due to potential delay, info snapshot at the time
                    //  player action is used is not accurate for pet actions as well.
                    // E.g., when pet is moving, any other action will be delayed;
                    //  once the pet is settled on a location, the character positions
                    //  are snapshot and pet action is processed based on this snapshot;
                    // but this also means the snapshot produced when player used
                    //  the "parent" action is already out-of-date.

                    // Just ignore if the pet is no longer present at this point (e.g. due to delay).
                    // Not very common as the game already defers removing pet objects
                    //  possibly to account for delays
                    var pet = PetWatcher.GetPet();
                    if (pet != null && pet.EntityId == casterEntityId)
                    {
                        var petGroup = PetWatcher.GetCurrentPetGroup();
                        if (!Config.ShouldDrawPetActions(petGroup))
                        {
                            LogUserDebug($"---Skip: Drawing for action#{actionId} " +
                                $"from {petGroup} pets configured OFF");
                            return;
                        }
                        if (PetWatcher.IsCurrentPetNameless()
                            && !Config.DrawGT)
                        {
                            // Assuming all nameless pets are ground placed objects ...
                            LogUserDebug($"---Skip: Drawing for action#{actionId} " +
                                "from possibly ground placed object configured OFF");
                            return;
                        }

                        // TODO: Check if the effect is auto-triggered if it is from placed object?
                        // (Assuming placed obj does not move, cached seq snapshot can be used.)
                        // (Configurable opt)

                        LogUserDebug($"---Add DrawData for action #{actionId} " +
                            $"from pet / pet-like object #{casterEntityId:X}, using current position info");

                        if (erdata.IsGTAction)
                            EffectRangeDrawing.AddEffectRangeToDraw(mainSeq,
                                DrawTrigger.Used, erdata, localPlayer.Position,
                                position, localPlayer.Rotation);
                        else
                        {
                            IGameObject? target = null;
                            if (headerTargetId == pet.GameObjectId) // Pet self-targeting
                                target = pet;
                            else if (headerTargetId == localPlayer.GameObjectId)
                                target = localPlayer;
                            else if (IsValidGameObjectId(headerTargetId))
                                target = ObjectTable.SearchById(headerTargetId);

                            if (target != null)
                            {
                                EffectRangeDrawing.AddEffectRangeToDraw(mainSeq,
                                    DrawTrigger.Used, erdata,
                                    pet.Position, target.Position, pet.Rotation);
                            }
                            else LogUserDebug($"---Failed: Target #{headerTargetId:X} not found");
                        }
                    }
                    else LogUserDebug($"---Skip: source actor #{casterEntityId:X} not matching pc or pet");
                }
            }
            catch (Exception e)
            {
                PluginLog.Error($"{e}");
            }
        }


        #region Checks

        private static bool ShouldProcessAction(byte actionType, uint actionId)
        {
            if (!IsPlayerLoaded)
            {
                LogUserDebug($"---Skip: PC not loaded");
                return false;
            }
            if (!ShouldProcessActionType(actionType)
                || !ShouldProcessAction(actionId))
            {
                LogUserDebug($"---Skip: Not processing " +
                    $"action#{actionId}, ActionType={actionType}");
                return false;
            }
            return true;
        }

        private static bool ShouldProcessActionType(byte actionType)
            => actionType == (byte)ActionType.Action
            || actionType == (byte)ActionType.PvPAction;

        private static bool ShouldProcessAction(uint actionId)
            => !ActionData.IsActionBlacklisted(actionId);


        private static bool ShouldDrawForActionCategory(
            Enums.ActionCategory actionCategory, bool allowCateogry0 = false)
            => ActionData.IsCombatActionCategory(actionCategory)
            || Config.DrawEx && ActionData.IsSpecialOrArtilleryActionCategory(actionCategory)
            || allowCateogry0 && actionCategory == 0;

        // Only check for circle and donut in Large EffectRange check
        private static bool ShouldDrawForEffectRange(EffectRangeData data)
            => data.EffectRange > 0
            && (!(data is CircleAoEEffectRangeData || data is DonutAoEEffectRangeData)
                || Config.LargeDrawOpt != 1
                || data.EffectRange < Config.LargeThreshold);

        // Note: will not draw for `None` (=0)
        private static bool ShouldDrawForHarmfulness(EffectRangeData data)
            => EffectRangeDataManager.IsHarmfulAction(data) && Config.DrawHarmful
            || EffectRangeDataManager.IsBeneficialAction(data) && Config.DrawBeneficial;


        private static bool CheckShouldDrawPostCustomisation(EffectRangeData data)
        {
            if (!ShouldDrawForEffectRange(data))
            {
                LogUserDebug($"---Skip action #{data.ActionId}: " +
                    $"Not drawing for actions of effect range = {data.EffectRange}");
                return false;
            }

            if (!ShouldDrawForHarmfulness(data))
            {
                LogUserDebug($"---Skip action #{data.ActionId}: " +
                    $"Not drawing for harmful/beneficial actions = {data.Harmfulness}");
                return false;
            }

            return true;
        }

        #endregion


        private static ActionSeqInfo? FindRecordedSeqInfo(
            ushort receivedSeq, uint receivedActionId)
        {
            foreach (var seqInfo in playerActionSeqs)
            {
                if (IsSeqExpired(seqInfo)) continue;
                if (receivedSeq > 0) // Primary effects from player actions
                {
                    if (receivedSeq == seqInfo.Seq)
                    {
                        LogUserDebug($"---* Recorded sequence matched");
                        return seqInfo;
                    }
                }
                else if (ActionData.AreRelatedPlayerTriggeredActions(
                    seqInfo.ActionId, receivedActionId))
                {
                    LogUserDebug($"---* Related recorded sequence found");
                    return seqInfo;
                }
            }
            LogUserDebug($"---* No recorded sequence matched");
            return null;
        }

        private static void AddSkippedSeq(ushort seq)
        {
            if (skippedSeqs.Count >= MaxSkippedSeqs) skippedSeqs.Clear();
            skippedSeqs.Add(seq);
        }

        private static void ClearSeqRecordCache()
        {
            playerActionSeqs.Clear();
            skippedSeqs.Clear();
        }

        private static bool IsSeqExpired(ActionSeqInfo info)
            => info.ElapsedSeconds > SeqExpiry;

        private static void OnClassJobChangedClearCache(uint classJobId)
            => ClearSeqRecordCache();

        private static void OnTerritoryChangedClearCache(uint terr)
            => ClearSeqRecordCache();


        static ActionWatcher()
        {
            UseActionHook = InteropProvider.HookFromAddress<ActionManager.Delegates.UseAction>(
                ActionManager.MemberFunctionPointers.UseAction, UseActionDetour);
            UseActionLocationHook = InteropProvider.HookFromAddress<ActionManager.Delegates.UseActionLocation>(
                ActionManager.MemberFunctionPointers.UseActionLocation, UseActionLocationDetour);
            ReceiveActionEffectHook = InteropProvider.HookFromAddress<ActionEffectHandler.Delegates.Receive>(
                ActionEffectHandler.MemberFunctionPointers.Receive, ReceiveActionEffectDetour);

            PluginLog.Information("ActionWatcher init:\n" +
                $"\tUseActionHook @{UseActionHook?.Address ?? 0:X}\n" +
                $"\tUseActionLocationHook @{UseActionLocationHook?.Address ?? 0:X}\n" +
                $"\tReceiveActionEffectHook @{ReceiveActionEffectHook?.Address ?? 0:X}");
        }

        public static void Enable()
        {
            UseActionHook?.Enable();
            UseActionLocationHook?.Enable();
            ReceiveActionEffectHook?.Enable();

            ClientState.TerritoryChanged += OnTerritoryChangedClearCache;
            ClassJobWatcher.ClassJobChanged += OnClassJobChangedClearCache;
        }

        public static void Disable()
        {
            UseActionHook?.Disable();
            UseActionLocationHook?.Disable();
            ReceiveActionEffectHook?.Disable();

            ClientState.TerritoryChanged -= OnTerritoryChangedClearCache;
            ClassJobWatcher.ClassJobChanged -= OnClassJobChangedClearCache;
        }

        public static void Dispose()
        {
            Disable();
            UseActionHook?.Dispose();
            UseActionLocationHook?.Dispose();
            ReceiveActionEffectHook?.Dispose();
        }

    }
}
