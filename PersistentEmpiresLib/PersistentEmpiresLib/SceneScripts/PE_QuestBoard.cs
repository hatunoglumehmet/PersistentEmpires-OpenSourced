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
    /// Daily quest system with various quest types and rewards
    /// </summary>
    public class PE_QuestBoard : PE_UsableFromDistance
    {
        public int MaxActiveQuests = 5;
        public int MaxDailyQuests = 3;
        
        private static List<Quest> AvailableQuests = new List<Quest>();
        private static Dictionary<string, List<string>> PlayerActiveQuests = new Dictionary<string, List<string>>();
        private static Dictionary<string, List<string>> PlayerCompletedQuests = new Dictionary<string, List<string>>();
        private static Dictionary<string, DateTime> PlayerLastQuestReset = new Dictionary<string, DateTime>();
        private DateTime LastQuestGeneration = DateTime.MinValue;

        protected override bool LockUserFrames => false;
        protected override bool LockUserPositions => false;

        protected override void OnInit()
        {
            base.OnInit();
            GenerateQuests();
            SetTextVariables();
        }

        private void SetTextVariables()
        {
            base.ActionMessage = new TextObject("Quest Board");
            base.DescriptionMessage = new TextObject("Press {KEY} to view available quests");
        }

        public override void OnUse(Agent userAgent)
        {
            base.OnUse(userAgent);
            if (!GameNetwork.IsServer) return;

            var representative = userAgent.MissionPeer.GetNetworkPeer().GetComponent<PersistentEmpireRepresentative>();
            OpenQuestBoard(representative);
            userAgent.StopUsingGameObjectMT(true);
        }

        private void OpenQuestBoard(PersistentEmpireRepresentative representative)
        {
            RefreshDailyQuests();
            
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var activeQuests = GetPlayerActiveQuests(playerId);
            var completedToday = GetPlayerCompletedQuestsToday(playerId);

            var message = "=== QUEST BOARD ===\n";
            message += $"Available Quests: {AvailableQuests.Count}\n";
            message += $"Your Active Quests: {activeQuests.Count}/{MaxActiveQuests}\n";
            message += $"Completed Today: {completedToday.Count}/{MaxDailyQuests}\n\n";

            message += "Commands:\n";
            message += "  /quest list - View available quests\n";
            message += "  /quest active - View your active quests\n";
            message += "  /quest accept <quest_id> - Accept a quest\n";
            message += "  /quest abandon <quest_id> - Abandon a quest\n";
            message += "  /quest progress <quest_id> - Check quest progress\n";
            message += "  /quest complete <quest_id> - Complete a quest\n";
            message += "  /quest history - View completed quests\n\n";

            // Show featured quests
            var featuredQuests = AvailableQuests.Where(q => q.Difficulty == QuestDifficulty.Hard).Take(3).ToList();
            if (featuredQuests.Count > 0)
            {
                message += "=== FEATURED QUESTS ===\n";
                foreach (var quest in featuredQuests)
                {
                    message += FormatQuestDisplay(quest) + "\n";
                }
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        public void ProcessQuestCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 2) return;

            string command = args[1].ToLower();

            switch (command)
            {
                case "list":
                    HandleListCommand(representative);
                    break;
                case "active":
                    HandleActiveCommand(representative);
                    break;
                case "accept":
                    HandleAcceptCommand(representative, args);
                    break;
                case "abandon":
                    HandleAbandonCommand(representative, args);
                    break;
                case "progress":
                    HandleProgressCommand(representative, args);
                    break;
                case "complete":
                    HandleCompleteCommand(representative, args);
                    break;
                case "history":
                    HandleHistoryCommand(representative);
                    break;
                default:
                    InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                        "Unknown quest command. Use /quest for help.");
                    break;
            }
        }

        private void HandleListCommand(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var availableForPlayer = AvailableQuests.Where(q => CanAcceptQuest(playerId, q)).ToList();

            var message = "=== AVAILABLE QUESTS ===\n";

            if (availableForPlayer.Count == 0)
            {
                message += "No quests available. Check back later!";
            }
            else
            {
                var groupedQuests = availableForPlayer.GroupBy(q => q.Difficulty);
                
                foreach (var group in groupedQuests.OrderBy(g => g.Key))
                {
                    message += $"\n{group.Key.ToString().ToUpper()} QUESTS:\n";
                    foreach (var quest in group)
                    {
                        message += FormatQuestDisplay(quest) + "\n";
                    }
                }
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleActiveCommand(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var activeQuestIds = GetPlayerActiveQuests(playerId);

            if (activeQuestIds.Count == 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You have no active quests.");
                return;
            }

            var message = "=== YOUR ACTIVE QUESTS ===\n";

            foreach (string questId in activeQuestIds)
            {
                var quest = AvailableQuests.FirstOrDefault(q => q.Id == questId);
                if (quest != null)
                {
                    message += FormatQuestDisplay(quest) + "\n";
                    message += $"  Progress: {GetQuestProgress(playerId, quest)}\n";
                }
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleAcceptCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /quest accept <quest_id>");
                return;
            }

            string questId = args[2];
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            var quest = AvailableQuests.FirstOrDefault(q => q.Id == questId);
            if (quest == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Quest not found.");
                return;
            }

            if (!CanAcceptQuest(playerId, quest))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Cannot accept this quest. Check requirements or active quest limit.");
                return;
            }

            // Add to player's active quests
            if (!PlayerActiveQuests.ContainsKey(playerId))
                PlayerActiveQuests[playerId] = new List<string>();
            
            PlayerActiveQuests[playerId].Add(questId);

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Quest accepted: {quest.Title}");

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.PlayerStartedQuest, 
                null, new object[] { questId });
        }

        private void HandleAbandonCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /quest abandon <quest_id>");
                return;
            }

            string questId = args[2];
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            if (!PlayerActiveQuests.ContainsKey(playerId) || !PlayerActiveQuests[playerId].Contains(questId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You don't have this quest active.");
                return;
            }

            PlayerActiveQuests[playerId].Remove(questId);

            var quest = AvailableQuests.FirstOrDefault(q => q.Id == questId);
            string questName = quest?.Title ?? questId;

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Quest abandoned: {questName}");
        }

        private void HandleProgressCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /quest progress <quest_id>");
                return;
            }

            string questId = args[2];
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            var quest = AvailableQuests.FirstOrDefault(q => q.Id == questId);
            if (quest == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Quest not found.");
                return;
            }

            if (!PlayerActiveQuests.ContainsKey(playerId) || !PlayerActiveQuests[playerId].Contains(questId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You don't have this quest active.");
                return;
            }

            var message = $"=== QUEST PROGRESS: {quest.Title} ===\n";
            message += $"Description: {quest.Description}\n";
            message += $"Progress: {GetQuestProgress(playerId, quest)}\n";
            message += $"Rewards: {FormatRewards(quest.Rewards)}\n";

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleCompleteCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /quest complete <quest_id>");
                return;
            }

            string questId = args[2];
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            var quest = AvailableQuests.FirstOrDefault(q => q.Id == questId);
            if (quest == null)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Quest not found.");
                return;
            }

            if (!PlayerActiveQuests.ContainsKey(playerId) || !PlayerActiveQuests[playerId].Contains(questId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You don't have this quest active.");
                return;
            }

            if (!IsQuestComplete(playerId, quest))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Quest requirements not met.");
                return;
            }

            // Complete the quest
            CompleteQuest(representative, quest);
        }

        private void HandleHistoryCommand(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            
            if (!PlayerCompletedQuests.ContainsKey(playerId) || PlayerCompletedQuests[playerId].Count == 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You haven't completed any quests yet.");
                return;
            }

            var completedQuestIds = PlayerCompletedQuests[playerId];
            var message = $"=== QUEST HISTORY ({completedQuestIds.Count} completed) ===\n";

            foreach (string questId in completedQuestIds.TakeLast(10)) // Show last 10
            {
                var quest = AvailableQuests.FirstOrDefault(q => q.Id == questId);
                if (quest != null)
                {
                    message += $"✓ {quest.Title} ({quest.Difficulty})\n";
                }
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void GenerateQuests()
        {
            if (DateTime.UtcNow - LastQuestGeneration < TimeSpan.FromHours(6))
                return;

            AvailableQuests.Clear();
            LastQuestGeneration = DateTime.UtcNow;

            // Generate various types of quests
            GenerateGatheringQuests();
            GenerateCombatQuests();
            GenerateTradingQuests();
            GenerateSocialQuests();
            GenerateExplorationQuests();
        }

        private void GenerateGatheringQuests()
        {
            var gatheringQuests = new[]
            {
                new Quest
                {
                    Id = "gather_wood_001",
                    Title = "Woodcutter's Work",
                    Description = "Gather 50 pieces of wood for the town's construction project.",
                    Type = QuestType.Gathering,
                    Difficulty = QuestDifficulty.Easy,
                    Requirements = new Dictionary<string, int> { ["pe_wood"] = 50 },
                    Rewards = new QuestReward { Gold = 250, Experience = 100, Items = new Dictionary<string, int>() }
                },
                new Quest
                {
                    Id = "mine_iron_001", 
                    Title = "Iron Rush",
                    Description = "Mine 30 iron ore for the blacksmith's orders.",
                    Type = QuestType.Gathering,
                    Difficulty = QuestDifficulty.Medium,
                    Requirements = new Dictionary<string, int> { ["pe_iron_ore"] = 30 },
                    Rewards = new QuestReward { Gold = 400, Experience = 150, Items = new Dictionary<string, int> { ["pe_mining_pick"] = 1 } }
                }
            };

            AvailableQuests.AddRange(gatheringQuests);
        }

        private void GenerateCombatQuests()
        {
            var combatQuests = new[]
            {
                new Quest
                {
                    Id = "eliminate_bandits_001",
                    Title = "Bandit Cleanup",
                    Description = "Eliminate 10 hostile players to clean up the roads.",
                    Type = QuestType.Combat,
                    Difficulty = QuestDifficulty.Medium,
                    Requirements = new Dictionary<string, int> { ["player_kills"] = 10 },
                    Rewards = new QuestReward { Gold = 600, Experience = 200, Items = new Dictionary<string, int>() }
                },
                new Quest
                {
                    Id = "castle_siege_001",
                    Title = "Siege Warfare",
                    Description = "Participate in 3 castle sieges.",
                    Type = QuestType.Combat,
                    Difficulty = QuestDifficulty.Hard,
                    Requirements = new Dictionary<string, int> { ["sieges_participated"] = 3 },
                    Rewards = new QuestReward { Gold = 1000, Experience = 300, Items = new Dictionary<string, int> { ["pe_siege_weapon"] = 1 } }
                }
            };

            AvailableQuests.AddRange(combatQuests);
        }

        private void GenerateTradingQuests()
        {
            var tradingQuests = new[]
            {
                new Quest
                {
                    Id = "merchant_route_001",
                    Title = "Trade Route Establishment",
                    Description = "Complete 5 successful trades at different markets.",
                    Type = QuestType.Trading,
                    Difficulty = QuestDifficulty.Medium,
                    Requirements = new Dictionary<string, int> { ["trades_completed"] = 5 },
                    Rewards = new QuestReward { Gold = 500, Experience = 175, Items = new Dictionary<string, int> { ["pe_merchant_cart"] = 1 } }
                }
            };

            AvailableQuests.AddRange(tradingQuests);
        }

        private void GenerateSocialQuests()
        {
            var socialQuests = new[]
            {
                new Quest
                {
                    Id = "faction_recruit_001",
                    Title = "Recruitment Drive", 
                    Description = "Recruit 3 new members to your faction.",
                    Type = QuestType.Social,
                    Difficulty = QuestDifficulty.Hard,
                    Requirements = new Dictionary<string, int> { ["faction_recruits"] = 3 },
                    Rewards = new QuestReward { Gold = 800, Experience = 250, Items = new Dictionary<string, int>() }
                }
            };

            AvailableQuests.AddRange(socialQuests);
        }

        private void GenerateExplorationQuests()
        {
            var explorationQuests = new[]
            {
                new Quest
                {
                    Id = "map_exploration_001",
                    Title = "Cartographer's Assistant",
                    Description = "Visit 10 different locations across the map.",
                    Type = QuestType.Exploration,
                    Difficulty = QuestDifficulty.Easy,
                    Requirements = new Dictionary<string, int> { ["locations_visited"] = 10 },
                    Rewards = new QuestReward { Gold = 300, Experience = 120, Items = new Dictionary<string, int> { ["pe_map"] = 1 } }
                }
            };

            AvailableQuests.AddRange(explorationQuests);
        }

        private bool CanAcceptQuest(string playerId, Quest quest)
        {
            var activeQuests = GetPlayerActiveQuests(playerId);
            var completedToday = GetPlayerCompletedQuestsToday(playerId);

            if (activeQuests.Count >= MaxActiveQuests) return false;
            if (completedToday.Count >= MaxDailyQuests) return false;
            if (activeQuests.Contains(quest.Id)) return false;

            return true;
        }

        private List<string> GetPlayerActiveQuests(string playerId)
        {
            return PlayerActiveQuests.ContainsKey(playerId) ? PlayerActiveQuests[playerId] : new List<string>();
        }

        private List<string> GetPlayerCompletedQuestsToday(string playerId)
        {
            if (!PlayerCompletedQuests.ContainsKey(playerId)) return new List<string>();
            
            // In a full implementation, this would check if quests were completed today
            return PlayerCompletedQuests[playerId];
        }

        private string GetQuestProgress(string playerId, Quest quest)
        {
            // This would check actual player progress against quest requirements
            // For now, return simulated progress
            if (quest.Requirements.Count == 0) return "Ready to complete";
            
            var firstReq = quest.Requirements.First();
            int current = new Random().Next(0, firstReq.Value + 1);
            return $"{current}/{firstReq.Value} {firstReq.Key}";
        }

        private bool IsQuestComplete(string playerId, Quest quest)
        {
            // This would check if all quest requirements are met
            // For now, simulate random completion
            return new Random().Next(100) < 30; // 30% chance quest is complete
        }

        private void CompleteQuest(PersistentEmpireRepresentative representative, Quest quest)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            
            // Remove from active quests
            if (PlayerActiveQuests.ContainsKey(playerId))
                PlayerActiveQuests[playerId].Remove(quest.Id);
            
            // Add to completed quests
            if (!PlayerCompletedQuests.ContainsKey(playerId))
                PlayerCompletedQuests[playerId] = new List<string>();
            PlayerCompletedQuests[playerId].Add(quest.Id);

            // Give rewards
            representative.GoldGain(quest.Rewards.Gold);
            
            foreach (var item in quest.Rewards.Items)
            {
                representative.GetInventory().AddCountedItem(item.Key, item.Value);
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Quest completed: {quest.Title}! Rewards: {FormatRewards(quest.Rewards)}");

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.PlayerCompletedQuest, 
                null, new object[] { quest.Id, quest.Rewards.Gold });
        }

        private string FormatQuestDisplay(Quest quest)
        {
            return $"[{quest.Id}] {quest.Title} ({quest.Difficulty}) - {quest.Rewards.Gold} gold";
        }

        private string FormatRewards(QuestReward rewards)
        {
            var parts = new List<string>();
            
            if (rewards.Gold > 0) parts.Add($"{rewards.Gold} gold");
            if (rewards.Experience > 0) parts.Add($"{rewards.Experience} exp");
            
            foreach (var item in rewards.Items)
            {
                parts.Add($"{item.Value}x {item.Key}");
            }

            return string.Join(", ", parts);
        }

        private void RefreshDailyQuests()
        {
            // Reset daily quest limits at midnight
            if (DateTime.UtcNow.Hour == 0 && DateTime.UtcNow.Minute < 30)
            {
                var playersToReset = PlayerLastQuestReset.Where(p => 
                    DateTime.UtcNow.Date > p.Value.Date).ToList();
                
                foreach (var player in playersToReset)
                {
                    PlayerLastQuestReset[player.Key] = DateTime.UtcNow;
                    // Reset daily quest completion count for player
                }
                
                GenerateQuests(); // Generate new daily quests
            }
        }

        public override ScriptComponentBehavior.TickRequirement GetTickRequirement()
        {
            return ScriptComponentBehavior.TickRequirement.TickOccasionally;
        }

        protected override void OnTickOccasionally(float currentFrameDeltaTime)
        {
            RefreshDailyQuests();
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            return $"Quest Board ({AvailableQuests.Count} available quests)";
        }

        public enum QuestType
        {
            Gathering,
            Combat,
            Trading,
            Social,
            Exploration,
            Crafting
        }

        public enum QuestDifficulty
        {
            Easy,
            Medium,
            Hard,
            Legendary
        }

        public class Quest
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public QuestType Type { get; set; }
            public QuestDifficulty Difficulty { get; set; }
            public Dictionary<string, int> Requirements { get; set; } = new Dictionary<string, int>();
            public QuestReward Rewards { get; set; } = new QuestReward();
            public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(1);
        }

        public class QuestReward
        {
            public int Gold { get; set; }
            public int Experience { get; set; }
            public Dictionary<string, int> Items { get; set; } = new Dictionary<string, int>();
        }
    }
}