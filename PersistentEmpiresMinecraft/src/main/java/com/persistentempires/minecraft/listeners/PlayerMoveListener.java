package com.persistentempires.minecraft.listeners;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import org.bukkit.event.EventHandler;
import org.bukkit.event.Listener;
import org.bukkit.event.player.PlayerMoveEvent;
import org.bukkit.event.player.PlayerItemHeldEvent;
import org.bukkit.event.inventory.InventoryClickEvent;

/**
 * Handles player movement and equipment changes
 */
public class PlayerMoveListener implements Listener {
    private final PersistentEmpiresPlugin plugin;

    public PlayerMoveListener(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
    }

    @EventHandler
    public void onPlayerMove(PlayerMoveEvent event) {
        // Update movement speed based on armor weight
        if (event.getFrom().getBlock().getLocation().equals(event.getTo().getBlock().getLocation())) {
            return; // Player didn't actually move blocks
        }

        plugin.getCombatManager().updateMovementSpeed(event.getPlayer());
    }

    @EventHandler
    public void onPlayerItemHeld(PlayerItemHeldEvent event) {
        // Update movement speed when switching items
        plugin.getServer().getScheduler().runTaskLater(plugin, () -> {
            plugin.getCombatManager().updateMovementSpeed(event.getPlayer());
        }, 1L);
    }

    @EventHandler
    public void onInventoryClick(InventoryClickEvent event) {
        if (event.getWhoClicked() instanceof org.bukkit.entity.Player player) {
            // Update movement speed when armor changes
            plugin.getServer().getScheduler().runTaskLater(plugin, () -> {
                plugin.getCombatManager().updateMovementSpeed(player);
            }, 1L);
        }
    }
}