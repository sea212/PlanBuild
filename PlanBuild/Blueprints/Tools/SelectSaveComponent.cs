﻿using Jotunn.Managers;
using PlanBuild.Plans;
using System;
using System.Linq;
using UnityEngine;

namespace PlanBuild.Blueprints.Tools
{
    internal class SelectSaveComponent : SelectionToolComponentBase
    {
        public override void Init()
        {
            base.Init();
            DisableSelectionProjector(); 
        }

        public override bool PlacePiece(Player self, Piece piece)
        {
            return MakeBlueprint();
        }

        private bool MakeBlueprint()
        {
            var bpname = $"blueprint{BlueprintManager.LocalBlueprints.Count() + 1:000}";
            Jotunn.Logger.LogInfo($"Capturing blueprint {bpname}");

            var bp = new Blueprint();
            if (bp.Capture(Selection.Instance))
            {
                Selection.Instance.Clear();
                TextInput.instance.m_queuedSign = new Blueprint.BlueprintSaveGUI(bp);
                TextInput.instance.Show(Localization.instance.Localize("$msg_bpcapture_save", bp.GetPieceCount().ToString()), bpname, 50);
            }
            else
            {
                Jotunn.Logger.LogWarning($"Could not capture blueprint {bpname}");
            }

            // Don't place the piece and clutter the world with it
            return false;
        }

        /// <summary>
        ///     Hook for patching
        /// </summary>
        /// <param name="newpiece"></param>
        internal virtual void OnPiecePlaced(GameObject placedPiece)
        {

        }
    }
}