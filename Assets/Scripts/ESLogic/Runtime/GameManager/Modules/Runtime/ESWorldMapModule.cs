using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    [TypeRegistryItem("世界模块/地图")]
    public sealed class ESWorldMapModule : ESWorldModule
    {
        private const string SaveSectionKey = "world.map.runtime";
        [NonSerialized] private ESWorldMapDefinition currentDefinition;
        [NonSerialized] private ESWorldMapRuntimeState currentState;
        [NonSerialized] private bool isDestroying;
        [NonSerialized] private Terrain activeTerrain;

        public bool IsMapLoaded => currentDefinition != null && currentState != null;
        public string CurrentMapId => currentState != null ? currentState.mapId : string.Empty;
        public ESWorldMapDefinition CurrentDefinition => currentDefinition;
        public ESWorldMapRuntimeState CurrentState => currentState;

        public void BindTerrain(Terrain terrain)
        {
            activeTerrain = terrain;
        }

        public bool TrySampleTerrain(Vector3 worldPosition, out ESWorldMapTerrainSample sample)
        {
            sample = new ESWorldMapTerrainSample();
            if (!IsMapLoaded) return false;
            ESWorldMapDefinition definition = currentDefinition;
            Vector2 mapPoint = new Vector2(worldPosition.x, worldPosition.z);
            if (!definition.Contains(mapPoint)) return false;
            float u = Mathf.InverseLerp(definition.worldMin.x, definition.worldMax.x, mapPoint.x);
            float v = Mathf.InverseLerp(definition.worldMin.y, definition.worldMax.y, mapPoint.y);
            float normalizedHeight;
            Vector3 normal;
            if (activeTerrain != null && activeTerrain.terrainData != null)
            {
                Vector3 terrainPosition = activeTerrain.transform.position;
                Vector3 size = activeTerrain.terrainData.size;
                Vector3 terrainPoint = new Vector3(Mathf.Lerp(0f, size.x, u), 0f, Mathf.Lerp(0f, size.z, v));
                normalizedHeight = activeTerrain.terrainData.GetInterpolatedHeight(u, v) / Mathf.Max(0.001f, size.y);
                normal = activeTerrain.terrainData.GetInterpolatedNormal(u, v);
                sample.worldPosition = terrainPosition + terrainPoint + Vector3.up * activeTerrain.terrainData.GetInterpolatedHeight(u, v);
            }
            else if (definition.heightfield != null)
            {
                normalizedHeight = definition.heightfield.SampleNormalized(u, v);
                normal = definition.heightfield.SampleNormal(u, v, definition.worldMax.x - definition.worldMin.x, definition.worldMax.y - definition.worldMin.y, definition.terrainHeightScale);
                sample.worldPosition = new Vector3(mapPoint.x, normalizedHeight * definition.terrainHeightScale, mapPoint.y);
            }
            else return false;
            sample.normal = normal;
            sample.normalizedHeight = normalizedHeight;
            sample.slopeDegrees = Vector3.Angle(Vector3.up, normal);
            sample.walkable = sample.slopeDegrees <= definition.maxWalkableSlope;
            sample.surfaceTag = definition.defaultSurfaceTag;
            sample.valid = true;
            return true;
        }

        public bool TryGetSurface(string surfaceTag, out ESWorldMapSurfaceDefinition surface)
        {
            surface = null;
            if (!IsMapLoaded || string.IsNullOrWhiteSpace(surfaceTag)) return false;
            if (currentDefinition.surfaces != null)
                for (int i = 0; i < currentDefinition.surfaces.Count; i++)
                    if (currentDefinition.surfaces[i] != null && string.Equals(currentDefinition.surfaces[i].surfaceTag, surfaceTag, StringComparison.Ordinal))
                    { surface = currentDefinition.surfaces[i]; return true; }
            return false;
        }

        public float GetMovementMultiplier(Vector3 worldPosition)
        {
            if (!TrySampleTerrain(worldPosition, out ESWorldMapTerrainSample sample)) return 0f;
            return TryGetSurface(sample.surfaceTag, out ESWorldMapSurfaceDefinition surface) ? surface.movementMultiplier : 1f;
        }

        public override void Start()
        {
            base.Start();
            ESGameSave.BeforeSave += FlushState;
            ESGameSave.ValidateCandidate += OnValidateSaveCandidate;
            ESGameSave.PrepareCandidate += OnPrepareSaveCandidate;
            ESGameSave.CommitCandidate += OnCommitSaveCandidate;
            ESGameSave.RollbackCandidate += OnRollbackSaveCandidate;
            ESGameSave.FinalizeCandidate += OnFinalizeSaveCandidate;
            ReplayCurrentSaveCandidate();
        }

        public override void OnDestroy()
        {
            isDestroying = true;
            ESGameSave.BeforeSave -= FlushState;
            ESGameSave.ValidateCandidate -= OnValidateSaveCandidate;
            ESGameSave.PrepareCandidate -= OnPrepareSaveCandidate;
            ESGameSave.CommitCandidate -= OnCommitSaveCandidate;
            ESGameSave.RollbackCandidate -= OnRollbackSaveCandidate;
            ESGameSave.FinalizeCandidate -= OnFinalizeSaveCandidate;
            currentDefinition = null;
            currentState = null;
            activeTerrain = null;
            base.OnDestroy();
        }

        public bool TryLoadMap(ESWorldMapDefinition definition, out string error)
        {
            error = null;
            if (definition == null || !definition.IsValid(out error)) return false;
            ESWorldMapDefinition previousDefinition = currentDefinition;
            ESWorldMapRuntimeState previousState = currentState;
            currentDefinition = definition;
            currentState = new ESWorldMapRuntimeState { mapId = definition.mapId, contentVersion = definition.contentVersion, contentHash = definition.contentHash };
            if (ESGameSave.TryGetCurrentCandidate(out ESGameSaveCandidate candidate))
            {
                ESGameSaveApplyResult validate = OnValidateSaveCandidate(candidate);
                if (!validate.Success) { currentDefinition = previousDefinition; currentState = previousState; error = validate.ErrorCode + ": " + validate.Message; return false; }
                ESGameSaveApplyResult prepare = OnPrepareSaveCandidate(candidate);
                if (!prepare.Success) { currentDefinition = previousDefinition; currentState = previousState; error = prepare.ErrorCode + ": " + prepare.Message; return false; }
                ESGameSaveApplyResult commit = OnCommitSaveCandidate(candidate, ESGameSaveApplyPhase.World);
                if (!commit.Success) { currentDefinition = previousDefinition; currentState = previousState; error = commit.ErrorCode + ": " + commit.Message; return false; }
            }
            return true;
        }

        public bool TryGetRegionAt(Vector2 position, out ESWorldMapRegionDefinition region)
        {
            region = null;
            if (!IsMapLoaded || !currentDefinition.Contains(position) || currentDefinition.regions == null) return false;
            int bestPriority = int.MinValue;
            for (int i = 0; i < currentDefinition.regions.Count; i++)
            {
                ESWorldMapRegionDefinition candidate = currentDefinition.regions[i];
                if (candidate != null && candidate.Contains(position) && candidate.priority >= bestPriority) { bestPriority = candidate.priority; region = candidate; }
            }
            return region != null;
        }

        public bool TryGetPoi(string poiId, out ESWorldMapPoiDefinition poi)
        {
            poi = null;
            return IsMapLoaded && currentDefinition.TryGetPoi(poiId, out poi);
        }

        public bool TryDiscoverRegion(string regionId)
        {
            if (!IsMapLoaded || !currentDefinition.TryGetRegion(regionId, out _)) return false;
            if (currentState.discoveredRegionIds == null) currentState.discoveredRegionIds = new System.Collections.Generic.List<string>();
            if (currentState.discoveredRegionIds.Contains(regionId)) return false;
            currentState.discoveredRegionIds.Add(regionId);
            return true;
        }

        public bool TryUnlockPoi(string poiId)
        {
            if (!IsMapLoaded || !currentDefinition.TryGetPoi(poiId, out ESWorldMapPoiDefinition poi) || !poi.discoverable) return false;
            if (currentState.unlockedPoiIds == null) currentState.unlockedPoiIds = new System.Collections.Generic.List<string>();
            if (currentState.unlockedPoiIds.Contains(poiId)) return false;
            currentState.unlockedPoiIds.Add(poiId);
            return true;
        }

        private void FlushState() { if (!isDestroying && IsMapLoaded) ESGameSave.SetCurrent(SaveSectionKey, currentState); }
        private ESGameSaveApplyResult OnValidateSaveCandidate(ESGameSaveCandidate candidate)
        {
            if (candidate == null || candidate.Archive == null) return ESGameSaveApplyResult.Fail("WorldMap.Candidate.Missing", "WorldMap 收到空候选 Archive。");
            ESGameSaveSectionPacket packet = candidate.FindSection(SaveSectionKey);
            if (packet == null) return ESGameSaveApplyResult.Ok();
            string error = null;
            if (packet.schemaVersion != 1 || string.IsNullOrWhiteSpace(packet.json) || !packet.TryRead(out ESWorldMapRuntimeState state) || state == null || !state.IsValid(out error)) return ESGameSaveApplyResult.Fail("WorldMap.State.Invalid", error ?? "地图运行状态无效。");
            candidate.SetParticipantData(this, state);
            return ESGameSaveApplyResult.Ok();
        }
        private ESGameSaveApplyResult OnPrepareSaveCandidate(ESGameSaveCandidate candidate)
        {
            if (candidate == null || !candidate.TryGetParticipantData(this, out ESWorldMapRuntimeState state)) return ESGameSaveApplyResult.Ok();
            candidate.SetParticipantData(this, new PreparedSave { incoming = state, previous = currentState != null ? currentState.Clone() : null });
            return ESGameSaveApplyResult.Ok();
        }
        private ESGameSaveApplyResult OnCommitSaveCandidate(ESGameSaveCandidate candidate, ESGameSaveApplyPhase phase)
        {
            if (phase != ESGameSaveApplyPhase.World || candidate == null || !candidate.TryGetParticipantData(this, out PreparedSave prepared)) return ESGameSaveApplyResult.Ok();
            if (currentDefinition == null || !string.Equals(currentDefinition.mapId, prepared.incoming.mapId, StringComparison.Ordinal)) return ESGameSaveApplyResult.Fail("WorldMap.MapNotLoaded", "目标地图尚未加载，无法应用地图存档。");
            if (currentDefinition.contentVersion != prepared.incoming.contentVersion || !string.Equals(currentDefinition.contentHash, prepared.incoming.contentHash, StringComparison.Ordinal)) return ESGameSaveApplyResult.Fail("WorldMap.ContentDrift", "地图内容版本或 Hash 与存档不匹配。");
            currentState = prepared.incoming.Clone(); prepared.committed = true; return ESGameSaveApplyResult.Ok();
        }
        private ESGameSaveApplyResult OnRollbackSaveCandidate(ESGameSaveCandidate candidate, ESGameSaveApplyPhase phase) { if (phase == ESGameSaveApplyPhase.World && candidate != null && candidate.TryGetParticipantData(this, out PreparedSave prepared) && prepared.committed) currentState = prepared.previous != null ? prepared.previous.Clone() : null; return ESGameSaveApplyResult.Ok(); }
        private ESGameSaveApplyResult OnFinalizeSaveCandidate(ESGameSaveCandidate candidate, ESGameSaveApplyPhase phase) => ESGameSaveApplyResult.Ok();
        private void ReplayCurrentSaveCandidate() { if (!ESGameSave.TryGetCurrentCandidate(out ESGameSaveCandidate candidate)) return; OnValidateSaveCandidate(candidate); OnPrepareSaveCandidate(candidate); }
        private sealed class PreparedSave { public ESWorldMapRuntimeState incoming; public ESWorldMapRuntimeState previous; public bool committed; }
    }
}
