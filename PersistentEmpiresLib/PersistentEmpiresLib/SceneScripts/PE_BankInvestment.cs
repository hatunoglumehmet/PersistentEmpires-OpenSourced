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
    /// Bank investment system with interest rates, loans, and financial planning
    /// </summary>
    public class PE_BankInvestment : PE_UsableFromDistance
    {
        public float DailyInterestRate = 0.02f; // 2% daily interest
        public float LoanInterestRate = 0.05f; // 5% daily loan interest
        public int MaxLoanAmount = 10000;
        public int MinInvestmentAmount = 100;
        
        private static Dictionary<string, InvestmentAccount> PlayerAccounts = new Dictionary<string, InvestmentAccount>();
        private static Dictionary<string, LoanAccount> PlayerLoans = new Dictionary<string, LoanAccount>();
        private DateTime LastInterestCalculation = DateTime.MinValue;

        protected override bool LockUserFrames => false;
        protected override bool LockUserPositions => false;

        protected override void OnInit()
        {
            base.OnInit();
            SetTextVariables();
        }

        private void SetTextVariables()
        {
            base.ActionMessage = new TextObject("Investment Bank");
            base.DescriptionMessage = new TextObject("Press {KEY} to access banking services");
        }

        public override void OnUse(Agent userAgent)
        {
            base.OnUse(userAgent);
            if (!GameNetwork.IsServer) return;

            var representative = userAgent.MissionPeer.GetNetworkPeer().GetComponent<PersistentEmpireRepresentative>();
            OpenBankingMenu(representative);
            userAgent.StopUsingGameObjectMT(true);
        }

        private void OpenBankingMenu(PersistentEmpireRepresentative representative)
        {
            CalculateInterest();
            
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var account = GetOrCreateAccount(playerId);
            var loan = PlayerLoans.ContainsKey(playerId) ? PlayerLoans[playerId] : null;

            var message = "=== INVESTMENT BANK ===\n";
            message += $"Your Investment Balance: {account.Balance} gold\n";
            message += $"Daily Interest Rate: {DailyInterestRate * 100:F1}%\n";
            message += $"Total Interest Earned: {account.TotalInterestEarned} gold\n";
            
            if (loan != null)
            {
                message += $"\nActive Loan: {loan.Amount} gold\n";
                message += $"Daily Interest: {LoanInterestRate * 100:F1}%\n";
                message += $"Days Remaining: {(loan.DueDate - DateTime.UtcNow).Days}\n";
            }

            message += "\nServices Available:\n";
            message += "  /bank deposit <amount> - Deposit gold for investment\n";
            message += "  /bank withdraw <amount> - Withdraw investment funds\n";
            message += "  /bank loan <amount> <days> - Request a loan\n";
            message += "  /bank repay [amount] - Repay loan (partial or full)\n";
            message += "  /bank statement - View detailed account statement\n";
            message += "  /bank calculator <amount> <days> - Calculate investment returns\n";

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        public void ProcessBankCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 2) return;

            string command = args[1].ToLower();
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;

            switch (command)
            {
                case "deposit":
                    HandleDepositCommand(representative, args);
                    break;
                case "withdraw":
                    HandleWithdrawCommand(representative, args);
                    break;
                case "loan":
                    HandleLoanCommand(representative, args);
                    break;
                case "repay":
                    HandleRepayCommand(representative, args);
                    break;
                case "statement":
                    HandleStatementCommand(representative);
                    break;
                case "calculator":
                    HandleCalculatorCommand(representative, args);
                    break;
                default:
                    InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                        "Unknown bank command. Use /bank for help.");
                    break;
            }
        }

        private void HandleDepositCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3 || !int.TryParse(args[2], out int amount) || amount <= 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /bank deposit <amount>");
                return;
            }

            if (amount < MinInvestmentAmount)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Minimum investment amount is {MinInvestmentAmount} gold.");
                return;
            }

            if (representative.Gold < amount)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Insufficient gold for this deposit.");
                return;
            }

            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var account = GetOrCreateAccount(playerId);

            representative.GoldLoss(amount);
            account.Balance += amount;
            account.TotalDeposited += amount;
            account.LastActivity = DateTime.UtcNow;

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Deposited {amount} gold. New investment balance: {account.Balance} gold.");

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.PlayerGoldWithdrawn, 
                null, new object[] { $"Bank deposit: {amount}" });
        }

        private void HandleWithdrawCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 3 || !int.TryParse(args[2], out int amount) || amount <= 0)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /bank withdraw <amount>");
                return;
            }

            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var account = GetOrCreateAccount(playerId);

            if (account.Balance < amount)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Insufficient investment balance. Available: {account.Balance} gold.");
                return;
            }

            account.Balance -= amount;
            account.TotalWithdrawn += amount;
            account.LastActivity = DateTime.UtcNow;
            representative.GoldGain(amount);

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Withdrew {amount} gold. Remaining investment balance: {account.Balance} gold.");

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.PlayerGoldGained, 
                null, new object[] { $"Bank withdrawal: {amount}" });
        }

        private void HandleLoanCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 4 || !int.TryParse(args[2], out int amount) || !int.TryParse(args[3], out int days))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /bank loan <amount> <days>");
                return;
            }

            if (amount <= 0 || amount > MaxLoanAmount)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Loan amount must be between 1 and {MaxLoanAmount} gold.");
                return;
            }

            if (days <= 0 || days > 30)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Loan duration must be between 1 and 30 days.");
                return;
            }

            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            
            if (PlayerLoans.ContainsKey(playerId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You already have an active loan. Repay it before taking another.");
                return;
            }

            // Check creditworthiness based on investment history
            var account = GetOrCreateAccount(playerId);
            if (account.TotalDeposited < amount / 2)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Insufficient credit history. You need more investment activity for this loan amount.");
                return;
            }

            float totalInterest = amount * LoanInterestRate * days;
            int totalRepayment = amount + (int)totalInterest;

            var loan = new LoanAccount
            {
                PlayerId = playerId,
                Amount = amount,
                TotalRepayment = totalRepayment,
                RemainingBalance = totalRepayment,
                DailyInterest = (int)(amount * LoanInterestRate),
                DueDate = DateTime.UtcNow.AddDays(days),
                TakenAt = DateTime.UtcNow
            };

            PlayerLoans[playerId] = loan;
            representative.GoldGain(amount);

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                $"Loan approved! Received {amount} gold. Total repayment: {totalRepayment} gold over {days} days.");

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.PlayerGoldGained, 
                null, new object[] { $"Bank loan: {amount}" });
        }

        private void HandleRepayCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            
            if (!PlayerLoans.ContainsKey(playerId))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "You don't have any active loans.");
                return;
            }

            var loan = PlayerLoans[playerId];
            int repayAmount = loan.RemainingBalance; // Default to full repayment

            if (args.Length >= 3 && int.TryParse(args[2], out int partialAmount))
            {
                repayAmount = Math.Min(partialAmount, loan.RemainingBalance);
            }

            if (representative.Gold < repayAmount)
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Insufficient gold. Need {repayAmount} gold for repayment.");
                return;
            }

            representative.GoldLoss(repayAmount);
            loan.RemainingBalance -= repayAmount;
            loan.TotalRepaid += repayAmount;

            if (loan.RemainingBalance <= 0)
            {
                PlayerLoans.Remove(playerId);
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Loan fully repaid! Paid {repayAmount} gold. Thank you for your business.");
            }
            else
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    $"Partial repayment of {repayAmount} gold. Remaining balance: {loan.RemainingBalance} gold.");
            }

            LoggerHelper.LogAnAction(representative.MissionPeer.GetNetworkPeer(), LogAction.PlayerGoldWithdrawn, 
                null, new object[] { $"Loan repayment: {repayAmount}" });
        }

        private void HandleStatementCommand(PersistentEmpireRepresentative representative)
        {
            string playerId = representative.MissionPeer.GetNetworkPeer().UserName;
            var account = GetOrCreateAccount(playerId);

            var message = "=== BANK STATEMENT ===\n";
            message += $"Account Holder: {representative.MissionPeer.GetNetworkPeer().UserName}\n";
            message += $"Current Investment Balance: {account.Balance} gold\n";
            message += $"Total Deposited: {account.TotalDeposited} gold\n";
            message += $"Total Withdrawn: {account.TotalWithdrawn} gold\n";
            message += $"Total Interest Earned: {account.TotalInterestEarned} gold\n";
            message += $"Account Opened: {account.CreatedAt:yyyy-MM-dd}\n";
            message += $"Last Activity: {account.LastActivity:yyyy-MM-dd HH:mm}\n";

            if (PlayerLoans.ContainsKey(playerId))
            {
                var loan = PlayerLoans[playerId];
                message += "\n=== ACTIVE LOAN ===\n";
                message += $"Original Amount: {loan.Amount} gold\n";
                message += $"Remaining Balance: {loan.RemainingBalance} gold\n";
                message += $"Daily Interest: {loan.DailyInterest} gold\n";
                message += $"Due Date: {loan.DueDate:yyyy-MM-dd}\n";
                message += $"Total Repaid: {loan.TotalRepaid} gold\n";
            }

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private void HandleCalculatorCommand(PersistentEmpireRepresentative representative, string[] args)
        {
            if (args.Length < 4 || !int.TryParse(args[2], out int amount) || !int.TryParse(args[3], out int days))
            {
                InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(),
                    "Usage: /bank calculator <amount> <days>");
                return;
            }

            float totalInterest = amount * DailyInterestRate * days;
            int finalAmount = amount + (int)totalInterest;

            var message = "=== INVESTMENT CALCULATOR ===\n";
            message += $"Initial Investment: {amount} gold\n";
            message += $"Investment Period: {days} days\n";
            message += $"Daily Interest Rate: {DailyInterestRate * 100:F1}%\n";
            message += $"Total Interest Earned: {(int)totalInterest} gold\n";
            message += $"Final Amount: {finalAmount} gold\n";
            message += $"Return on Investment: {(totalInterest / amount * 100):F1}%\n";

            InformationComponent.Instance.SendMessageToPlayer(representative.MissionPeer.GetNetworkPeer(), message);
        }

        private InvestmentAccount GetOrCreateAccount(string playerId)
        {
            if (!PlayerAccounts.ContainsKey(playerId))
            {
                PlayerAccounts[playerId] = new InvestmentAccount
                {
                    PlayerId = playerId,
                    CreatedAt = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow
                };
            }
            return PlayerAccounts[playerId];
        }

        private void CalculateInterest()
        {
            if (DateTime.UtcNow - LastInterestCalculation < TimeSpan.FromHours(24))
                return;

            LastInterestCalculation = DateTime.UtcNow;

            // Calculate interest for all accounts
            foreach (var account in PlayerAccounts.Values)
            {
                if (account.Balance > 0)
                {
                    int dailyInterest = (int)(account.Balance * DailyInterestRate);
                    account.Balance += dailyInterest;
                    account.TotalInterestEarned += dailyInterest;
                    account.LastInterestCalculation = DateTime.UtcNow;
                }
            }

            // Calculate loan interest and check for overdue loans
            var overdueLoans = new List<string>();
            foreach (var loan in PlayerLoans.Values.ToList())
            {
                // Add daily interest to remaining balance
                loan.RemainingBalance += loan.DailyInterest;

                // Check if loan is overdue
                if (DateTime.UtcNow > loan.DueDate)
                {
                    overdueLoans.Add(loan.PlayerId);
                }
            }

            // Handle overdue loans
            foreach (string playerId in overdueLoans)
            {
                HandleOverdueLoan(playerId);
            }
        }

        private void HandleOverdueLoan(string playerId)
        {
            var loan = PlayerLoans[playerId];
            
            // Add penalty interest (double the daily rate)
            int penalty = loan.DailyInterest * 2;
            loan.RemainingBalance += penalty;

            // Notify player if online
            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.UserName == playerId && peer.IsConnectionActive)
                {
                    InformationComponent.Instance.SendMessageToPlayer(peer,
                        $"OVERDUE LOAN WARNING: Your loan is {(DateTime.UtcNow - loan.DueDate).Days} days overdue! " +
                        $"Penalty applied: {penalty} gold. Current balance: {loan.RemainingBalance} gold.");
                    break;
                }
            }

            // After 7 days overdue, force collections
            if (DateTime.UtcNow - loan.DueDate > TimeSpan.FromDays(7))
            {
                ForceCollectLoan(playerId);
            }
        }

        private void ForceCollectLoan(string playerId)
        {
            var loan = PlayerLoans[playerId];
            
            // Try to collect from player's current gold if online
            foreach (var peer in GameNetwork.NetworkPeers)
            {
                if (peer.UserName == playerId && peer.IsConnectionActive)
                {
                    var rep = peer.GetComponent<PersistentEmpireRepresentative>();
                    if (rep != null)
                    {
                        int collected = Math.Min(rep.Gold, loan.RemainingBalance);
                        if (collected > 0)
                        {
                            rep.GoldLoss(collected);
                            loan.RemainingBalance -= collected;
                            
                            InformationComponent.Instance.SendMessageToPlayer(peer,
                                $"FORCED COLLECTION: {collected} gold collected for overdue loan. " +
                                $"Remaining debt: {loan.RemainingBalance} gold.");
                        }
                    }
                    break;
                }
            }

            // If still has remaining balance, mark as bad debt but remove loan
            if (loan.RemainingBalance > 0)
            {
                // In a full implementation, this could be tracked for credit score
                PlayerLoans.Remove(playerId);
            }
        }

        public override ScriptComponentBehavior.TickRequirement GetTickRequirement()
        {
            return ScriptComponentBehavior.TickRequirement.TickOccasionally;
        }

        protected override void OnTickOccasionally(float currentFrameDeltaTime)
        {
            CalculateInterest();
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            return $"Investment Bank (Interest: {DailyInterestRate * 100:F1}% daily)";
        }

        private class InvestmentAccount
        {
            public string PlayerId { get; set; }
            public int Balance { get; set; } = 0;
            public int TotalDeposited { get; set; } = 0;
            public int TotalWithdrawn { get; set; } = 0;
            public int TotalInterestEarned { get; set; } = 0;
            public DateTime CreatedAt { get; set; }
            public DateTime LastActivity { get; set; }
            public DateTime LastInterestCalculation { get; set; }
        }

        private class LoanAccount
        {
            public string PlayerId { get; set; }
            public int Amount { get; set; }
            public int TotalRepayment { get; set; }
            public int RemainingBalance { get; set; }
            public int DailyInterest { get; set; }
            public int TotalRepaid { get; set; } = 0;
            public DateTime TakenAt { get; set; }
            public DateTime DueDate { get; set; }
        }
    }
}