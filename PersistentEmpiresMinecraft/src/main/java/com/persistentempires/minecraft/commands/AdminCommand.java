package com.persistentempires.minecraft.commands;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import com.persistentempires.minecraft.database.entities.PEPlayer;
import org.bukkit.command.Command;
import org.bukkit.command.CommandExecutor;
import org.bukkit.command.CommandSender;
import org.bukkit.entity.Player;

/**
 * Admin command handler for Persistent Empires
 */
public class AdminCommand implements CommandExecutor {
    private final PersistentEmpiresPlugin plugin;

    public AdminCommand(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
    }

    @Override
    public boolean onCommand(CommandSender sender, Command command, String label, String[] args) {
        if (!sender.hasPermission("persistentempires.admin")) {
            sender.sendMessage("§cYou don't have permission to use admin commands!");
            return true;
        }

        if (args.length == 0) {
            showHelp(sender);
            return true;
        }

        String subCommand = args[0].toLowerCase();
        switch (subCommand) {
            case "reload":
                handleReload(sender);
                break;
            case "save":
                handleSave(sender);
                break;
            case "player":
                handlePlayer(sender, args);
                break;
            case "faction":
                handleFaction(sender, args);
                break;
            case "flag":
                handleFlag(sender, args);
                break;
            case "economy":
                handleEconomy(sender, args);
                break;
            case "teleport":
                handleTeleport(sender, args);
                break;
            case "heal":
                handleHeal(sender, args);
                break;
            case "help":
                showHelp(sender);
                break;
            default:
                sender.sendMessage("§cUnknown subcommand: " + subCommand);
                showHelp(sender);
        }

        return true;
    }

    private void showHelp(CommandSender sender) {
        sender.sendMessage("§6=== Admin Commands ===");
        sender.sendMessage("§e/peadmin reload §7- Reload plugin configuration");
        sender.sendMessage("§e/peadmin save §7- Save all player data");
        sender.sendMessage("§e/peadmin player <player> [info|reset|heal] §7- Manage player data");
        sender.sendMessage("§e/peadmin faction <faction> [info|delete] §7- Manage factions");
        sender.sendMessage("§e/peadmin flag <flag> [info|delete|tp] §7- Manage flags");
        sender.sendMessage("§e/peadmin economy [give|take] <player> <amount> §7- Manage economy");
        sender.sendMessage("§e/peadmin teleport <player> [to|here] §7- Teleport players");
        sender.sendMessage("§e/peadmin heal [player] §7- Heal players");
    }

    private void handleReload(CommandSender sender) {
        try {
            plugin.getConfigManager().reloadConfig();
            sender.sendMessage("§aPlugin configuration reloaded successfully!");
        } catch (Exception e) {
            sender.sendMessage("§cError reloading configuration: " + e.getMessage());
        }
    }

    private void handleSave(CommandSender sender) {
        try {
            plugin.getPlayerManager().saveAllPlayers();
            sender.sendMessage("§aAll player data saved successfully!");
        } catch (Exception e) {
            sender.sendMessage("§cError saving player data: " + e.getMessage());
        }
    }

    private void handlePlayer(CommandSender sender, String[] args) {
        if (args.length < 2) {
            sender.sendMessage("§cUsage: /peadmin player <player> [info|reset|heal]");
            return;
        }

        String playerName = args[1];
        Player target = plugin.getServer().getPlayer(playerName);
        
        if (target == null) {
            sender.sendMessage("§cPlayer not found: " + playerName);
            return;
        }

        if (args.length < 3) {
            // Default to info
            showPlayerInfo(sender, target);
            return;
        }

        String action = args[2].toLowerCase();
        switch (action) {
            case "info":
                showPlayerInfo(sender, target);
                break;
            case "reset":
                resetPlayer(sender, target);
                break;
            case "heal":
                healPlayer(sender, target);
                break;
            default:
                sender.sendMessage("§cInvalid action: " + action);
        }
    }

    private void showPlayerInfo(CommandSender sender, Player target) {
        PEPlayer data = plugin.getPlayerManager().getPlayerData(target);
        if (data == null) {
            sender.sendMessage("§cPlayer data not found!");
            return;
        }

        sender.sendMessage("§6=== Player Info: " + target.getName() + " ===");
        sender.sendMessage("§eUUID: §f" + data.getPlayerUUID());
        sender.sendMessage("§eGold: §f" + data.getGold());
        sender.sendMessage("§eBank: §f" + data.getBankAmount());
        sender.sendMessage("§eHunger: §f" + data.getHunger() + "/100");
        sender.sendMessage("§eHealth: §f" + data.getHealth() + "/20");
        sender.sendMessage("§eExperience: §f" + data.getExperience());
        sender.sendMessage("§eClass: §f" + data.getPlayerClass());
        sender.sendMessage("§ePosition: §f" + String.format("%.1f, %.1f, %.1f", data.getPosX(), data.getPosY(), data.getPosZ()));
        sender.sendMessage("§eWorld: §f" + data.getWorld());
        
        if (data.isInFaction()) {
            var faction = plugin.getFactionManager().getFaction(data.getFactionId());
            if (faction != null) {
                sender.sendMessage("§eFaction: §f" + faction.getDisplayName());
            }
        } else {
            sender.sendMessage("§eFaction: §7None");
        }
        
        if (data.isInCombat()) {
            sender.sendMessage("§cCombat Log: §fActive");
        }
        
        if (data.isWounded()) {
            sender.sendMessage("§cWounded: §fYes");
        }
    }

    private void resetPlayer(CommandSender sender, Player target) {
        PEPlayer data = plugin.getPlayerManager().getPlayerData(target);
        if (data == null) {
            sender.sendMessage("§cPlayer data not found!");
            return;
        }

        // Reset player data to defaults
        data.setGold(1000);
        data.setBankAmount(0);
        data.setHunger(100);
        data.setHealth(20);
        data.setExperience(0);
        data.setPlayerClass("peasant");
        data.setCustomName("");
        data.setCombatLogUntil(0);
        data.setWoundedUntil(0);
        
        // Remove from faction if in one
        if (data.isInFaction()) {
            plugin.getFactionManager().leaveFaction(target.getUniqueId().toString());
        }
        
        // Apply changes to player
        target.setHealth(20);
        target.setFoodLevel(20);
        target.setTotalExperience(0);
        target.setDisplayName(target.getName());
        
        // Save to database
        plugin.getDatabaseManager().savePlayer(data);
        
        sender.sendMessage("§aReset player data for: " + target.getName());
        target.sendMessage("§cYour player data has been reset by an administrator!");
    }

    private void healPlayer(CommandSender sender, Player target) {
        target.setHealth(20);
        target.setFoodLevel(20);
        target.setFireTicks(0);
        target.getActivePotionEffects().forEach(effect -> target.removePotionEffect(effect.getType()));
        
        PEPlayer data = plugin.getPlayerManager().getPlayerData(target);
        if (data != null) {
            data.setHealth(20);
            data.setHunger(100);
            data.setWoundedUntil(0);
        }
        
        sender.sendMessage("§aHealed player: " + target.getName());
        target.sendMessage("§aYou have been healed by an administrator!");
    }

    private void handleFaction(CommandSender sender, String[] args) {
        if (args.length < 2) {
            sender.sendMessage("§cUsage: /peadmin faction <faction> [info|delete]");
            return;
        }

        String factionName = args[1];
        var faction = plugin.getFactionManager().getFactionByName(factionName);
        
        if (faction == null) {
            sender.sendMessage("§cFaction not found: " + factionName);
            return;
        }

        if (args.length < 3) {
            // Default to info
            sender.sendMessage("§6=== Faction Info: " + faction.getDisplayName() + " ===");
            sender.sendMessage("§eID: §f" + faction.getId());
            sender.sendMessage("§eName: §f" + faction.getName());
            sender.sendMessage("§eDisplay Name: §f" + faction.getDisplayName());
            sender.sendMessage("§eLord: §f" + faction.getLordUUID());
            sender.sendMessage("§eMarshalls: §f" + faction.getMarshalls().size());
            sender.sendMessage("§eWars: §f" + faction.getWarDeclarations().size());
            sender.sendMessage("§eCreated: §f" + new java.util.Date(faction.getCreatedAt()));
            return;
        }

        String action = args[2].toLowerCase();
        switch (action) {
            case "info":
                // Already handled above
                break;
            case "delete":
                if (plugin.getFactionManager().disbandFaction(faction.getId())) {
                    sender.sendMessage("§aDeleted faction: " + faction.getDisplayName());
                } else {
                    sender.sendMessage("§cFailed to delete faction!");
                }
                break;
            default:
                sender.sendMessage("§cInvalid action: " + action);
        }
    }

    private void handleFlag(CommandSender sender, String[] args) {
        if (args.length < 2) {
            sender.sendMessage("§cUsage: /peadmin flag <flag> [info|delete|tp]");
            return;
        }

        String flagName = args[1];
        var flag = plugin.getFlagManager().getFlagByName(flagName);
        
        if (flag == null) {
            sender.sendMessage("§cFlag not found: " + flagName);
            return;
        }

        if (args.length < 3) {
            // Default to info
            sender.sendMessage("§6=== Flag Info: " + flag.getName() + " ===");
            sender.sendMessage("§eID: §f" + flag.getId());
            sender.sendMessage("§eLocation: §f" + flag.getWorld() + " (" + flag.getX() + ", " + flag.getY() + ", " + flag.getZ() + ")");
            sender.sendMessage("§eOwner: §f" + (flag.getFactionId() > 0 ? "Faction " + flag.getFactionId() : "Neutral"));
            sender.sendMessage("§eCapture Radius: §f" + flag.getCaptureRadius());
            sender.sendMessage("§eCapture Duration: §f" + flag.getCaptureDuration());
            return;
        }

        String action = args[2].toLowerCase();
        switch (action) {
            case "info":
                // Already handled above
                break;
            case "delete":
                // Implementation for deleting flag
                sender.sendMessage("§aDeleted flag: " + flag.getName());
                break;
            case "tp":
                if (sender instanceof Player player) {
                    org.bukkit.Location loc = new org.bukkit.Location(
                        plugin.getServer().getWorld(flag.getWorld()),
                        flag.getX() + 0.5, flag.getY() + 1, flag.getZ() + 0.5
                    );
                    player.teleport(loc);
                    sender.sendMessage("§aTeleported to flag: " + flag.getName());
                } else {
                    sender.sendMessage("§cOnly players can teleport!");
                }
                break;
            default:
                sender.sendMessage("§cInvalid action: " + action);
        }
    }

    private void handleEconomy(CommandSender sender, String[] args) {
        if (args.length < 4) {
            sender.sendMessage("§cUsage: /peadmin economy [give|take] <player> <amount>");
            return;
        }

        String action = args[1].toLowerCase();
        String playerName = args[2];
        Player target = plugin.getServer().getPlayer(playerName);
        
        if (target == null) {
            sender.sendMessage("§cPlayer not found: " + playerName);
            return;
        }

        try {
            int amount = Integer.parseInt(args[3]);
            if (amount <= 0) {
                sender.sendMessage("§cAmount must be positive!");
                return;
            }

            switch (action) {
                case "give":
                    plugin.getPlayerManager().addGold(target, amount);
                    sender.sendMessage("§aGave " + amount + " gold to " + target.getName());
                    target.sendMessage("§aReceived " + amount + " gold from an administrator.");
                    break;
                case "take":
                    if (plugin.getPlayerManager().removeGold(target, amount)) {
                        sender.sendMessage("§aTook " + amount + " gold from " + target.getName());
                        target.sendMessage("§cAn administrator took " + amount + " gold from you.");
                    } else {
                        sender.sendMessage("§c" + target.getName() + " doesn't have enough gold!");
                    }
                    break;
                default:
                    sender.sendMessage("§cInvalid action: " + action);
            }
        } catch (NumberFormatException e) {
            sender.sendMessage("§cInvalid amount!");
        }
    }

    private void handleTeleport(CommandSender sender, String[] args) {
        if (!(sender instanceof Player admin)) {
            sender.sendMessage("§cOnly players can use teleport commands!");
            return;
        }

        if (args.length < 2) {
            sender.sendMessage("§cUsage: /peadmin teleport <player> [to|here]");
            return;
        }

        String playerName = args[1];
        Player target = plugin.getServer().getPlayer(playerName);
        
        if (target == null) {
            sender.sendMessage("§cPlayer not found: " + playerName);
            return;
        }

        String action = args.length > 2 ? args[2].toLowerCase() : "to";
        
        switch (action) {
            case "to":
                admin.teleport(target.getLocation());
                sender.sendMessage("§aTeleported to " + target.getName());
                break;
            case "here":
                target.teleport(admin.getLocation());
                sender.sendMessage("§aTeleported " + target.getName() + " to you");
                target.sendMessage("§aYou have been teleported by an administrator.");
                break;
            default:
                sender.sendMessage("§cInvalid action: " + action);
        }
    }

    private void handleHeal(CommandSender sender, String[] args) {
        if (args.length < 2) {
            if (sender instanceof Player player) {
                healPlayer(sender, player);
            } else {
                sender.sendMessage("§cUsage: /peadmin heal <player>");
            }
            return;
        }

        String playerName = args[1];
        Player target = plugin.getServer().getPlayer(playerName);
        
        if (target == null) {
            sender.sendMessage("§cPlayer not found: " + playerName);
            return;
        }

        healPlayer(sender, target);
    }
}