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
    /// Workshop ownership system with production, upgrades, and passive income
    /// </summary>
    public class PE_Workshop : PE_UsableFromDistance
    {
        public string WorkshopType = "Smithy"; // Smithy, Tannery, Brewery, Weaver, etc.
        public int PurchasePrice = 5000;
        public int Level = 1;
        public int MaxLevel = 5;
        public bool IsForSale = true;
        
        private string OwnerId = "";
        private string OwnerName = "";
        private DateTime LastProduction = DateTime.MinValue;
        private DateTime PurchasedAt = DateTime.MinValue;
        private int ProductionEfficiency = 100;
        private int TotalProfit = 0;
        private Dictionary<string, int> StoredGoods = new Dictionary<string, int>();
        private List<WorkshopEmployee> Employees = new List<WorkshopEmployee>();

        protected override bool LockUserFrames => false;
        protected override bool LockUserPositions => false;

        protected override void OnInit()
        {
            base.OnInit();
            SetTextVariables();
        }

        private void SetTextVariables()
        {
            if (IsForSale)
            {
                base.ActionMessage = new TextObject($"{WorkshopType} - For Sale");
                base.DescriptionMessage = new TextObject("Press {KEY} to purchase workshop");
            }
            else
            {
                base.ActionMessage = new TextObject($"{WorkshopType} - {OwnerName}");
                base.DescriptionMessage = new TextObject("Press {KEY} to manage workshop");
            }
        }

        public override void OnUse(Agent userAgent)
        {
            base.OnUse(userAgent);
            if (!GameNetwork.IsServer) return;

            var representative = userAgent.MissionPeer.GetNetworkPeer().GetComponent<PersistentEmpireRepresentative>();
            
            if (IsForSale)
            {
                HandlePurchaseAttempt(representative);
            }
            else if (OwnerId == representative.MissionPeer.GetNetworkPeer().UserName)
            {
                OpenWorkshopManagement(representative);
            }
            else
            {
                ShowWorkshopInfo(representative);
            }
            
            userAgent.StopUsingGameObjectMT(true);
        }

        private void HandlePurchaseAttempt(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            if (representative.Gold < PurchasePrice)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Insufficient gold. Workshop costs {PurchasePrice} gold.");
                return;
            }

            // Check if player already owns a workshop of this type
            if (PlayerOwnsWorkshopType(playerId, WorkshopType))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"You already own a {WorkshopType}. Sell it first before buying another.");
                return;
            }

            // Purchase workshop
            representative.GoldLoss(PurchasePrice);
            OwnerId = playerId;
            OwnerName = representative.MissionPeer.GetNetworkPeer().UserName;
            IsForSale = false;
            PurchasedAt = DateTime.UtcNow;
            LastProduction = DateTime.UtcNow;
            
            SetTextVariables();

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Workshop purchased! You now own this {WorkshopType}.");

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.PlayerBoughtProperty, 
                null, new object[] { WorkshopType, PurchasePrice });
        }

        private void OpenWorkshopManagement(PersistentEmpireRepresentative representative)
        {
            ProcessProduction();
            
            var message = $"=== {WorkshopType.ToUpper()} MANAGEMENT ===\n";
            message += $"Level: {Level}/{MaxLevel}\n";
            message += $"Efficiency: {ProductionEfficiency}%\n";
            message += $"Total Profit: {TotalProfit} gold\n";
            message += $"Employees: {Employees.Count}/5\n";
            message += $"Stored Goods: {StoredGoods.Count} types\n\n";

            message += "Commands:\n";
            message += "  /workshop produce - Start production cycle\n";
            message += "  /workshop upgrade - Upgrade workshop level\n";
            message += "  /workshop hire - Hire an employee\n";
            message += "  /workshop fire <employee> - Fire an employee\n";
            message += "  /workshop storage - View stored goods\n";
            message += "  /workshop sell - Sell workshop\n";
            message += "  /workshop collect - Collect stored profits\n";
            message += "  /workshop repair - Repair workshop (restore efficiency)\n\n";

            message += GetProductionInfo();

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void ShowWorkshopInfo(PersistentEmpireRepresentative representative)
        {
            ProcessProduction();
            
            var message = $"=== {WorkshopType.ToUpper()} INFO ===\n";
            message += $"Owner: {OwnerName}\n";
            message += $"Level: {Level}/{MaxLevel}\n";
            message += $"Type: {WorkshopType}\n";
            message += $"Established: {PurchasedAt:yyyy-MM-dd}\n";
            
            // Show limited info to non-owners
            message += $"Apparent Efficiency: {GetApparentEfficiency()}%\n";
            
            if (CanPlayerWorkHere(representative))
            {
                message += "\nYou could work here as an employee!";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        public void ProcessWorkshopCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (OwnerId != representative.MissionPeer.GetNetworkPeer().UserName)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You don't own this workshop.");
                return;
            }

            if (args.Length < 2) return;

            string command = args[1].ToLower();

            switch (command)
            {
                case "produce":
                    HandleProduceCommand(representative);
                    break;
                case "upgrade":
                    HandleUpgradeCommand(representative);
                    break;
                case "hire":
                    HandleHireCommand(representative);
                    break;
                case "fire":
                    HandleFireCommand(representative, args);
                    break;
                case "storage":
                    HandleStorageCommand(representative);
                    break;
                case "sell":
                    HandleSellCommand(representative);
                    break;
                case "collect":
                    HandleCollectCommand(representative);
                    break;
                case "repair":
                    HandleRepairCommand(representative);
                    break;
                default:
                    InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                        "Unknown workshop command.");
                    break;
            }
        }

        private void HandleProduceCommand(PersistentEmpireRepresentative representative)
        {
            if (DateTime.UtcNow - LastProduction < TimeSpan.FromHours(1))
            {
                var timeLeft = TimeSpan.FromHours(1) - (DateTime.UtcNow - LastProduction);
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Production on cooldown. Time remaining: {timeLeft.Minutes} minutes.");
                return;
            }

            var productionResult = PerformProduction(representative);
            
            if (productionResult.Success)
            {
                // Add produced goods to storage
                foreach (var item in productionResult.ProducedItems)
                {
                    if (!StoredGoods.ContainsKey(item.Key))
                        StoredGoods[item.Key] = 0;
                    StoredGoods[item.Key] += item.Value;
                }

                TotalProfit += productionResult.Profit;
                LastProduction = DateTime.UtcNow;

                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Production completed! Produced: {FormatItems(productionResult.ProducedItems)}, Profit: {productionResult.Profit} gold");
            }
            else
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    productionResult.ErrorMessage);
            }
        }

        private void HandleUpgradeCommand(PersistentEmpireRepresentative representative)
        {
            if (Level >= MaxLevel)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Workshop is already at maximum level.");
                return;
            }

            int upgradeCost = CalculateUpgradeCost();
            
            if (representative.Gold < upgradeCost)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Insufficient gold. Upgrade costs {upgradeCost} gold.");
                return;
            }

            representative.GoldLoss(upgradeCost);
            Level++;
            ProductionEfficiency = Math.Min(100, ProductionEfficiency + 10); // Upgrades improve efficiency

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Workshop upgraded to level {Level}! Efficiency increased to {ProductionEfficiency}%.");

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.PlayerUpgradedBuilding, 
                null, new object[] { WorkshopType, Level });
        }

        private void HandleHireCommand(PersistentEmpireRepresentative representative)
        {
            if (Employees.Count >= 5)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Workshop is at maximum employee capacity (5).");
                return;
            }

            int hireCost = 500 + (Level * 200);
            
            if (representative.Gold < hireCost)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Insufficient gold. Hiring costs {hireCost} gold.");
                return;
            }

            representative.GoldLoss(hireCost);
            
            var employee = GenerateRandomEmployee();
            Employees.Add(employee);

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Hired {employee.Name} as {employee.Role} (Skill: {employee.SkillLevel}).");
        }

        private void HandleFireCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /workshop fire <employee_name>");
                return;
            }

            string employeeName = args[2];
            var employee = Employees.FirstOrDefault(e => e.Name.Equals(employeeName, StringComparison.OrdinalIgnoreCase));
            
            if (employee == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Employee not found.");
                return;
            }

            Employees.Remove(employee);
            
            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Fired {employee.Name}.");
        }

        private void HandleStorageCommand(PersistentEmpireRepresentative representative)
        {
            if (StoredGoods.Count == 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Storage is empty.");
                return;
            }

            var message = "=== WORKSHOP STORAGE ===\n";
            foreach (var item in StoredGoods)
            {
                message += $"{item.Key}: {item.Value}\n";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleSellCommand(PersistentEmpireRepresentative representative)
        {
            int sellPrice = CalculateSellPrice();
            
            representative.GoldGain(sellPrice);
            
            // Return stored goods to player
            var inventory = representative.GetInventory();
            foreach (var item in StoredGoods)
            {
                inventory.AddCountedItem(item.Key, item.Value);
            }
            
            // Reset workshop
            OwnerId = "";
            OwnerName = "";
            IsForSale = true;
            Level = 1;
            ProductionEfficiency = 100;
            TotalProfit = 0;
            StoredGoods.Clear();
            Employees.Clear();
            
            SetTextVariables();

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Workshop sold for {sellPrice} gold. Stored goods returned to inventory.");

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.PlayerSoldProperty, 
                null, new object[] { WorkshopType, sellPrice });
        }

        private void HandleCollectCommand(PersistentEmpireRepresentative representative)
        {
            if (TotalProfit <= 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "No profits to collect.");
                return;
            }

            representative.GoldGain(TotalProfit);
            int collected = TotalProfit;
            TotalProfit = 0;

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Collected {collected} gold in profits.");
        }

        private void HandleRepairCommand(PersistentEmpireRepresentative representative)
        {
            if (ProductionEfficiency >= 100)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Workshop is in perfect condition.");
                return;
            }

            int repairCost = (100 - ProductionEfficiency) * 10;
            
            if (representative.Gold < repairCost)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Insufficient gold. Repairs cost {repairCost} gold.");
                return;
            }

            representative.GoldLoss(repairCost);
            ProductionEfficiency = 100;

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                "Workshop repaired to 100% efficiency.");
        }

        private ProductionResult PerformProduction(PersistentEmpireRepresentative representative)
        {
            var result = new ProductionResult();
            
            // Check if player has required materials
            var requiredMaterials = GetRequiredMaterials();
            var inventory = representative.GetInventory();
            
            foreach (var material in requiredMaterials)
            {
                if (!inventory.IsInventoryIncludes(material.Key) || 
                    inventory.GetCountedItemCount(material.Key) < material.Value)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Insufficient materials. Need {material.Value}x {material.Key}";
                    return result;
                }
            }

            // Consume materials
            foreach (var material in requiredMaterials)
            {
                inventory.RemoveCountedItem(material.Key, material.Value);
            }

            // Calculate production output
            var baseOutput = GetBaseProductionOutput();
            float efficiencyMultiplier = ProductionEfficiency / 100f;
            float levelMultiplier = 1.0f + (Level - 1) * 0.2f;
            float employeeMultiplier = 1.0f + (Employees.Sum(e => e.SkillLevel) / 100f);

            foreach (var output in baseOutput)
            {
                int producedAmount = (int)(output.Value * efficiencyMultiplier * levelMultiplier * employeeMultiplier);
                result.ProducedItems[output.Key] = producedAmount;
            }

            // Calculate profit (selling price - material cost)
            result.Profit = CalculateProductionProfit(result.ProducedItems, requiredMaterials);

            // Reduce efficiency over time (wear and tear)
            ProductionEfficiency = Math.Max(50, ProductionEfficiency - 2);

            result.Success = true;
            return result;
        }

        private Dictionary<string, int> GetRequiredMaterials()
        {
            return WorkshopType.ToLower() switch
            {
                "smithy" => new Dictionary<string, int> { ["pe_iron_ingot"] = 5, ["pe_coal"] = 3 },
                "tannery" => new Dictionary<string, int> { ["pe_hide"] = 10, ["pe_salt"] = 2 },
                "brewery" => new Dictionary<string, int> { ["pe_grain"] = 15, ["pe_hops"] = 5 },
                "weaver" => new Dictionary<string, int> { ["pe_wool"] = 8, ["pe_dye"] = 2 },
                _ => new Dictionary<string, int> { ["pe_wood"] = 10 }
            };
        }

        private Dictionary<string, int> GetBaseProductionOutput()
        {
            return WorkshopType.ToLower() switch
            {
                "smithy" => new Dictionary<string, int> { ["pe_sword"] = 2, ["pe_horseshoe"] = 5 },
                "tannery" => new Dictionary<string, int> { ["pe_leather"] = 8, ["pe_leather_armor"] = 1 },
                "brewery" => new Dictionary<string, int> { ["pe_ale"] = 20, ["pe_beer"] = 10 },
                "weaver" => new Dictionary<string, int> { ["pe_cloth"] = 12, ["pe_robe"] = 2 },
                _ => new Dictionary<string, int> { ["pe_furniture"] = 3 }
            };
        }

        private int CalculateProductionProfit(Dictionary<string, int> produced, Dictionary<string, int> materials)
        {
            // This would use actual market prices in a full implementation
            int revenue = produced.Sum(p => p.Value * GetItemValue(p.Key));
            int costs = materials.Sum(m => m.Value * GetItemValue(m.Key));
            
            // Pay employee wages
            int wages = Employees.Sum(e => e.DailyWage);
            
            return Math.Max(0, revenue - costs - wages);
        }

        private int GetItemValue(string itemId)
        {
            // Simplified item values - would use dynamic pricing in full implementation
            return itemId switch
            {
                var id when id.Contains("sword") => 150,
                var id when id.Contains("armor") => 200,
                var id when id.Contains("ale") => 15,
                var id when id.Contains("beer") => 25,
                var id when id.Contains("cloth") => 30,
                var id when id.Contains("leather") => 20,
                var id when id.Contains("iron") => 50,
                var id when id.Contains("wood") => 10,
                _ => 25
            };
        }

        private int CalculateUpgradeCost()
        {
            return Level * 2000 + 1000;
        }

        private int CalculateSellPrice()
        {
            int baseValue = PurchasePrice + (Level - 1) * 1000;
            float conditionMultiplier = ProductionEfficiency / 100f;
            return (int)(baseValue * conditionMultiplier * 0.8f); // 20% depreciation
        }

        private WorkshopEmployee GenerateRandomEmployee()
        {
            var names = new[] { "John", "Mary", "Peter", "Sarah", "David", "Emma", "Robert", "Lisa" };
            var roles = GetWorkshopRoles();
            
            var random = new Random();
            return new WorkshopEmployee
            {
                Name = names[random.Next(names.Length)],
                Role = roles[random.Next(roles.Length)],
                SkillLevel = random.Next(30, 80),
                DailyWage = random.Next(50, 150),
                HiredAt = DateTime.UtcNow
            };
        }

        private string[] GetWorkshopRoles()
        {
            return WorkshopType.ToLower() switch
            {
                "smithy" => new[] { "Apprentice Smith", "Bellows Operator", "Tool Maker" },
                "tannery" => new[] { "Hide Preparer", "Tanning Specialist", "Leather Worker" },
                "brewery" => new[] { "Brewer", "Fermenter", "Quality Tester" },
                "weaver" => new[] { "Spinner", "Weaver", "Dye Specialist" },
                _ => new[] { "General Worker", "Supervisor", "Craftsman" }
            };
        }

        private string GetProductionInfo()
        {
            var nextProduction = LastProduction.AddHours(1);
            var timeUntilNext = nextProduction - DateTime.UtcNow;
            
            var info = "=== PRODUCTION INFO ===\n";
            
            if (timeUntilNext.TotalSeconds > 0)
            {
                info += $"Next production available in: {timeUntilNext.Minutes} minutes\n";
            }
            else
            {
                info += "Production available now!\n";
            }
            
            info += "Required materials:\n";
            foreach (var material in GetRequiredMaterials())
            {
                info += $"  {material.Key}: {material.Value}\n";
            }
            
            info += "Expected output:\n";
            foreach (var output in GetBaseProductionOutput())
            {
                info += $"  {output.Key}: ~{output.Value}\n";
            }
            
            return info;
        }

        private int GetApparentEfficiency()
        {
            // Show approximate efficiency to non-owners
            return (ProductionEfficiency / 10) * 10; // Round to nearest 10
        }

        private bool CanPlayerWorkHere(PersistentEmpireRepresentative representative)
        {
            // Check if player could be hired as employee (skill requirements, etc.)
            return Employees.Count < 5 && representative.GetSkillValue("Crafting") >= 25;
        }

        private bool PlayerOwnsWorkshopType(string playerId, string workshopType)
        {
            // In a full implementation, this would check a global workshop registry
            return false; // Simplified for now
        }

        private string FormatItems(Dictionary<string, int> items)
        {
            return string.Join(", ", items.Select(i => $"{i.Value}x {i.Key}"));
        }

        private void ProcessProduction()
        {
            // Automatic passive production for idle workshops (reduced efficiency)
            if (!string.IsNullOrEmpty(OwnerId) && 
                DateTime.UtcNow - LastProduction > TimeSpan.FromHours(6))
            {
                // Passive income generation
                int passiveIncome = Level * 50 + Employees.Sum(e => e.SkillLevel);
                TotalProfit += passiveIncome;
                LastProduction = DateTime.UtcNow;
            }
        }

        public override ScriptComponentBehavior.TickRequirement GetTickRequirement()
        {
            return ScriptComponentBehavior.TickRequirement.TickOccasionally;
        }

        protected override void OnTickOccasionally(float currentFrameDeltaTime)
        {
            if (!IsForSale)
            {
                ProcessProduction();
            }
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            if (IsForSale)
            {
                return $"{WorkshopType} - For Sale ({PurchasePrice} gold)";
            }
            else
            {
                return $"{WorkshopType} - Level {Level} (Owner: {OwnerName})";
            }
        }

        private class ProductionResult
        {
            public bool Success { get; set; }
            public Dictionary<string, int> ProducedItems { get; set; } = new Dictionary<string, int>();
            public int Profit { get; set; }
            public string ErrorMessage { get; set; } = "";
        }

        private class WorkshopEmployee
        {
            public string Name { get; set; }
            public string Role { get; set; }
            public int SkillLevel { get; set; }
            public int DailyWage { get; set; }
            public DateTime HiredAt { get; set; }
        }
    }
}