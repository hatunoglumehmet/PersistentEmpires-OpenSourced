package com.persistentempires.minecraft.listeners;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import org.bukkit.event.EventHandler;
import org.bukkit.event.Listener;
import org.bukkit.event.inventory.InventoryCloseEvent;
import org.bukkit.event.inventory.InventoryOpenEvent;
import org.bukkit.entity.Player;

/**
 * Handles inventory events
 */
public class InventoryListener implements Listener {
    private final PersistentEmpiresPlugin plugin;

    public InventoryListener(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
    }

    @EventHandler
    public void onInventoryOpen(InventoryOpenEvent event) {
        if (!(event.getPlayer() instanceof Player player)) {
            return;
        }

        // Check if player is in combat
        if (plugin.getCombatManager().isInCombat(player)) {
            event.setCancelled(true);
            player.sendMessage("§cYou cannot open inventories while in combat!");
            return;
        }

        // Handle special inventory types
        String inventoryTitle = event.getView().getTitle().toLowerCase();
        
        if (inventoryTitle.contains("chest") || inventoryTitle.contains("bank")) {
            // Check if player has permission to access this chest/bank
            // This could be expanded to check faction permissions, etc.
        }
    }

    @EventHandler
    public void onInventoryClose(InventoryCloseEvent event) {
        if (!(event.getPlayer() instanceof Player player)) {
            return;
        }

        // Save player inventory changes
        plugin.getPlayerManager().savePlayer(player);
    }
}