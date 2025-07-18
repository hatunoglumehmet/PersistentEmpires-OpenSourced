package com.persistentempires.minecraft.listeners;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import org.bukkit.event.EventHandler;
import org.bukkit.event.Listener;
import org.bukkit.event.player.PlayerInteractEvent;
import org.bukkit.event.block.Action;

/**
 * Handles player interaction events
 */
public class PlayerInteractListener implements Listener {
    private final PersistentEmpiresPlugin plugin;

    public PlayerInteractListener(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
    }

    @EventHandler
    public void onPlayerInteract(PlayerInteractEvent event) {
        if (event.getAction() != Action.RIGHT_CLICK_BLOCK) {
            return;
        }

        if (event.getClickedBlock() == null) {
            return;
        }

        // Check if player clicked on a flag
        var flag = plugin.getFlagManager().getFlagAt(event.getClickedBlock().getLocation());
        if (flag != null) {
            event.setCancelled(true);
            
            // Check if player is in combat
            if (plugin.getCombatManager().isInCombat(event.getPlayer())) {
                event.getPlayer().sendMessage("§cYou cannot interact with flags while in combat!");
                return;
            }
            
            // Show flag info
            event.getPlayer().sendMessage("§6=== Flag: " + flag.getName() + " ===");
            
            if (flag.getFactionId() > 0) {
                var faction = plugin.getFactionManager().getFaction(flag.getFactionId());
                if (faction != null) {
                    event.getPlayer().sendMessage("§eOwner: " + faction.getDisplayName());
                } else {
                    event.getPlayer().sendMessage("§eOwner: §cUnknown");
                }
            } else {
                event.getPlayer().sendMessage("§eOwner: §7Neutral");
            }
            
            event.getPlayer().sendMessage("§eUse /capture start " + flag.getName() + " to capture");
            return;
        }

        // Check for crafting stations
        String blockType = event.getClickedBlock().getType().toString();
        if (blockType.contains("ANVIL") || blockType.contains("CRAFTING_TABLE") || 
            blockType.contains("FURNACE") || blockType.contains("SMITHING_TABLE")) {
            
            // Check if player is in combat
            if (plugin.getCombatManager().isInCombat(event.getPlayer())) {
                event.setCancelled(true);
                event.getPlayer().sendMessage("§cYou cannot craft while in combat!");
                return;
            }
            
            // Show available recipes
            if (blockType.contains("CRAFTING_TABLE")) {
                var recipes = plugin.getCraftingManager().getAvailableRecipes(event.getPlayer());
                event.getPlayer().sendMessage("§6Available Recipes:");
                for (var recipe : recipes) {
                    event.getPlayer().sendMessage("§e- " + recipe.getDisplayName() + " (Level " + recipe.getRequiredLevel() + ")");
                }
                event.getPlayer().sendMessage("§eUse /craft <recipe> to start crafting");
            }
        }
    }
}