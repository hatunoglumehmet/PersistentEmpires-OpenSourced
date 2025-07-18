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
    /// Player-to-player auction house with bidding, buyout, and advanced search
    /// </summary>
    public class PE_AuctionHouse : PE_UsableFromDistance
    {
        public float CommissionRate = 0.05f; // 5% commission
        public int MaxListingsPerPlayer = 10;
        public int MaxAuctionDurationHours = 72;
        
        private static List<AuctionListing> ActiveListings = new List<AuctionListing>();
        private static Dictionary<string, List<string>> PlayerListings = new Dictionary<string, List<string>>();
        private static Dictionary<string, List<string>> PlayerBids = new Dictionary<string, List<string>>();

        protected override bool LockUserFrames => false;
        protected override bool LockUserPositions => false;

        protected override void OnInit()
        {
            base.OnInit();
            SetTextVariables();
        }

        private void SetTextVariables()
        {
            base.ActionMessage = new TextObject("Auction House");
            base.DescriptionMessage = new TextObject("Press {KEY} to access auctions");
        }

        public override void OnUse(Agent userAgent)
        {
            base.OnUse(userAgent);
            if (!GameNetwork.IsServer) return;

            var representative = userAgent.MissionPeer.GetNetworkPeer().GetComponent<PersistentEmpireRepresentative>();
            OpenAuctionMenu(representative);
            userAgent.StopUsingGameObjectMT(true);
        }

        private void OpenAuctionMenu(PersistentEmpireRepresentative representative)
        {
            CleanExpiredListings();
            
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var message = "=== AUCTION HOUSE ===\n";
            message += $"Active Listings: {ActiveListings.Count}\n";
            message += $"Your Listings: {GetPlayerListingCount(playerId)}/{MaxListingsPerPlayer}\n";
            message += $"Commission Rate: {CommissionRate * 100:F1}%\n\n";

            message += "Commands:\n";
            message += "  /auction browse [category] - Browse listings\n";
            message += "  /auction search <item> - Search for items\n";
            message += "  /auction sell <item> <starting_bid> [buyout] - List item\n";
            message += "  /auction bid <listing_id> <amount> - Place bid\n";
            message += "  /auction buyout <listing_id> - Buy instantly\n";
            message += "  /auction mylistings - View your listings\n";
            message += "  /auction mybids - View your bids\n";
            message += "  /auction cancel <listing_id> - Cancel your listing\n\n";

            // Show featured listings
            var featuredListings = GetFeaturedListings();
            if (featuredListings.Count > 0)
            {
                message += "=== FEATURED LISTINGS ===\n";
                foreach (var listing in featuredListings)
                {
                    message += FormatListingDisplay(listing, false) + "\n";
                }
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        public void ProcessAuctionCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 2) return;

            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            string command = args[1].ToLower();

            switch (command)
            {
                case "browse":
                    HandleBrowseCommand(representative, args);
                    break;
                case "search":
                    HandleSearchCommand(representative, args);
                    break;
                case "sell":
                    HandleSellCommand(representative, args);
                    break;
                case "bid":
                    HandleBidCommand(representative, args);
                    break;
                case "buyout":
                    HandleBuyoutCommand(representative, args);
                    break;
                case "mylistings":
                    HandleMyListingsCommand(representative);
                    break;
                case "mybids":
                    HandleMyBidsCommand(representative);
                    break;
                case "cancel":
                    HandleCancelCommand(representative, args);
                    break;
                default:
                    InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                        "Unknown auction command. Type '/auction' for help.");
                    break;
            }
        }

        private void HandleBrowseCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            string category = args.Length > 2 ? args[2].ToLower() : "all";
            var filteredListings = FilterListingsByCategory(category);

            var message = $"=== BROWSING: {category.ToUpper()} ===\n";
            message += $"Found {filteredListings.Count} listings\n\n";

            foreach (var listing in filteredListings.Take(20)) // Limit to 20 for readability
            {
                message += FormatListingDisplay(listing, true) + "\n";
            }

            if (filteredListings.Count > 20)
            {
                message += $"\n... and {filteredListings.Count - 20} more listings";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleSearchCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /auction search <item_name>");
                return;
            }

            string searchTerm = string.Join(" ", args.Skip(2)).ToLower();
            var results = ActiveListings.Where(l => l.ItemId.ToLower().Contains(searchTerm)).ToList();

            var message = $"=== SEARCH RESULTS: '{searchTerm}' ===\n";
            message += $"Found {results.Count} matching listings\n\n";

            foreach (var listing in results.Take(15))
            {
                message += FormatListingDisplay(listing, true) + "\n";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleSellCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 4)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /auction sell <item_id> <starting_bid> [buyout_price]");
                return;
            }

            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            string itemId = args[2];
            
            if (!int.TryParse(args[3], out int startingBid) || startingBid <= 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Invalid starting bid amount.");
                return;
            }

            int? buyoutPrice = null;
            if (args.Length > 4 && int.TryParse(args[4], out int buyout) && buyout > startingBid)
            {
                buyoutPrice = buyout;
            }

            // Check player listing limit
            if (GetPlayerListingCount(playerId) >= MaxListingsPerPlayer)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"You have reached the maximum listing limit ({MaxListingsPerPlayer}).");
                return;
            }

            // Check if player has the item
            var inventory = representative.GetInventory();
            if (!inventory.IsInventoryIncludes(itemId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You don't have this item in your inventory.");
                return;
            }

            // Create listing
            var listing = new AuctionListing
            {
                Id = Guid.NewGuid().ToString(),
                SellerId = playerId,
                SellerName = representative.MissionPeer.GetNetworkPeer().UserName,
                ItemId = itemId,
                ItemCount = 1,
                StartingBid = startingBid,
                CurrentBid = startingBid,
                BuyoutPrice = buyoutPrice,
                ExpiresAt = DateTime.UtcNow.AddHours(MaxAuctionDurationHours),
                CreatedAt = DateTime.UtcNow
            };

            // Remove item from inventory
            inventory.RemoveCountedItem(itemId, 1);
            
            // Add to listings
            ActiveListings.Add(listing);
            
            if (!PlayerListings.ContainsKey(playerId))
                PlayerListings[playerId] = new List<string>();
            PlayerListings[playerId].Add(listing.Id);

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Listed {itemId} with starting bid {startingBid} gold. Listing ID: {listing.Id}");
        }

        private void HandleBidCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 4)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /auction bid <listing_id> <bid_amount>");
                return;
            }

            string listingId = args[2];
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            if (!int.TryParse(args[3], out int bidAmount) || bidAmount <= 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Invalid bid amount.");
                return;
            }

            var listing = ActiveListings.FirstOrDefault(l => l.Id == listingId);
            if (listing == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Listing not found.");
                return;
            }

            if (listing.SellerId == playerId)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You cannot bid on your own listing.");
                return;
            }

            if (bidAmount <= listing.CurrentBid)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Bid must be higher than current bid ({listing.CurrentBid} gold).");
                return;
            }

            if (representative.Gold < bidAmount)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Insufficient gold for this bid.");
                return;
            }

            // Refund previous bidder if any
            if (!string.IsNullOrEmpty(listing.HighestBidderId))
            {
                RefundBidder(listing.HighestBidderId, listing.CurrentBid);
            }

            // Place new bid
            representative.GoldLoss(bidAmount);
            listing.CurrentBid = bidAmount;
            listing.HighestBidderId = playerId;
            listing.HighestBidderName = representative.MissionPeer.GetNetworkPeer().UserName;

            // Track player bids
            if (!PlayerBids.ContainsKey(playerId))
                PlayerBids[playerId] = new List<string>();
            if (!PlayerBids[playerId].Contains(listingId))
                PlayerBids[playerId].Add(listingId);

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Bid placed successfully! You are now the highest bidder with {bidAmount} gold.");

            // Notify seller
            NotifyPlayer(listing.SellerId, $"New bid on your {listing.ItemId}: {bidAmount} gold");
        }

        private void HandleBuyoutCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /auction buyout <listing_id>");
                return;
            }

            string listingId = args[2];
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            var listing = ActiveListings.FirstOrDefault(l => l.Id == listingId);
            if (listing == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Listing not found.");
                return;
            }

            if (!listing.BuyoutPrice.HasValue)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "This listing doesn't have a buyout price.");
                return;
            }

            if (listing.SellerId == playerId)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You cannot buy out your own listing.");
                return;
            }

            if (representative.Gold < listing.BuyoutPrice.Value)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Insufficient gold for buyout.");
                return;
            }

            // Complete the sale
            CompleteSale(listing, playerId, listing.BuyoutPrice.Value);
            
            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Successfully bought {listing.ItemId} for {listing.BuyoutPrice.Value} gold!");
        }

        private void HandleMyListingsCommand(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var myListings = ActiveListings.Where(l => l.SellerId == playerId).ToList();

            var message = "=== YOUR LISTINGS ===\n";
            if (myListings.Count == 0)
            {
                message += "You have no active listings.";
            }
            else
            {
                foreach (var listing in myListings)
                {
                    message += FormatListingDisplay(listing, true) + "\n";
                }
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleMyBidsCommand(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            
            if (!PlayerBids.ContainsKey(playerId) || PlayerBids[playerId].Count == 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You have no active bids.");
                return;
            }

            var message = "=== YOUR BIDS ===\n";
            foreach (string listingId in PlayerBids[playerId])
            {
                var listing = ActiveListings.FirstOrDefault(l => l.Id == listingId);
                if (listing != null && listing.HighestBidderId == playerId)
                {
                    message += $"[{listing.Id}] {listing.ItemId} - Your bid: {listing.CurrentBid} gold\n";
                }
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleCancelCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /auction cancel <listing_id>");
                return;
            }

            string listingId = args[2];
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            var listing = ActiveListings.FirstOrDefault(l => l.Id == listingId && l.SellerId == playerId);
            if (listing == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Listing not found or you don't own it.");
                return;
            }

            if (!string.IsNullOrEmpty(listing.HighestBidderId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Cannot cancel listing with active bids.");
                return;
            }

            // Return item to seller
            var inventory = representative.GetInventory();
            inventory.AddCountedItem(listing.ItemId, listing.ItemCount);

            // Remove listing
            ActiveListings.Remove(listing);
            if (PlayerListings.ContainsKey(playerId))
                PlayerListings[playerId].Remove(listingId);

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Listing cancelled. {listing.ItemId} returned to your inventory.");
        }

        private void CompleteSale(AuctionListing listing, string buyerId, int salePrice)
        {
            // Calculate commission
            int commission = (int)(salePrice * CommissionRate);
            int sellerReceives = salePrice - commission;

            // Pay seller
            PayPlayer(listing.SellerId, sellerReceives);

            // Give item to buyer
            GiveItemToPlayer(buyerId, listing.ItemId, listing.ItemCount);

            // Refund any previous bidders
            if (!string.IsNullOrEmpty(listing.HighestBidderId) && listing.HighestBidderId != buyerId)
            {
                RefundBidder(listing.HighestBidderId, listing.CurrentBid);
            }

            // Remove listing
            ActiveListings.Remove(listing);
            
            // Clean up tracking
            if (PlayerListings.ContainsKey(listing.SellerId))
                PlayerListings[listing.SellerId].Remove(listing.Id);

            // Notify parties
            NotifyPlayer(listing.SellerId, $"Your {listing.ItemId} sold for {salePrice} gold (you received {sellerReceives} after commission)");
            NotifyPlayer(buyerId, $"You successfully purchased {listing.ItemId} for {salePrice} gold");
        }

        private string FormatListingDisplay(AuctionListing listing, bool showId)
        {
            string timeLeft = GetTimeLeftString(listing.ExpiresAt);
            string buyoutText = listing.BuyoutPrice.HasValue ? $" | Buyout: {listing.BuyoutPrice}g" : "";
            string bidderText = !string.IsNullOrEmpty(listing.HighestBidderName) ? $" (by {listing.HighestBidderName})" : "";
            
            if (showId)
            {
                return $"[{listing.Id}] {listing.ItemId} x{listing.ItemCount} | Current: {listing.CurrentBid}g{bidderText}{buyoutText} | {timeLeft}";
            }
            else
            {
                return $"{listing.ItemId} x{listing.ItemCount} | Current: {listing.CurrentBid}g{bidderText}{buyoutText} | {timeLeft}";
            }
        }

        private string GetTimeLeftString(DateTime expiresAt)
        {
            var timeLeft = expiresAt - DateTime.UtcNow;
            if (timeLeft.TotalDays >= 1)
                return $"{(int)timeLeft.TotalDays}d {timeLeft.Hours}h";
            else if (timeLeft.TotalHours >= 1)
                return $"{(int)timeLeft.TotalHours}h {timeLeft.Minutes}m";
            else
                return $"{timeLeft.Minutes}m";
        }

        private List<AuctionListing> GetFeaturedListings()
        {
            return ActiveListings
                .Where(l => l.BuyoutPrice.HasValue || !string.IsNullOrEmpty(l.HighestBidderId))
                .OrderByDescending(l => l.CurrentBid)
                .Take(5)
                .ToList();
        }

        private List<AuctionListing> FilterListingsByCategory(string category)
        {
            if (category == "all")
                return ActiveListings.ToList();

            return ActiveListings.Where(l => GetItemCategory(l.ItemId) == category).ToList();
        }

        private string GetItemCategory(string itemId)
        {
            if (itemId.Contains("weapon")) return "weapons";
            if (itemId.Contains("armor")) return "armor";
            if (itemId.Contains("horse")) return "mounts";
            if (itemId.Contains("food") || itemId.Contains("drink")) return "consumables";
            if (itemId.Contains("ore") || itemId.Contains("ingot")) return "materials";
            return "misc";
        }

        private int GetPlayerListingCount(string playerId)
        {
            return PlayerListings.ContainsKey(playerId) ? PlayerListings[playerId].Count : 0;
        }

        private void CleanExpiredListings()
        {
            var expiredListings = ActiveListings.Where(l => l.ExpiresAt < DateTime.UtcNow).ToList();
            
            foreach (var listing in expiredListings)
            {
                // If there's a highest bidder, complete the sale
                if (!string.IsNullOrEmpty(listing.HighestBidderId))
                {
                    CompleteSale(listing, listing.HighestBidderId, listing.CurrentBid);
                }
                else
                {
                    // Return item to seller
                    GiveItemToPlayer(listing.SellerId, listing.ItemId, listing.ItemCount);
                    ActiveListings.Remove(listing);
                    
                    NotifyPlayer(listing.SellerId, $"Your listing for {listing.ItemId} expired and was returned to you");
                }
            }
        }

        private void RefundBidder(string playerId, int amount)
        {
            PayPlayer(playerId, amount);
            NotifyPlayer(playerId, $"Your bid of {amount} gold has been refunded");
        }

        private void PayPlayer(string playerId, int amount)
        {
            // Implementation would depend on how to find and pay offline players
            // For now, we'll handle online players only
            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.UserName == playerId && peer.IsConnectionActive)
                {
                    var rep = peer.GetComponent<PersistentEmpireRepresentative>();
                    if (rep != null)
                    {
                        rep.GoldGain(amount);
                    }
                    break;
                }
            }
        }

        private void GiveItemToPlayer(string playerId, string itemId, int count)
        {
            // Implementation would depend on how to find and give items to offline players
            // For now, we'll handle online players only
            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.UserName == playerId && peer.IsConnectionActive)
                {
                    var rep = peer.GetComponent<PersistentEmpireRepresentative>();
                    if (rep != null)
                    {
                        rep.GetInventory().AddCountedItem(itemId, count);
                    }
                    break;
                }
            }
        }

        private void NotifyPlayer(string playerId, string message)
        {
            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.UserName == playerId && peer.IsConnectionActive)
                {
                    InformationComponent.Instance.SendMessageToPlayer(peer, message);
                    break;
                }
            }
        }

        public override ScriptComponentBehavior.TickRequirement GetTickRequirement()
        {
            return ScriptComponentBehavior.TickRequirement.TickOccasionally;
        }

        protected override void OnTickOccasionally(float currentFrameDeltaTime)
        {
            CleanExpiredListings();
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            return $"Auction House ({ActiveListings.Count} active listings)";
        }

        private class AuctionListing
        {
            public string Id { get; set; }
            public string SellerId { get; set; }
            public string SellerName { get; set; }
            public string ItemId { get; set; }
            public int ItemCount { get; set; }
            public int StartingBid { get; set; }
            public int CurrentBid { get; set; }
            public int? BuyoutPrice { get; set; }
            public string HighestBidderId { get; set; }
            public string HighestBidderName { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}