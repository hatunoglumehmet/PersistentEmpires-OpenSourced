package com.persistentempires.minecraft.listeners;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import org.bukkit.event.EventHandler;
import org.bukkit.event.Listener;
import org.bukkit.event.player.PlayerQuitEvent;

/**
 * Handles player quit events
 */
public class PlayerQuitListener implements Listener {
    private final PersistentEmpiresPlugin plugin;

    public PlayerQuitListener(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
    }

    @EventHandler
    public void onPlayerQuit(PlayerQuitEvent event) {
        // Cancel any active flag capture
        plugin.getFlagManager().cancelCapture(event.getPlayer());
        
        // Cancel any active crafting
        plugin.getCraftingManager().cancelCrafting(event.getPlayer());
        
        // Save and unload player data
        plugin.getPlayerManager().unloadPlayer(event.getPlayer());
    }
}