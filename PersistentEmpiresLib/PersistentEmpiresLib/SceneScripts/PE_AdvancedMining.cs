using PersistentEmpiresLib.Data;
using PersistentEmpiresLib.Helpers;
using PersistentEmpiresLib.NetworkMessages.Server;
using PersistentEmpiresLib.PersistentEmpiresMission.MissionBehaviors;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace PersistentEmpiresLib.SceneScripts
{
    /// <summary>
    /// Advanced mining system with rare ore discoveries, mining camps, and vein depletion
    /// </summary>
    public class PE_AdvancedMining : PE_UsableFromDistance
    {
        public string OreType = "Iron";
        public int VeinRichness = 100; // Total ore in vein
        public string RequiredPickaxe = "pe_mining_pick";
        public int RequiredMiningSkill = 20;
        public bool HasRareOres = true;
        public int RareOreChancePercent = 5;
        public string RareOreType = "Gold";
        
        private int CurrentRichness;
        private Random randomizer = new Random();
        private DateTime LastMined = DateTime.MinValue;
        private Dictionary<string, int> PlayerMiningProgress = new Dictionary<string, int>();
        private bool IsEstablishedCamp = false;
        private string CampOwner = "";

        protected override bool LockUserFrames => false;
        protected override bool LockUserPositions => false;

        protected override void OnInit()
        {
            base.OnInit();
            CurrentRichness = VeinRichness;
            SetTextVariables();
        }

        private void SetTextVariables()
        {
            if (CurrentRichness <= 0)
            {
                base.ActionMessage = new TextObject("Depleted Mine");
                base.DescriptionMessage = new TextObject("This vein has been exhausted");
            }
            else if (IsEstablishedCamp)
            {
                base.ActionMessage = new TextObject($"Mining Camp ({CampOwner})");
                base.DescriptionMessage = new TextObject("Press {KEY} to work at mining camp");
            }
            else
            {
                base.ActionMessage = new TextObject($"Mine {OreType}");
                base.DescriptionMessage = new TextObject("Press {KEY} to mine ore");
            }
        }

        public override void OnUse(Agent userAgent)
        {
            base.OnUse(userAgent);
            if (!GameNetwork.IsServer) return;

            var representative = userAgent.MissionPeer.GetNetworkPeer().GetComponent<PersistentEmpireRepresentative>();
            var inventory = representative.GetInventory();

            if (CurrentRichness <= 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(userAgent.MissionPeer.GetNetworkPeer(), 
                    "This mining vein has been depleted.");
                userAgent.StopUsingGameObjectMT(true);
                return;
            }

            if (!HasRequiredTools(inventory))
            {
                InformationComponent.Instance.SendMessageToPlayer(userAgent.MissionPeer.GetNetworkPeer(), 
                    $"You need a {RequiredPickaxe} to mine here.");
                userAgent.StopUsingGameObjectMT(true);
                return;
            }

            if (representative.GetSkillValue(RequiredMiningSkill) < RequiredMiningSkill)
            {
                InformationComponent.Instance.SendMessageToPlayer(userAgent.MissionPeer.GetNetworkPeer(), 
                    $"You need {RequiredMiningSkill} mining skill to work this vein.");
                userAgent.StopUsingGameObjectMT(true);
                return;
            }

            StartMining(userAgent, representative, inventory);
        }

        private bool HasRequiredTools(Inventory inventory)
        {
            return inventory.IsInventoryIncludes(RequiredPickaxe);
        }

        private void StartMining(Agent userAgent, PersistentEmpireRepresentative representative, Inventory inventory)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            
            // Get player's progress at this mine
            if (!PlayerMiningProgress.ContainsKey(playerId))
                PlayerMiningProgress[playerId] = 0;

            // Mining yields based on skill and tool quality
            var miningResults = PerformMining(representative, playerId);
            
            // Apply results
            foreach (var result in miningResults.MinedOres)
            {
                inventory.AddCountedItem(result.Key, result.Value);
            }

            // Gold bonus for rare finds
            if (miningResults.GoldBonus > 0)
            {
                representative.GoldGain(miningResults.GoldBonus);
            }

            // Experience gain
            representative.SetSkillValue(RequiredMiningSkill, 
                representative.GetSkillValue(RequiredMiningSkill) + miningResults.ExpGained);

            // Check for camp establishment
            CheckForCampEstablishment(playerId, representative);

            // Damage pickaxe over time
            DamagePickaxe(inventory);

            // Send results to player
            SendMiningResults(userAgent.MissionPeer.GetNetworkPeer(), miningResults);

            userAgent.StopUsingGameObjectMT(true);
        }

        private MiningResult PerformMining(PersistentEmpireRepresentative representative, string playerId)
        {
            var result = new MiningResult();
            
            int miningSkill = representative.GetSkillValue(RequiredMiningSkill);
            int baseYield = CalculateBaseYield(miningSkill);
            
            // Regular ore
            result.MinedOres[GetOreItem(OreType, 1)] = baseYield;
            CurrentRichness -= baseYield;
            
            // Rare ore chance
            if (HasRareOres && randomizer.Next(100) < RareOreChancePercent + (miningSkill / 10))
            {
                int rareYield = randomizer.Next(1, 3);
                result.MinedOres[GetOreItem(RareOreType, GetRareOreQuality())] = rareYield;
                result.GoldBonus += rareYield * 100; // Bonus gold for rare finds
                result.SpecialFind = $"Found rare {RareOreType}!";
            }
            
            // Gemstone chance (very rare)
            if (randomizer.Next(1000) < 1 + (miningSkill / 20))
            {
                string gemType = GetRandomGemstone();
                result.MinedOres[gemType] = 1;
                result.GoldBonus += 500;
                result.SpecialFind = $"Discovered a {gemType}!";
            }
            
            // Mining camp progress
            PlayerMiningProgress[playerId]++;
            
            // Experience calculation
            result.ExpGained = baseYield + (result.MinedOres.Count > 1 ? 5 : 0);
            
            // Vein depletion effects
            if (CurrentRichness < VeinRichness * 0.2f)
            {
                result.ExpGained = (int)(result.ExpGained * 0.5f); // Less exp when vein is depleted
            }

            LastMined = DateTime.UtcNow;
            return result;
        }

        private int CalculateBaseYield(int miningSkill)
        {
            int baseYield = 1;
            
            // Skill bonus
            baseYield += miningSkill / 25;
            
            // Camp bonus
            if (IsEstablishedCamp)
            {
                baseYield += 2;
            }
            
            // Vein richness affects yield
            float richnessMultiplier = (float)CurrentRichness / VeinRichness;
            if (richnessMultiplier > 0.8f) baseYield += 1; // Rich vein bonus
            else if (richnessMultiplier < 0.3f) baseYield = Math.Max(1, baseYield - 1); // Depleted penalty
            
            return baseYield;
        }

        private string GetOreItem(string oreType, int quality)
        {
            string prefix = "pe_ore_";
            string qualitySuffix = quality switch
            {
                3 => "_pure",
                2 => "_refined",
                _ => ""
            };
            
            return prefix + oreType.ToLower() + qualitySuffix;
        }

        private int GetRareOreQuality()
        {
            return randomizer.Next(100) switch
            {
                < 5 => 3,  // Pure (5%)
                < 20 => 2, // Refined (15%)
                _ => 1     // Regular (80%)
            };
        }

        private string GetRandomGemstone()
        {
            string[] gemstones = { "pe_ruby", "pe_emerald", "pe_sapphire", "pe_diamond", "pe_amethyst" };
            return gemstones[randomizer.Next(gemstones.Length)];
        }

        private void CheckForCampEstablishment(string playerId, PersistentEmpireRepresentative representative)
        {
            if (!IsEstablishedCamp && PlayerMiningProgress[playerId] >= 50)
            {
                // Player has mined enough to establish a camp
                if (representative.Gold >= 1000) // Cost to establish camp
                {
                    representative.GoldLoss(1000);
                    IsEstablishedCamp = true;
                    CampOwner = playerId;
                    SetTextVariables();
                    
                    InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), 
                        "You have established a mining camp! Mining efficiency increased.");
                }
            }
        }

        private void DamagePickaxe(Inventory inventory)
        {
            // 5% chance to damage pickaxe
            if (randomizer.Next(100) < 5)
            {
                // Remove pickaxe and add damaged version
                if (inventory.IsInventoryIncludes(RequiredPickaxe))
                {
                    inventory.RemoveCountedItem(RequiredPickaxe, 1);
                    inventory.AddCountedItem("pe_damaged_pickaxe", 1);
                }
            }
        }

        private void SendMiningResults(NetworkCommunicator peer, MiningResult result)
        {
            string message = "Mined: ";
            foreach (var ore in result.MinedOres)
            {
                message += $"{ore.Value}x {ore.Key}, ";
            }
            message = message.TrimEnd(',', ' ');
            
            if (result.GoldBonus > 0)
            {
                message += $" | Bonus: +{result.GoldBonus} gold";
            }
            
            if (!string.IsNullOrEmpty(result.SpecialFind))
            {
                message += $" | {result.SpecialFind}";
            }
            
            InformationComponent.Instance.SendMessageToPlayer(peer, message);
        }

        public override ScriptComponentBehavior.TickRequirement GetTickRequirement()
        {
            return ScriptComponentBehavior.TickRequirement.TickOccasionally;
        }

        protected override void OnTickOccasionally(float currentFrameDeltaTime)
        {
            // Vein regeneration over time (very slow)
            if (DateTime.UtcNow - LastMined > TimeSpan.FromHours(24) && CurrentRichness < VeinRichness)
            {
                CurrentRichness = Math.Min(VeinRichness, CurrentRichness + 1);
            }
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            if (CurrentRichness <= 0) return "Depleted mining vein";
            
            string status = CurrentRichness > VeinRichness * 0.8f ? "Rich" : 
                           CurrentRichness > VeinRichness * 0.5f ? "Moderate" : 
                           CurrentRichness > VeinRichness * 0.2f ? "Poor" : "Nearly depleted";
            
            return $"{OreType} vein ({status})";
        }

        private class MiningResult
        {
            public Dictionary<string, int> MinedOres = new Dictionary<string, int>();
            public int GoldBonus = 0;
            public int ExpGained = 0;
            public string SpecialFind = "";
        }
    }
}