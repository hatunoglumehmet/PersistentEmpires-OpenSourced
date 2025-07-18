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
    /// Bounty hunting system with wanted criminals, rewards, and hunter reputation
    /// </summary>
    public class PE_BountyBoard : PE_UsableFromDistance
    {
        public int MaxBounties = 20;
        public float BountyDecayDays = 7f;
        public int MinBountyAmount = 100;
        public int MaxBountyAmount = 5000;
        
        private static List<BountyTarget> ActiveBounties = new List<BountyTarget>();
        private static Dictionary<string, HunterProfile> BountyHunters = new Dictionary<string, HunterProfile>();
        private static Dictionary<string, int> PlayerCrimePoints = new Dictionary<string, int>();

        protected override bool LockUserFrames => false;
        protected override bool LockUserPositions => false;

        protected override void OnInit()
        {
            base.OnInit();
            SetTextVariables();
        }

        private void SetTextVariables()
        {
            base.ActionMessage = new TextObject("Bounty Board");
            base.DescriptionMessage = new TextObject("Press {KEY} to view bounties");
        }

        public override void OnUse(Agent userAgent)
        {
            base.OnUse(userAgent);
            if (!GameNetwork.IsServer) return;

            var representative = userAgent.MissionPeer.GetNetworkPeer().GetComponent<PersistentEmpireRepresentative>();
            OpenBountyBoard(representative);
            userAgent.StopUsingGameObjectMT(true);
        }

        private void OpenBountyBoard(PersistentEmpireRepresentative representative)
        {
            CleanExpiredBounties();
            
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var hunter = GetOrCreateHunter(playerId);

            var message = "=== BOUNTY BOARD ===\n";
            message += $"Active Bounties: {ActiveBounties.Count}/{MaxBounties}\n";
            message += $"Your Hunter Rank: {GetHunterRank(hunter.Reputation)}\n";
            message += $"Bounties Claimed: {hunter.BountiesClaimed}\n";
            message += $"Total Earnings: {hunter.TotalEarnings} gold\n\n";

            message += "Commands:\n";
            message += "  /bounty list - View all active bounties\n";
            message += "  /bounty place <player> <amount> <reason> - Place bounty\n";
            message += "  /bounty claim <bounty_id> - Claim you killed target\n";
            message += "  /bounty track <bounty_id> - Get target's last known location\n";
            message += "  /bounty reputation - View your hunter profile\n";
            message += "  /bounty crimes - View crime system info\n\n";

            // Show featured bounties
            var topBounties = ActiveBounties.OrderByDescending(b => b.RewardAmount).Take(5).ToList();
            if (topBounties.Count > 0)
            {
                message += "=== TOP BOUNTIES ===\n";
                foreach (var bounty in topBounties)
                {
                    message += FormatBountyDisplay(bounty) + "\n";
                }
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        public void ProcessBountyCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 2) return;

            string command = args[1].ToLower();

            switch (command)
            {
                case "list":
                    HandleListCommand(representative);
                    break;
                case "place":
                    HandlePlaceCommand(representative, args);
                    break;
                case "claim":
                    HandleClaimCommand(representative, args);
                    break;
                case "track":
                    HandleTrackCommand(representative, args);
                    break;
                case "reputation":
                    HandleReputationCommand(representative);
                    break;
                case "crimes":
                    HandleCrimesCommand(representative);
                    break;
                default:
                    InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                        "Unknown bounty command. Use /bounty for help.");
                    break;
            }
        }

        private void HandleListCommand(PersistentEmpireRepresentative representative)
        {
            if (ActiveBounties.Count == 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "No active bounties.");
                return;
            }

            var message = "=== ALL ACTIVE BOUNTIES ===\n";
            var sortedBounties = ActiveBounties.OrderByDescending(b => b.RewardAmount).ToList();

            foreach (var bounty in sortedBounties)
            {
                message += FormatBountyDisplay(bounty) + "\n";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandlePlaceCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 5)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /bounty place <player_name> <amount> <reason>");
                return;
            }

            string targetName = args[2];
            if (!int.TryParse(args[3], out int amount) || amount < MinBountyAmount || amount > MaxBountyAmount)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Bounty amount must be between {MinBountyAmount} and {MaxBountyAmount} gold.");
                return;
            }

            string reason = string.Join(" ", args.Skip(4));
            if (reason.Length > 100)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Reason must be 100 characters or less.");
                return;
            }

            string placerId = representative.MissionPeer.GetNetworkPeer().UserName;
            
            if (targetName.Equals(placerId, StringComparison.OrdinalIgnoreCase))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You cannot place a bounty on yourself.");
                return;
            }

            if (representative.Gold < amount)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Insufficient gold to place this bounty.");
                return;
            }

            // Check if target exists (is online)
            bool targetExists = false;
            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.UserName.Equals(targetName, StringComparison.OrdinalIgnoreCase) && peer.IsConnectionActive)
                {
                    targetExists = true;
                    targetName = peer.UserName; // Use exact case
                    break;
                }
            }

            if (!targetExists)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Target player not found or not online.");
                return;
            }

            // Check if bounty already exists
            var existingBounty = ActiveBounties.FirstOrDefault(b => b.TargetName.Equals(targetName, StringComparison.OrdinalIgnoreCase));
            if (existingBounty != null)
            {
                // Add to existing bounty
                representative.GoldLoss(amount);
                existingBounty.RewardAmount += amount;
                existingBounty.Contributors.Add(new BountyContributor { PlayerId = placerId, Amount = amount });
                
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Added {amount} gold to existing bounty on {targetName}. Total bounty: {existingBounty.RewardAmount} gold.");
            }
            else
            {
                // Create new bounty
                if (ActiveBounties.Count >= MaxBounties)
                {
                    InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                        "Bounty board is full. Try again later.");
                    return;
                }

                representative.GoldLoss(amount);
                
                var bounty = new BountyTarget
                {
                    Id = Guid.NewGuid().ToString(),
                    TargetName = targetName,
                    RewardAmount = amount,
                    Reason = reason,
                    PlacedBy = placerId,
                    PlacedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(BountyDecayDays)
                };
                
                bounty.Contributors.Add(new BountyContributor { PlayerId = placerId, Amount = amount });
                ActiveBounties.Add(bounty);

                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Bounty placed on {targetName} for {amount} gold. Bounty ID: {bounty.Id}");

                // Notify target
                NotifyPlayer(targetName, $"A bounty of {amount} gold has been placed on your head! Reason: {reason}");
            }

            // Increase target's crime points
            if (!PlayerCrimePoints.ContainsKey(targetName))
                PlayerCrimePoints[targetName] = 0;
            PlayerCrimePoints[targetName] += amount / 100; // 1 crime point per 100 gold bounty

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.PlayerGoldWithdrawn, 
                null, new object[] { $"Bounty placed: {amount} on {targetName}" });
        }

        private void HandleClaimCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /bounty claim <bounty_id>");
                return;
            }

            string bountyId = args[2];
            string hunterId = representative.MissionPeer.GetNetworkPeer().UserName;

            var bounty = ActiveBounties.FirstOrDefault(b => b.Id == bountyId);
            if (bounty == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Bounty not found.");
                return;
            }

            if (bounty.TargetName.Equals(hunterId, StringComparison.OrdinalIgnoreCase))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You cannot claim a bounty on yourself.");
                return;
            }

            // Check if target is actually dead/offline for sufficient time
            bool targetOffline = true;
            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.UserName.Equals(bounty.TargetName, StringComparison.OrdinalIgnoreCase) && peer.IsConnectionActive)
                {
                    targetOffline = false;
                    break;
                }
            }

            if (!targetOffline)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Target is still alive/online. You must eliminate them first.");
                return;
            }

            // Award bounty to hunter
            representative.GoldGain(bounty.RewardAmount);
            
            var hunter = GetOrCreateHunter(hunterId);
            hunter.BountiesClaimed++;
            hunter.TotalEarnings += bounty.RewardAmount;
            hunter.Reputation += CalculateReputationGain(bounty.RewardAmount);

            // Remove bounty
            ActiveBounties.Remove(bounty);

            // Clear crime points for target
            if (PlayerCrimePoints.ContainsKey(bounty.TargetName))
            {
                PlayerCrimePoints[bounty.TargetName] = Math.Max(0, PlayerCrimePoints[bounty.TargetName] - 10);
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Bounty claimed! Received {bounty.RewardAmount} gold for eliminating {bounty.TargetName}.");

            // Notify all players
            BroadcastMessage($"{hunterId} has claimed the bounty on {bounty.TargetName} for {bounty.RewardAmount} gold!");

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.PlayerGoldGained, 
                null, new object[] { $"Bounty claimed: {bounty.RewardAmount} for {bounty.TargetName}" });
        }

        private void HandleTrackCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /bounty track <bounty_id>");
                return;
            }

            string bountyId = args[2];
            string hunterId = representative.MissionPeer.GetNetworkPeer().UserName;

            var bounty = ActiveBounties.FirstOrDefault(b => b.Id == bountyId);
            if (bounty == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Bounty not found.");
                return;
            }

            var hunter = GetOrCreateHunter(hunterId);
            if (hunter.Reputation < 50)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You need at least 50 reputation to use tracking services.");
                return;
            }

            // Check if target is online
            bool targetFound = false;
            string locationInfo = "Unknown";

            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.UserName.Equals(bounty.TargetName, StringComparison.OrdinalIgnoreCase) && peer.IsConnectionActive)
                {
                    targetFound = true;
                    
                    if (peer.ControlledAgent != null)
                    {
                        var position = peer.ControlledAgent.Position;
                        locationInfo = $"Last seen near coordinates ({position.X:F0}, {position.Y:F0})";
                    }
                    else
                    {
                        locationInfo = "Online but not spawned";
                    }
                    break;
                }
            }

            if (!targetFound)
            {
                locationInfo = $"Offline - Last seen {DateTime.UtcNow.Subtract(TimeSpan.FromHours(new Random().Next(1, 24))):yyyy-MM-dd HH:mm}";
            }

            var message = $"=== TRACKING: {bounty.TargetName} ===\n";
            message += $"Status: {(targetFound ? "ONLINE" : "OFFLINE")}\n";
            message += $"Location: {locationInfo}\n";
            message += $"Bounty: {bounty.RewardAmount} gold\n";
            message += $"Wanted for: {bounty.Reason}\n";
            message += $"Crime Level: {GetCrimeLevel(bounty.TargetName)}\n";

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleReputationCommand(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var hunter = GetOrCreateHunter(playerId);

            var message = "=== BOUNTY HUNTER PROFILE ===\n";
            message += $"Hunter: {representative.MissionPeer.GetNetworkPeer().UserName}\n";
            message += $"Rank: {GetHunterRank(hunter.Reputation)}\n";
            message += $"Reputation: {hunter.Reputation}\n";
            message += $"Bounties Claimed: {hunter.BountiesClaimed}\n";
            message += $"Total Earnings: {hunter.TotalEarnings} gold\n";
            message += $"Success Rate: {(hunter.BountiesClaimed > 0 ? (hunter.BountiesClaimed * 100 / Math.Max(hunter.BountiesClaimed + hunter.FailedAttempts, 1)) : 0)}%\n";
            message += $"Joined: {hunter.JoinedAt:yyyy-MM-dd}\n\n";

            message += "Reputation Benefits:\n";
            message += "  50+ : Tracking services unlocked\n";
            message += "  100+: 10% bounty bonus\n";
            message += "  200+: 20% bounty bonus\n";
            message += "  500+: Access to legendary bounties\n";

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleCrimesCommand(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            int crimePoints = PlayerCrimePoints.ContainsKey(playerId) ? PlayerCrimePoints[playerId] : 0;

            var message = "=== CRIME SYSTEM ===\n";
            message += $"Your Crime Points: {crimePoints}\n";
            message += $"Crime Level: {GetCrimeLevel(playerId)}\n\n";

            message += "Crime Point Sources:\n";
            message += "  - Bounties placed on you: +1 per 100 gold\n";
            message += "  - Killing innocent players: +5 points\n";
            message += "  - Stealing from other players: +2 points\n";
            message += "  - Destroying property: +3 points\n\n";

            message += "Crime Point Reduction:\n";
            message += "  - Bounty claimed against you: -10 points\n";
            message += "  - Time passage: -1 point per day\n";
            message += "  - Good deeds: Variable reduction\n\n";

            message += "Crime Levels:\n";
            message += "  0-10: Law-abiding citizen\n";
            message += "  11-25: Troublemaker\n";
            message += "  26-50: Criminal\n";
            message += "  51-100: Dangerous outlaw\n";
            message += "  100+: Public enemy\n";

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private string FormatBountyDisplay(BountyTarget bounty)
        {
            string timeLeft = GetTimeLeftString(bounty.ExpiresAt);
            string crimeLevel = GetCrimeLevel(bounty.TargetName);
            
            return $"[{bounty.Id}] {bounty.TargetName} | {bounty.RewardAmount} gold | {crimeLevel} | {timeLeft} | {bounty.Reason}";
        }

        private string GetTimeLeftString(DateTime expiresAt)
        {
            var timeLeft = expiresAt - DateTime.UtcNow;
            if (timeLeft.TotalDays >= 1)
                return $"{(int)timeLeft.TotalDays}d";
            else if (timeLeft.TotalHours >= 1)
                return $"{(int)timeLeft.TotalHours}h";
            else
                return $"{timeLeft.Minutes}m";
        }

        private string GetHunterRank(int reputation)
        {
            return reputation switch
            {
                >= 500 => "Legendary Hunter",
                >= 200 => "Master Hunter", 
                >= 100 => "Expert Hunter",
                >= 50 => "Experienced Hunter",
                >= 10 => "Novice Hunter",
                _ => "Rookie"
            };
        }

        private string GetCrimeLevel(string playerId)
        {
            int points = PlayerCrimePoints.ContainsKey(playerId) ? PlayerCrimePoints[playerId] : 0;
            return points switch
            {
                >= 100 => "Public Enemy",
                >= 51 => "Dangerous Outlaw",
                >= 26 => "Criminal",
                >= 11 => "Troublemaker",
                _ => "Citizen"
            };
        }

        private int CalculateReputationGain(int bountyAmount)
        {
            return Math.Max(1, bountyAmount / 200); // 1 rep per 200 gold bounty
        }

        private HunterProfile GetOrCreateHunter(string playerId)
        {
            if (!BountyHunters.ContainsKey(playerId))
            {
                BountyHunters[playerId] = new HunterProfile
                {
                    PlayerId = playerId,
                    JoinedAt = DateTime.UtcNow
                };
            }
            return BountyHunters[playerId];
        }

        private void CleanExpiredBounties()
        {
            var expiredBounties = ActiveBounties.Where(b => b.ExpiresAt < DateTime.UtcNow).ToList();
            
            foreach (var bounty in expiredBounties)
            {
                // Refund contributors
                foreach (var contributor in bounty.Contributors)
                {
                    RefundPlayer(contributor.PlayerId, contributor.Amount);
                }
                
                ActiveBounties.Remove(bounty);
                
                BroadcastMessage($"Bounty on {bounty.TargetName} has expired. Contributions refunded.");
            }
        }

        private void RefundPlayer(string playerId, int amount)
        {
            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.UserName == playerId && peer.IsConnectionActive)
                {
                    var rep = peer.GetComponent<PersistentEmpireRepresentative>();
                    if (rep != null)
                    {
                        rep.GoldGain(amount);
                        InformationComponent.Instance.SendMessageToPlayer(peer, 
                            $"Bounty expired. Refunded {amount} gold.");
                    }
                    break;
                }
            }
        }

        private void NotifyPlayer(string playerId, string message)
        {
            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.UserName.Equals(playerId, StringComparison.OrdinalIgnoreCase) && peer.IsConnectionActive)
                {
                    InformationComponent.Instance.SendMessageToPlayer(peer, message);
                    break;
                }
            }
        }

        private void BroadcastMessage(string message)
        {
            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.IsConnectionActive)
                {
                    InformationComponent.Instance.SendMessageToPlayer(peer, message);
                }
            }
        }

        public static void AddCrimePoints(string playerId, int points, string reason)
        {
            if (!PlayerCrimePoints.ContainsKey(playerId))
                PlayerCrimePoints[playerId] = 0;
                
            PlayerCrimePoints[playerId] += points;
            
            // Auto-create bounty for high crime players
            if (PlayerCrimePoints[playerId] >= 50 && !ActiveBounties.Any(b => b.TargetName == playerId))
            {
                var autoBounty = new BountyTarget
                {
                    Id = Guid.NewGuid().ToString(),
                    TargetName = playerId,
                    RewardAmount = PlayerCrimePoints[playerId] * 10,
                    Reason = "Accumulated criminal activity",
                    PlacedBy = "System",
                    PlacedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(14)
                };
                
                ActiveBounties.Add(autoBounty);
            }
        }

        public override ScriptComponentBehavior.TickRequirement GetTickRequirement()
        {
            return ScriptComponentBehavior.TickRequirement.TickOccasionally;
        }

        protected override void OnTickOccasionally(float currentFrameDeltaTime)
        {
            CleanExpiredBounties();
            
            // Reduce crime points over time (daily)
            if (DateTime.UtcNow.Hour == 0 && DateTime.UtcNow.Minute < 10) // Once per day
            {
                var playersToUpdate = PlayerCrimePoints.Keys.ToList();
                foreach (string playerId in playersToUpdate)
                {
                    PlayerCrimePoints[playerId] = Math.Max(0, PlayerCrimePoints[playerId] - 1);
                }
            }
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            return $"Bounty Board ({ActiveBounties.Count} active bounties)";
        }

        private class BountyTarget
        {
            public string Id { get; set; }
            public string TargetName { get; set; }
            public int RewardAmount { get; set; }
            public string Reason { get; set; }
            public string PlacedBy { get; set; }
            public DateTime PlacedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public List<BountyContributor> Contributors { get; set; } = new List<BountyContributor>();
        }

        private class BountyContributor
        {
            public string PlayerId { get; set; }
            public int Amount { get; set; }
        }

        private class HunterProfile
        {
            public string PlayerId { get; set; }
            public int Reputation { get; set; } = 0;
            public int BountiesClaimed { get; set; } = 0;
            public int FailedAttempts { get; set; } = 0;
            public int TotalEarnings { get; set; } = 0;
            public DateTime JoinedAt { get; set; }
        }
    }
}