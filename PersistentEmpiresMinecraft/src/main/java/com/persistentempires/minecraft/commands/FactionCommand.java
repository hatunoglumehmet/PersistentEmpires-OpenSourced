package com.persistentempires.minecraft.commands;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import com.persistentempires.minecraft.database.entities.PEFaction;
import com.persistentempires.minecraft.database.entities.PEPlayer;
import org.bukkit.command.Command;
import org.bukkit.command.CommandExecutor;
import org.bukkit.command.CommandSender;
import org.bukkit.entity.Player;

/**
 * Command handler for faction-related commands
 */
public class FactionCommand implements CommandExecutor {
    private final PersistentEmpiresPlugin plugin;

    public FactionCommand(PersistentEmpiresPlugin plugin) {
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
            case "join":
                handleJoin(player, args);
                break;
            case "leave":
                handleLeave(player);
                break;
            case "info":
                handleInfo(player, args);
                break;
            case "list":
                handleList(player);
                break;
            case "invite":
                handleInvite(player, args);
                break;
            case "kick":
                handleKick(player, args);
                break;
            case "war":
                handleWar(player, args);
                break;
            case "peace":
                handlePeace(player, args);
                break;
            case "marshall":
                handleMarshall(player, args);
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
        player.sendMessage("§6=== Faction Commands ===");
        player.sendMessage("§e/faction create <name> <displayName> §7- Create a new faction");
        player.sendMessage("§e/faction join <faction> §7- Join an existing faction");
        player.sendMessage("§e/faction leave §7- Leave your current faction");
        player.sendMessage("§e/faction info [faction] §7- Show faction information");
        player.sendMessage("§e/faction list §7- List all factions");
        player.sendMessage("§e/faction invite <player> §7- Invite a player to your faction");
        player.sendMessage("§e/faction kick <player> §7- Kick a player from your faction");
        player.sendMessage("§e/faction war <faction> §7- Declare war on another faction");
        player.sendMessage("§e/faction peace <faction> §7- Make peace with another faction");
        player.sendMessage("§e/faction marshall [add|remove] <player> §7- Manage marshalls");
    }

    private void handleCreate(Player player, String[] args) {
        if (args.length < 3) {
            player.sendMessage("§cUsage: /faction create <name> <displayName>");
            return;
        }

        String factionName = args[1];
        String displayName = String.join(" ", java.util.Arrays.copyOfRange(args, 2, args.length));

        // Check if player is already in a faction
        if (plugin.getFactionManager().isPlayerInFaction(player.getUniqueId().toString())) {
            player.sendMessage("§cYou are already in a faction!");
            return;
        }

        // Validate faction name
        if (factionName.length() < 3 || factionName.length() > 16) {
            player.sendMessage("§cFaction name must be between 3 and 16 characters!");
            return;
        }

        if (displayName.length() > 32) {
            player.sendMessage("§cDisplay name too long! Maximum 32 characters.");
            return;
        }

        // Check if faction name is already taken
        if (plugin.getFactionManager().getFactionByName(factionName) != null) {
            player.sendMessage("§cFaction name already taken!");
            return;
        }

        // Check if player has enough gold
        int factionCost = plugin.getConfig().getInt("factions.creation_cost", 5000);
        if (!plugin.getPlayerManager().removeGold(player, factionCost)) {
            player.sendMessage("§cYou need " + factionCost + " gold to create a faction!");
            return;
        }

        // Create faction
        PEFaction faction = plugin.getFactionManager().createFaction(factionName, displayName, player.getUniqueId().toString());
        if (faction != null) {
            player.sendMessage("§aFaction '" + displayName + "' created successfully!");
            player.sendMessage("§eYou are now the lord of this faction.");
            player.sendMessage("§eCost: " + factionCost + " gold");
        } else {
            player.sendMessage("§cFailed to create faction!");
        }
    }

    private void handleJoin(Player player, String[] args) {
        if (args.length < 2) {
            player.sendMessage("§cUsage: /faction join <faction>");
            return;
        }

        String factionName = args[1];
        PEFaction faction = plugin.getFactionManager().getFactionByName(factionName);

        if (faction == null) {
            player.sendMessage("§cFaction not found: " + factionName);
            return;
        }

        if (plugin.getFactionManager().joinFaction(player.getUniqueId().toString(), faction.getId())) {
            player.sendMessage("§aJoined faction: " + faction.getDisplayName());
        } else {
            player.sendMessage("§cFailed to join faction!");
        }
    }

    private void handleLeave(Player player) {
        if (!plugin.getFactionManager().isPlayerInFaction(player.getUniqueId().toString())) {
            player.sendMessage("§cYou are not in a faction!");
            return;
        }

        PEFaction faction = plugin.getFactionManager().getPlayerFaction(player.getUniqueId().toString());
        if (plugin.getFactionManager().leaveFaction(player.getUniqueId().toString())) {
            player.sendMessage("§aLeft faction: " + faction.getDisplayName());
        } else {
            player.sendMessage("§cFailed to leave faction!");
        }
    }

    private void handleInfo(Player player, String[] args) {
        PEFaction faction;
        
        if (args.length < 2) {
            // Show player's faction info
            faction = plugin.getFactionManager().getPlayerFaction(player.getUniqueId().toString());
            if (faction == null) {
                player.sendMessage("§cYou are not in a faction!");
                return;
            }
        } else {
            // Show specified faction info
            String factionName = args[1];
            faction = plugin.getFactionManager().getFactionByName(factionName);
            if (faction == null) {
                player.sendMessage("§cFaction not found: " + factionName);
                return;
            }
        }

        // Display faction info
        player.sendMessage("§6=== Faction Information ===");
        player.sendMessage("§eName: §f" + faction.getDisplayName());
        player.sendMessage("§eID: §f" + faction.getId());
        player.sendMessage("§eLord: §f" + getPlayerName(faction.getLordUUID()));
        player.sendMessage("§eMarshalls: §f" + faction.getMarshalls().size());
        player.sendMessage("§eWars: §f" + faction.getWarDeclarations().size());
        player.sendMessage("§eCreated: §f" + new java.util.Date(faction.getCreatedAt()));
        
        if (faction.getWarDeclarations().size() > 0) {
            player.sendMessage("§eAt war with:");
            for (int warFactionId : faction.getWarDeclarations()) {
                PEFaction warFaction = plugin.getFactionManager().getFaction(warFactionId);
                if (warFaction != null) {
                    player.sendMessage("§c- " + warFaction.getDisplayName());
                }
            }
        }
    }

    private void handleList(Player player) {
        var factions = plugin.getFactionManager().getAllFactions();
        
        if (factions.isEmpty()) {
            player.sendMessage("§cNo factions exist!");
            return;
        }

        player.sendMessage("§6=== Faction List ===");
        for (PEFaction faction : factions.values()) {
            String lordName = getPlayerName(faction.getLordUUID());
            player.sendMessage("§e" + faction.getDisplayName() + " §7(Lord: " + lordName + ")");
        }
    }

    private void handleInvite(Player player, String[] args) {
        if (args.length < 2) {
            player.sendMessage("§cUsage: /faction invite <player>");
            return;
        }

        PEFaction faction = plugin.getFactionManager().getPlayerFaction(player.getUniqueId().toString());
        if (faction == null) {
            player.sendMessage("§cYou are not in a faction!");
            return;
        }

        if (!faction.hasManagementPermission(player.getUniqueId().toString())) {
            player.sendMessage("§cYou don't have permission to invite players!");
            return;
        }

        String targetName = args[1];
        Player target = plugin.getServer().getPlayer(targetName);
        if (target == null) {
            player.sendMessage("§cPlayer not found: " + targetName);
            return;
        }

        if (plugin.getFactionManager().isPlayerInFaction(target.getUniqueId().toString())) {
            player.sendMessage("§c" + target.getName() + " is already in a faction!");
            return;
        }

        // Send invitation
        target.sendMessage("§a" + player.getName() + " has invited you to join faction: " + faction.getDisplayName());
        target.sendMessage("§eUse /faction join " + faction.getName() + " to accept.");
        player.sendMessage("§aInvitation sent to " + target.getName());
    }

    private void handleKick(Player player, String[] args) {
        if (args.length < 2) {
            player.sendMessage("§cUsage: /faction kick <player>");
            return;
        }

        PEFaction faction = plugin.getFactionManager().getPlayerFaction(player.getUniqueId().toString());
        if (faction == null) {
            player.sendMessage("§cYou are not in a faction!");
            return;
        }

        if (!faction.hasManagementPermission(player.getUniqueId().toString())) {
            player.sendMessage("§cYou don't have permission to kick players!");
            return;
        }

        String targetName = args[1];
        Player target = plugin.getServer().getPlayer(targetName);
        if (target == null) {
            player.sendMessage("§cPlayer not found: " + targetName);
            return;
        }

        PEFaction targetFaction = plugin.getFactionManager().getPlayerFaction(target.getUniqueId().toString());
        if (targetFaction == null || targetFaction.getId() != faction.getId()) {
            player.sendMessage("§c" + target.getName() + " is not in your faction!");
            return;
        }

        if (faction.isLord(target.getUniqueId().toString())) {
            player.sendMessage("§cYou cannot kick the lord!");
            return;
        }

        if (plugin.getFactionManager().leaveFaction(target.getUniqueId().toString())) {
            player.sendMessage("§aKicked " + target.getName() + " from the faction.");
            target.sendMessage("§cYou have been kicked from faction: " + faction.getDisplayName());
        } else {
            player.sendMessage("§cFailed to kick player!");
        }
    }

    private void handleWar(Player player, String[] args) {
        if (args.length < 2) {
            player.sendMessage("§cUsage: /faction war <faction>");
            return;
        }

        PEFaction faction = plugin.getFactionManager().getPlayerFaction(player.getUniqueId().toString());
        if (faction == null) {
            player.sendMessage("§cYou are not in a faction!");
            return;
        }

        if (!faction.hasManagementPermission(player.getUniqueId().toString())) {
            player.sendMessage("§cYou don't have permission to declare war!");
            return;
        }

        String targetFactionName = args[1];
        PEFaction targetFaction = plugin.getFactionManager().getFactionByName(targetFactionName);
        if (targetFaction == null) {
            player.sendMessage("§cFaction not found: " + targetFactionName);
            return;
        }

        if (targetFaction.getId() == faction.getId()) {
            player.sendMessage("§cYou cannot declare war on your own faction!");
            return;
        }

        if (plugin.getFactionManager().declareWar(faction.getId(), targetFaction.getId())) {
            player.sendMessage("§cWar declared against: " + targetFaction.getDisplayName());
            
            // Broadcast to both factions
            broadcastToFaction(faction, "§c" + faction.getDisplayName() + " has declared war on " + targetFaction.getDisplayName() + "!");
            broadcastToFaction(targetFaction, "§c" + faction.getDisplayName() + " has declared war on " + targetFaction.getDisplayName() + "!");
        } else {
            player.sendMessage("§cFailed to declare war! (Check cooldown)");
        }
    }

    private void handlePeace(Player player, String[] args) {
        if (args.length < 2) {
            player.sendMessage("§cUsage: /faction peace <faction>");
            return;
        }

        PEFaction faction = plugin.getFactionManager().getPlayerFaction(player.getUniqueId().toString());
        if (faction == null) {
            player.sendMessage("§cYou are not in a faction!");
            return;
        }

        if (!faction.hasManagementPermission(player.getUniqueId().toString())) {
            player.sendMessage("§cYou don't have permission to make peace!");
            return;
        }

        String targetFactionName = args[1];
        PEFaction targetFaction = plugin.getFactionManager().getFactionByName(targetFactionName);
        if (targetFaction == null) {
            player.sendMessage("§cFaction not found: " + targetFactionName);
            return;
        }

        if (!plugin.getFactionManager().areAtWar(faction.getId(), targetFaction.getId())) {
            player.sendMessage("§cYou are not at war with " + targetFaction.getDisplayName());
            return;
        }

        if (plugin.getFactionManager().makePeace(faction.getId(), targetFaction.getId())) {
            player.sendMessage("§aPeace made with: " + targetFaction.getDisplayName());
            
            // Broadcast to both factions
            broadcastToFaction(faction, "§a" + faction.getDisplayName() + " has made peace with " + targetFaction.getDisplayName() + "!");
            broadcastToFaction(targetFaction, "§a" + faction.getDisplayName() + " has made peace with " + targetFaction.getDisplayName() + "!");
        } else {
            player.sendMessage("§cFailed to make peace! (Check cooldown)");
        }
    }

    private void handleMarshall(Player player, String[] args) {
        if (args.length < 3) {
            player.sendMessage("§cUsage: /faction marshall [add|remove] <player>");
            return;
        }

        PEFaction faction = plugin.getFactionManager().getPlayerFaction(player.getUniqueId().toString());
        if (faction == null) {
            player.sendMessage("§cYou are not in a faction!");
            return;
        }

        if (!faction.isLord(player.getUniqueId().toString())) {
            player.sendMessage("§cOnly the lord can manage marshalls!");
            return;
        }

        String action = args[1].toLowerCase();
        String targetName = args[2];
        Player target = plugin.getServer().getPlayer(targetName);
        if (target == null) {
            player.sendMessage("§cPlayer not found: " + targetName);
            return;
        }

        PEFaction targetFaction = plugin.getFactionManager().getPlayerFaction(target.getUniqueId().toString());
        if (targetFaction == null || targetFaction.getId() != faction.getId()) {
            player.sendMessage("§c" + target.getName() + " is not in your faction!");
            return;
        }

        switch (action) {
            case "add":
                if (plugin.getFactionManager().addMarshall(faction.getId(), target.getUniqueId().toString())) {
                    player.sendMessage("§aAdded " + target.getName() + " as a marshall.");
                    target.sendMessage("§aYou have been promoted to marshall in " + faction.getDisplayName() + "!");
                } else {
                    player.sendMessage("§cFailed to add marshall!");
                }
                break;
            case "remove":
                if (plugin.getFactionManager().removeMarshall(faction.getId(), target.getUniqueId().toString())) {
                    player.sendMessage("§aRemoved " + target.getName() + " from marshall position.");
                    target.sendMessage("§cYou have been demoted from marshall in " + faction.getDisplayName() + "!");
                } else {
                    player.sendMessage("§cFailed to remove marshall!");
                }
                break;
            default:
                player.sendMessage("§cInvalid action: " + action);
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
        
        // Try to get from database
        // This would query the database for the player name
        return "Unknown";
    }

    private void broadcastToFaction(PEFaction faction, String message) {
        for (Player player : plugin.getServer().getOnlinePlayers()) {
            PEFaction playerFaction = plugin.getFactionManager().getPlayerFaction(player.getUniqueId().toString());
            if (playerFaction != null && playerFaction.getId() == faction.getId()) {
                player.sendMessage(message);
            }
        }
    }
}