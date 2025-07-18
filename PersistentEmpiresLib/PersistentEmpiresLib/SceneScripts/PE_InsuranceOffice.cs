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
    /// Insurance system for protecting valuable items from loss on death
    /// </summary>
    public class PE_InsuranceOffice : PE_UsableFromDistance
    {
        public float BasePremiumRate = 0.05f; // 5% of item value per month
        public int MaxPoliciesPerPlayer = 10;
        public float ClaimProcessingFee = 0.1f; // 10% processing fee
        
        private static Dictionary<string, List<InsurancePolicy>> PlayerPolicies = new Dictionary<string, List<InsurancePolicy>>();
        private static Dictionary<string, List<InsuranceClaim>> PendingClaims = new Dictionary<string, List<InsuranceClaim>>();
        private static Dictionary<string, PlayerInsuranceProfile> PlayerProfiles = new Dictionary<string, PlayerInsuranceProfile>();

        protected override bool LockUserFrames => false;
        protected override bool LockUserPositions => false;

        protected override void OnInit()
        {
            base.OnInit();
            SetTextVariables();
        }

        private void SetTextVariables()
        {
            base.ActionMessage = new TextObject("Insurance Office");
            base.DescriptionMessage = new TextObject("Press {KEY} to access insurance services");
        }

        public override void OnUse(Agent userAgent)
        {
            base.OnUse(userAgent);
            if (!GameNetwork.IsServer) return;

            var representative = userAgent.MissionPeer.GetNetworkPeer().GetComponent<PersistentEmpireRepresentative>();
            OpenInsuranceOffice(representative);
            userAgent.StopUsingGameObjectMT(true);
        }

        private void OpenInsuranceOffice(PersistentEmpireRepresentative representative)
        {
            ProcessExpiredPolicies();
            
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var profile = GetOrCreateProfile(playerId);
            var activePolicies = GetPlayerPolicies(playerId);
            var pendingClaims = GetPlayerClaims(playerId);

            var message = "=== INSURANCE OFFICE ===\n";
            message += $"Active Policies: {activePolicies.Count}/{MaxPoliciesPerPlayer}\n";
            message += $"Pending Claims: {pendingClaims.Count}\n";
            message += $"Risk Rating: {GetRiskRating(profile)}\n";
            message += $"Total Claims Paid: {profile.TotalClaimsPaid} gold\n\n";

            message += "Services Available:\n";
            message += "  /insurance quote <item_id> - Get insurance quote\n";
            message += "  /insurance buy <item_id> <months> - Purchase policy\n";
            message += "  /insurance renew <policy_id> <months> - Renew policy\n";
            message += "  /insurance claim <policy_id> - File insurance claim\n";
            message += "  /insurance policies - View your policies\n";
            message += "  /insurance claims - View pending claims\n";
            message += "  /insurance cancel <policy_id> - Cancel policy\n\n";

            message += "Coverage Types:\n";
            message += "  • Death Protection - Item lost on death\n";
            message += "  • Theft Protection - Item stolen by players\n";
            message += "  • Damage Protection - Item damaged beyond repair\n";
            message += "  • Total Loss - Item completely lost/destroyed\n";

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        public void ProcessInsuranceCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 2) return;

            string command = args[1].ToLower();

            switch (command)
            {
                case "quote":
                    HandleQuoteCommand(representative, args);
                    break;
                case "buy":
                    HandleBuyCommand(representative, args);
                    break;
                case "renew":
                    HandleRenewCommand(representative, args);
                    break;
                case "claim":
                    HandleClaimCommand(representative, args);
                    break;
                case "policies":
                    HandlePoliciesCommand(representative);
                    break;
                case "claims":
                    HandleClaimsCommand(representative);
                    break;
                case "cancel":
                    HandleCancelCommand(representative, args);
                    break;
                default:
                    InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                        "Unknown insurance command. Use /insurance for help.");
                    break;
            }
        }

        private void HandleQuoteCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /insurance quote <item_id>");
                return;
            }

            string itemId = args[2];
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            // Check if player has the item
            var inventory = representative.GetInventory();
            if (!inventory.IsInventoryIncludes(itemId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You don't own this item.");
                return;
            }

            var itemValue = GetItemValue(itemId);
            var profile = GetOrCreateProfile(playerId);
            var quote = CalculateInsuranceQuote(itemValue, profile);

            var message = $"=== INSURANCE QUOTE ===\n";
            message += $"Item: {itemId}\n";
            message += $"Estimated Value: {itemValue} gold\n";
            message += $"Risk Rating: {GetRiskRating(profile)}\n\n";
            
            message += "Monthly Premiums:\n";
            message += $"  1 Month: {quote.MonthlyPremium} gold\n";
            message += $"  3 Months: {quote.MonthlyPremium * 3 * 0.95f:F0} gold (5% discount)\n";
            message += $"  6 Months: {quote.MonthlyPremium * 6 * 0.9f:F0} gold (10% discount)\n";
            message += $"  12 Months: {quote.MonthlyPremium * 12 * 0.8f:F0} gold (20% discount)\n\n";
            
            message += $"Coverage: Up to {itemValue} gold\n";
            message += $"Deductible: {quote.Deductible} gold\n";
            message += $"Claim Processing Fee: {ClaimProcessingFee * 100}%\n";

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleBuyCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 4)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /insurance buy <item_id> <months>");
                return;
            }

            string itemId = args[2];
            if (!int.TryParse(args[3], out int months) || months < 1 || months > 12)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Duration must be between 1 and 12 months.");
                return;
            }

            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var activePolicies = GetPlayerPolicies(playerId);

            if (activePolicies.Count >= MaxPoliciesPerPlayer)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Maximum policy limit reached ({MaxPoliciesPerPlayer}).");
                return;
            }

            // Check if item is already insured
            if (activePolicies.Any(p => p.ItemId == itemId && p.IsActive))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "This item is already insured.");
                return;
            }

            var inventory = representative.GetInventory();
            if (!inventory.IsInventoryIncludes(itemId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You don't own this item.");
                return;
            }

            var itemValue = GetItemValue(itemId);
            var profile = GetOrCreateProfile(playerId);
            var quote = CalculateInsuranceQuote(itemValue, profile);

            // Calculate total premium with discount
            float discount = months switch
            {
                >= 12 => 0.8f,
                >= 6 => 0.9f,
                >= 3 => 0.95f,
                _ => 1.0f
            };

            int totalPremium = (int)(quote.MonthlyPremium * months * discount);

            if (representative.Gold < totalPremium)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Insufficient gold. Premium costs {totalPremium} gold.");
                return;
            }

            // Purchase policy
            representative.GoldLoss(totalPremium);

            var policy = new InsurancePolicy
            {
                Id = Guid.NewGuid().ToString(),
                PlayerId = playerId,
                ItemId = itemId,
                CoverageAmount = itemValue,
                MonthlyPremium = quote.MonthlyPremium,
                Deductible = quote.Deductible,
                PurchasedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(months * 30),
                IsActive = true,
                PremiumPaid = totalPremium
            };

            if (!PlayerPolicies.ContainsKey(playerId))
                PlayerPolicies[playerId] = new List<InsurancePolicy>();
            PlayerPolicies[playerId].Add(policy);

            // Update profile
            profile.TotalPremiumsPaid += totalPremium;
            profile.ActivePolicies++;

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Insurance policy purchased! Policy ID: {policy.Id}. Coverage: {itemValue} gold for {months} months.");

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.PlayerBoughtInsurance, 
                null, new object[] { itemId, totalPremium });
        }

        private void HandleRenewCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 4)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /insurance renew <policy_id> <months>");
                return;
            }

            string policyId = args[2];
            if (!int.TryParse(args[3], out int months) || months < 1 || months > 12)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Duration must be between 1 and 12 months.");
                return;
            }

            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var policy = GetPlayerPolicies(playerId).FirstOrDefault(p => p.Id == policyId);

            if (policy == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Policy not found.");
                return;
            }

            if (!policy.IsActive)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Policy is not active.");
                return;
            }

            var profile = GetOrCreateProfile(playerId);
            int renewalPremium = (int)(policy.MonthlyPremium * months * 0.95f); // 5% renewal discount

            if (representative.Gold < renewalPremium)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Insufficient gold. Renewal costs {renewalPremium} gold.");
                return;
            }

            representative.GoldLoss(renewalPremium);
            policy.ExpiresAt = policy.ExpiresAt.AddDays(months * 30);
            policy.PremiumPaid += renewalPremium;
            profile.TotalPremiumsPaid += renewalPremium;

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Policy renewed for {months} months. New expiration: {policy.ExpiresAt:yyyy-MM-dd}");
        }

        private void HandleClaimCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /insurance claim <policy_id>");
                return;
            }

            string policyId = args[2];
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            var policy = GetPlayerPolicies(playerId).FirstOrDefault(p => p.Id == policyId);

            if (policy == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Policy not found.");
                return;
            }

            if (!policy.IsActive || policy.ExpiresAt < DateTime.UtcNow)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Policy is not active or has expired.");
                return;
            }

            // Check if item is actually missing
            var inventory = representative.GetInventory();
            if (inventory.IsInventoryIncludes(policy.ItemId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Cannot claim insurance for an item you still possess.");
                return;
            }

            // Check for existing claims on this policy
            var existingClaims = GetPlayerClaims(playerId);
            if (existingClaims.Any(c => c.PolicyId == policyId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "A claim is already pending for this policy.");
                return;
            }

            // Create insurance claim
            var claim = new InsuranceClaim
            {
                Id = Guid.NewGuid().ToString(),
                PolicyId = policyId,
                PlayerId = playerId,
                ClaimAmount = policy.CoverageAmount - policy.Deductible,
                ProcessingFee = (int)(policy.CoverageAmount * ClaimProcessingFee),
                FiledAt = DateTime.UtcNow,
                Status = ClaimStatus.Pending,
                Description = "Item lost - player death"
            };

            if (!PendingClaims.ContainsKey(playerId))
                PendingClaims[playerId] = new List<InsuranceClaim>();
            PendingClaims[playerId].Add(claim);

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Insurance claim filed. Claim ID: {claim.Id}. Estimated payout: {claim.ClaimAmount - claim.ProcessingFee} gold (after fees).");

            // Auto-approve simple claims (in full implementation, this might require manual review)
            if (ShouldAutoApproveClaim(claim, GetOrCreateProfile(playerId)))
            {
                ProcessClaim(representative, claim, true);
            }
        }

        private void HandlePoliciesCommand(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var policies = GetPlayerPolicies(playerId);

            if (policies.Count == 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You have no insurance policies.");
                return;
            }

            var message = "=== YOUR INSURANCE POLICIES ===\n";

            foreach (var policy in policies)
            {
                string status = policy.IsActive && policy.ExpiresAt > DateTime.UtcNow ? "ACTIVE" : "EXPIRED";
                message += $"[{policy.Id}] {policy.ItemId}\n";
                message += $"  Status: {status}\n";
                message += $"  Coverage: {policy.CoverageAmount} gold\n";
                message += $"  Expires: {policy.ExpiresAt:yyyy-MM-dd}\n";
                message += $"  Deductible: {policy.Deductible} gold\n\n";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleClaimsCommand(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var claims = GetPlayerClaims(playerId);

            if (claims.Count == 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You have no pending claims.");
                return;
            }

            var message = "=== YOUR INSURANCE CLAIMS ===\n";

            foreach (var claim in claims)
            {
                message += $"[{claim.Id}] Policy: {claim.PolicyId}\n";
                message += $"  Status: {claim.Status}\n";
                message += $"  Amount: {claim.ClaimAmount} gold\n";
                message += $"  Filed: {claim.FiledAt:yyyy-MM-dd}\n";
                message += $"  Description: {claim.Description}\n\n";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleCancelCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /insurance cancel <policy_id>");
                return;
            }

            string policyId = args[2];
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            var policy = GetPlayerPolicies(playerId).FirstOrDefault(p => p.Id == policyId);

            if (policy == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Policy not found.");
                return;
            }

            if (!policy.IsActive)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Policy is not active.");
                return;
            }

            // Calculate refund (partial based on time remaining)
            var timeRemaining = policy.ExpiresAt - DateTime.UtcNow;
            var totalDuration = policy.ExpiresAt - policy.PurchasedAt;
            float refundRatio = (float)(timeRemaining.TotalDays / totalDuration.TotalDays);
            int refund = (int)(policy.PremiumPaid * refundRatio * 0.8f); // 20% cancellation fee

            if (refund > 0)
            {
                representative.GoldGain(refund);
            }

            policy.IsActive = false;
            var profile = GetOrCreateProfile(playerId);
            profile.ActivePolicies--;

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Policy cancelled. Refund: {refund} gold.");
        }

        private InsuranceQuote CalculateInsuranceQuote(int itemValue, PlayerInsuranceProfile profile)
        {
            float riskMultiplier = GetRiskMultiplier(profile);
            float monthlyPremium = itemValue * BasePremiumRate * riskMultiplier;
            int deductible = Math.Max(100, itemValue / 10); // 10% deductible, minimum 100 gold

            return new InsuranceQuote
            {
                MonthlyPremium = (int)monthlyPremium,
                Deductible = deductible,
                CoverageAmount = itemValue
            };
        }

        private float GetRiskMultiplier(PlayerInsuranceProfile profile)
        {
            float multiplier = 1.0f;

            // Claims history
            if (profile.ClaimsHistory > 0)
            {
                multiplier += profile.ClaimsHistory * 0.2f; // 20% increase per claim
            }

            // Length of relationship
            var accountAge = DateTime.UtcNow - profile.FirstPolicyDate;
            if (accountAge.TotalDays > 365)
            {
                multiplier *= 0.9f; // 10% discount for long-term customers
            }

            // Premium payment history
            if (profile.TotalPremiumsPaid > 10000)
            {
                multiplier *= 0.95f; // 5% discount for high-value customers
            }

            return Math.Max(0.5f, Math.Min(3.0f, multiplier)); // Cap between 50% and 300%
        }

        private string GetRiskRating(PlayerInsuranceProfile profile)
        {
            float multiplier = GetRiskMultiplier(profile);
            
            return multiplier switch
            {
                <= 0.8f => "Excellent",
                <= 1.0f => "Good",
                <= 1.3f => "Average",
                <= 1.7f => "High",
                _ => "Very High"
            };
        }

        private bool ShouldAutoApproveClaim(InsuranceClaim claim, PlayerInsuranceProfile profile)
        {
            // Auto-approve if low risk player and claim amount is reasonable
            return profile.ClaimsHistory <= 2 && claim.ClaimAmount <= 5000;
        }

        private void ProcessClaim(PersistentEmpireRepresentative representative, InsuranceClaim claim, bool approved)
        {
            if (approved)
            {
                int payout = claim.ClaimAmount - claim.ProcessingFee;
                representative.GoldGain(payout);
                claim.Status = ClaimStatus.Approved;
                
                var profile = GetOrCreateProfile(claim.PlayerId);
                profile.ClaimsHistory++;
                profile.TotalClaimsPaid += payout;

                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Insurance claim approved! Payout: {payout} gold.");

                // Deactivate the policy after successful claim
                var policy = GetPlayerPolicies(claim.PlayerId).FirstOrDefault(p => p.Id == claim.PolicyId);
                if (policy != null)
                {
                    policy.IsActive = false;
                    profile.ActivePolicies--;
                }
            }
            else
            {
                claim.Status = ClaimStatus.Denied;
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Insurance claim denied. Contact support for details.");
            }

            // Remove from pending claims
            if (PendingClaims.ContainsKey(claim.PlayerId))
            {
                PendingClaims[claim.PlayerId].Remove(claim);
            }
        }

        private PlayerInsuranceProfile GetOrCreateProfile(string playerId)
        {
            if (!PlayerProfiles.ContainsKey(playerId))
            {
                PlayerProfiles[playerId] = new PlayerInsuranceProfile
                {
                    PlayerId = playerId,
                    FirstPolicyDate = DateTime.UtcNow
                };
            }
            return PlayerProfiles[playerId];
        }

        private List<InsurancePolicy> GetPlayerPolicies(string playerId)
        {
            return PlayerPolicies.ContainsKey(playerId) ? PlayerPolicies[playerId] : new List<InsurancePolicy>();
        }

        private List<InsuranceClaim> GetPlayerClaims(string playerId)
        {
            return PendingClaims.ContainsKey(playerId) ? PendingClaims[playerId] : new List<InsuranceClaim>();
        }

        private int GetItemValue(string itemId)
        {
            // This would use dynamic pricing in a full implementation
            return itemId switch
            {
                var id when id.Contains("legendary") => 10000,
                var id when id.Contains("masterwork") => 5000,
                var id when id.Contains("fine") => 2000,
                var id when id.Contains("sword") => 1000,
                var id when id.Contains("armor") => 1500,
                var id when id.Contains("horse") => 3000,
                _ => 500
            };
        }

        private void ProcessExpiredPolicies()
        {
            foreach (var playerPolicies in PlayerPolicies.Values)
            {
                foreach (var policy in playerPolicies.Where(p => p.IsActive && p.ExpiresAt < DateTime.UtcNow))
                {
                    policy.IsActive = false;
                    var profile = GetOrCreateProfile(policy.PlayerId);
                    profile.ActivePolicies--;
                }
            }
        }

        public static void HandlePlayerDeath(string playerId, List<string> lostItems)
        {
            // This would be called when a player dies and loses items
            if (!PlayerPolicies.ContainsKey(playerId)) return;

            var activePolicies = PlayerPolicies[playerId].Where(p => p.IsActive && p.ExpiresAt > DateTime.UtcNow);
            
            foreach (string lostItem in lostItems)
            {
                var policy = activePolicies.FirstOrDefault(p => p.ItemId == lostItem);
                if (policy != null)
                {
                    // Auto-file claim for insured items lost on death
                    var claim = new InsuranceClaim
                    {
                        Id = Guid.NewGuid().ToString(),
                        PolicyId = policy.Id,
                        PlayerId = playerId,
                        ClaimAmount = policy.CoverageAmount - policy.Deductible,
                        ProcessingFee = (int)(policy.CoverageAmount * 0.1f),
                        FiledAt = DateTime.UtcNow,
                        Status = ClaimStatus.Pending,
                        Description = "Item lost on player death - auto-filed"
                    };

                    if (!PendingClaims.ContainsKey(playerId))
                        PendingClaims[playerId] = new List<InsuranceClaim>();
                    PendingClaims[playerId].Add(claim);
                }
            }
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            return "Insurance Office - Protect your valuable items";
        }

        public enum ClaimStatus
        {
            Pending,
            Approved,
            Denied,
            Processing
        }

        private class InsurancePolicy
        {
            public string Id { get; set; }
            public string PlayerId { get; set; }
            public string ItemId { get; set; }
            public int CoverageAmount { get; set; }
            public int MonthlyPremium { get; set; }
            public int Deductible { get; set; }
            public DateTime PurchasedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public bool IsActive { get; set; }
            public int PremiumPaid { get; set; }
        }

        private class InsuranceClaim
        {
            public string Id { get; set; }
            public string PolicyId { get; set; }
            public string PlayerId { get; set; }
            public int ClaimAmount { get; set; }
            public int ProcessingFee { get; set; }
            public DateTime FiledAt { get; set; }
            public ClaimStatus Status { get; set; }
            public string Description { get; set; }
        }

        private class PlayerInsuranceProfile
        {
            public string PlayerId { get; set; }
            public DateTime FirstPolicyDate { get; set; }
            public int ClaimsHistory { get; set; } = 0;
            public int ActivePolicies { get; set; } = 0;
            public int TotalPremiumsPaid { get; set; } = 0;
            public int TotalClaimsPaid { get; set; } = 0;
        }

        private class InsuranceQuote
        {
            public int MonthlyPremium { get; set; }
            public int Deductible { get; set; }
            public int CoverageAmount { get; set; }
        }
    }
}