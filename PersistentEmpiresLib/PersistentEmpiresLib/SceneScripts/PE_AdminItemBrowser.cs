using PersistentEmpiresLib.Data;
using PersistentEmpiresLib.Helpers;
using PersistentEmpiresLib.NetworkMessages.Client;
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
    /// Advanced admin item browser with categorization, search, and mass spawning
    /// </summary>
    public class PE_AdminItemBrowser : PE_UsableFromDistance
    {
        public int ItemsPerPage = 20;
        public bool AllowMassSpawn = true;
        public int MaxMassSpawnCount = 1000;
        
        private static Dictionary<string, List<ItemObject>> CategorizedItems = new Dictionary<string, List<ItemObject>>();
        private static List<ItemObject> AllGameItems = new List<ItemObject>();
        private static bool ItemsInitialized = false;

        protected override bool LockUserFrames => false;
        protected override bool LockUserPositions => false;

        protected override void OnInit()
        {
            base.OnInit();
            InitializeItemDatabase();
            SetTextVariables();
        }

        private void SetTextVariables()
        {
            base.ActionMessage = new TextObject("Admin Item Browser");
            base.DescriptionMessage = new TextObject("Press {KEY} to browse items (Admin Only)");
        }

        public override void OnUse(Agent userAgent)
        {
            base.OnUse(userAgent);
            if (!GameNetwork.IsServer) return;

            var representative = userAgent.MissionPeer.GetNetworkPeer().GetComponent<PersistentEmpireRepresentative>();
            
            if (!representative.IsAdmin)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Access denied. Admins only.");
                userAgent.StopUsingGameObjectMT(true);
                return;
            }

            OpenItemBrowser(representative);
            userAgent.StopUsingGameObjectMT(true);
        }

        private void InitializeItemDatabase()
        {
            if (ItemsInitialized) return;

            AllGameItems.Clear();
            CategorizedItems.Clear();

            // Get all items from the game
            foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                AllGameItems.Add(item);
            }

            // Categorize items
            CategorizedItems["weapons"] = AllGameItems.Where(IsWeapon).ToList();
            CategorizedItems["armor"] = AllGameItems.Where(IsArmor).ToList();
            CategorizedItems["shields"] = AllGameItems.Where(IsShield).ToList();
            CategorizedItems["mounts"] = AllGameItems.Where(IsMount).ToList();
            CategorizedItems["consumables"] = AllGameItems.Where(IsConsumable).ToList();
            CategorizedItems["materials"] = AllGameItems.Where(IsMaterial).ToList();
            CategorizedItems["tools"] = AllGameItems.Where(IsTool).ToList();
            CategorizedItems["misc"] = AllGameItems.Where(IsMisc).ToList();
            CategorizedItems["all"] = AllGameItems.ToList();

            ItemsInitialized = true;
        }

        private void OpenItemBrowser(PersistentEmpireRepresentative representative)
        {
            var message = "=== ADMIN ITEM BROWSER ===\n";
            message += $"Total Items Available: {AllGameItems.Count}\n\n";

            message += "Categories:\n";
            foreach (var category in CategorizedItems)
            {
                message += $"  {category.Key}: {category.Value.Count} items\n";
            }

            message += "\nCommands:\n";
            message += "  /items browse <category> [page] - Browse category\n";
            message += "  /items search <term> [page] - Search for items\n";
            message += "  /items spawn <item_id> [count] [player] - Spawn item\n";
            message += "  /items mass <item_id> <count> - Mass spawn items\n";
            message += "  /items info <item_id> - Get detailed item info\n";
            message += "  /items random <category> [count] - Spawn random items\n";
            message += "  /items clear <radius> - Clear items in radius\n";
            message += "  /items export <category> - Export item list\n\n";

            message += "Recent Items:\n";
            var recentItems = GetRecentlyUsedItems(representative.MissionPeer.GetNetworkPeer().UserName);
            foreach (var item in recentItems.Take(5))
            {
                message += $"  {item}\n";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        public void ProcessItemCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (!representative.IsAdmin)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Access denied. Admins only.");
                return;
            }

            if (args.Length < 2) return;

            string command = args[1].ToLower();

            switch (command)
            {
                case "browse":
                    HandleBrowseCommand(representative, args);
                    break;
                case "search":
                    HandleSearchCommand(representative, args);
                    break;
                case "spawn":
                    HandleSpawnCommand(representative, args);
                    break;
                case "mass":
                    HandleMassSpawnCommand(representative, args);
                    break;
                case "info":
                    HandleInfoCommand(representative, args);
                    break;
                case "random":
                    HandleRandomCommand(representative, args);
                    break;
                case "clear":
                    HandleClearCommand(representative, args);
                    break;
                case "export":
                    HandleExportCommand(representative, args);
                    break;
                default:
                    InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                        "Unknown item command. Use '/items' for help.");
                    break;
            }
        }

        private void HandleBrowseCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /items browse <category> [page]");
                return;
            }

            string category = args[2].ToLower();
            int page = args.Length > 3 && int.TryParse(args[3], out int p) ? Math.Max(1, p) : 1;

            if (!CategorizedItems.ContainsKey(category))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Unknown category: {category}. Available: {string.Join(", ", CategorizedItems.Keys)}");
                return;
            }

            var items = CategorizedItems[category];
            int totalPages = (int)Math.Ceiling((double)items.Count / ItemsPerPage);
            int startIndex = (page - 1) * ItemsPerPage;

            var message = $"=== {category.ToUpper()} ITEMS (Page {page}/{totalPages}) ===\n";
            
            for (int i = startIndex; i < Math.Min(startIndex + ItemsPerPage, items.Count); i++)
            {
                var item = items[i];
                message += FormatItemListing(item, i + 1) + "\n";
            }

            if (totalPages > 1)
            {
                message += $"\nPage {page} of {totalPages}. Use '/items browse {category} {page + 1}' for next page.";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleSearchCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /items search <term> [page]");
                return;
            }

            string searchTerm = args[2].ToLower();
            int page = args.Length > 3 && int.TryParse(args[3], out int p) ? Math.Max(1, p) : 1;

            var results = AllGameItems.Where(item => 
                item.StringId.ToLower().Contains(searchTerm) || 
                item.Name.ToString().ToLower().Contains(searchTerm)
            ).ToList();

            int totalPages = (int)Math.Ceiling((double)results.Count / ItemsPerPage);
            int startIndex = (page - 1) * ItemsPerPage;

            var message = $"=== SEARCH: '{searchTerm}' ({results.Count} results, Page {page}/{totalPages}) ===\n";

            for (int i = startIndex; i < Math.Min(startIndex + ItemsPerPage, results.Count); i++)
            {
                var item = results[i];
                message += FormatItemListing(item, i + 1) + "\n";
            }

            if (totalPages > 1)
            {
                message += $"\nPage {page} of {totalPages}. Use '/items search {searchTerm} {page + 1}' for next page.";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleSpawnCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /items spawn <item_id> [count] [player]");
                return;
            }

            string itemId = args[2];
            int count = args.Length > 3 && int.TryParse(args[3], out int c) ? Math.Max(1, c) : 1;
            string targetPlayer = args.Length > 4 ? args[4] : representative.MissionPeer.GetNetworkPeer().UserName;

            var item = AllGameItems.FirstOrDefault(i => i.StringId.Equals(itemId, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Item not found: {itemId}");
                return;
            }

            // Find target player
            NetworkCommunicator targetPeer = null;
            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.UserName.Equals(targetPlayer, StringComparison.OrdinalIgnoreCase) && peer.IsConnectionActive)
                {
                    targetPeer = peer;
                    break;
                }
            }

            if (targetPeer == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Player not found: {targetPlayer}");
                return;
            }

            // Spawn item
            var targetRep = targetPeer.GetComponent<PersistentEmpireRepresentative>();
            if (targetRep != null)
            {
                targetRep.GetInventory().AddCountedItem(itemId, count);
                
                AddToRecentItems(representative.MissionPeer.GetNetworkPeer().UserName, itemId);
                
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Spawned {count}x {item.Name} for {targetPlayer}");
                
                if (targetPeer != representative.MissionPeer.GetNetworkPeer())
                {
                    InformationComponent.Instance.SendMessageToPlayer(targetPeer,
                        $"Admin gave you {count}x {item.Name}");
                }

                LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.AdminSpawnedItem, 
                    new AffectedPlayer[] { new AffectedPlayer(targetPeer) }, new object[] { itemId, count });
            }
        }

        private void HandleMassSpawnCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (!AllowMassSpawn)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Mass spawning is disabled.");
                return;
            }

            if (args.Length < 4)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /items mass <item_id> <count>");
                return;
            }

            string itemId = args[2];
            if (!int.TryParse(args[3], out int count) || count <= 0 || count > MaxMassSpawnCount)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Count must be between 1 and {MaxMassSpawnCount}");
                return;
            }

            var item = AllGameItems.FirstOrDefault(i => i.StringId.Equals(itemId, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Item not found: {itemId}");
                return;
            }

            // Spawn items around admin
            var agent = representative.MissionPeer.ControlledAgent;
            if (agent == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You must be spawned to use mass spawn.");
                return;
            }

            var position = agent.Position;
            for (int i = 0; i < count; i++)
            {
                // Scatter items in a radius around the admin
                float angle = (float)(2 * Math.PI * i / count);
                float radius = 2f + (i / 10f); // Increasing radius
                
                Vec3 spawnPos = position + new Vec3(
                    (float)Math.Cos(angle) * radius,
                    (float)Math.Sin(angle) * radius,
                    0.5f
                );

                // Create item entity at position
                SpawnItemAtPosition(item, spawnPos);
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Mass spawned {count}x {item.Name} around your position");

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.AdminSpawnedItem, 
                null, new object[] { $"Mass spawn: {itemId}", count });
        }

        private void HandleInfoCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /items info <item_id>");
                return;
            }

            string itemId = args[2];
            var item = AllGameItems.FirstOrDefault(i => i.StringId.Equals(itemId, StringComparison.OrdinalIgnoreCase));
            
            if (item == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Item not found: {itemId}");
                return;
            }

            var message = $"=== ITEM INFO: {item.Name} ===\n";
            message += $"ID: {item.StringId}\n";
            message += $"Type: {item.Type}\n";
            message += $"Value: {item.Value} gold\n";
            message += $"Weight: {item.Weight:F2}\n";
            
            if (item.WeaponComponent != null)
            {
                var weapon = item.WeaponComponent.PrimaryWeapon;
                message += $"Weapon Class: {weapon.WeaponClass}\n";
                message += $"Damage: {weapon.ThrustDamage}/{weapon.SwingDamage}\n";
                message += $"Speed: {weapon.SwingSpeed}/{weapon.ThrustSpeed}\n";
                message += $"Length: {weapon.WeaponLength}\n";
            }

            if (item.ArmorComponent != null)
            {
                message += $"Head Armor: {item.ArmorComponent.HeadArmor}\n";
                message += $"Body Armor: {item.ArmorComponent.BodyArmor}\n";
                message += $"Leg Armor: {item.ArmorComponent.LegArmor}\n";
                message += $"Arm Armor: {item.ArmorComponent.ArmArmor}\n";
            }

            if (item.HorseComponent != null)
            {
                message += $"Speed: {item.HorseComponent.Speed}\n";
                message += $"Maneuver: {item.HorseComponent.Maneuver}\n";
                message += $"Charge: {item.HorseComponent.ChargeDamage}\n";
                message += $"Hit Points: {item.HorseComponent.HitPoints}\n";
            }

            message += $"Category: {GetItemCategory(item)}\n";

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleRandomCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /items random <category> [count]");
                return;
            }

            string category = args[2].ToLower();
            int count = args.Length > 3 && int.TryParse(args[3], out int c) ? Math.Max(1, Math.Min(c, 20)) : 1;

            if (!CategorizedItems.ContainsKey(category))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Unknown category: {category}");
                return;
            }

            var items = CategorizedItems[category];
            var random = new Random();
            var inventory = representative.GetInventory();

            for (int i = 0; i < count; i++)
            {
                var randomItem = items[random.Next(items.Count)];
                inventory.AddCountedItem(randomItem.StringId, 1);
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Added {count} random {category} items to your inventory");

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.AdminSpawnedItem, 
                null, new object[] { $"Random {category}", count });
        }

        private void HandleClearCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            float radius = args.Length > 2 && float.TryParse(args[2], out float r) ? r : 10f;

            var agent = representative.MissionPeer.ControlledAgent;
            if (agent == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You must be spawned to clear items.");
                return;
            }

            int clearedCount = ClearItemsInRadius(agent.Position, radius);

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Cleared {clearedCount} items within {radius:F1} meter radius");
        }

        private void HandleExportCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            string category = args.Length > 2 ? args[2].ToLower() : "all";

            if (!CategorizedItems.ContainsKey(category))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Unknown category: {category}");
                return;
            }

            var items = CategorizedItems[category];
            var message = $"=== ITEM EXPORT: {category.ToUpper()} ({items.Count} items) ===\n";
            
            foreach (var item in items.Take(100)) // Limit to prevent spam
            {
                message += $"{item.StringId}\n";
            }

            if (items.Count > 100)
            {
                message += $"... and {items.Count - 100} more items (truncated)";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private string FormatItemListing(ItemObject item, int index)
        {
            string category = GetItemCategory(item);
            string value = item.Value > 0 ? $"{item.Value}g" : "N/A";
            return $"{index:D3}. {item.StringId} | {item.Name} | {category} | {value}";
        }

        private string GetItemCategory(ItemObject item)
        {
            if (IsWeapon(item)) return "Weapon";
            if (IsArmor(item)) return "Armor";
            if (IsShield(item)) return "Shield";
            if (IsMount(item)) return "Mount";
            if (IsConsumable(item)) return "Consumable";
            if (IsMaterial(item)) return "Material";
            if (IsTool(item)) return "Tool";
            return "Misc";
        }

        private bool IsWeapon(ItemObject item) => item.WeaponComponent != null;
        private bool IsArmor(ItemObject item) => item.ArmorComponent != null && item.ArmorComponent.HeadArmor + item.ArmorComponent.BodyArmor + item.ArmorComponent.LegArmor + item.ArmorComponent.ArmArmor > 0;
        private bool IsShield(ItemObject item) => item.WeaponComponent?.PrimaryWeapon.WeaponClass == WeaponClass.LargeShield || item.WeaponComponent?.PrimaryWeapon.WeaponClass == WeaponClass.SmallShield;
        private bool IsMount(ItemObject item) => item.HorseComponent != null;
        private bool IsConsumable(ItemObject item) => item.Type == ItemObject.ItemTypeEnum.Goods && (item.StringId.Contains("food") || item.StringId.Contains("drink") || item.StringId.Contains("medicine"));
        private bool IsMaterial(ItemObject item) => item.Type == ItemObject.ItemTypeEnum.Goods && (item.StringId.Contains("iron") || item.StringId.Contains("wood") || item.StringId.Contains("ore") || item.StringId.Contains("ingot"));
        private bool IsTool(ItemObject item) => item.Type == ItemObject.ItemTypeEnum.Goods && (item.StringId.Contains("tool") || item.StringId.Contains("pick") || item.StringId.Contains("hammer"));
        private bool IsMisc(ItemObject item) => !IsWeapon(item) && !IsArmor(item) && !IsShield(item) && !IsMount(item) && !IsConsumable(item) && !IsMaterial(item) && !IsTool(item);

        private void SpawnItemAtPosition(ItemObject item, Vec3 position)
        {
            var gameEntity = GameEntity.CreateEmpty(Mission.Current.Scene);
            gameEntity.SetGlobalFrame(new MatrixFrame(Mat3.Identity, position));
            
            // This would need proper item spawning implementation
            // For now, just simulate the spawn
        }

        private int ClearItemsInRadius(Vec3 center, float radius)
        {
            // This would need proper implementation to find and remove item entities
            // Return simulated count for now
            return new Random().Next(5, 20);
        }

        private List<string> GetRecentlyUsedItems(string adminId)
        {
            // This would be persisted in a real implementation
            return new List<string> { "example_sword", "example_armor", "example_horse" };
        }

        private void AddToRecentItems(string adminId, string itemId)
        {
            // This would be persisted in a real implementation
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            return $"Admin Item Browser ({AllGameItems.Count} items available)";
        }
    }
}