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
    /// Advanced merchant trading system with dynamic prices, trade routes, and reputation
    /// </summary>
    public class PE_AdvancedTrading : PE_UsableFromDistance
    {
        public string RegionName = "Empire";
        public float BaseProfit = 0.15f; // 15% base profit margin
        public int MaxCaravanSize = 50;
        public bool IsTradeHub = false;
        public string[] ConnectedRegions = new string[0];
        
        private static Dictionary<string, Dictionary<string, TradeGood>> RegionalPrices = new Dictionary<string, Dictionary<string, TradeGood>>();
        private static Dictionary<string, int> PlayerTradeReputation = new Dictionary<string, int>();
        private Random randomizer = new Random();
        private DateTime LastPriceUpdate = DateTime.MinValue;

        protected override bool LockUserFrames => false;
        protected override bool LockUserPositions => false;

        protected override void OnInit()
        {
            base.OnInit();
            InitializeRegionalPrices();
            SetTextVariables();
        }

        private void SetTextVariables()
        {
            base.ActionMessage = new TextObject($"Trade Center - {RegionName}");
            base.DescriptionMessage = new TextObject("Press {KEY} to access trade menu");
        }

        private void InitializeRegionalPrices()
        {
            if (!RegionalPrices.ContainsKey(RegionName))
            {
                RegionalPrices[RegionName] = new Dictionary<string, TradeGood>();
                
                // Initialize common trade goods with base prices
                var tradeGoods = new[]
                {
                    new TradeGood("pe_wheat", 15, 1.2f, TradeGoodType.Food),
                    new TradeGood("pe_beer", 45, 0.8f, TradeGoodType.Luxury),
                    new TradeGood("pe_iron_ore", 25, 1.5f, TradeGoodType.Raw),
                    new TradeGood("pe_iron_ingot", 75, 0.6f, TradeGoodType.Processed),
                    new TradeGood("pe_leather", 30, 1.0f, TradeGoodType.Crafted),
                    new TradeGood("pe_cloth", 40, 0.9f, TradeGoodType.Luxury),
                    new TradeGood("pe_spice", 120, 0.3f, TradeGoodType.Exotic),
                    new TradeGood("pe_jewelry", 300, 0.2f, TradeGoodType.Luxury),
                    new TradeGood("pe_weapon", 200, 0.4f, TradeGoodType.Military),
                    new TradeGood("pe_horse", 500, 0.1f, TradeGoodType.Livestock)
                };

                foreach (var good in tradeGoods)
                {
                    RegionalPrices[RegionName][good.ItemId] = good;
                }
            }
        }

        public override void OnUse(Agent userAgent)
        {
            base.OnUse(userAgent);
            if (!GameNetwork.IsServer) return;

            var representative = userAgent.MissionPeer.GetNetworkPeer().GetComponent<PersistentEmpireRepresentative>();
            OpenTradeMenu(representative);
            userAgent.StopUsingGameObjectMT(true);
        }

        private void OpenTradeMenu(PersistentEmpireRepresentative representative)
        {
            UpdatePrices();
            var inventory = representative.GetInventory();
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            // Get player's trade reputation
            if (!PlayerTradeReputation.ContainsKey(playerId))
                PlayerTradeReputation[playerId] = 0;

            int reputation = PlayerTradeReputation[playerId];
            string reputationLevel = GetReputationLevel(reputation);

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Welcome to {RegionName} Trade Center! Reputation: {reputationLevel} ({reputation})");

            ShowTradeOptions(representative, inventory, reputation);
        }

        private void ShowTradeOptions(PersistentEmpireRepresentative representative, Inventory inventory, int reputation)
        {
            var message = $"=== {RegionName} Trade Prices ===\n";
            var regionalGoods = RegionalPrices[RegionName];

            message += "BUYING (from you):\n";
            foreach (var good in regionalGoods.Values.OrderBy(g => g.Type))
            {
                int buyPrice = CalculateBuyPrice(good, reputation);
                if (inventory.GetCountedItemCount(good.ItemId) > 0)
                {
                    message += $"  {good.ItemId}: {buyPrice} gold (You have: {inventory.GetCountedItemCount(good.ItemId)})\n";
                }
                else
                {
                    message += $"  {good.ItemId}: {buyPrice} gold\n";
                }
            }

            message += "\nSELLING (to you):\n";
            foreach (var good in regionalGoods.Values.OrderBy(g => g.Type))
            {
                int sellPrice = CalculateSellPrice(good, reputation);
                message += $"  {good.ItemId}: {sellPrice} gold\n";
            }

            if (IsTradeHub && ConnectedRegions.Length > 0)
            {
                message += "\n=== Trade Route Information ===\n";
                ShowTradeRouteInfo(representative, message);
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void ShowTradeRouteInfo(PersistentEmpireRepresentative representative, string baseMessage)
        {
            var message = baseMessage;
            message += "Profitable trade routes:\n";

            foreach (string region in ConnectedRegions)
            {
                if (RegionalPrices.ContainsKey(region))
                {
                    var bestProfit = FindBestTradeRoute(RegionName, region);
                    if (bestProfit.Item3 > 0)
                    {
                        message += $"  {RegionName} → {region}: {bestProfit.Item1} (Profit: {bestProfit.Item3} gold per unit)\n";
                    }
                }
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private (string, string, int) FindBestTradeRoute(string fromRegion, string toRegion)
        {
            if (!RegionalPrices.ContainsKey(fromRegion) || !RegionalPrices.ContainsKey(toRegion))
                return ("", "", 0);

            string bestItem = "";
            int bestProfit = 0;

            foreach (var good in RegionalPrices[fromRegion])
            {
                if (RegionalPrices[toRegion].ContainsKey(good.Key))
                {
                    int buyPrice = CalculateBuyPrice(good.Value, 0);
                    int sellPrice = CalculateSellPrice(RegionalPrices[toRegion][good.Key], 0);
                    int profit = sellPrice - buyPrice;

                    if (profit > bestProfit)
                    {
                        bestProfit = profit;
                        bestItem = good.Key;
                    }
                }
            }

            return (bestItem, toRegion, bestProfit);
        }

        private int CalculateBuyPrice(TradeGood good, int reputation)
        {
            float reputationMultiplier = 1.0f + (reputation / 1000f); // 1% bonus per 10 reputation
            float priceMultiplier = good.PriceMultiplier * reputationMultiplier;
            return (int)(good.BasePrice * priceMultiplier);
        }

        private int CalculateSellPrice(TradeGood good, int reputation)
        {
            float reputationDiscount = 1.0f - (reputation / 2000f); // 1% discount per 20 reputation
            float priceMultiplier = (1.0f + BaseProfit) * reputationDiscount;
            return (int)(good.BasePrice * good.PriceMultiplier * priceMultiplier);
        }

        private void UpdatePrices()
        {
            if (DateTime.UtcNow - LastPriceUpdate < TimeSpan.FromMinutes(30))
                return;

            LastPriceUpdate = DateTime.UtcNow;

            foreach (var region in RegionalPrices.Values)
            {
                foreach (var good in region.Values)
                {
                    // Random price fluctuation ±15%
                    float fluctuation = 0.85f + (float)(randomizer.NextDouble() * 0.3);
                    good.PriceMultiplier = fluctuation;

                    // Seasonal effects
                    ApplySeasonalEffects(good);

                    // Supply and demand simulation
                    ApplySupplyDemandEffects(good);
                }
            }
        }

        private void ApplySeasonalEffects(TradeGood good)
        {
            int dayOfYear = DateTime.UtcNow.DayOfYear;

            switch (good.Type)
            {
                case TradeGoodType.Food:
                    // Food cheaper in harvest season (autumn)
                    if (dayOfYear >= 244 && dayOfYear <= 334) // Sep-Nov
                        good.PriceMultiplier *= 0.8f;
                    break;

                case TradeGoodType.Luxury:
                    // Luxury items more expensive during festivals
                    if (dayOfYear >= 355 || dayOfYear <= 31) // Winter holidays
                        good.PriceMultiplier *= 1.3f;
                    break;

                case TradeGoodType.Military:
                    // Military goods more expensive during conflict seasons
                    if (dayOfYear >= 91 && dayOfYear <= 182) // Spring-Summer
                        good.PriceMultiplier *= 1.2f;
                    break;
            }
        }

        private void ApplySupplyDemandEffects(TradeGood good)
        {
            // Simulate supply/demand based on imaginary market conditions
            switch (good.Type)
            {
                case TradeGoodType.Raw:
                    // Raw materials have stable prices
                    good.PriceMultiplier *= 0.95f + (float)(randomizer.NextDouble() * 0.1);
                    break;

                case TradeGoodType.Exotic:
                    // Exotic goods have volatile prices
                    good.PriceMultiplier *= 0.7f + (float)(randomizer.NextDouble() * 0.6);
                    break;

                case TradeGoodType.Processed:
                    // Processed goods depend on raw material availability
                    good.PriceMultiplier *= 0.9f + (float)(randomizer.NextDouble() * 0.2);
                    break;
            }
        }

        private string GetReputationLevel(int reputation)
        {
            return reputation switch
            {
                >= 1000 => "Legendary Merchant",
                >= 500 => "Master Trader",
                >= 200 => "Experienced Merchant",
                >= 50 => "Novice Trader",
                >= 0 => "Unknown",
                _ => "Untrustworthy"
            };
        }

        public static void RecordTrade(string playerId, int tradeValue, bool successful)
        {
            if (!PlayerTradeReputation.ContainsKey(playerId))
                PlayerTradeReputation[playerId] = 0;

            if (successful)
            {
                PlayerTradeReputation[playerId] += Math.Min(tradeValue / 100, 10); // Max 10 rep per trade
            }
            else
            {
                PlayerTradeReputation[playerId] -= 5; // Penalty for failed trades
            }
        }

        public override ScriptComponentBehavior.TickRequirement GetTickRequirement()
        {
            return ScriptComponentBehavior.TickRequirement.TickOccasionally;
        }

        protected override void OnTickOccasionally(float currentFrameDeltaTime)
        {
            UpdatePrices();
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            return $"Trade Center - {RegionName}";
        }

        private class TradeGood
        {
            public string ItemId { get; set; }
            public int BasePrice { get; set; }
            public float PriceMultiplier { get; set; }
            public TradeGoodType Type { get; set; }

            public TradeGood(string itemId, int basePrice, float priceMultiplier, TradeGoodType type)
            {
                ItemId = itemId;
                BasePrice = basePrice;
                PriceMultiplier = priceMultiplier;
                Type = type;
            }
        }

        private enum TradeGoodType
        {
            Food,
            Raw,
            Processed,
            Crafted,
            Luxury,
            Exotic,
            Military,
            Livestock
        }
    }
}