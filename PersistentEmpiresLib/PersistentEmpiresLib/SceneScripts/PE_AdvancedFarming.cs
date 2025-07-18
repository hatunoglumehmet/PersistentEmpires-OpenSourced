using PersistentEmpiresLib.Data;
using PersistentEmpiresLib.Helpers;
using PersistentEmpiresLib.NetworkMessages.Server;
using PersistentEmpiresLib.PersistentEmpiresMission.MissionBehaviors;
using System;
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
    /// Advanced farming system with seasonal bonuses, crop rotation, and quality grades
    /// </summary>
    public class PE_AdvancedFarming : PE_UsableFromDistance
    {
        public string CropType = "Wheat";
        public int GrowthTimeHours = 24;
        public int BaseYield = 5;
        public string RequiredTool = "pe_farming_hoe";
        public string RequiredSeeds = "pe_wheat_seeds";
        public int RequiredFarmingSkill = 25;
        public bool RotationBonus = false;
        public string PreviousCrop = "";
        public int SeasonalBonusPercentage = 20;
        
        private DateTime PlantedAt;
        private DateTime LastHarvested;
        private bool IsPlanted = false;
        private bool IsGrown = false;
        private int ConsecutiveHarvests = 0;
        private Random randomizer = new Random();

        protected override bool LockUserFrames => false;
        protected override bool LockUserPositions => false;

        protected override void OnInit()
        {
            base.OnInit();
            SetTextVariables();
        }

        private void SetTextVariables()
        {
            if (!IsPlanted)
            {
                base.ActionMessage = new TextObject("Plant " + CropType);
                base.DescriptionMessage = new TextObject("Press {KEY} to plant seeds");
            }
            else if (!IsGrown)
            {
                base.ActionMessage = new TextObject("Growing " + CropType);
                base.DescriptionMessage = new TextObject("Crop is growing...");
            }
            else
            {
                base.ActionMessage = new TextObject("Harvest " + CropType);
                base.DescriptionMessage = new TextObject("Press {KEY} to harvest");
            }
        }

        public override void OnUse(Agent userAgent)
        {
            base.OnUse(userAgent);
            if (!GameNetwork.IsServer) return;

            var representative = userAgent.MissionPeer.GetNetworkPeer().GetComponent<PersistentEmpireRepresentative>();
            var inventory = representative.GetInventory();

            if (!IsPlanted)
            {
                // Planting phase
                if (!HasRequiredItems(inventory)) return;

                if (representative.GetSkillValue(RequiredFarmingSkill) < RequiredFarmingSkill)
                {
                    InformationComponent.Instance.SendMessageToPlayer(userAgent.MissionPeer.GetNetworkPeer(), 
                        $"You need {RequiredFarmingSkill} farming skill to plant this crop.");
                    return;
                }

                ConsumeRequiredItems(inventory);
                PlantCrop();
                InformationComponent.Instance.SendMessageToPlayer(userAgent.MissionPeer.GetNetworkPeer(), 
                    $"Planted {CropType}. It will be ready in {GrowthTimeHours} hours.");
            }
            else if (IsGrown)
            {
                // Harvesting phase
                var yield = CalculateYield();
                var quality = CalculateQuality();
                
                string harvestedItem = GetHarvestedItem(quality);
                inventory.AddCountedItem(harvestedItem, yield);
                
                // Experience gain
                representative.SetSkillValue(RequiredFarmingSkill, 
                    representative.GetSkillValue(RequiredFarmingSkill) + CalculateExpGain());
                
                // Gold bonus for high quality crops
                if (quality > 1)
                {
                    int goldBonus = yield * quality * 10;
                    representative.GoldGain(goldBonus);
                    InformationComponent.Instance.SendMessageToPlayer(userAgent.MissionPeer.GetNetworkPeer(), 
                        $"Quality bonus: +{goldBonus} gold!");
                }

                ConsecutiveHarvests++;
                LastHarvested = DateTime.UtcNow;
                ResetCrop();

                InformationComponent.Instance.SendMessageToPlayer(userAgent.MissionPeer.GetNetworkPeer(), 
                    $"Harvested {yield} {harvestedItem} (Quality: {GetQualityName(quality)})");
            }

            userAgent.StopUsingGameObjectMT(true);
        }

        private bool HasRequiredItems(Inventory inventory)
        {
            return inventory.IsInventoryIncludes(RequiredTool) && 
                   inventory.IsInventoryIncludes(RequiredSeeds);
        }

        private void ConsumeRequiredItems(Inventory inventory)
        {
            inventory.RemoveCountedItem(RequiredSeeds, 1);
        }

        private void PlantCrop()
        {
            IsPlanted = true;
            PlantedAt = DateTime.UtcNow;
            SetTextVariables();
        }

        private int CalculateYield()
        {
            int yield = BaseYield;
            
            // Seasonal bonus
            yield += (int)(yield * GetSeasonalMultiplier());
            
            // Rotation bonus
            if (RotationBonus && PreviousCrop != CropType)
            {
                yield += (int)(yield * 0.25f); // 25% rotation bonus
            }
            
            // Consecutive harvest penalty
            if (ConsecutiveHarvests > 3)
            {
                yield -= ConsecutiveHarvests - 3;
            }
            
            // Random variation ±20%
            yield += randomizer.Next(-yield / 5, yield / 5);
            
            return Math.Max(1, yield);
        }

        private int CalculateQuality()
        {
            // Base quality 1-5 based on various factors
            int quality = 1;
            
            // Farming skill bonus
            if (Mission.Current.GetMissionBehavior<AdminServerBehavior>() != null)
            {
                var user = base.UserAgent?.MissionPeer?.GetNetworkPeer()?.GetComponent<PersistentEmpireRepresentative>();
                if (user != null)
                {
                    int farmingSkill = user.GetSkillValue(RequiredFarmingSkill);
                    quality += farmingSkill / 30; // +1 quality per 30 skill points
                }
            }
            
            // Seasonal bonus
            if (GetSeasonalMultiplier() > 0)
            {
                quality += 1;
            }
            
            // Random factor
            if (randomizer.Next(100) < 15) quality += 1; // 15% chance for +1 quality
            
            return Math.Min(5, quality);
        }

        private float GetSeasonalMultiplier()
        {
            // Simple seasonal simulation based on day of year
            int dayOfYear = DateTime.UtcNow.DayOfYear;
            
            // Spring/Summer bonus for most crops
            if (dayOfYear >= 80 && dayOfYear <= 265) // Roughly Mar-Sep
            {
                return SeasonalBonusPercentage / 100f;
            }
            
            return 0f;
        }

        private string GetHarvestedItem(int quality)
        {
            return quality switch
            {
                5 => "pe_" + CropType.ToLower() + "_legendary",
                4 => "pe_" + CropType.ToLower() + "_masterwork",
                3 => "pe_" + CropType.ToLower() + "_fine",
                2 => "pe_" + CropType.ToLower() + "_good",
                _ => "pe_" + CropType.ToLower()
            };
        }

        private string GetQualityName(int quality)
        {
            return quality switch
            {
                5 => "Legendary",
                4 => "Masterwork", 
                3 => "Fine",
                2 => "Good",
                _ => "Common"
            };
        }

        private int CalculateExpGain()
        {
            return BaseYield + ConsecutiveHarvests;
        }

        private void ResetCrop()
        {
            IsPlanted = false;
            IsGrown = false;
            PreviousCrop = CropType;
            SetTextVariables();
        }

        public override ScriptComponentBehavior.TickRequirement GetTickRequirement()
        {
            return ScriptComponentBehavior.TickRequirement.TickOccasionally;
        }

        protected override void OnTickOccasionally(float currentFrameDeltaTime)
        {
            if (IsPlanted && !IsGrown)
            {
                var timeSincePlanted = DateTime.UtcNow - PlantedAt;
                if (timeSincePlanted.TotalHours >= GrowthTimeHours)
                {
                    IsGrown = true;
                    SetTextVariables();
                }
            }
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            if (!IsPlanted) return $"Farmland for {CropType}";
            if (!IsGrown) return $"Growing {CropType}";
            return $"Ready to harvest {CropType}";
        }
    }
}