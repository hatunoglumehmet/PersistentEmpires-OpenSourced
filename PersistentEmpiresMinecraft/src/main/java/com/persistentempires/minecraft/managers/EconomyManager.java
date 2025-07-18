package com.persistentempires.minecraft.managers;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import com.persistentempires.minecraft.database.entities.PEPlayer;
import org.bukkit.entity.Player;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Manages the economy system including gold, banking, and transactions
 */
public class EconomyManager {
    private final PersistentEmpiresPlugin plugin;
    private final Map<String, Long> lastTransactionTime = new ConcurrentHashMap<>();

    public EconomyManager(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
    }

    /**
     * Deposits gold into a player's bank account
     */
    public boolean depositGold(Player player, int amount) {
        if (amount <= 0) return false;
        
        PEPlayer data = plugin.getPlayerManager().getPlayerData(player);
        if (data == null) return false;

        // Check if player has enough gold
        if (data.getGold() < amount) {
            player.sendMessage(plugin.getConfigManager().getMessage("insufficient_funds"));
            return false;
        }

        // Check bank limit
        int bankLimit = plugin.getConfig().getInt("economy.bank_limit", 1000000);
        if (data.getBankAmount() + amount > bankLimit) {
            player.sendMessage(plugin.getConfigManager().getMessage("bank_limit_exceeded"));
            return false;
        }

        // Perform transaction
        data.removeGold(amount);
        data.addBankAmount(amount);
        
        // Log transaction
        logTransaction(player, "DEPOSIT", amount, "Bank deposit");
        
        player.sendMessage("§aDeposited " + amount + " gold into your bank account.");
        return true;
    }

    /**
     * Withdraws gold from a player's bank account
     */
    public boolean withdrawGold(Player player, int amount) {
        if (amount <= 0) return false;
        
        PEPlayer data = plugin.getPlayerManager().getPlayerData(player);
        if (data == null) return false;

        // Check if player has enough in bank
        if (data.getBankAmount() < amount) {
            player.sendMessage(plugin.getConfigManager().getMessage("insufficient_bank_funds"));
            return false;
        }

        // Perform transaction
        data.removeBankAmount(amount);
        data.addGold(amount);
        
        // Log transaction
        logTransaction(player, "WITHDRAW", amount, "Bank withdrawal");
        
        player.sendMessage("§aWithdrew " + amount + " gold from your bank account.");
        return true;
    }

    /**
     * Transfers gold from one player to another
     */
    public boolean transferGold(Player sender, Player recipient, int amount) {
        if (amount <= 0) return false;
        
        PEPlayer senderData = plugin.getPlayerManager().getPlayerData(sender);
        PEPlayer recipientData = plugin.getPlayerManager().getPlayerData(recipient);
        
        if (senderData == null || recipientData == null) return false;

        // Check if sender has enough gold
        if (senderData.getGold() < amount) {
            sender.sendMessage(plugin.getConfigManager().getMessage("insufficient_funds"));
            return false;
        }

        // Check for spam protection
        if (isTransactionSpam(sender)) {
            sender.sendMessage("§cPlease wait before making another transaction.");
            return false;
        }

        // Perform transaction
        senderData.removeGold(amount);
        recipientData.addGold(amount);
        
        // Log transactions
        logTransaction(sender, "TRANSFER_OUT", amount, "Transfer to " + recipient.getName());
        logTransaction(recipient, "TRANSFER_IN", amount, "Transfer from " + sender.getName());
        
        // Update last transaction time
        lastTransactionTime.put(sender.getUniqueId().toString(), System.currentTimeMillis());
        
        sender.sendMessage("§aTransferred " + amount + " gold to " + recipient.getName());
        recipient.sendMessage("§aReceived " + amount + " gold from " + sender.getName());
        return true;
    }

    /**
     * Handles trading between players
     */
    public boolean initiateTrade(Player player1, Player player2) {
        // Check if both players are online and not in combat
        if (!isValidForTrade(player1) || !isValidForTrade(player2)) {
            return false;
        }

        // Create trade session
        // This would open a trading interface
        player1.sendMessage("§aInitiated trade with " + player2.getName());
        player2.sendMessage("§a" + player1.getName() + " wants to trade with you.");
        
        return true;
    }

    /**
     * Calculates tax for a transaction
     */
    public int calculateTax(int amount) {
        // 5% tax on transactions
        return (int) (amount * 0.05);
    }

    /**
     * Handles market transactions
     */
    public boolean buyFromMarket(Player player, String itemName, int quantity, int pricePerItem) {
        PEPlayer data = plugin.getPlayerManager().getPlayerData(player);
        if (data == null) return false;

        int totalCost = quantity * pricePerItem;
        int tax = calculateTax(totalCost);
        int finalCost = totalCost + tax;

        // Check if player has enough gold
        if (data.getGold() < finalCost) {
            player.sendMessage(plugin.getConfigManager().getMessage("insufficient_funds"));
            return false;
        }

        // Perform transaction
        data.removeGold(finalCost);
        
        // Give items to player
        // This would be implemented with proper item giving logic
        
        // Log transaction
        logTransaction(player, "MARKET_BUY", finalCost, "Bought " + quantity + "x " + itemName);
        
        player.sendMessage("§aBought " + quantity + "x " + itemName + " for " + finalCost + " gold (including " + tax + " tax)");
        return true;
    }

    /**
     * Handles selling items to market
     */
    public boolean sellToMarket(Player player, String itemName, int quantity, int pricePerItem) {
        PEPlayer data = plugin.getPlayerManager().getPlayerData(player);
        if (data == null) return false;

        int totalEarnings = quantity * pricePerItem;
        int tax = calculateTax(totalEarnings);
        int finalEarnings = totalEarnings - tax;

        // Check if player has the items
        // This would be implemented with proper item checking logic
        
        // Remove items from player
        // This would be implemented with proper item removal logic
        
        // Add gold to player
        data.addGold(finalEarnings);
        
        // Log transaction
        logTransaction(player, "MARKET_SELL", finalEarnings, "Sold " + quantity + "x " + itemName);
        
        player.sendMessage("§aSold " + quantity + "x " + itemName + " for " + finalEarnings + " gold (after " + tax + " tax)");
        return true;
    }

    /**
     * Handles auction house transactions
     */
    public boolean placeBid(Player player, int auctionId, int bidAmount) {
        PEPlayer data = plugin.getPlayerManager().getPlayerData(player);
        if (data == null) return false;

        // Check if player has enough gold
        if (data.getGold() < bidAmount) {
            player.sendMessage(plugin.getConfigManager().getMessage("insufficient_funds"));
            return false;
        }

        // Check auction validity
        // This would check if the auction exists and is active
        
        // Place bid
        // This would be implemented with proper auction logic
        
        player.sendMessage("§aPlaced bid of " + bidAmount + " gold on auction #" + auctionId);
        return true;
    }

    /**
     * Handles loan system
     */
    public boolean takeLoan(Player player, int amount) {
        PEPlayer data = plugin.getPlayerManager().getPlayerData(player);
        if (data == null) return false;

        // Check loan eligibility
        // This would check credit score, existing loans, etc.
        
        // Give loan
        data.addGold(amount);
        
        // Log transaction
        logTransaction(player, "LOAN", amount, "Loan taken");
        
        player.sendMessage("§aReceived loan of " + amount + " gold.");
        return true;
    }

    /**
     * Handles insurance for items
     */
    public boolean insureItem(Player player, org.bukkit.inventory.ItemStack item, int insuranceAmount) {
        PEPlayer data = plugin.getPlayerManager().getPlayerData(player);
        if (data == null) return false;

        // Check if player has enough gold
        if (data.getGold() < insuranceAmount) {
            player.sendMessage(plugin.getConfigManager().getMessage("insufficient_funds"));
            return false;
        }

        // Pay insurance
        data.removeGold(insuranceAmount);
        
        // Add insurance tag to item
        // This would be implemented with proper item meta handling
        
        // Log transaction
        logTransaction(player, "INSURANCE", insuranceAmount, "Insured item: " + item.getType().toString());
        
        player.sendMessage("§aInsured " + item.getType().toString() + " for " + insuranceAmount + " gold.");
        return true;
    }

    /**
     * Handles investment system
     */
    public boolean makeInvestment(Player player, String investmentType, int amount) {
        PEPlayer data = plugin.getPlayerManager().getPlayerData(player);
        if (data == null) return false;

        // Check if player has enough gold
        if (data.getGold() < amount) {
            player.sendMessage(plugin.getConfigManager().getMessage("insufficient_funds"));
            return false;
        }

        // Make investment
        data.removeGold(amount);
        
        // Log transaction
        logTransaction(player, "INVESTMENT", amount, "Investment in " + investmentType);
        
        player.sendMessage("§aInvested " + amount + " gold in " + investmentType);
        return true;
    }

    /**
     * Processes daily interest for bank accounts
     */
    public void processDailyInterest() {
        // This would be called daily to add interest to bank accounts
        for (Player player : plugin.getServer().getOnlinePlayers()) {
            PEPlayer data = plugin.getPlayerManager().getPlayerData(player);
            if (data != null && data.getBankAmount() > 0) {
                int interest = (int) (data.getBankAmount() * 0.01); // 1% daily interest
                data.addBankAmount(interest);
                
                if (interest > 0) {
                    player.sendMessage("§aEarned " + interest + " gold in bank interest.");
                    logTransaction(player, "INTEREST", interest, "Daily bank interest");
                }
            }
        }
    }

    private boolean isValidForTrade(Player player) {
        return player.isOnline() && !plugin.getCombatManager().isInCombat(player);
    }

    private boolean isTransactionSpam(Player player) {
        Long lastTime = lastTransactionTime.get(player.getUniqueId().toString());
        if (lastTime == null) return false;
        
        return System.currentTimeMillis() - lastTime < 5000; // 5 second cooldown
    }

    private void logTransaction(Player player, String type, int amount, String description) {
        // Log transaction to database
        // This would be implemented with proper database logging
        plugin.getLogger().info("Transaction: " + player.getName() + " - " + type + " - " + amount + " - " + description);
    }

    // Getters and utility methods
    public int getGold(Player player) {
        return plugin.getPlayerManager().getGold(player);
    }

    public int getBankAmount(Player player) {
        return plugin.getPlayerManager().getBankAmount(player);
    }

    public boolean hasEnoughGold(Player player, int amount) {
        return getGold(player) >= amount;
    }

    public boolean hasEnoughBankAmount(Player player, int amount) {
        return getBankAmount(player) >= amount;
    }
}