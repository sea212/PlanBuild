﻿using BepInEx.Configuration;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using PlanBuild.Blueprints;
using PlanBuild.Plans;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlanBuild.PlanBuild
{
    internal class PlanManager
    {
        private static PlanManager _instance;
        public static PlanManager Instance
        {
            get
            {
                if (_instance == null) _instance = new PlanManager();
                return _instance;
            }
        }

        public static ConfigEntry<bool> showAllPieces;
        public readonly Dictionary<string, PlanPiecePrefab> planPiecePrefabs = new Dictionary<string, PlanPiecePrefab>();

        internal void Init()
        {
            showAllPieces = PlanBuildPlugin.Instance.Config.Bind("General", "Plan unknown pieces", false, new ConfigDescription("Show all plans, even for pieces you don't know yet"));
            showAllPieces.SettingChanged += (object sender, EventArgs e) => UpdateKnownRecipes();

            PieceManager.OnPiecesRegistered += CreatePlanTable;
            On.Player.OnSpawned += OnPlayerOnSpawned;
        }

        private void CreatePlanTable()
        {
            // Create plan piece table for the plan mode
            var categories = PieceManager.Instance.GetPieceCategories().Where(x => x != BlueprintRunePrefab.CategoryBlueprints && x != BlueprintRunePrefab.CategoryTools);

            CustomPieceTable planPieceTable = new CustomPieceTable(
                PlanPiecePrefab.PlanHammerPieceTableName,
                new PieceTableConfig()
                {
                    CanRemovePieces = true,
                    UseCategories = true,
                    UseCustomCategories = true,
                    CustomCategories = categories.ToArray()
                }
            );
            PieceManager.Instance.AddPieceTable(planPieceTable);

            // Add empty lists up to the max categories count
            for (int i = planPieceTable.PieceTable.m_availablePieces.Count; i < (int)Piece.PieceCategory.All; i++)
            {
                planPieceTable.PieceTable.m_availablePieces.Add(new List<Piece>());
            }

            // Resize selectedPiece array
            Array.Resize(ref planPieceTable.PieceTable.m_selectedPiece, planPieceTable.PieceTable.m_availablePieces.Count);

            // Set table on the rune
            ItemDrop rune = ItemManager.Instance.GetItem(BlueprintRunePrefab.BlueprintRuneName).ItemDrop;
            rune.m_itemData.m_shared.m_buildPieces = planPieceTable.PieceTable;

            // Needs to run only once 
            PieceManager.OnPiecesRegistered -= CreatePlanTable;
        }

        private void OnPlayerOnSpawned(On.Player.orig_OnSpawned orig, Player self)
        {
            orig(self);
            ScanPieceTables();
        }

        public void TogglePlanBuildMode()
        {
            if (ScanPieceTables())
            {
                UpdateKnownRecipes();
            }
            if (Player.m_localPlayer.m_visEquipment.m_rightItem != BlueprintRunePrefab.BlueprintRuneName)
            {
                return;
            }
            ItemDrop.ItemData blueprintRune = Player.m_localPlayer.GetInventory().GetItem(BlueprintRunePrefab.BlueprintRuneItemName);
            if (blueprintRune == null)
            {
                return;
            }
            PieceTable planHammerPieceTable = PieceManager.Instance.GetPieceTable(PlanPiecePrefab.PlanHammerPieceTableName);
            PieceTable bluePrintRunePieceTable = PieceManager.Instance.GetPieceTable(BlueprintRunePrefab.PieceTableName);
            if (blueprintRune.m_shared.m_buildPieces == planHammerPieceTable)
            {
                blueprintRune.m_shared.m_buildPieces = bluePrintRunePieceTable;
                if (blueprintRune.m_shared.m_buildPieces.m_selectedCategory == 0)
                {
                    blueprintRune.m_shared.m_buildPieces.m_selectedCategory = PieceManager.Instance.AddPieceCategory(BlueprintRunePrefab.PieceTableName, BlueprintRunePrefab.CategoryTools);
                }
            }
            else
            {
                blueprintRune.m_shared.m_buildPieces = planHammerPieceTable;
            }
            Player.m_localPlayer.UnequipItem(blueprintRune);
            Player.m_localPlayer.EquipItem(blueprintRune);

            Color color = blueprintRune.m_shared.m_buildPieces == planHammerPieceTable ? Color.red : Color.cyan;
            ShaderHelper.SetEmissionColor(Player.m_localPlayer.m_visEquipment.m_rightItemInstance, color);

            Player.m_localPlayer.UpdateKnownRecipesList();
        }

        private void UpdateKnownRecipes()
        {
            Player player = Player.m_localPlayer;
            if (!showAllPieces.Value)
            {
                foreach (PlanPiecePrefab planPieceConfig in planPiecePrefabs.Values)
                {
                    if (!player.HaveRequirements(planPieceConfig.originalPiece, Player.RequirementMode.IsKnown))
                    {
#if DEBUG
                        Jotunn.Logger.LogInfo("Removing planned piece from m_knownRecipes: " + planPieceConfig.Piece.m_name);
#endif
                        player.m_knownRecipes.Remove(planPieceConfig.Piece.m_name);
                    }
#if DEBUG
                    else
                    {
                        Jotunn.Logger.LogDebug("Player knows about " + planPieceConfig.originalPiece.m_name);
                    }
#endif
                }
            }
            player.UpdateKnownRecipesList();
            PieceManager.Instance.GetPieceTable(PlanPiecePrefab.PlanHammerPieceTableName)
                .UpdateAvailable(player.m_knownRecipes, player, true, false);
        }

        internal bool ScanPieceTables()
        {
            Jotunn.Logger.LogDebug("Scanning PieceTables for Pieces");
            bool addedPiece = false;
            foreach (GameObject item in ObjectDB.instance.m_items)
            {
                PieceTable pieceTable = item.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces;
                if (pieceTable == null)
                {
                    continue;
                }
                foreach (GameObject piecePrefab in pieceTable.m_pieces)
                {
                    if (!piecePrefab)
                    {
                        Jotunn.Logger.LogWarning($"Invalid prefab in {item.name} PieceTable");
                        continue;
                    }
                    Piece piece = piecePrefab.GetComponent<Piece>();
                    if (!piece)
                    {
                        Jotunn.Logger.LogWarning($"Recipe in {item.name} has no Piece?! " + piecePrefab.name);
                        continue;
                    }
                    try
                    {
                        if (piece.name == "piece_repair")
                        {
                            continue;
                        }
                        if (planPiecePrefabs.ContainsKey(piece.name))
                        {
                            continue;
                        }
                        if (!CanCreatePlan(piece))
                        {
                            continue;
                        }
                        if (!EnsurePrefabRegistered(piece))
                        {
                            continue;
                        }

                        PlanPiecePrefab planPiece = new PlanPiecePrefab(piece);
                        PieceManager.Instance.AddPiece(planPiece);
                        planPiecePrefabs.Add(piece.name, planPiece);
                        PrefabManager.Instance.RegisterToZNetScene(planPiece.PiecePrefab);
                        PieceTable planPieceTable = PieceManager.Instance.GetPieceTable(PlanPiecePrefab.PlanHammerPieceTableName);
                        if (!planPieceTable.m_pieces.Contains(planPiece.PiecePrefab))
                        {
                            planPieceTable.m_pieces.Add(planPiece.PiecePrefab);
                            addedPiece = true;
                        }
                    }
                    catch (Exception e)
                    {
                        Jotunn.Logger.LogWarning("Error while creating plan of " + piece.name + ": " + e);
                    }
                }
            }
            return addedPiece;
        }

        public static bool CanCreatePlan(Piece piece)
        {
            return piece.m_enabled
                && piece.GetComponent<Ship>() == null
                && piece.GetComponent<Plant>() == null
                && piece.GetComponent<TerrainModifier>() == null
                && piece.GetComponent<TerrainOp>() == null
                && piece.m_resources.Length != 0;
        }

        private bool EnsurePrefabRegistered(Piece piece)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(piece.gameObject.name);
            if (prefab)
            {
                return true;
            }
            Jotunn.Logger.LogWarning("Piece " + piece.name + " in Hammer not fully registered? Could not find prefab " + piece.gameObject.name);
            if (!ZNetScene.instance.m_prefabs.Contains(piece.gameObject))
            {
                Jotunn.Logger.LogWarning(" Not registered in ZNetScene.m_prefabs! Adding now");
                ZNetScene.instance.m_prefabs.Add(piece.gameObject);
            }
            if (!ZNetScene.instance.m_namedPrefabs.ContainsKey(piece.gameObject.name.GetStableHashCode()))
            {
                Jotunn.Logger.LogWarning(" Not registered in ZNetScene.m_namedPrefabs! Adding now");
                ZNetScene.instance.m_namedPrefabs[piece.gameObject.name.GetStableHashCode()] = piece.gameObject;
            }
            //Prefab was added incorrectly, make sure the game doesn't delete it when logging out
            GameObject prefabParent = piece.gameObject.transform.parent?.gameObject;
            if (!prefabParent)
            {
                Jotunn.Logger.LogWarning(" Prefab has no parent?! Adding to Jotunn");
                PrefabManager.Instance.AddPrefab(piece.gameObject);
            }
            else if (prefabParent.scene.buildIndex != -1)
            {
                Jotunn.Logger.LogWarning(" Prefab container not marked as DontDestroyOnLoad! Marking now");
                Object.DontDestroyOnLoad(prefabParent);
            }
            return PrefabManager.Instance.GetPrefab(piece.gameObject.name) != null;
        }

    }
}