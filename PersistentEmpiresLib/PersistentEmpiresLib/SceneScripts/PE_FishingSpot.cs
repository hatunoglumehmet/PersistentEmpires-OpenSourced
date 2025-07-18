using PersistentEmpiresLib.Data;
using PersistentEmpiresLib.Helpers;
using PersistentEmpiresLib.NetworkMessages.Server;
using PersistentEmpiresLib.PersistentEmpiresMission.MissionBehaviors;
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Advanced fishing system with different fish types, rarity, and skill progression
    /// </summary>
    public class PE_FishingSpot : PE_UsableFromDistance
    {
        public string WaterType = "Freshwater"; // Freshwater, Saltwater, Deep
        public int FishAbundance = 100; // Affects catch rates
        public string RequiredRod = "pe_fishing_rod";
        public string RequiredBait = "pe_worms";
        public int RequiredFishingSkill = 0;
        public bool HasRareFish = true;
        
        private DateTime LastFished = DateTime.MinValue;
        private Dictionary<string, int> PlayerFishingProgress = new Dictionary<string, int>();
        private Random randomizer = new Random();

        protected override bool LockUserFrames => true;
        protected override bool LockUserPositions => true;

        protected override void OnInit()
        {
            base.OnInit();
            SetTextVariables();
        }

        private void SetTextVariables()
        {
            base.ActionMessage = new TextObject($"Fishing Spot ({WaterType})");
            base.DescriptionMessage = new TextObject("Press {KEY} to start fishing");
        }

        public override void OnUse(Agent userAgent)
        {
            base.OnUse(userAgent);
            if (!GameNetwork.IsServer) return;

            var representative = userAgent.MissionPeer.GetNetworkPeer().GetComponent<PersistentEmpireRepresentative>();
            var inventory = representative.GetInventory();

            if (!HasRequiredEquipment(inventory))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"You need a {RequiredRod} and {RequiredBait} to fish here.");
                userAgent.StopUsingGameObjectMT(true);
                return;
            }

            if (representative.GetSkillValue("Fishing") < RequiredFishingSkill)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"You need {RequiredFishingSkill} fishing skill to fish in these waters.");
                userAgent.StopUsingGameObjectMT(true);
                return;
            }

            StartFishing(userAgent, representative, inventory);
        }

        private bool HasRequiredEquipment(Inventory inventory)
        {
            return inventory.IsInventoryIncludes(RequiredRod) && inventory.IsInventoryIncludes(RequiredBait);
        }

        private void StartFishing(Agent userAgent, PersistentEmpireRepresentative representative, Inventory inventory)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            
            // Check if spot is overfished
            if (DateTime.UtcNow - LastFished < TimeSpan.FromMinutes(5) && FishAbundance < 50)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "This fishing spot seems overfished. Try again later.");
                userAgent.StopUsingGameObjectMT(true);
                return;
            }

            // Consume bait
            inventory.RemoveCountedItem(RequiredBait, 1);

            // Fishing process simulation
            var fishingResult = PerformFishing(representative, playerId);
            
            // Apply results
            foreach (var fish in fishingResult.CaughtFish)
            {
                inventory.AddCountedItem(fish.Key, fish.Value);
            }

            // Experience gain
            int currentSkill = representative.GetSkillValue("Fishing");
            int expGain = fishingResult.ExpGained;
            representative.SetSkillValue("Fishing", currentSkill + expGain);

            // Chance to catch junk or treasure
            if (fishingResult.JunkCaught.Count > 0)
            {
                foreach (var junk in fishingResult.JunkCaught)
                {
                    inventory.AddCountedItem(junk.Key, junk.Value);
                }
            }

            // Damage fishing rod over time
            if (randomizer.Next(100) < 5) // 5% chance
            {
                DamageFishingRod(inventory);
            }

            // Update spot abundance
            FishAbundance = Math.Max(10, FishAbundance - fishingResult.CaughtFish.Values.Sum());
            LastFished = DateTime.UtcNow;

            // Track player progress
            if (!PlayerFishingProgress.ContainsKey(playerId))
                PlayerFishingProgress[playerId] = 0;
            PlayerFishingProgress[playerId]++;

            // Send results to player
            SendFishingResults(representative.MissionPeer.GetNetworkPeer(), fishingResult);

            userAgent.StopUsingGameObjectMT(true);
        }

        private FishingResult PerformFishing(PersistentEmpireRepresentative representative, string playerId)
        {
            var result = new FishingResult();
            
            int fishingSkill = representative.GetSkillValue("Fishing");
            float skillMultiplier = 1.0f + (fishingSkill / 100f);
            float abundanceMultiplier = FishAbundance / 100f;
            
            // Base catch chance
            float catchChance = 0.7f * skillMultiplier * abundanceMultiplier;
            
            if (randomizer.NextDouble() < catchChance)
            {
                // Determine fish type and rarity
                var caughtFish = DetermineFishCatch(fishingSkill);
                result.CaughtFish[caughtFish.FishId] = caughtFish.Count;
                result.ExpGained = caughtFish.ExpValue;
                
                // Special catch effects
                if (caughtFish.IsRare)
                {
                    result.SpecialCatch = $"Rare catch: {caughtFish.FishId}!";
                    result.ExpGained *= 2;
                }
                
                // Check for legendary fish (very rare)
                if (HasRareFish && randomizer.Next(1000) < 1 + (fishingSkill / 50))
                {
                    var legendaryFish = GetLegendaryFish();
                    result.CaughtFish[legendaryFish] = 1;
                    result.SpecialCatch = $"LEGENDARY CATCH: {legendaryFish}!";
                    result.ExpGained += 100;
                }
            }
            else
            {
                // Failed to catch fish, but still gain some experience
                result.ExpGained = 1;
                
                // Chance to catch junk or treasure
                if (randomizer.Next(100) < 15) // 15% chance for junk/treasure
                {
                    var junkItem = GetJunkOrTreasure(fishingSkill);
                    result.JunkCaught[junkItem.ItemId] = junkItem.Count;
                    
                    if (junkItem.IsTreasure)
                    {
                        result.SpecialCatch = $"Treasure found: {junkItem.ItemId}!";
                        result.ExpGained += 20;
                    }
                }
            }
            
            // Weather and time bonuses
            ApplyEnvironmentalBonuses(result);
            
            return result;
        }

        private FishCatch DetermineFishCatch(int fishingSkill)
        {
            var fishTypes = GetAvailableFish();
            
            // Weight fish by skill requirements and rarity
            var weightedFish = new List<(FishData fish, float weight)>();
            
            foreach (var fish in fishTypes)
            {
                if (fishingSkill >= fish.RequiredSkill)
                {
                    float weight = fish.BaseWeight;
                    
                    // Increase weight for easier fish at lower skills
                    if (fishingSkill < fish.RequiredSkill + 20)
                    {
                        weight *= 2.0f;
                    }
                    
                    weightedFish.Add((fish, weight));
                }
            }
            
            if (weightedFish.Count == 0)
            {
                // Fallback to basic fish
                return new FishCatch
                {
                    FishId = "pe_small_fish",
                    Count = 1,
                    ExpValue = 2,
                    IsRare = false
                };
            }
            
            // Select fish based on weights
            float totalWeight = weightedFish.Sum(f => f.weight);
            float randomValue = (float)(randomizer.NextDouble() * totalWeight);
            float currentWeight = 0;
            
            foreach (var (fish, weight) in weightedFish)
            {
                currentWeight += weight;
                if (randomValue <= currentWeight)
                {
                    int count = randomizer.Next(fish.MinCount, fish.MaxCount + 1);
                    return new FishCatch
                    {
                        FishId = fish.FishId,
                        Count = count,
                        ExpValue = fish.ExpValue * count,
                        IsRare = fish.IsRare
                    };
                }
            }
            
            // Fallback
            var fallback = weightedFish.First().fish;
            return new FishCatch
            {
                FishId = fallback.FishId,
                Count = 1,
                ExpValue = fallback.ExpValue,
                IsRare = fallback.IsRare
            };
        }

        private List<FishData> GetAvailableFish()
        {
            var allFish = new List<FishData>();
            
            switch (WaterType.ToLower())
            {
                case "freshwater":
                    allFish.AddRange(GetFreshwaterFish());
                    break;
                case "saltwater":
                    allFish.AddRange(GetSaltwaterFish());
                    break;
                case "deep":
                    allFish.AddRange(GetDeepWaterFish());
                    break;
                default:
                    allFish.AddRange(GetFreshwaterFish());
                    break;
            }
            
            return allFish;
        }

        private List<FishData> GetFreshwaterFish()
        {
            return new List<FishData>
            {
                new FishData { FishId = "pe_small_fish", RequiredSkill = 0, BaseWeight = 50, MinCount = 1, MaxCount = 3, ExpValue = 2 },
                new FishData { FishId = "pe_trout", RequiredSkill = 10, BaseWeight = 30, MinCount = 1, MaxCount = 2, ExpValue = 5 },
                new FishData { FishId = "pe_bass", RequiredSkill = 25, BaseWeight = 20, MinCount = 1, MaxCount = 1, ExpValue = 8 },
                new FishData { FishId = "pe_pike", RequiredSkill = 40, BaseWeight = 15, MinCount = 1, MaxCount = 1, ExpValue = 12, IsRare = true },
                new FishData { FishId = "pe_golden_carp", RequiredSkill = 60, BaseWeight = 5, MinCount = 1, MaxCount = 1, ExpValue = 20, IsRare = true }
            };
        }

        private List<FishData> GetSaltwaterFish()
        {
            return new List<FishData>
            {
                new FishData { FishId = "pe_mackerel", RequiredSkill = 15, BaseWeight = 40, MinCount = 1, MaxCount = 2, ExpValue = 6 },
                new FishData { FishId = "pe_cod", RequiredSkill = 30, BaseWeight = 25, MinCount = 1, MaxCount = 1, ExpValue = 10 },
                new FishData { FishId = "pe_tuna", RequiredSkill = 50, BaseWeight = 15, MinCount = 1, MaxCount = 1, ExpValue = 15, IsRare = true },
                new FishData { FishId = "pe_shark", RequiredSkill = 75, BaseWeight = 5, MinCount = 1, MaxCount = 1, ExpValue = 30, IsRare = true }
            };
        }

        private List<FishData> GetDeepWaterFish()
        {
            return new List<FishData>
            {
                new FishData { FishId = "pe_deep_fish", RequiredSkill = 35, BaseWeight = 30, MinCount = 1, MaxCount = 1, ExpValue = 12 },
                new FishData { FishId = "pe_anglerfish", RequiredSkill = 60, BaseWeight = 15, MinCount = 1, MaxCount = 1, ExpValue = 18, IsRare = true },
                new FishData { FishId = "pe_giant_squid", RequiredSkill = 80, BaseWeight = 3, MinCount = 1, MaxCount = 1, ExpValue = 40, IsRare = true }
            };
        }

        private string GetLegendaryFish()
        {
            var legendaryFish = new[] { "pe_dragon_fish", "pe_phoenix_salmon", "pe_crystal_eel", "pe_void_leviathan" };
            return legendaryFish[randomizer.Next(legendaryFish.Length)];
        }

        private JunkTreasure GetJunkOrTreasure(int fishingSkill)
        {
            // Higher skill = better chance for treasure
            bool isTreasure = randomizer.Next(100) < (fishingSkill / 10);
            
            if (isTreasure)
            {
                var treasures = new[]
                {
                    new JunkTreasure { ItemId = "pe_old_coin", Count = randomizer.Next(1, 5), IsTreasure = true },
                    new JunkTreasure { ItemId = "pe_pearl", Count = 1, IsTreasure = true },
                    new JunkTreasure { ItemId = "pe_message_bottle", Count = 1, IsTreasure = true },
                    new JunkTreasure { ItemId = "pe_ancient_ring", Count = 1, IsTreasure = true }
                };
                return treasures[randomizer.Next(treasures.Length)];
            }
            else
            {
                var junk = new[]
                {
                    new JunkTreasure { ItemId = "pe_old_boot", Count = 1, IsTreasure = false },
                    new JunkTreasure { ItemId = "pe_rusty_can", Count = 1, IsTreasure = false },
                    new JunkTreasure { ItemId = "pe_seaweed", Count = randomizer.Next(1, 3), IsTreasure = false },
                    new JunkTreasure { ItemId = "pe_driftwood", Count = randomizer.Next(1, 2), IsTreasure = false }
                };
                return junk[randomizer.Next(junk.Length)];
            }
        }

        private void ApplyEnvironmentalBonuses(FishingResult result)
        {
            // Time of day bonuses
            int hour = DateTime.UtcNow.Hour;
            if ((hour >= 5 && hour <= 7) || (hour >= 18 && hour <= 20)) // Dawn/Dusk
            {
                result.ExpGained = (int)(result.ExpGained * 1.2f);
                if (randomizer.Next(100) < 20) // 20% chance for bonus fish
                {
                    if (result.CaughtFish.Count > 0)
                    {
                        var firstFish = result.CaughtFish.First();
                        result.CaughtFish[firstFish.Key]++;
                    }
                }
            }
            
            // Weather bonuses (simulated)
            if (randomizer.Next(100) < 10) // 10% chance for good weather
            {
                result.ExpGained = (int)(result.ExpGained * 1.1f);
            }
            
            // Seasonal bonuses
            int dayOfYear = DateTime.UtcNow.DayOfYear;
            if (WaterType.ToLower() == "freshwater" && (dayOfYear >= 91 && dayOfYear <= 182)) // Spring
            {
                result.ExpGained = (int)(result.ExpGained * 1.15f);
            }
            else if (WaterType.ToLower() == "saltwater" && (dayOfYear >= 183 && dayOfYear <= 273)) // Summer
            {
                result.ExpGained = (int)(result.ExpGained * 1.15f);
            }
        }

        private void DamageFishingRod(Inventory inventory)
        {
            if (inventory.IsInventoryIncludes(RequiredRod))
            {
                inventory.RemoveCountedItem(RequiredRod, 1);
                inventory.AddCountedItem("pe_damaged_fishing_rod", 1);
            }
        }

        private void SendFishingResults(NetworkCommunicator peer, FishingResult result)
        {
            string message = "Fishing Results: ";
            
            if (result.CaughtFish.Count > 0)
            {
                foreach (var fish in result.CaughtFish)
                {
                    message += $"{fish.Value}x {fish.Key}, ";
                }
                message = message.TrimEnd(',', ' ');
            }
            else
            {
                message += "Nothing caught";
            }
            
            if (result.JunkCaught.Count > 0)
            {
                message += " | Junk: ";
                foreach (var junk in result.JunkCaught)
                {
                    message += $"{junk.Value}x {junk.Key}, ";
                }
                message = message.TrimEnd(',', ' ');
            }
            
            if (!string.IsNullOrEmpty(result.SpecialCatch))
            {
                message += $" | {result.SpecialCatch}";
            }
            
            message += $" | EXP: +{result.ExpGained}";
            
            InformationComponent.Instance.SendMessageToPlayer(peer, message);
        }

        public override ScriptComponentBehavior.TickRequirement GetTickRequirement()
        {
            return ScriptComponentBehavior.TickRequirement.TickOccasionally;
        }

        protected override void OnTickOccasionally(float currentFrameDeltaTime)
        {
            // Replenish fish abundance over time
            if (DateTime.UtcNow - LastFished > TimeSpan.FromMinutes(30) && FishAbundance < 100)
            {
                FishAbundance = Math.Min(100, FishAbundance + 5);
            }
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            string abundance = FishAbundance > 80 ? "Abundant" : 
                              FishAbundance > 50 ? "Moderate" : 
                              FishAbundance > 20 ? "Scarce" : "Depleted";
            
            return $"{WaterType} fishing spot ({abundance})";
        }

        private class FishingResult
        {
            public Dictionary<string, int> CaughtFish = new Dictionary<string, int>();
            public Dictionary<string, int> JunkCaught = new Dictionary<string, int>();
            public int ExpGained = 0;
            public string SpecialCatch = "";
        }

        private class FishData
        {
            public string FishId { get; set; }
            public int RequiredSkill { get; set; }
            public float BaseWeight { get; set; }
            public int MinCount { get; set; }
            public int MaxCount { get; set; }
            public int ExpValue { get; set; }
            public bool IsRare { get; set; } = false;
        }

        private class FishCatch
        {
            public string FishId { get; set; }
            public int Count { get; set; }
            public int ExpValue { get; set; }
            public bool IsRare { get; set; }
        }

        private class JunkTreasure
        {
            public string ItemId { get; set; }
            public int Count { get; set; }
            public bool IsTreasure { get; set; }
        }
    }
}