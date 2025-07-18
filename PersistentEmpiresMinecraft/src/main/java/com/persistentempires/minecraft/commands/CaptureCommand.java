package com.persistentempires.minecraft.commands;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import com.persistentempires.minecraft.database.entities.PEFlag;
import org.bukkit.Location;
import org.bukkit.command.Command;
import org.bukkit.command.CommandExecutor;
import org.bukkit.command.CommandSender;
import org.bukkit.entity.Player;

/**
 * Command handler for flag capturing system
 */
public class CaptureCommand implements CommandExecutor {
    private final PersistentEmpiresPlugin plugin;

    public CaptureCommand(PersistentEmpiresPlugin plugin) {
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
            case "create":
                handleCreate(player, args);
                break;
            case "start":
                handleStart(player, args);
                break;
            case "cancel":
                handleCancel(player);
                break;
            case "info":
                handleInfo(player, args);
                break;
            case "list":
                handleList(player);
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
        player.sendMessage("§6=== Capture Commands ===");
        player.sendMessage("§e/capture create <name> §7- Create a new flag at your location");
        player.sendMessage("§e/capture start <flag> §7- Start capturing a flag");
        player.sendMessage("§e/capture cancel §7- Cancel current capture");
        player.sendMessage("§e/capture info <flag> §7- Show flag information");
        player.sendMessage("§e/capture list §7- List all flags");
        player.sendMessage("§e/capture help §7- Show this help message");
    }

    private void handleCreate(Player player, String[] args) {
        if (!player.hasPermission("persistentempires.admin")) {
            player.sendMessage("§cYou don't have permission to create flags!");
            return;
        }

        if (args.length < 2) {
            player.sendMessage("§cUsage: /capture create <name>");
            return;
        }

        String flagName = args[1];
        
        // Validate flag name
        if (flagName.length() < 3 || flagName.length() > 32) {
            player.sendMessage("§cFlag name must be between 3 and 32 characters!");
            return;
        }

        // Check if flag name already exists
        if (plugin.getFlagManager().getFlagByName(flagName) != null) {
            player.sendMessage("§cFlag name already exists!");
            return;
        }

        // Create flag at player's location
        Location location = player.getLocation();
        PEFlag flag = plugin.getFlagManager().createFlag(flagName, location);
        
        if (flag != null) {
            player.sendMessage("§aFlag '" + flagName + "' created at your location!");
            player.sendMessage("§eFlag ID: " + flag.getId());
        } else {
            player.sendMessage("§cFailed to create flag!");
        }
    }

    private void handleStart(Player player, String[] args) {
        if (args.length < 2) {
            player.sendMessage("§cUsage: /capture start <flag>");
            return;
        }

        String flagName = args[1];
        PEFlag flag = plugin.getFlagManager().getFlagByName(flagName);
        
        if (flag == null) {
            player.sendMessage("§cFlag not found: " + flagName);
            return;
        }

        // Check if player is in a faction
        if (!plugin.getFactionManager().isPlayerInFaction(player.getUniqueId().toString())) {
            player.sendMessage("§cYou must be in a faction to capture flags!");
            return;
        }

        // Check if player is already capturing
        if (plugin.getFlagManager().isPlayerCapturing(player)) {
            player.sendMessage("§cYou are already capturing a flag!");
            return;
        }

        // Check if player is in range
        Location playerLoc = player.getLocation();
        Location flagLoc = new Location(
            plugin.getServer().getWorld(flag.getWorld()),
            flag.getX(), flag.getY(), flag.getZ()
        );
        
        if (playerLoc.getWorld() != flagLoc.getWorld() || playerLoc.distance(flagLoc) > flag.getCaptureRadius()) {
            player.sendMessage("§cYou are too far from the flag to capture it!");
            return;
        }

        // Start capture
        if (plugin.getFlagManager().startCapture(player, flag)) {
            player.sendMessage("§aStarted capturing flag: " + flag.getName());
            player.sendMessage("§eStay within " + flag.getCaptureRadius() + " blocks and wait " + flag.getCaptureDuration() + " seconds.");
        } else {
            player.sendMessage("§cFailed to start capture!");
        }
    }

    private void handleCancel(Player player) {
        if (!plugin.getFlagManager().isPlayerCapturing(player)) {
            player.sendMessage("§cYou are not capturing any flag!");
            return;
        }

        plugin.getFlagManager().cancelCapture(player);
        player.sendMessage("§aCancelled flag capture.");
    }

    private void handleInfo(Player player, String[] args) {
        if (args.length < 2) {
            player.sendMessage("§cUsage: /capture info <flag>");
            return;
        }

        String flagName = args[1];
        PEFlag flag = plugin.getFlagManager().getFlagByName(flagName);
        
        if (flag == null) {
            player.sendMessage("§cFlag not found: " + flagName);
            return;
        }

        // Display flag info
        player.sendMessage("§6=== Flag Information ===");
        player.sendMessage("§eName: §f" + flag.getName());
        player.sendMessage("§eID: §f" + flag.getId());
        player.sendMessage("§eLocation: §f" + flag.getWorld() + " (" + flag.getX() + ", " + flag.getY() + ", " + flag.getZ() + ")");
        player.sendMessage("§eCapture Radius: §f" + flag.getCaptureRadius() + " blocks");
        player.sendMessage("§eCapture Duration: §f" + flag.getCaptureDuration() + " seconds");
        
        if (flag.getFactionId() > 0) {
            var faction = plugin.getFactionManager().getFaction(flag.getFactionId());
            if (faction != null) {
                player.sendMessage("§eOwner: §f" + faction.getDisplayName());
            } else {
                player.sendMessage("§eOwner: §cUnknown Faction");
            }
        } else {
            player.sendMessage("§eOwner: §7Neutral");
        }

        if (flag.getLastCapturedAt() > 0) {
            player.sendMessage("§eLast Captured: §f" + new java.util.Date(flag.getLastCapturedAt()));
            if (!flag.getLastCapturedBy().isEmpty()) {
                player.sendMessage("§eLast Captured By: §f" + getPlayerName(flag.getLastCapturedBy()));
            }
        }
    }

    private void handleList(Player player) {
        var flags = plugin.getFlagManager().getAllFlags();
        
        if (flags.isEmpty()) {
            player.sendMessage("§cNo flags exist!");
            return;
        }

        player.sendMessage("§6=== Flag List ===");
        for (PEFlag flag : flags.values()) {
            String ownerName = "Neutral";
            if (flag.getFactionId() > 0) {
                var faction = plugin.getFactionManager().getFaction(flag.getFactionId());
                if (faction != null) {
                    ownerName = faction.getDisplayName();
                }
            }
            
            player.sendMessage("§e" + flag.getName() + " §7(Owner: " + ownerName + ")");
        }
    }

    private String getPlayerName(String uuid) {
        if (uuid == null || uuid.isEmpty()) return "Unknown";
        
        try {
            Player player = plugin.getServer().getPlayer(java.util.UUID.fromString(uuid));
            if (player != null) {
                return player.getName();
            }
        } catch (IllegalArgumentException e) {
            // Invalid UUID format
        }
        
        return "Unknown";
    }
}