package com.persistentempires.minecraft.commands;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import com.persistentempires.minecraft.database.entities.PEPlayer;
import org.bukkit.command.Command;
import org.bukkit.command.CommandExecutor;
import org.bukkit.command.CommandSender;
import org.bukkit.entity.Player;

/**
 * Main command handler for Persistent Empires
 */
public class PersistentEmpiresCommand implements CommandExecutor {
    private final PersistentEmpiresPlugin plugin;

    public PersistentEmpiresCommand(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
    }

    @Override
    public boolean onCommand(CommandSender sender, Command command, String label, String[] args) {
        if (!(sender instanceof Player player)) {
            sender.sendMessage("§cThis command can only be used by players!");
            return true;
        }

        if (args.length == 0) {
            showHelp(player);
            return true;
        }

        String subCommand = args[0].toLowerCase();
        switch (subCommand) {
            case "stats":
                showStats(player);
                break;
            case "bank":
                handleBank(player, args);
                break;
            case "gold":
                handleGold(player, args);
                break;
            case "class":
                handleClass(player, args);
                break;
            case "name":
                handleName(player, args);
                break;
            case "help":
                showHelp(player);
                break;
            default:
                player.sendMessage("§cUnknown subcommand: " + subCommand);
                showHelp(player);
        }

        return true;
    }

    private void showHelp(Player player) {
        player.sendMessage("§6=== Persistent Empires Commands ===");
        player.sendMessage("§e/pe stats §7- Show your character statistics");
        player.sendMessage("§e/pe bank [deposit|withdraw] <amount> §7- Manage your bank account");
        player.sendMessage("§e/pe gold [give|take] <player> <amount> §7- Transfer gold (admin only)");
        player.sendMessage("§e/pe class <class> §7- Change your character class");
        player.sendMessage("§e/pe name <name> §7- Change your display name");
        player.sendMessage("§e/pe help §7- Show this help message");
    }

    private void showStats(Player player) {
        PEPlayer data = plugin.getPlayerManager().getPlayerData(player);
        if (data == null) {
            player.sendMessage("§cPlayer data not found!");
            return;
        }

        player.sendMessage("§6=== Character Statistics ===");
        player.sendMessage("§eGold: §f" + data.getGold());
        player.sendMessage("§eBank: §f" + data.getBankAmount());
        player.sendMessage("§eHunger: §f" + data.getHunger() + "/100");
        player.sendMessage("§eHealth: §f" + data.getHealth() + "/20");
        player.sendMessage("§eExperience: §f" + data.getExperience());
        player.sendMessage("§eClass: §f" + data.getPlayerClass());
        
        if (data.isInFaction()) {
            player.sendMessage("§eFaction: §f" + plugin.getFactionManager().getPlayerFaction(player.getUniqueId().toString()).getName());
        } else {
            player.sendMessage("§eFaction: §7None");
        }
        
        if (data.isInCombat()) {
            player.sendMessage("§cCombat Log: §fActive");
        }
        
        if (data.isWounded()) {
            player.sendMessage("§cWounded: §fYes");
        }
    }

    private void handleBank(Player player, String[] args) {
        if (args.length < 2) {
            player.sendMessage("§cUsage: /pe bank [deposit|withdraw] <amount>");
            return;
        }

        String action = args[1].toLowerCase();
        if (args.length < 3) {
            player.sendMessage("§cUsage: /pe bank " + action + " <amount>");
            return;
        }

        try {
            int amount = Integer.parseInt(args[2]);
            if (amount <= 0) {
                player.sendMessage("§cAmount must be positive!");
                return;
            }

            switch (action) {
                case "deposit":
                    if (plugin.getEconomyManager().depositGold(player, amount)) {
                        player.sendMessage("§aDeposited " + amount + " gold into your bank account.");
                    }
                    break;
                case "withdraw":
                    if (plugin.getEconomyManager().withdrawGold(player, amount)) {
                        player.sendMessage("§aWithdrew " + amount + " gold from your bank account.");
                    }
                    break;
                default:
                    player.sendMessage("§cInvalid action: " + action);
            }
        } catch (NumberFormatException e) {
            player.sendMessage("§cInvalid amount!");
        }
    }

    private void handleGold(Player player, String[] args) {
        if (!player.hasPermission("persistentempires.admin")) {
            player.sendMessage("§cYou don't have permission to use this command!");
            return;
        }

        if (args.length < 4) {
            player.sendMessage("§cUsage: /pe gold [give|take] <player> <amount>");
            return;
        }

        String action = args[1].toLowerCase();
        String targetName = args[2];
        Player target = plugin.getServer().getPlayer(targetName);

        if (target == null) {
            player.sendMessage("§cPlayer not found: " + targetName);
            return;
        }

        try {
            int amount = Integer.parseInt(args[3]);
            if (amount <= 0) {
                player.sendMessage("§cAmount must be positive!");
                return;
            }

            switch (action) {
                case "give":
                    plugin.getPlayerManager().addGold(target, amount);
                    player.sendMessage("§aGave " + amount + " gold to " + target.getName());
                    target.sendMessage("§aReceived " + amount + " gold from an administrator.");
                    break;
                case "take":
                    if (plugin.getPlayerManager().removeGold(target, amount)) {
                        player.sendMessage("§aTook " + amount + " gold from " + target.getName());
                        target.sendMessage("§cAn administrator took " + amount + " gold from you.");
                    } else {
                        player.sendMessage("§c" + target.getName() + " doesn't have enough gold!");
                    }
                    break;
                default:
                    player.sendMessage("§cInvalid action: " + action);
            }
        } catch (NumberFormatException e) {
            player.sendMessage("§cInvalid amount!");
        }
    }

    private void handleClass(Player player, String[] args) {
        if (args.length < 2) {
            player.sendMessage("§cUsage: /pe class <class>");
            player.sendMessage("§eAvailable classes: peasant, soldier, archer, knight, merchant, blacksmith, doctor");
            return;
        }

        String className = args[1].toLowerCase();
        String[] validClasses = {"peasant", "soldier", "archer", "knight", "merchant", "blacksmith", "doctor"};
        
        boolean validClass = false;
        for (String validClassName : validClasses) {
            if (validClassName.equals(className)) {
                validClass = true;
                break;
            }
        }

        if (!validClass) {
            player.sendMessage("§cInvalid class: " + className);
            player.sendMessage("§eAvailable classes: peasant, soldier, archer, knight, merchant, blacksmith, doctor");
            return;
        }

        plugin.getPlayerManager().setPlayerClass(player, className);
        player.sendMessage("§aChanged your class to: " + className);
    }

    private void handleName(Player player, String[] args) {
        if (args.length < 2) {
            player.sendMessage("§cUsage: /pe name <name>");
            return;
        }

        String newName = String.join(" ", java.util.Arrays.copyOfRange(args, 1, args.length));
        
        if (newName.length() > 32) {
            player.sendMessage("§cName too long! Maximum 32 characters.");
            return;
        }

        if (newName.length() < 3) {
            player.sendMessage("§cName too short! Minimum 3 characters.");
            return;
        }

        // Check if player has enough gold for name change
        int nameCost = plugin.getConfig().getInt("general.name_change_cost", 1000);
        if (!plugin.getPlayerManager().removeGold(player, nameCost)) {
            player.sendMessage("§cYou need " + nameCost + " gold to change your name!");
            return;
        }

        plugin.getPlayerManager().setCustomName(player, newName);
        player.sendMessage("§aChanged your display name to: " + newName);
        player.sendMessage("§eCost: " + nameCost + " gold");
    }
}