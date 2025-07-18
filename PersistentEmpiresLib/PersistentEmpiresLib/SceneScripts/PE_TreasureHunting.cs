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
    /// Treasure hunting system with maps, clues, and hidden treasures
    /// </summary>
    public class PE_TreasureHunting : PE_UsableFromDistance
    {
        public string LocationName = "Mysterious Cave";
        public bool HasTreasure = true;
        public int TreasureValue = 1000;
        public string RequiredItem = ""; // Optional item needed to access
        public bool RequiresMap = false;
        public string TreasureMapId = "";
        public int DigDifficulty = 3; // Number of attempts needed
        
        private static List<TreasureMap> AvailableMaps = new List<TreasureMap>();
        private static Dictionary<string, List<string>> PlayerMaps = new Dictionary<string, List<string>>();
        private static Dictionary<string, TreasureHuntProgress> ActiveHunts = new Dictionary<string, TreasureHuntProgress>();
        
        private bool IsTreasureFound = false;
        private DateTime LastDug = DateTime.MinValue;
        private int DigAttempts = 0;
        private Random randomizer = new Random();

        protected override bool LockUserFrames => true;
        protected override bool LockUserPositions => true;

        protected override void OnInit()
        {
            base.OnInit();
            GenerateTreasureMaps();
            SetTextVariables();
        }

        private void SetTextVariables()
        {
            if (HasTreasure && !IsTreasureFound)
            {
                base.ActionMessage = new TextObject($"Dig Site - {LocationName}");
                base.DescriptionMessage = new TextObject("Press {KEY} to search for treasure");
            }
            else if (IsTreasureFound)
            {
                base.ActionMessage = new TextObject($"Empty Site - {LocationName}");
                base.DescriptionMessage = new TextObject("This treasure has already been found");
            }
            else
            {
                base.ActionMessage = new TextObject($"Treasure Map Vendor");
                base.DescriptionMessage = new TextObject("Press {KEY} to browse treasure maps");
            }
        }

        public override void OnUse(Agent userAgent)
        {
            base.OnUse(userAgent);
            if (!GameNetwork.IsServer) return;

            var representative = userAgent.MissionPeer.GetNetworkPeer().GetComponent<PersistentEmpireRepresentative>();
            
            if (HasTreasure)
            {
                HandleTreasureDigging(representative);
            }
            else
            {
                OpenMapVendor(representative);
            }
            
            userAgent.StopUsingGameObjectMT(true);
        }

        private void HandleTreasureDigging(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            if (IsTreasureFound)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "This treasure has already been found by someone else.");
                return;
            }

            // Check if player has required items
            var inventory = representative.GetInventory();
            
            if (!string.IsNullOrEmpty(RequiredItem) && !inventory.IsInventoryIncludes(RequiredItem))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"You need a {RequiredItem} to search here.");
                return;
            }

            if (RequiresMap && !PlayerHasRequiredMap(playerId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You need the treasure map for this location.");
                return;
            }

            // Check cooldown
            if (DateTime.UtcNow - LastDug < TimeSpan.FromMinutes(5))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You need to rest before digging again.");
                return;
            }

            StartDigging(representative, playerId, inventory);
        }

        private void StartDigging(PersistentEmpireRepresentative representative, string playerId, Inventory inventory)
        {
            LastDig = DateTime.UtcNow;
            DigAttempts++;

            // Consume digging tool durability
            if (inventory.IsInventoryIncludes("pe_shovel"))
            {
                if (randomizer.Next(100) < 10) // 10% chance to break shovel
                {
                    inventory.RemoveCountedItem("pe_shovel", 1);
                    inventory.AddCountedItem("pe_broken_shovel", 1);
                    InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                        "Your shovel broke! You'll need to get it repaired or find a new one.");
                }
            }

            var digResult = PerformDigging(representative, playerId);
            
            if (digResult.TreasureFound)
            {
                // Found the main treasure!
                IsTreasureFound = true;
                
                foreach (var treasure in digResult.TreasureItems)
                {
                    inventory.AddCountedItem(treasure.Key, treasure.Value);
                }
                
                if (digResult.GoldFound > 0)
                {
                    representative.GoldGain(digResult.GoldFound);
                }

                // Award experience
                int currentArchaeology = representative.GetSkillValue("Archaeology");
                representative.SetSkillValue("Archaeology", currentArchaeology + digResult.ExpGained);

                SetTextVariables();
                
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"TREASURE FOUND! You discovered: {FormatTreasure(digResult)}");

                // Broadcast to all players
                BroadcastTreasureDiscovery(representative.MissionPeer.GetNetworkPeer().UserName, LocationName, digResult.GoldFound);

                LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.PlayerFoundTreasure, 
                    null, new object[] { LocationName, digResult.GoldFound });
            }
            else if (digResult.MinorFindFound)
            {
                // Found something minor
                foreach (var item in digResult.MinorItems)
                {
                    inventory.AddCountedItem(item.Key, item.Value);
                }
                
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"You found something: {FormatMinorFinds(digResult.MinorItems)}");
            }
            else
            {
                // Nothing found this time
                var clues = GenerateClues(DigAttempts);
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Nothing found... {clues}");
            }

            // Update hunt progress
            UpdateHuntProgress(playerId, digResult);
        }

        private void OpenMapVendor(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var playerMaps = GetPlayerMaps(playerId);

            var message = "=== TREASURE MAP VENDOR ===\n";
            message += $"Your Maps: {playerMaps.Count}\n";
            message += $"Available Maps: {AvailableMaps.Count(m => !playerMaps.Contains(m.Id))}\n\n";

            message += "Commands:\n";
            message += "  /treasure buy <map_id> - Purchase treasure map\n";
            message += "  /treasure maps - View your maps\n";
            message += "  /treasure available - Browse available maps\n";
            message += "  /treasure clues <map_id> - Get clues for your map\n";
            message += "  /treasure progress - View hunt progress\n\n";

            message += "Featured Maps:\n";
            var featuredMaps = AvailableMaps.Where(m => !playerMaps.Contains(m.Id))
                .OrderByDescending(m => m.EstimatedValue).Take(3);
            
            foreach (var map in featuredMaps)
            {
                message += $"  [{map.Id}] {map.Title} - {map.Price} gold (Est. value: {map.EstimatedValue})\n";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        public void ProcessTreasureCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 2) return;

            string command = args[1].ToLower();

            switch (command)
            {
                case "buy":
                    HandleBuyMapCommand(representative, args);
                    break;
                case "maps":
                    HandleMapsCommand(representative);
                    break;
                case "available":
                    HandleAvailableCommand(representative);
                    break;
                case "clues":
                    HandleCluesCommand(representative, args);
                    break;
                case "progress":
                    HandleProgressCommand(representative);
                    break;
                default:
                    InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                        "Unknown treasure command. Use /treasure for help.");
                    break;
            }
        }

        private void HandleBuyMapCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /treasure buy <map_id>");
                return;
            }

            string mapId = args[2];
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            var map = AvailableMaps.FirstOrDefault(m => m.Id == mapId);
            if (map == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Map not found.");
                return;
            }

            var playerMaps = GetPlayerMaps(playerId);
            if (playerMaps.Contains(mapId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You already own this map.");
                return;
            }

            if (representative.Gold < map.Price)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Insufficient gold. Map costs {map.Price} gold.");
                return;
            }

            representative.GoldLoss(map.Price);
            
            if (!PlayerMaps.ContainsKey(playerId))
                PlayerMaps[playerId] = new List<string>();
            PlayerMaps[playerId].Add(mapId);

            // Add physical map item to inventory
            representative.GetInventory().AddCountedItem($"pe_treasure_map_{mapId}", 1);

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Purchased treasure map: {map.Title}. Check your inventory!");

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.PlayerBoughtItem, 
                null, new object[] { $"treasure_map_{mapId}", map.Price });
        }

        private void HandleMapsCommand(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var playerMaps = GetPlayerMaps(playerId);

            if (playerMaps.Count == 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You don't own any treasure maps.");
                return;
            }

            var message = "=== YOUR TREASURE MAPS ===\n";

            foreach (string mapId in playerMaps)
            {
                var map = AvailableMaps.FirstOrDefault(m => m.Id == mapId);
                if (map != null)
                {
                    message += $"[{map.Id}] {map.Title}\n";
                    message += $"  Difficulty: {map.Difficulty}\n";
                    message += $"  Est. Value: {map.EstimatedValue} gold\n";
                    message += $"  Hints: {map.Clues.Count}\n\n";
                }
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleAvailableCommand(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var playerMaps = GetPlayerMaps(playerId);
            var availableMaps = AvailableMaps.Where(m => !playerMaps.Contains(m.Id)).ToList();

            if (availableMaps.Count == 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "No new maps available for purchase.");
                return;
            }

            var message = "=== AVAILABLE TREASURE MAPS ===\n";

            foreach (var map in availableMaps.OrderBy(m => m.Price))
            {
                message += $"[{map.Id}] {map.Title} - {map.Price} gold\n";
                message += $"  Difficulty: {map.Difficulty}\n";
                message += $"  Est. Value: {map.EstimatedValue} gold\n";
                message += $"  Description: {map.Description}\n\n";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleCluesCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /treasure clues <map_id>");
                return;
            }

            string mapId = args[2];
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            var playerMaps = GetPlayerMaps(playerId);
            if (!playerMaps.Contains(mapId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You don't own this map.");
                return;
            }

            var map = AvailableMaps.FirstOrDefault(m => m.Id == mapId);
            if (map == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Map not found.");
                return;
            }

            var message = $"=== CLUES FOR {map.Title.ToUpper()} ===\n";
            message += $"Location: {map.LocationHint}\n\n";

            for (int i = 0; i < map.Clues.Count; i++)
            {
                message += $"Clue {i + 1}: {map.Clues[i]}\n";
            }

            if (map.RequiredItems.Count > 0)
            {
                message += $"\nRequired Items: {string.Join(", ", map.RequiredItems)}\n";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleProgressCommand(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            if (!ActiveHunts.ContainsKey(playerId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You have no active treasure hunts.");
                return;
            }

            var progress = ActiveHunts[playerId];
            var message = "=== TREASURE HUNT PROGRESS ===\n";
            message += $"Locations Searched: {progress.LocationsSearched}\n";
            message += $"Treasures Found: {progress.TreasuresFound}\n";
            message += $"Total Value Found: {progress.TotalValueFound} gold\n";
            message += $"Archaeology Skill: {representative.GetSkillValue("Archaeology")}\n";

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private DigResult PerformDigging(PersistentEmpireRepresentative representative, string playerId)
        {
            var result = new DigResult();
            
            int archaeologySkill = representative.GetSkillValue("Archaeology");
            float skillMultiplier = 1.0f + (archaeologySkill / 100f);
            
            // Base chance to find treasure increases with attempts
            float treasureChance = 0.1f + (DigAttempts * 0.15f) + (archaeologySkill * 0.01f);
            
            if (DigAttempts >= DigDifficulty && randomizer.NextDouble() < treasureChance)
            {
                // Found the main treasure!
                result.TreasureFound = true;
                result.GoldFound = (int)(TreasureValue * skillMultiplier);
                result.ExpGained = 50 + (TreasureValue / 100);
                
                // Add treasure items
                result.TreasureItems = GenerateTreasureItems();
            }
            else if (randomizer.NextDouble() < 0.3f + (archaeologySkill * 0.005f))
            {
                // Found minor items
                result.MinorFindFound = true;
                result.MinorItems = GenerateMinorFinds();
                result.ExpGained = 5 + (archaeologySkill / 10);
            }
            else
            {
                // Nothing found
                result.ExpGained = 1;
            }
            
            return result;
        }

        private Dictionary<string, int> GenerateTreasureItems()
        {
            var treasures = new Dictionary<string, int>();
            
            // Always include some valuable items
            treasures["pe_ancient_coin"] = randomizer.Next(5, 15);
            treasures["pe_jeweled_dagger"] = 1;
            
            // Chance for rare items
            if (randomizer.Next(100) < 30)
            {
                treasures["pe_ancient_artifact"] = 1;
            }
            
            if (randomizer.Next(100) < 20)
            {
                treasures["pe_precious_gem"] = randomizer.Next(1, 3);
            }
            
            if (randomizer.Next(100) < 10)
            {
                treasures["pe_legendary_relic"] = 1;
            }
            
            return treasures;
        }

        private Dictionary<string, int> GenerateMinorFinds()
        {
            var items = new Dictionary<string, int>();
            
            var minorFinds = new[]
            {
                "pe_old_coin", "pe_rusty_key", "pe_broken_pottery", 
                "pe_ancient_bone", "pe_metal_fragment", "pe_carved_stone"
            };
            
            int numItems = randomizer.Next(1, 4);
            for (int i = 0; i < numItems; i++)
            {
                string item = minorFinds[randomizer.Next(minorFinds.Length)];
                if (!items.ContainsKey(item))
                    items[item] = 0;
                items[item] += randomizer.Next(1, 3);
            }
            
            return items;
        }

        private string GenerateClues(int digAttempts)
        {
            return digAttempts switch
            {
                1 => "The ground feels different here...",
                2 => "You sense something valuable nearby...",
                3 => "The earth seems to hide ancient secrets...",
                4 => "You're getting closer to something important...",
                _ => "Keep searching, the treasure must be here somewhere!"
            };
        }

        private bool PlayerHasRequiredMap(string playerId)
        {
            if (string.IsNullOrEmpty(TreasureMapId)) return true;
            
            var playerMaps = GetPlayerMaps(playerId);
            return playerMaps.Contains(TreasureMapId);
        }

        private List<string> GetPlayerMaps(string playerId)
        {
            return PlayerMaps.ContainsKey(playerId) ? PlayerMaps[playerId] : new List<string>();
        }

        private void UpdateHuntProgress(string playerId, DigResult result)
        {
            if (!ActiveHunts.ContainsKey(playerId))
            {
                ActiveHunts[playerId] = new TreasureHuntProgress { PlayerId = playerId };
            }

            var progress = ActiveHunts[playerId];
            progress.LocationsSearched++;
            
            if (result.TreasureFound)
            {
                progress.TreasuresFound++;
                progress.TotalValueFound += result.GoldFound;
            }
        }

        private void GenerateTreasureMaps()
        {
            if (AvailableMaps.Count > 0) return; // Already generated

            AvailableMaps.AddRange(new[]
            {
                new TreasureMap
                {
                    Id = "map_001",
                    Title = "Pirate's Hidden Cache",
                    Description = "A weathered map showing the location of a pirate's treasure.",
                    Price = 500,
                    EstimatedValue = 2000,
                    Difficulty = TreasureDifficulty.Easy,
                    LocationHint = "Near the old lighthouse by the shore",
                    Clues = new List<string>
                    {
                        "Where the waves meet the land",
                        "A beacon once guided ships to safety",
                        "Buried beneath the twisted oak"
                    },
                    RequiredItems = new List<string> { "pe_shovel" }
                },
                new TreasureMap
                {
                    Id = "map_002",
                    Title = "Ancient Burial Hoard",
                    Description = "Marks the resting place of an ancient king's wealth.",
                    Price = 1200,
                    EstimatedValue = 5000,
                    Difficulty = TreasureDifficulty.Medium,
                    LocationHint = "In the shadow of the great mountain",
                    Clues = new List<string>
                    {
                        "Where stone giants watch over the valley",
                        "Three standing stones mark the path",
                        "The king's final rest lies beneath"
                    },
                    RequiredItems = new List<string> { "pe_shovel", "pe_ancient_key" }
                },
                new TreasureMap
                {
                    Id = "map_003",
                    Title = "Dragon's Lost Hoard",
                    Description = "Legend speaks of a dragon's treasure hidden long ago.",
                    Price = 2500,
                    EstimatedValue = 10000,
                    Difficulty = TreasureDifficulty.Hard,
                    LocationHint = "Deep within the forbidden caves",
                    Clues = new List<string>
                    {
                        "Where fire once dwelt in darkness",
                        "The path is guarded by ancient traps",
                        "Only the brave may claim the prize"
                    },
                    RequiredItems = new List<string> { "pe_shovel", "pe_torch", "pe_rope" }
                }
            });
        }

        private string FormatTreasure(DigResult result)
        {
            var parts = new List<string>();
            
            if (result.GoldFound > 0)
                parts.Add($"{result.GoldFound} gold");
            
            foreach (var item in result.TreasureItems)
                parts.Add($"{item.Value}x {item.Key}");
            
            return string.Join(", ", parts);
        }

        private string FormatMinorFinds(Dictionary<string, int> items)
        {
            return string.Join(", ", items.Select(i => $"{i.Value}x {i.Key}"));
        }

        private void BroadcastTreasureDiscovery(string playerName, string location, int value)
        {
            string message = $"🏆 {playerName} discovered a treasure worth {value} gold at {location}!";
            
            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.IsConnectionActive)
                {
                    InformationComponent.Instance.SendMessageToPlayer(peer, message);
                }
            }
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            if (HasTreasure)
            {
                if (IsTreasureFound)
                    return $"Empty dig site - {LocationName}";
                else
                    return $"Treasure site - {LocationName} (Attempts: {DigAttempts})";
            }
            else
            {
                return "Treasure Map Vendor";
            }
        }

        public enum TreasureDifficulty
        {
            Easy,
            Medium,
            Hard,
            Legendary
        }

        private class TreasureMap
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public int Price { get; set; }
            public int EstimatedValue { get; set; }
            public TreasureDifficulty Difficulty { get; set; }
            public string LocationHint { get; set; }
            public List<string> Clues { get; set; } = new List<string>();
            public List<string> RequiredItems { get; set; } = new List<string>();
        }

        private class DigResult
        {
            public bool TreasureFound { get; set; } = false;
            public bool MinorFindFound { get; set; } = false;
            public Dictionary<string, int> TreasureItems { get; set; } = new Dictionary<string, int>();
            public Dictionary<string, int> MinorItems { get; set; } = new Dictionary<string, int>();
            public int GoldFound { get; set; } = 0;
            public int ExpGained { get; set; } = 0;
        }

        private class TreasureHuntProgress
        {
            public string PlayerId { get; set; }
            public int LocationsSearched { get; set; } = 0;
            public int TreasuresFound { get; set; } = 0;
            public int TotalValueFound { get; set; } = 0;
        }
    }
}